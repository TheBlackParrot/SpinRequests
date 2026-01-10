using System.Threading.Tasks;

namespace SpinRequests.Patches;

internal static class TrackTimeTracking
{
    internal static double NeededTrackTime = 0f;
    internal static bool HasSetPlayed;
    
    internal static Task? TimerTask;

    internal static async Task RunTimer()
    {
        if (TimerTask != null)
        {
            return;
        }
        
        while (true)
        {
            await Task.Delay(1000);
            PlaybackTimeTimer();
        }
    }
    
    private static void PlaybackTimeTimer()
    {
        if (PlayState.Active?.previewState != PreviewState.NotPreview)
        {
            // null will also hit here
            return;
        }
        if (Plugin.PlayedMapHistory.Count == 0 || HasSetPlayed)
        {
            return;
        }
        
#if DEBUG
        Plugin.Log.LogInfo($"Track time: {PlayState.Active.currentTrackTime}");
#endif

        // ReSharper disable once InvertIf
        if (PlayState.Active.currentTrackTime >= NeededTrackTime && !HasSetPlayed)
        {
#if DEBUG
            Plugin.Log.LogInfo("set played on map");
#endif
            HasSetPlayed = true;
            
            Plugin.AddToCrossedThresholdList(Plugin.PlayedMapHistory[0].FileReference);
        }
    }
}