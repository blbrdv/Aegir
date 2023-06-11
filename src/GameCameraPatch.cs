using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Aegir
{
    
    [HarmonyPatch(typeof(GameCamera))]
    public class GameCameraPatch
    {
        public static float DefaultMinWaterDistance { get; private set; }

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void AwakePostfix(float ___m_minWaterDistance)
        {
            DefaultMinWaterDistance = ___m_minWaterDistance;
            Plugin.Logger.LogDebug($"Default min water distance is {___m_minWaterDistance}");
        }

    }

}
