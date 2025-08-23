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
using SpinShareLib.Types;

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
        
        if (!int.TryParse(path.Last().Replace("/", string.Empty).ToLower(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int id))
        {
            goto finalResponse;
        }

        Content<SongDetail> content = await Plugin.SpinShare.getSongDetail(id.ToString());
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
                
                case "query":
                    response = await HandleQueryAddContext(path);
                    break;
                
                case "add":
                    response = await HandleQueryAddContext(path, true, urlQuery);
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