using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

internal static class PassiveAutoApplyUtility
{
    private const int PassiveCheckInterval = 120;
    private const string DistortedMetabolismAbilityDefName = "VPEMYX_DistortedMetabolismActivate";
    private const string EmbraceGloomAbilityDefName = "VPEMYX_EmbraceGloomActivate";

    public static void Apply(CompAbilities compAbilities, bool ignoreInterval)
    {
        Pawn pawn = compAbilities?.Pawn;
        if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!ignoreInterval && !pawn.IsHashIntervalTick(PassiveCheckInterval))
        {
            return;
        }

        bool hasDistortedMetabolism = false;
        bool hasEmbraceGloom = false;
        var learned = compAbilities.LearnedAbilities;
        if (learned == null || learned.Count == 0)
        {
            return;
        }

        for (int i = 0; i < learned.Count; i++)
        {
            string defName = learned[i]?.def?.defName;
            if (defName == DistortedMetabolismAbilityDefName)
            {
                hasDistortedMetabolism = true;
            }
            else if (defName == EmbraceGloomAbilityDefName)
            {
                hasEmbraceGloom = true;
            }

            if (hasDistortedMetabolism && hasEmbraceGloom)
            {
                break;
            }
        }

        if (hasDistortedMetabolism)
        {
            EnsureHediff(pawn, MyExtensionDefOf.VPEMYX_DistortedMetabolism);
        }

        if (hasEmbraceGloom)
        {
            EnsureHediff(pawn, MyExtensionDefOf.VPEMYX_EmbraceGloom);
        }
    }

    private static void EnsureHediff(Pawn pawn, HediffDef hediffDef)
    {
        if (hediffDef == null || pawn?.health?.hediffSet == null)
        {
            return;
        }

        if (!pawn.health.hediffSet.HasHediff(hediffDef))
        {
            pawn.health.AddHediff(hediffDef);
        }
    }
}

[HarmonyPatch(typeof(CompAbilities), nameof(CompAbilities.CompTick))]
public static class Patch_CompAbilities_CompTick_PassiveAutoApply
{
    public static void Postfix(CompAbilities __instance)
    {
        PassiveAutoApplyUtility.Apply(__instance, ignoreInterval: false);
    }
}

[HarmonyPatch]
public static class Patch_CompAbilities_PassiveAutoApply_OnLearn
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        string[] names =
        {
            "LearnAbility",
            "GiveAbility",
            "TryLearnAbility",
            "AddAbility",
        };

        for (int i = 0; i < names.Length; i++)
        {
            MethodInfo method = AccessTools.Method(typeof(CompAbilities), names[i]);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    public static void Postfix(CompAbilities __instance)
    {
        PassiveAutoApplyUtility.Apply(__instance, ignoreInterval: true);
    }
}
