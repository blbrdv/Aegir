using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;

namespace Aegir
{

    [HarmonyPatch(typeof(Player))]
    public class PlayerPatch
    {

        readonly static Type str = typeof(string);


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

        // Adding line into HUD left corner message indicating camera clipping status
        [HarmonyTranspiler]
        [HarmonyPatch("ToggleDebugFly")]
	    static IEnumerable<CodeInstruction> ToggleDebugFlyTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = new List<CodeInstruction>(instructions);

            // call string [System.Runtime]System.String::Concat(string, string)
            var concatOperand = AccessTools.Method(str, "Concat", new Type[] { str, str });
            // call string [System.Runtime]System.String::Concat(string, string, string, string)
            var newConcatInstruction = new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(
                        str, 
                        "Concat", 
                        new Type[] { str, str, str, str }));


            var local = generator.DeclareLocal(typeof(bool));
            var newInstructions = new List<CodeInstruction>() {
                // ldstr "\nCamera clipping:"
                new CodeInstruction(OpCodes.Ldstr, "\nCamera clipping:"),

                // ldarg.0
                new CodeInstruction(OpCodes.Ldarg_0),

                // ldfld bool PLayer::m_debugFly
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "m_debugFly")),

                // ldc.i4.0
                new CodeInstruction(OpCodes.Ldc_I4_0),

                // ceq
                new CodeInstruction(OpCodes.Ceq),

                // stloc.0 (local)
                new CodeInstruction(OpCodes.Stloc, local),

                // ldloca.s 0
                new CodeInstruction(OpCodes.Ldloca_S, (byte)0),

                // call instance string [System.Runtime]System.Boolean::ToString()
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(bool), "ToString"))
            };

            var insertingAt = -1;
            for (var index = 0; index < code.Count(); index++)
            {
                if (code[index].opcode == OpCodes.Call && (MethodInfo)code[index].operand == concatOperand)
                {
                    insertingAt = index;
                    break;
                }
            }

            if (insertingAt != -1)
            {
                code[insertingAt] = newConcatInstruction;
                code.InsertRange(insertingAt, newInstructions);

                Plugin.Logger.LogDebug($"ToggleDebugFly patched");
            }

            return code.AsEnumerable();
        }

    }

}
