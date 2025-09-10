using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using SpinCore.Translation;
using SpinRequests.Classes;
using SpinRequests.Services;
using SpinRequests.UI;
using SpinShareLib;
using UnityEngine;

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
        TranslationHelper.AddTranslation("SpinRequests_GitHubButtonText", "SpinRequests Releases (GitHub)");

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
        MainCamera.OnCurrentCameraChanged += MainCameraOnCurrentCameraChanged;
    }

    private static void MainCameraOnCurrentCameraChanged(Camera _)
    {
        MainCamera.OnCurrentCameraChanged -= MainCameraOnCurrentCameraChanged;
        
        Task.Run(async () =>
        {
            HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"{nameof(SpinRequests)}/{MyPluginInfo.PLUGIN_VERSION} (https://github.com/TheBlackParrot/SpinRequests)");
            HttpResponseMessage responseMessage = await httpClient.GetAsync("https://api.github.com/repos/TheBlackParrot/SpinRequests/releases/latest");
            responseMessage.EnsureSuccessStatusCode();
            string json = await responseMessage.Content.ReadAsStringAsync();
            
            ReleaseVersion? releaseVersion = JsonConvert.DeserializeObject<ReleaseVersion>(json);
            if (releaseVersion == null)
            {
                Log.LogInfo("Could not get newest release version information");
                return;
            }
            if (releaseVersion.Version == null)
            {
                Log.LogInfo("Could not get newest release version information");
                return;
            }
            if (releaseVersion.IsPreRelease)
            {
                Log.LogInfo("Newest release version is a pre-release");
                return;
            }
            
            Version currentVersion = new(MyPluginInfo.PLUGIN_VERSION);
            Version latestVersion = new(releaseVersion.Version);
            if (currentVersion < latestVersion)
            {
                Log.LogMessage($"{nameof(SpinRequests)} is out of date! (using v{currentVersion}, latest is v{latestVersion})");
                
                await Awaitable.MainThreadAsync();
                NotificationSystemGUI.AddMessage(
                    $"<b>{nameof(SpinRequests)}</b> has an update available! See the shortcut button in the Mod Settings page to grab the latest update. <alpha=#AA>(v{currentVersion} -> v{latestVersion})", 10f);
            }
            else
            {
                Log.LogMessage($"{nameof(SpinRequests)} is up to date!");
            }
        });
    }

    private void OnDisable()
    {
        Track.OnStartedPlayingTrack -= TrackOnStartedPlayingTrack;
    }

    internal static List<QueueEntry> PlayedMapHistory = [];
    private static void TrackOnStartedPlayingTrack(PlayableTrackDataHandle dataHandle, PlayState[] _)
    {
        QueueEntry newEntry = new(dataHandle.Data);
        
        if (PlayedMapHistory.Count > 0)
        {
            QueueEntry previousEntry = PlayedMapHistory[0];
            if (previousEntry.FileReference == newEntry.FileReference)
            {
                // same map, don't duplicate it
                return;
            }
        }
        
        PlayedMapHistory = PlayedMapHistory.Prepend(newEntry).ToList();
    }
}