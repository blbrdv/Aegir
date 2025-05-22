using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Aegir
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            harmony.PatchAll();
            Logger.LogMessage($"Plugin is loaded!");
        }
    }
}
