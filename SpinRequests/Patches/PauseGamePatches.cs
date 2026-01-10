using HarmonyLib;
using SpinRequests.UI;

namespace SpinRequests.Patches;

[HarmonyPatch]
internal static class PauseGamePatches
{
    [HarmonyPatch(typeof(Track), nameof(Track.HandlePauseGame))]
    [HarmonyPostfix]
    private static void HandlePauseGame_Patch()
    {
        _ = QueueList.LoadBufferedQueue(false);
    }
}