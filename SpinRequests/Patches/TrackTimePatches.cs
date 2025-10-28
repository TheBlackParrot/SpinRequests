using HarmonyLib;

namespace SpinRequests.Patches;

//PlayState.Active.UpdatePlaybackTime();
//PlayState.Active.SetMusicPlaybackTime();

[HarmonyPatch]
internal class TrackTimePatches
{
    internal static double _neededTrackTime = 0f;
    internal static bool _hasSetPlayed = false;
    
    [HarmonyPatch(typeof(PlayState), nameof(PlayState.UpdatePlaybackTime))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    internal static void UpdatePlaybackTime_Patch(PlayState __instance)
    {
        if (__instance.previewState != PreviewState.NotPreview)
        {
            return;
        }

        if (Plugin.PlayedMapHistory.Count == 0 || _hasSetPlayed)
        {
            return;
        }

        // ReSharper disable once InvertIf
        if (__instance.currentTrackTime >= _neededTrackTime && !_hasSetPlayed)
        {
#if DEBUG
            Plugin.Log.LogInfo("set played on map");
#endif
            _hasSetPlayed = true;
            
            Plugin.AddToCrossedThresholdList(Plugin.PlayedMapHistory[0].FileReference);
        }
    }
}