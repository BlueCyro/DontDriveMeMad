using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using CodeX;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;


namespace DontDriveMeMad;
public class DontDriveMeMad : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "DontDriveMeMad";
    public override string Version => "1.0.0";

    public override void OnEngineInit()
    {
        Harmony harmony = new Harmony("net.Cyro.DontDriveMeMad");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(FullBodyCalibrator), "OnCommonUpdate")]
    static class SliderPatcher
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int FoundIndex = -1;
            // First we find an initial anchor point. Luckily a slot called "Calibration Reference" is added relatively close to the sliders we want to edit, so we search for that first.
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand != null && codes[i].operand.GetType() == typeof(string) && codes[i].operand.ToString() == "Calibration Reference")
                {
                    NeosMod.Msg($"Found {codes[i].opcode} with value {codes[i].operand}");
                    FoundIndex = i;
                    break;
                }
            }

            if (FoundIndex == -1)
            {
                NeosMod.Msg("Could not find target string \"Calibration Reference\" in OnCommonUpdate for FullBodyCalibrator, cancelling");
                return codes;
            }

            int RotatableIndex = -1;
            // Next we search forward from our anchor point to find a reference in the code to the "Rotatable" field on the slider.
            for (int i = FoundIndex; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo && codes[i].operand as FieldInfo == typeof(Slider).GetField("Rotatable", BindingFlags.Instance | BindingFlags.Public))
                {
                    NeosMod.Msg($"Found {codes[i].opcode} with value {codes[i].operand}");
                    RotatableIndex = i;
                    break;
                }
            }

            if (RotatableIndex == -1)
            {
                NeosMod.Msg("Could not find Rotatable FieldInfo in OnCommonUpdate for FullBodyCalibrator, cancelling");
                return codes;
            }

            int SetterIndex = -1;
            // Lastly, we'll search for where it sets the Rotatable with it's setter. The goal here is to find the end of the part of the code where it sets the properties
            // so we can neatly insert our own code to set DontDrive to true as well. Technically we could just skip forwards in the instructions by 2, but that's more brittle.
            for (int i = RotatableIndex; i < codes.Count; i++)
            {
                if (codes[i - 1].opcode == OpCodes.Ldc_I4_1 && 
                    codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo && 
                    codes[i].operand as MethodInfo == typeof(SyncField<bool>).GetProperty("Value", BindingFlags.Instance | BindingFlags.Public).GetSetMethod())
                {
                    NeosMod.Msg($"Found {codes[i].opcode} with value {codes[i].operand}");
                    SetterIndex = i;
                    break;
                }
            }

            if (SetterIndex == -1)
            {
                NeosMod.Msg("Could not find end of target code block, cancelling (start crying)");
                return codes;
            }

            NeosMod.Msg("Successfully found all conditions, inserting opcodes for DontDrive");

            // Now that we've found all of our conditions, we can neatly insert code right at the end of that code block which will flick DontDrive to true.
            // Handily, the slider will already be on the stack so we can just call the `Dup` opcode to duplicate it again like the previous code block does ;)
            codes.InsertRange(SetterIndex + 1, new List<CodeInstruction>() {
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Ldfld, typeof(Slider).GetField("DontDrive", BindingFlags.Instance | BindingFlags.Public)),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Callvirt, typeof(SyncField<bool>).GetProperty("Value", BindingFlags.Instance | BindingFlags.Public).GetSetMethod())
            });

            NeosMod.Msg("Code insertion complete, enjoy not going mad when calibrating your full body!");

            return codes;
        }
    }
}
