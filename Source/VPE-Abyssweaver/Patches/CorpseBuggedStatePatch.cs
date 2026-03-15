using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

[HarmonyPatch(typeof(Corpse), nameof(Corpse.SpawnSetup))]
public static class Patch_Corpse_SpawnSetup_BuggedStateFix
{
    public static bool Prefix(Corpse __instance)
    {
        if (__instance == null)
        {
            return true;
        }

        try
        {
            if (!__instance.Bugged)
            {
                return true;
            }

            if (TryRepair(__instance) && !__instance.Bugged)
            {
                return true;
            }

            // Unrecoverable bugged corpse: discard it to avoid persistent error spam.
            __instance.Destroy(DestroyMode.Vanish);
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool TryRepair(Corpse corpse)
    {
        Pawn inner = corpse.InnerPawn;
        if (inner == null)
        {
            return false;
        }

        if (inner.kindDef != null)
        {
            return true;
        }

        PawnKindDef fallback = ResolveFallbackKind(inner);
        if (fallback == null)
        {
            return false;
        }

        inner.kindDef = fallback;
        return true;
    }

    private static PawnKindDef ResolveFallbackKind(Pawn pawn)
    {
        if (pawn == null || pawn.def == null)
        {
            return null;
        }

        if (pawn.RaceProps?.Humanlike == true)
        {
            return PawnKindDefOf.Colonist;
        }

        return DefDatabase<PawnKindDef>.AllDefsListForReading
            .FirstOrDefault(k => k != null && k.race == pawn.def);
    }
}
