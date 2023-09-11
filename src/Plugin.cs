using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Aegir
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new(PluginInfo.PLUGIN_GUID);

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            harmony.PatchAll();
            Logger.LogMessage($"Plugin is loaded!");
        }
    }
}
