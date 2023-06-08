using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Aegir
{
    
    [HarmonyPatch(typeof(GameCamera))]
    public class CameraPatch
    {
        private static float defaultMinWaterDistance;

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void AwakePostfix(float ___m_minWaterDistance)
        {
            defaultMinWaterDistance = ___m_minWaterDistance;
            Plugin.Logger.LogDebug($"Default min water distance is {defaultMinWaterDistance}");
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetCameraPosition")]
        static void GetCameraPositionPrefix(ref float ___m_minWaterDistance)
        {
            var localPlayer = Player.m_localPlayer;
            if (localPlayer == null)
            {
                return;
            }

            if (localPlayer.InDebugFlyMode())
            {
                ___m_minWaterDistance = -1000f;
            }
            else
            {
                ___m_minWaterDistance = defaultMinWaterDistance;
            }

            Plugin.Logger.LogDebug($"Min water distance is {___m_minWaterDistance}");
        }

    }

}
