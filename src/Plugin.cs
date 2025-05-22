using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Aegir;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

    internal new static ManualLogSource Logger;

    private void Awake()
    {
        Logger = base.Logger;

        _harmony.PatchAll();
        Logger.LogMessage($"Plugin is loaded!");
    }
}
