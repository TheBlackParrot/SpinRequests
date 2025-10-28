using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using SpinRequests.Classes;
using SpinRequests.UI;
using SpinShareLib.Types;
using JsonException = System.Text.Json.JsonException;

namespace SpinRequests.Services;

internal class HttpApi
{
    private static ManualLogSource Log => Plugin.Log;
    private static HttpListener? _httpListener;

    private static readonly int HttpPort = Plugin.HttpPort.Value;
    private static readonly string HttpAddress = Plugin.HttpAddress.Value;
    
    // (we don't have System.Web) ;w;
    // https://stackoverflow.com/a/53153418
    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> dic = new();
        
        Regex reg = new("(?:[?&]|^)([^&]+)=([^&]*)");
        MatchCollection matches = reg.Matches(query);
        foreach (Match match in matches) {
            dic[match.Groups[1].Value] = Uri.UnescapeDataString(match.Groups[2].Value);
        }
        
        return dic;
    }

    public void Initialize()
    {
        if (_httpListener != null)
        {
            return;
        }

        try
        {
            _httpListener = new HttpListener
            {
                Prefixes = { $"http://{HttpAddress}:{HttpPort}/" }
            };
        }
        catch (Exception e)
        {
            Log.LogError(e);
            return;
        }

        try
        {
            _httpListener.Start();
        }
        catch (SocketException)
        {
            Log.LogWarning(
                $"Unable to start HTTP server on {HttpAddress}:{HttpPort}. More than likely, this address and port combination is already being used on this address.");
            return;
        }
        
        Log.LogMessage($"HTTP API Listening on {HttpAddress}:{HttpPort}");
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    HttpListenerContext context = await _httpListener.GetContextAsync();
                    await HandleContext(context);
                }
                catch (Exception e)
                {
                    Log.LogError(e);
                }
            }
            // ReSharper disable once FunctionNeverReturns
            // this is an intentional infinite loop
        });
    }
    
    private static async Task<KeyValuePair<int, byte[]>> HandleQueryAddContext(string[] path,
        bool addToQueue = false,
        Dictionary<string, string>? query = null)
    {
        int code = 400;
        byte[] response = Encoding.Default.GetBytes("{\"message\": \"Invalid request\"}");

        bool forced = false;
        if (query != null)
        {
            if (query.TryGetValue("force", out string forcedString))
            {
                if (bool.TryParse(forcedString, out bool forcedBool))
                {
                    forced = forcedBool;
                }
            }
        }

        if (addToQueue && !QueueList.IsOpen)
        {
            if (!forced)
            {
                code = 403;
                response = Encoding.Default.GetBytes("{\"message\": \"The queue is closed\"}");
                goto finalResponse;
            }
        }

        bool trySearch = !int.TryParse(path.Last().Replace("/", string.Empty).ToLower(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int id);

        if (trySearch)
        {
            string searchString = Uri.UnescapeDataString(path.Last().Replace("/", string.Empty));
            Plugin.Log.LogInfo($"Searching {searchString}");
            try
            {
                Content<Search> search = await Plugin.SpinShare.search(searchString);

                code = 404;
                response = Encoding.Default.GetBytes("{\"message\": \"No results for search\"}");
                if (search.status != 200)
                {
                    Plugin.Log.LogInfo("Status wasn't 200");
                    goto finalResponse;
                }

                if (search.data.songs.Length == 0)
                {
                    Plugin.Log.LogInfo("No results found for search");
                    goto finalResponse;
                }

                if (search.data.songs[0] == null)
                {
                    // ok buddy
                    Plugin.Log.LogInfo("First result was null (wtf)");
                    goto finalResponse;
                }

                // SpinShareLib's Song class is missing metadata fields that are actually also there in the API result
                // so we do it the less efficient way ;w;
                id = search.data.songs[0].id;
            }
            catch (Exception exception)
            {
                if (exception is TaskCanceledException)
                {
                    code = 504;
                    response = Encoding.Default.GetBytes("{\"message\": \"SpinShare API request timed out\"}");
                    Plugin.Log.LogInfo("Request timed out");
                    goto finalResponse;
                }

                code = 500;
                response = Encoding.Default.GetBytes("{\"message\": \"" + exception.Message + "\"}");
                Plugin.Log.LogError(exception);
                goto finalResponse;
            }
        }

        Content<SongDetail> content;
        try
        {
            content = await Plugin.SpinShare.getSongDetail(id.ToString());
        }
        catch (Exception exception)
        {
            switch (exception)
            {
                case TaskCanceledException:
                    code = 504;
                    response = Encoding.Default.GetBytes("{\"message\": \"SpinShare API request timed out\"}");
                    Plugin.Log.LogInfo("Request timed out");
                    goto finalResponse;
                    
                case JsonException:
                    code = 404;
                    response = Encoding.Default.GetBytes("{\"message\": \"This map does not exist\"}");
                    Plugin.Log.LogInfo("Map doesn't exist");
                    goto finalResponse;
            }

            code = 500;
            response = Encoding.Default.GetBytes("{\"message\": \"" + exception.Message + "\"}");
            Plugin.Log.LogError(exception);
            goto finalResponse;
        }

        SongDetail details = content.data;
        QueueEntry serializedData = new(details, query);

        // ReSharper disable once InvertIf
        if (serializedData.SpinShareKey != null)
        {
            if (addToQueue)
            {
                await serializedData.AddToQueue();
            }
            
            code = 200;
            response = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(serializedData));
        }
        
        finalResponse:
            return new KeyValuePair<int, byte[]>(code, response);
    }

    private static KeyValuePair<int, byte[]> HandleQueueContext(Dictionary<string, string>? query = null)
    {
        string? requester = null;
        if (query != null)
        {
            if (query.TryGetValue("user", out string? user))
            {
                requester = user;
            }   
        }

        byte[] response = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(requester)
            ? JsonConvert.SerializeObject(QueueList.Entries.Concat(QueueList.BufferedList))
            : JsonConvert.SerializeObject(QueueList.Entries.Concat(QueueList.BufferedList).Where(x => x.Requester == requester)));

        return new KeyValuePair<int, byte[]>(200, response);
    }
    
    private static byte[] GetSessionHistory(Dictionary<string, string>? query = null)
    {
        int limit = 0;
        bool onlyPlayed = false;
        if (query != null)
        {
            if (query.TryGetValue("limit", out string? limitStr))
            {
                int.TryParse(limitStr, out limit);
            }

            if (query.TryGetValue("onlyPlayed", out string? onlyPlayedStr))
            {
                bool.TryParse(onlyPlayedStr, out onlyPlayed);
            }
        }

        List<QueueEntry> output = Plugin.PlayedMapHistory.Where(x => !onlyPlayed || x.HasPlayed).ToList();
        limit = Math.Max(Math.Min(limit, output.Count), 0);
        if (limit > 0)
        { 
            output = output.GetRange(0, limit);   
        }
        
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(output));
    }

    private static async Task HandleContext(HttpListenerContext context)
    {
        string[] path = context.Request.Url.Segments;
        Dictionary<string, string> urlQuery = ParseQuery(context.Request.Url.Query);
        
        Log.LogInfo($"GET request path: {string.Join(", ", path)}");
        
        KeyValuePair<int, byte[]> response;
        
        if (path.Length <= 1)
        {
            response = new KeyValuePair<int, byte[]>(200, Encoding.Default.GetBytes("{\"message\": \"Hello!\"}"));
        }
        else
        {
            switch (path[1].Replace("/", string.Empty).ToLower())
            {
                case "favicon.ico":
                    context.Response.StatusCode = 404;
                    context.Response.KeepAlive = false;
                    context.Response.ContentLength64 = 0;
                    context.Response.Close();
                    return;
                
                case "add":
                    response = await HandleQueryAddContext(path, true, urlQuery);
                    break;
                
                case "history":
                    response = new KeyValuePair<int, byte[]>(200, GetSessionHistory(urlQuery));
                    break;
                
                case "query":
                    response = await HandleQueryAddContext(path);
                    break;
                
                case "queue":
                    response = HandleQueueContext(urlQuery);
                    break;
                
                default:
                    response = new KeyValuePair<int, byte[]>(501, Encoding.Default.GetBytes("{\"message\": \"Not implemented\"}"));
                    break;
            }
        }
        
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.Key;
        context.Response.KeepAlive = false;
        context.Response.ContentLength64 = response.Value.Length;
        
        Stream outputStream = context.Response.OutputStream;
        await outputStream.WriteAsync(response.Value, 0, response.Value.Length);
        
        outputStream.Close();
        context.Response.Close();
    }
}