using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpinCore.UI;
using SpinRequests.Classes;
using SpinRequests.Services;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SpinRequests.UI;

internal static class QueueList
{
    private static Button? _queueListButton;
    internal static CustomSidePanel? QueueListPanel;
    internal static CustomGroup? QueueListContainer;
    
    internal static readonly List<QueueEntry> BufferedList = [];
    internal static readonly List<QueueEntry> Entries = [];
    
#if DEBUG
    internal static bool IsOpen = true;
#else
    internal static bool IsOpen;
#endif
    
    private static string PersistentQueueFilename => Path.Combine(Plugin.DataPath, "queue.json");

    internal static void CreateQueueListPanel()
    {
        // this has to be in a Task.Run instead of just making this an async Task. do i know why? Nope
        Task.Run(async () =>
        {
            await Awaitable.MainThreadAsync();

            Sprite? sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "Playlist");
            
            QueueListPanel = UIHelper.CreateSidePanel(nameof(QueueListPanel), "SpinRequests_RequestQueueText", sprite);
            QueueListPanel.OnSidePanelLoaded += QueueListPanelOnSidePanelLoaded;
        });
    }

    private static void UpdateAllRequesterInfoFields()
    {
        foreach (QueueEntry queueEntry in Entries)
        {
            queueEntry.UpdateRequesterInformation();
        }
    }

    private static void QueueListPanelOnSidePanelLoaded(Transform panelTransform)
    {
        QueueListPanel!.OnSidePanelLoaded -= QueueListPanelOnSidePanelLoaded;
        
        _queueListButton = GameObject.Find("Dot Selector Button QueueListPanel").GetComponent<Button>();
        _queueListButton.onClick.AddCall(new InvokableCall(UpdateAllRequesterInfoFields));
        
        CustomGroup queueListOptionsGroup = UIHelper.CreateGroup(panelTransform, "QueueListOptionsGroup");
        queueListOptionsGroup.LayoutDirection = Axis.Horizontal;
        UIHelper.CreateSmallToggle(queueListOptionsGroup, "AllowRequestsToggle",
            "SpinRequests_AllowRequestsText", IsOpen, value =>
            {
                IsOpen = value;
                SocketApi.Broadcast("RequestsAllowed", IsOpen);
            });

        UIHelper.CreateSectionHeader(panelTransform, "QueueListListHeader", "SpinRequests_MenuButtonText", false);

        QueueListContainer = UIHelper.CreateGroup(panelTransform, "QueueListContainer");
        CheckIndicatorDot();
        _ = LoadBufferedQueue();
    }

    internal static async Task LoadBufferedQueue(bool silent = true)
    {
        await Awaitable.MainThreadAsync();
        await LoadPersistentQueue();
        
        foreach (QueueEntry entry in BufferedList)
        {
            try
            {
                await entry.AddToQueue(silent);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
        
        BufferedList.Clear();
    }

    private static async Task LoadPersistentQueue()
    {
        await Awaitable.MainThreadAsync();
        
        if (!File.Exists(PersistentQueueFilename))
        {
            Plugin.Log.LogInfo("No persistent queue data, skipping loading it");
            return;
        }
        
        Plugin.Log.LogInfo("Loading persistent queue...");
        
        QueueEntry[]? entries = JsonConvert.DeserializeObject<QueueEntry[]>(File.ReadAllText(PersistentQueueFilename));
        if (entries == null)
        {
            Plugin.Log.LogInfo("Persistent queue was empty (null)");
            return;
        }

        if (entries.Length > 0)
        {
            BufferedList.AddRange(entries);
            Plugin.Log.LogInfo("Loaded persistent queue");
        }
        else
        {
            Plugin.Log.LogInfo("Persistent queue was empty");
        }
    }

    internal static void SavePersistentQueue() => File.WriteAllText(Path.Combine(Plugin.DataPath, "queue.json"),
        JsonConvert.SerializeObject(Entries, Formatting.Indented));

    internal static void CheckIndicatorDot()
    {
        GameObject? button = GameObject.Find("Dot Selector Button QueueListPanel");
        Transform? indicatorDotTransform = button?.transform.Find("IconContainer/IndicatorDot");
        indicatorDotTransform?.gameObject.SetActive(Entries.Count + BufferedList.Count != 0);
    }
}