using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpinCore.UI;
using SpinRequests.Classes;
using UnityEngine;

namespace SpinRequests.UI;

internal static class QueueList
{
    internal static CustomSidePanel? QueueListPanel;
    internal static CustomGroup? QueueListContainer;
    
    internal static readonly List<QueueEntry> BufferedList = [];
    internal static readonly List<QueueEntry> Entries = [];

    internal static void CreateQueueListPanel()
    {
        // this has to be in a Task.Run instead of just making this an async Task. do i know why? Nope
        Task.Run(async () =>
        {
            await Awaitable.MainThreadAsync();

            Sprite? sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "Playlist");
            
            QueueListPanel = UIHelper.CreateSidePanel(nameof(QueueListPanel), "SpinRequests_RequestQueueText", sprite);
            QueueListPanel.OnSidePanelLoaded += QueueListPanelOnSidePanelLoaded;
            
            CheckIndicatorDot();
        });
    }

    private static void QueueListPanelOnSidePanelLoaded(Transform panelTransform)
    {
        QueueListPanel!.OnSidePanelLoaded -= QueueListPanelOnSidePanelLoaded;
        QueueListContainer = UIHelper.CreateGroup(panelTransform, "QueueListContainer");
        
        CheckIndicatorDot();

        _ = LoadBufferedQueue();
    }

    private static async Task LoadBufferedQueue()
    {
        await Awaitable.MainThreadAsync();
        
        foreach (QueueEntry entry in BufferedList)
        {
            await entry.AddToQueue(true);
        }
        
        BufferedList.Clear();
    }

    internal static void CheckIndicatorDot()
    {
        GameObject? button = GameObject.Find("Dot Selector Button QueueListPanel");
        if (button == null)
        {
            return;
        }
        
        Transform? indicatorDotTransform = button.transform.Find("IconContainer/IndicatorDot");
        indicatorDotTransform?.gameObject.SetActive(Entries.Concat(BufferedList).Any());
    }
}