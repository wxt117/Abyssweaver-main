using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

[HarmonyPatch(typeof(ThoughtHandler), "TotalMoodOffset")]
public static class Patch_ThoughtHandler_TotalMoodOffset_EmbraceGloom
{
    private static readonly AccessTools.FieldRef<ThoughtHandler, Pawn> PawnFieldRef =
        AccessTools.FieldRefAccess<ThoughtHandler, Pawn>("pawn");

    private static readonly HashSet<string> DirectThoughtDefNames = new HashSet<string>
    {
        "UnnaturalDarkness",
        "SwallowedByDarkness",
        "UnnaturalCorpse",
        "DarknessLifted",
        "ImprisonedWithEntity",
        "CapturedEntity"
    };

    private static readonly HashSet<string> WeatherConditionDefNames = new HashSet<string>
    {
        "UnnaturalDarkness",
        "UnnaturalFog",
        "GrayPall",
        "BloodRain",
        "UnnaturalHeat"
    };

    public static void Postfix(ThoughtHandler __instance, ref float __result)
    {
        Pawn pawn = PawnFieldRef(__instance);
        if (pawn?.health?.hediffSet == null || !pawn.health.hediffSet.HasHediff(MyExtensionDefOf.VPEMYX_EmbraceGloom))
        {
            return;
        }

        List<Thought> groups = new List<Thought>();
        __instance.GetDistinctMoodThoughtGroups(groups);

        float ignoredPenalty = 0f;
        for (int i = 0; i < groups.Count; i++)
        {
            Thought thought = groups[i];
            ThoughtDef def = thought?.def;
            if (def == null || !ShouldIgnore(def))
            {
                continue;
            }

            float offset = __instance.MoodOffsetOfGroup(thought);
            if (offset < 0f)
            {
                ignoredPenalty -= offset;
            }
        }

        if (ignoredPenalty > 0f)
        {
            __result += ignoredPenalty;
        }
    }

    private static bool ShouldIgnore(ThoughtDef def)
    {
        if (def == null)
        {
            return false;
        }

        if (DirectThoughtDefNames.Contains(def.defName))
        {
            return true;
        }

        if (def.gameCondition != null)
        {
            if (WeatherConditionDefNames.Contains(def.gameCondition.defName))
            {
                return true;
            }

            if (ContainsKeyword(def.gameCondition.defName))
            {
                return true;
            }
        }

        if (ContainsKeyword(def.defName) || ContainsKeyword(def.workerClass?.Name))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsKeyword(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string lower = text.ToLowerInvariant();
        return lower.Contains("dark") ||
               lower.Contains("entity") ||
               lower.Contains("unnatural") ||
               lower.Contains("void") ||
               lower.Contains("graypall") ||
               lower.Contains("bloodrain") ||
               lower.Contains("fog");
    }
}
