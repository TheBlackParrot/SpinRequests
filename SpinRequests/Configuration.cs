using System;
using BepInEx.Configuration;
using SpinCore.Translation;
using SpinCore.UI;
using UnityEngine;

namespace SpinRequests;

public partial class Plugin
{
    internal static ConfigEntry<int> HttpPort = null!;
    internal static ConfigEntry<string> HttpAddress = null!;
    
    internal static ConfigEntry<int> SocketPort = null!;
    internal static ConfigEntry<string> SocketAddress = null!;
    
    internal static ConfigEntry<bool> EnableQueueNotifications = null!;
    internal static ConfigEntry<bool> DeleteOldMapFiles = null!;
    internal static ConfigEntry<bool> JumpToMapAfterDownloading = null!;
    private static ConfigEntry<int> _considerPlayedAfterThisPercentage = null!;
    private static ConfigEntry<int> _sessionPersistenceLength = null!;

    private void RegisterConfigEntries()
    {
        HttpAddress = Config.Bind("API", nameof(HttpAddress), "127.0.0.1", 
            "IP address for the HTTP API to listen on");
        
        HttpPort = Config.Bind("API", nameof(HttpPort), 6969, 
            "Port for the HTTP API to listen on");
        
        SocketAddress = Config.Bind("API", nameof(SocketAddress), "127.0.0.1",
            "IP address for the WebSocket firehose to listen on");
        
        SocketPort = Config.Bind("API", nameof(SocketPort), 6970,
            "Port for the WebSocket firehose to listen on");

        EnableQueueNotifications = Config.Bind("General", nameof(EnableQueueNotifications), true,
            "Show notifications for maps added to the queue");
        DeleteOldMapFiles = Config.Bind("General", nameof(DeleteOldMapFiles), false,
            "Delete old map files when downloading updated maps");
        JumpToMapAfterDownloading = Config.Bind("General", nameof(JumpToMapAfterDownloading), true,
            "Automatically jump to the downloaded map in the map list once downloading finishes");
        _considerPlayedAfterThisPercentage = Config.Bind("General", "ConsiderPlayedAfterThisPercentage", 0,
            "How much of the chart must be played before it's considered an already played chart");
        _sessionPersistenceLength = Config.Bind("Persistence", "SessionPersistenceLength", 0,
            "How many hours between the file write time of the session history and the current time to wait before considering the current session as a new session");
    }

    private static void CreateModPage()
    {
        CustomPage rootModPage = UIHelper.CreateCustomPage("ModSettings");
        rootModPage.OnPageLoad += RootModPageOnOnPageLoad;
        
        UIHelper.RegisterMenuInModSettingsRoot($"{TRANSLATION_PREFIX}ModName", rootModPage);
        
        TranslationHelper.AddTranslation($"{TRANSLATION_PREFIX}{nameof(EnableQueueNotifications)}", "Enable queue notifications");
        TranslationHelper.AddTranslation($"{TRANSLATION_PREFIX}{nameof(DeleteOldMapFiles)}", "Delete old map files when downloading updated maps");
        TranslationHelper.AddTranslation($"{TRANSLATION_PREFIX}{nameof(JumpToMapAfterDownloading)}", "Jump to downloaded maps once download finishes");
        TranslationHelper.AddTranslation($"{TRANSLATION_PREFIX}{nameof(_considerPlayedAfterThisPercentage)}", "Consider played after % of chart");
        TranslationHelper.AddTranslation($"{TRANSLATION_PREFIX}{nameof(_sessionPersistenceLength)}", "Hours to remember session history");
        TranslationHelper.AddTranslation($"{TRANSLATION_PREFIX}GitHubButtonText", "SpinRequests Releases (GitHub)");
    }

