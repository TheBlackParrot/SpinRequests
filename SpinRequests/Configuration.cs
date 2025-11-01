using System;
using BepInEx.Configuration;
using SpinCore.Translation;
using SpinCore.UI;
using UnityEngine;
using XDMenuPlay;

namespace SpinRequests;

public partial class Plugin
{
    internal static ConfigEntry<int> HttpPort = null!;
    internal static ConfigEntry<string> HttpAddress = null!;
    
    internal static ConfigEntry<int> SocketPort = null!;
    internal static ConfigEntry<string> SocketAddress = null!;
    
    internal static ConfigEntry<bool> EnableQueueNotifications = null!;
    internal static ConfigEntry<bool> DeleteOldMapFiles = null!;
    internal static ConfigEntry<int> ConsiderPlayedAfterThisPercentage = null!;
    internal static ConfigEntry<int> SessionPersistenceLength = null!;

    private void RegisterConfigEntries()
    {
        HttpAddress = Config.Bind("API", "HttpAddress", "127.0.0.1", 
            "IP address for the HTTP API to listen on");
        
        HttpPort = Config.Bind("API", "HttpPort", 6969, 
            "Port for the HTTP API to listen on");
        
        SocketAddress = Config.Bind("API", "SocketAddress", "127.0.0.1",
            "IP address for the WebSocket firehose to listen on");
        
        SocketPort = Config.Bind("API", "SocketPort", 6970,
            "Port for the WebSocket firehose to listen on");

        EnableQueueNotifications = Config.Bind("General", "EnableQueueNotifications", true,
            "Show notifications for maps added to the queue");
        DeleteOldMapFiles = Config.Bind("General", "DeleteOldMapFiles", false,
            "Delete old map files when downloading updated maps");
        ConsiderPlayedAfterThisPercentage = Config.Bind("General", "ConsiderPlayedAfterThisPercentage", 0,
            "How much of the chart must be played before it's considered an already played chart");
        SessionPersistenceLength = Config.Bind("Persistence", "SessionPersistenceLength", 0,
            "How many hours between the file write time of the session history and the current time to wait before considering the current session as a new session");
    }

    private static void CreateModPage()
    {
        CustomPage rootModPage = UIHelper.CreateCustomPage("ModSettings");
        rootModPage.OnPageLoad += RootModPageOnOnPageLoad;
        
        UIHelper.RegisterMenuInModSettingsRoot("SpinRequests_ModName", rootModPage);
        
        TranslationHelper.AddTranslation("SpinRequests_EnableQueueNotifications", "Enable queue notifications");
        TranslationHelper.AddTranslation("SpinRequests_DeleteOldMapFiles", "Delete old map files when downloading updated maps");
    }

    private static void RootModPageOnOnPageLoad(Transform rootModPageTransform)
    {
        CustomGroup modGroup = UIHelper.CreateGroup(rootModPageTransform, nameof(SpinRequests));
        UIHelper.CreateSectionHeader(modGroup, "ModGroupHeader", "SpinRequests_ModName", false);
            
        #region EnableQueueNotifications
        CustomGroup enableQueueNotificationsGroup = UIHelper.CreateGroup(modGroup, "EnableQueueNotificationsGroup");
        enableQueueNotificationsGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateSmallToggle(enableQueueNotificationsGroup, nameof(EnableQueueNotifications),
            "SpinRequests_EnableQueueNotifications", EnableQueueNotifications.Value, value =>
            {
                EnableQueueNotifications.Value = value;
            });
        #endregion
        
        #region DeleteOldMapFiles
        CustomGroup deleteOldMapFilesGroup = UIHelper.CreateGroup(modGroup, "DeleteOldMapFilesGroup");
        deleteOldMapFilesGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateSmallToggle(deleteOldMapFilesGroup, nameof(DeleteOldMapFiles),
            "SpinRequests_DeleteOldMapFiles", DeleteOldMapFiles.Value, value =>
            {
                DeleteOldMapFiles.Value = value;
            });
        #endregion
        
        #region ConsiderPlayedAfterThisPercentage
        CustomGroup considerPlayedGroup = UIHelper.CreateGroup(modGroup, "ConsiderPlayedGroup");
        considerPlayedGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateLabel(considerPlayedGroup, "ConsiderPlayedLabel", "SpinRequests_ConsiderPlayedAfterThisPercentage");
        CustomInputField considerPlayedInput = UIHelper.CreateInputField(considerPlayedGroup, "ConsiderPlayedInput", (_, newValue) =>
        {
            if (!int.TryParse(newValue, out int value))
            {
                return;
            }
            
            ConsiderPlayedAfterThisPercentage.Value = Math.Min(Math.Max(0, value), 100);
        });
        considerPlayedInput.InputField.SetText(ConsiderPlayedAfterThisPercentage.Value.ToString());
        #endregion
        
        #region SessionPersistenceLength
        CustomGroup sessionPersistenceLengthGroup = UIHelper.CreateGroup(modGroup, "SessionPersistenceLength");
        sessionPersistenceLengthGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateLabel(sessionPersistenceLengthGroup, "SessionPersistenceLengthLabel", "SpinRequests_SessionPersistenceLength");
        CustomInputField sessionPersistenceLengthInput = UIHelper.CreateInputField(sessionPersistenceLengthGroup,
            "SessionPersistenceLengthInput", (_, newValue) =>
        {
            if (!int.TryParse(newValue, out int value))
            {
                return;
            }
            
            SessionPersistenceLength.Value = Math.Min(Math.Max(0, value), 24);
        });
        sessionPersistenceLengthInput.InputField.SetText(SessionPersistenceLength.Value.ToString());
        #endregion

        UIHelper.CreateButton(modGroup, "OpenSpinRequestsRepositoryButton", "SpinRequests_GitHubButtonText", () =>
        {
            Application.OpenURL($"https://github.com/TheBlackParrot/{nameof(SpinRequests)}/releases/latest");
        });
    }
}