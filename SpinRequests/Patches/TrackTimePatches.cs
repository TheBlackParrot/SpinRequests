using HarmonyLib;

namespace SpinRequests.Patches;

[HarmonyPatch]
internal class TrackTimePatches
{
    internal static double NeededTrackTime = 0f;
    internal static bool HasSetPlayed;
    
    [HarmonyPatch(typeof(PlayState), nameof(PlayState.UpdatePlaybackTime))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    internal static void UpdatePlaybackTime_Patch(PlayState __instance)
    {
        if (__instance.previewState != PreviewState.NotPreview)
        {
            return;
        }

        if (Plugin.PlayedMapHistory.Count == 0 || HasSetPlayed)
        {
            return;
        }

        // ReSharper disable once InvertIf
        if (__instance.currentTrackTime >= NeededTrackTime && !HasSetPlayed)
        {
#if DEBUG
            Plugin.Log.LogInfo("set played on map");
#endif
            HasSetPlayed = true;
            
            Plugin.AddToCrossedThresholdList(Plugin.PlayedMapHistory[0].FileReference);
        }
    }
}