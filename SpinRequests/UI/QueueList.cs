using System.Collections.Generic;
using System.Threading.Tasks;
using SpinCore.UI;
using SpinRequests.Classes;
using UnityEngine;

namespace SpinRequests.UI;

internal static class QueueList
{
    internal static CustomSidePanel QueueListPanel = null!;
    internal static CustomGroup? QueueListContainer;
    
    internal static readonly List<QueueEntry> BufferedList = [];

    internal static void CreateQueueListPanel()
    {
        // figure out the sprite later i cba
        // this will *work* i just need the game to Load
        //Sprite? trackListIcon = GameObject.Find("TrackListButton").transform.Find("IconContainer/Icon").GetComponent<Image>().sprite;
        
        QueueListPanel = UIHelper.CreateSidePanel(nameof(QueueListPanel), "SpinRequests_RequestQueueText");
        QueueListPanel.OnSidePanelLoaded += QueueListPanelOnSidePanelLoaded;
    }

    private static void QueueListPanelOnSidePanelLoaded(Transform panelTransform)
    {
        QueueListPanel.OnSidePanelLoaded -= QueueListPanelOnSidePanelLoaded;
        QueueListContainer = UIHelper.CreateGroup(panelTransform, "QueueListContainer");

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
}