    private static void RootModPageOnOnPageLoad(Transform rootModPageTransform)
    {
        CustomGroup modGroup = UIHelper.CreateGroup(rootModPageTransform, nameof(SpinRequests));
        UIHelper.CreateSectionHeader(modGroup, "ModGroupHeader", $"{TRANSLATION_PREFIX}ModName", false);
            
        #region EnableQueueNotifications
        CustomGroup enableQueueNotificationsGroup = UIHelper.CreateGroup(modGroup, "EnableQueueNotificationsGroup");
        enableQueueNotificationsGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateSmallToggle(enableQueueNotificationsGroup, nameof(EnableQueueNotifications),
            $"{TRANSLATION_PREFIX}{nameof(EnableQueueNotifications)}", EnableQueueNotifications.Value, value =>
            {
                EnableQueueNotifications.Value = value;
            });
        #endregion
        
        #region DeleteOldMapFiles
        CustomGroup deleteOldMapFilesGroup = UIHelper.CreateGroup(modGroup, "DeleteOldMapFilesGroup");
        deleteOldMapFilesGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateSmallToggle(deleteOldMapFilesGroup, nameof(DeleteOldMapFiles),
            $"{TRANSLATION_PREFIX}{nameof(DeleteOldMapFiles)}", DeleteOldMapFiles.Value, value =>
            {
                DeleteOldMapFiles.Value = value;
            });
        #endregion
        
        #region JumpToMapAfterDownloading
        CustomGroup jumpToMapAfterDownloadingGroup = UIHelper.CreateGroup(modGroup, "JumpToMapAfterDownloadingGroup");
        jumpToMapAfterDownloadingGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateSmallToggle(jumpToMapAfterDownloadingGroup, nameof(JumpToMapAfterDownloading),
            $"{TRANSLATION_PREFIX}{nameof(JumpToMapAfterDownloading)}", JumpToMapAfterDownloading.Value, value =>
            {
                JumpToMapAfterDownloading.Value = value;
            });
        #endregion
        
        #region ConsiderPlayedAfterThisPercentage
        CustomGroup considerPlayedGroup = UIHelper.CreateGroup(modGroup, "ConsiderPlayedGroup");
        considerPlayedGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateLabel(considerPlayedGroup, "ConsiderPlayedLabel", $"{TRANSLATION_PREFIX}{nameof(_considerPlayedAfterThisPercentage)}");
        CustomInputField considerPlayedInput = UIHelper.CreateInputField(considerPlayedGroup, "ConsiderPlayedInput", (_, newValue) =>
        {
            if (!int.TryParse(newValue, out int value))
            {
                return;
            }
            
            _considerPlayedAfterThisPercentage.Value = Math.Min(Math.Max(0, value), 100);
        });
        considerPlayedInput.InputField.SetText(_considerPlayedAfterThisPercentage.Value.ToString());
        #endregion
        
        #region SessionPersistenceLength
        CustomGroup sessionPersistenceLengthGroup = UIHelper.CreateGroup(modGroup, "SessionPersistenceLength");
        sessionPersistenceLengthGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateLabel(sessionPersistenceLengthGroup, "SessionPersistenceLengthLabel", $"{TRANSLATION_PREFIX}{nameof(_sessionPersistenceLength)}");
        CustomInputField sessionPersistenceLengthInput = UIHelper.CreateInputField(sessionPersistenceLengthGroup,
            "SessionPersistenceLengthInput", (_, newValue) =>
        {
            if (!int.TryParse(newValue, out int value))
            {
                return;
            }
            
            _sessionPersistenceLength.Value = Math.Min(Math.Max(0, value), 24);
        });
        sessionPersistenceLengthInput.InputField.SetText(_sessionPersistenceLength.Value.ToString());
        #endregion

        UIHelper.CreateButton(modGroup, "OpenSpinRequestsRepositoryButton", $"{TRANSLATION_PREFIX}GitHubButtonText", () =>
        {
            Application.OpenURL($"https://github.com/TheBlackParrot/{nameof(SpinRequests)}/releases/latest");
        });
    }
}