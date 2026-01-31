using HarmonyLib;
using Unity.Scenes;

namespace KindredSacrifice.Patches;

[HarmonyPatch(typeof(SceneSectionStreamingSystem), nameof(SceneSectionStreamingSystem.ShutdownAsynchrnonousStreamingSupport))]
public static class InitializationPatch
{
	[HarmonyPostfix]
	public static void OneShot_AfterLoad_InitializationPatch()
	{
		if (!Core.IsServer) return;

		Core.InitializeAfterLoaded();
		Core.SacrificeService?.LoadExistingSacrificeCage();
		Plugin.Harmony?.Unpatch(typeof(SceneSectionStreamingSystem).GetMethod("ShutdownAsynchrnonousStreamingSupport"), typeof(InitializationPatch).GetMethod("OneShot_AfterLoad_InitializationPatch"));
	}
}
