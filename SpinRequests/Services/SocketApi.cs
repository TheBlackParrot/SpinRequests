using System;
using JetBrains.Annotations;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SpinRequests.Services;

[JsonObject(MemberSerialization.OptIn)]
internal class Message(string eventType, object? obj = null)
{
    [JsonProperty] private long Timestamp => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    [JsonProperty] private string EventType => eventType;
    [JsonProperty] private object? Data => obj;
}

[UsedImplicitly]
internal class SocketApi : IDisposable
{
    private static WebSocketServer? _socketServer;
    private static WebSocketServiceHost? _socketServiceHost;

    public static void Broadcast(string eventType, object? obj = null)
    {
        _socketServiceHost?.Sessions.Broadcast(JsonConvert.SerializeObject(new Message(eventType, obj)));
    }

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public void Initialize()
    {
        _socketServer = new WebSocketServer($"ws://{Plugin.SocketAddress.Value}:{Plugin.SocketPort.Value}");
        _socketServer.AddWebSocketService<SocketMessageHandler>("/");

        if (_socketServer.WebSocketServices.TryGetServiceHost("/", out WebSocketServiceHost webSocketServiceHost))
        {
            _socketServiceHost = webSocketServiceHost;
        }

        try
        {
            _socketServer.Start();
            Plugin.Log.LogMessage($"WebSocket firehose started on {Plugin.SocketAddress.Value}:{Plugin.SocketPort.Value}");
        }
        catch (InvalidOperationException)
        {
            Plugin.Log.LogWarning($"Unable to start WebSocket firehose on {Plugin.SocketAddress.Value}:{Plugin.SocketPort.Value}. More than likely, this port is already being used on this address.");
        }
    }

    public void Dispose()
    {
        _socketServer?.Stop();
        Plugin.Log.LogInfo("WebSocket server stopped");
    }
}

internal class SocketMessageHandler : WebSocketBehavior
{
    public SocketMessageHandler()
    {
        IgnoreExtensions = true;
    }
    
    protected override void OnOpen()
    {
        Plugin.Log.LogInfo("Websocket connection opened");
        base.OnOpen();
    }
    protected override void OnClose(CloseEventArgs e)
    {
        Plugin.Log.LogInfo("Websocket connection closed");
        base.OnClose(e);
    }

    protected override void OnError(ErrorEventArgs e)
    {
    }
}