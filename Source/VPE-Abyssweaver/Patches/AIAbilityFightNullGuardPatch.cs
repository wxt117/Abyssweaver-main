using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VPE_MyExtension;

[HarmonyPatch(typeof(JobGiver_AIAbilityFight), "TryGiveJob")]
public static class Patch_JobGiver_AIAbilityFight_TryGiveJob
{
    public static bool Prefix(Pawn pawn, ref Job __result)
    {
        if (pawn?.abilities != null)
        {
            return true;
        }

        __result = null;
        return false;
    }

    public static Exception Finalizer(Exception __exception, ref Job __result)
    {
        if (__exception is NullReferenceException)
        {
            __result = null;
            return null;
        }

        return __exception;
    }
}
