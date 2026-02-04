using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;

namespace Aegir;

[HarmonyPatch(typeof(Player))]
public class PlayerPatch
{
    private static readonly Type Str = typeof(string);

    [HarmonyPostfix]
    [HarmonyPatch("ToggleDebugFly")]
    // ReSharper disable once InconsistentNaming
    static void ToggleDebugFlyPostfix(bool ___m_debugFly)
    {
        var distance = GameCameraPatch.DefaultMinWaterDistance;

        if (___m_debugFly) distance = -1000f;

        GameCamera.instance.m_minWaterDistance = distance;
        Plugin.Logger.LogDebug($"Min water distance set to {distance}");
    }

    // Adding line into HUD left corner message indicating camera clipping status
    [HarmonyTranspiler]
    [HarmonyPatch("ToggleDebugFly")]
    static IEnumerable<CodeInstruction> ToggleDebugFlyTranspiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        var code = new List<CodeInstruction>(instructions);

        // call string [System.Runtime]System.String::Concat(string, string)
        var concatOperand = AccessTools.Method(Str, "Concat", [Str, Str]);
        // call string [System.Runtime]System.String::Concat(string, string, string, string)
        var newConcatInstruction = new CodeInstruction(
            OpCodes.Call,
            AccessTools.Method(
                Str,
                "Concat",
                [Str, Str, Str, Str]));

        var local = generator.DeclareLocal(typeof(bool));
        var newInstructions = new List<CodeInstruction>
        {
            // ldstr "\nCamera clipping:"
            new(OpCodes.Ldstr, "\nCamera clipping:"),

            // ldarg.0
            new(OpCodes.Ldarg_0),

            // ldfld bool PLayer::m_debugFly
            new(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "m_debugFly")),

            // ldc.i4.0
            new(OpCodes.Ldc_I4_0),

            // ceq
            new(OpCodes.Ceq),

            // stloc.0 (local)
            new(OpCodes.Stloc, local),

            // ldloca.s 0
            new(OpCodes.Ldloca_S, (byte)0),

            // call instance string [System.Runtime]System.Boolean::ToString()
            new(OpCodes.Call, AccessTools.Method(typeof(bool), "ToString"))
        };

        var insertingAt = -1;
        for (var index = 0; index < code.Count; index++)
        {
            if (code[index].opcode != OpCodes.Call || (MethodInfo)code[index].operand != concatOperand) continue;
            insertingAt = index;
            break;
        }

        if (insertingAt == -1) return code.AsEnumerable();

        code[insertingAt] = newConcatInstruction;
        code.InsertRange(insertingAt, newInstructions);

        Plugin.Logger.LogDebug("ToggleDebugFly patched");

        return code.AsEnumerable();
    }
}
