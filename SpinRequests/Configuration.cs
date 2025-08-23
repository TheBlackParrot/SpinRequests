using BepInEx.Configuration;

namespace SpinRequests;

public partial class Plugin
{
    internal static ConfigEntry<int> HttpPort = null!;
    internal static ConfigEntry<string> HttpAddress = null!;

    private void RegisterConfigEntries()
    {
        HttpAddress = Config.Bind("API", "HttpAddress", "127.0.0.1", 
            "IP address for the HTTP API to listen on");
        
        HttpPort = Config.Bind("API", "HttpPort", 6969, 
            "Port for the HTTP API to listen on");
    }
}