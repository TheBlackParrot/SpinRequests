using HarmonyLib;
using SpinRequests.UI;
using XDMenuPlay;

namespace SpinRequests.Patches;

[HarmonyPatch]
internal static class CompleteSequencePatches
{
    [HarmonyPatch(typeof(CompleteSequenceGameState), nameof(CompleteSequenceGameState.OnBecameActive))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void OnBecameActive_Patch()
    {
        _ = QueueList.LoadBufferedQueue(false);
    }
}