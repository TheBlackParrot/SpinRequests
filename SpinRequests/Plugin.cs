﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using SpinCore.Translation;
using SpinRequests.Classes;
using SpinRequests.Patches;
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
    private static readonly Harmony HarmonyInstance = new(MyPluginInfo.PLUGIN_GUID);
    
    internal static string CustomsPath => CustomAssetLoadingHelper.CUSTOM_DATA_PATH;
    internal static string DataPath => Path.Combine(Paths.ConfigPath, "SpinRequests");
    internal static readonly SSAPI SpinShare = new();
    
    private static readonly string SessionPlayHistoryPath = Path.Combine(DataPath, "sessionPlayHistory.json");
    private static readonly string SessionThresholdHistoryPath = Path.Combine(DataPath, "sessionThresholdHistory.json");

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
        TranslationHelper.AddTranslation("SpinRequests_PlayButtonTextDownloading", "Downloading...");
        TranslationHelper.AddTranslation("SpinRequests_SkipButtonText", "Skip");
        TranslationHelper.AddTranslation("SpinRequests_AllowRequestsText", "Allow requests");
        TranslationHelper.AddTranslation("SpinRequests_GitHubButtonText", "SpinRequests Releases (GitHub)");
        TranslationHelper.AddTranslation("SpinRequests_ConsiderPlayedAfterThisPercentage", "Consider played after % of chart");
        TranslationHelper.AddTranslation("SpinRequests_SessionPersistenceLength", "Hours to remember session history");

        if (!Directory.Exists(DataPath))
        {
            Directory.CreateDirectory(DataPath);
        }

        LoadPreviousSessionData();
        
        Logger.LogInfo("Plugin loaded");
    }

    private void OnEnable()
    {
        QueueList.CreateQueueListPanel();
        Track.OnStartedPlayingTrack += TrackOnStartedPlayingTrack;
        MainCamera.OnCurrentCameraChanged += MainCameraOnCurrentCameraChanged;
        
        HarmonyInstance.PatchAll();
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
#if DEBUG
            // just so we can see the notifications
            if(currentVersion != latestVersion)
#else
            if (currentVersion < latestVersion)
#endif
            {
                Log.LogMessage($"{nameof(SpinRequests)} is out of date! (using v{currentVersion}, latest is v{latestVersion})");
                
                await Awaitable.MainThreadAsync();
                NotificationSystemGUI.AddMessage(
                    $"<b>{nameof(SpinRequests)}</b> has an update available! <alpha=#AA>(v{currentVersion} <alpha=#77>-> <alpha=#AA>v{latestVersion})\n<alpha=#FF><size=67%>See the shortcut button in the Mod Settings page to grab the latest update.", 15f);
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
        HarmonyInstance.UnpatchSelf();
    }

    private static void TrackOnStartedPlayingTrack(PlayableTrackDataHandle dataHandle, PlayState[] _)
    {
        TrackTimePatches._hasSetPlayed = false;
        TrackTimePatches._neededTrackTime = dataHandle.Data.SoundEndTime * (ConsiderPlayedAfterThisPercentage.Value / 100f);
        
        AddToPlayedMapHistory(dataHandle);
    }

    internal static List<QueueEntry> PlayedMapHistory = [];
    internal static List<string?> MapsThatCrossedPlayedThreshold = [];
    
    private const int SECONDS_IN_AN_HOUR = 3600;
    private static void LoadPreviousSessionData()
    {
        long currentTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        if (File.Exists(SessionPlayHistoryPath))
        {
            long lastWriteTimeUtc = ((DateTimeOffset)File.GetLastWriteTimeUtc(SessionPlayHistoryPath)).ToUnixTimeSeconds();
            if (currentTimeUtc - lastWriteTimeUtc > SessionPersistenceLength.Value * SECONDS_IN_AN_HOUR)
            {
                Log.LogInfo($"Previous session's play history is more than {SessionPersistenceLength.Value} hour(s) old, not loading it");
            }
            else
            {
                Log.LogInfo("Loading previous session's play history...");
                PlayedMapHistory =
                    JsonConvert.DeserializeObject<List<QueueEntry>>(File.ReadAllText(SessionPlayHistoryPath)) ?? [];
            }
        }
        
        // ReSharper disable once InvertIf
        if (File.Exists(SessionThresholdHistoryPath))
        {
            long lastWriteTimeUtc = ((DateTimeOffset)File.GetLastWriteTimeUtc(SessionThresholdHistoryPath)).ToUnixTimeSeconds();
            if (currentTimeUtc - lastWriteTimeUtc > SessionPersistenceLength.Value * SECONDS_IN_AN_HOUR)
            {
                Log.LogInfo($"Previous session's threshold history is more than {SessionPersistenceLength.Value} hour(s) old, not loading it");
            }
            else
            {
                Log.LogInfo("Loading previous session's threshold history...");
                MapsThatCrossedPlayedThreshold =
                    JsonConvert.DeserializeObject<List<string?>>(File.ReadAllText(SessionThresholdHistoryPath)) ?? [];
            }
        }
    }
    
    private static void AddToPlayedMapHistory(PlayableTrackDataHandle dataHandle)
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
        
        PlayedMapHistory.Insert(0, newEntry);
        File.WriteAllText(SessionPlayHistoryPath, JsonConvert.SerializeObject(PlayedMapHistory));
    }

    internal static void AddToCrossedThresholdList(string? fileReference)
    {
        if (fileReference == null)
        {
            return;
        }

        if (MapsThatCrossedPlayedThreshold.Contains(fileReference))
        {
            return;
        }
        
        MapsThatCrossedPlayedThreshold.Add(fileReference);
        File.WriteAllText(SessionThresholdHistoryPath, JsonConvert.SerializeObject(MapsThatCrossedPlayedThreshold));
    }
}