using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Aegir
{

    [HarmonyPatch(typeof(Player))]
    public class PlayerPatch
    {

        [HarmonyPostfix]
        [HarmonyPatch("ToggleDebugFly")]
        static void ToggleDebugFlyPostfix(bool ___m_debugFly)
        {
            var distance = GameCameraPatch.DefaultMinWaterDistance;

            if (___m_debugFly)
            {
                distance = -1000f;
            }

            GameCamera.instance.m_minWaterDistance = distance;
            Plugin.Logger.LogDebug($"Min water distance set to {distance}");
        }

    }

}
