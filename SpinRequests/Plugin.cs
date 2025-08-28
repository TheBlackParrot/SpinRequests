using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using SpinCore.Translation;
using SpinRequests.Classes;
using SpinRequests.Services;
using SpinRequests.UI;
using SpinShareLib;

namespace SpinRequests;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("srxd.raoul1808.spincore", "1.1.2")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    internal static string CustomsPath => CustomAssetLoadingHelper.CUSTOM_DATA_PATH;
    internal static string DataPath => Path.Combine(Paths.ConfigPath, "SpinRequests");
    internal static readonly SSAPI SpinShare = new();

    private void Awake()
    {
        Log = Logger;
        
#if DEBUG
        Log.LogInfo(CustomsPath);
#endif
        
        RegisterConfigEntries();
        CreateModPage();
        
        HttpApi httpApi = new();
        httpApi.Initialize();
        
        SocketApi socketApi = new();
        socketApi.Initialize();
        
        TranslationHelper.AddTranslation("SpinRequests_ModName", nameof(SpinRequests));
        TranslationHelper.AddTranslation("SpinRequests_RequestQueueText", "Map Request Queue");
        TranslationHelper.AddTranslation("SpinRequests_MenuButtonText", "Requests");
        TranslationHelper.AddTranslation("SpinRequests_PlayButtonText", "Play");
        TranslationHelper.AddTranslation("SpinRequests_SkipButtonText", "Skip");
        TranslationHelper.AddTranslation("SpinRequests_AllowRequestsText", "Allow requests");

        if (!Directory.Exists(DataPath))
        {
            Directory.CreateDirectory(DataPath);
        }
        
        Logger.LogInfo("Plugin loaded");
    }

    private void OnEnable()
    {
        QueueList.CreateQueueListPanel();
        Track.OnStartedPlayingTrack += TrackOnStartedPlayingTrack;
    }

    private void OnDisable()
    {
        Track.OnStartedPlayingTrack -= TrackOnStartedPlayingTrack;
    }

    internal static List<QueueEntry> PlayedMapHistory = [];
    private static void TrackOnStartedPlayingTrack(PlayableTrackDataHandle dataHandle, PlayState[] _)
    {
        PlayedMapHistory = PlayedMapHistory.Prepend(new QueueEntry(dataHandle.Data)).ToList();
    }
}