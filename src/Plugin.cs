using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Aegir;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;
    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        Logger = base.Logger;

        _harmony.PatchAll();
        Logger.LogMessage("Plugin is loaded!");
    }
}
