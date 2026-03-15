using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse.AI;
using Verse;

namespace VPE_MyExtension;

[StaticConstructorOnStartup]
public static class HarmonyBootstrap
{
    static HarmonyBootstrap()
    {
        new Harmony("VPE_MyExtension.SourceFilters").PatchAll();
    }
}

internal static class NociosphereControllerUtility
{
    public static bool IsFriendlyNociosphere(Pawn pawn)
    {
        if (pawn?.kindDef?.defName != "Nociosphere")
        {
            return false;
        }

        Faction controller = GetControllerFaction(pawn);
        return controller != null && !controller.HostileTo(Faction.OfPlayer);
    }

    public static bool IsHostileToController(Pawn source, Thing target)
    {
        if (source == null || target == null)
        {
            return false;
        }

        Faction controller = GetControllerFaction(source);
        if (controller == null)
        {
            return source.HostileTo(target);
        }

        Faction targetFaction = target.Faction;
        if (targetFaction != null)
        {
            return controller.HostileTo(targetFaction);
        }

        return source.HostileTo(target);
    }

    public static Faction GetControllerFaction(Pawn pawn)
    {
        if (pawn?.health?.hediffSet != null)
        {
            HediffDef timerDef = GetTimerDef();
            if (timerDef != null)
            {
                Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
                HediffComp_VoidReturn comp = timer?.TryGetComp<HediffComp_VoidReturn>();
                Faction owner = comp?.GetOwnerFaction();
                if (owner != null)
                {
                    return owner;
                }
            }
        }

        return pawn?.Faction;
    }

    public static HediffDef GetTimerDef()
    {
        return MyExtensionDefOf.VPEMYX_VoidReturnTimer ??
               DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_VoidReturnTimer");
    }

    public static bool HasVoidTimer(Pawn pawn)
    {
        HediffDef timerDef = GetTimerDef();
        return timerDef != null && pawn?.health?.hediffSet?.HasHediff(timerDef) == true;
    }

    public static bool TryGetOwnerFaction(Pawn pawn, out Faction ownerFaction)
    {
        ownerFaction = null;
        if (pawn == null)
        {
            return false;
        }

        HediffDef timerDef = GetTimerDef();
        if (timerDef == null || pawn.health?.hediffSet == null)
        {
            return false;
        }

        Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
        if (timer == null)
        {
            return false;
        }

        HediffComp_VoidReturn comp = timer.TryGetComp<HediffComp_VoidReturn>();
        ownerFaction = comp?.GetOwnerFaction();
        return ownerFaction != null;
    }

    public static void EnsureTimerAndOwner(Pawn pawn, Faction ownerFaction, Pawn source = null)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return;
        }

        if (EntitySummonUtility.IsFleshMoldingConstruct(pawn))
        {
            return;
        }

        HediffDef timerDef = GetTimerDef();
        if (timerDef == null)
        {
            return;
        }

        Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
        if (timer == null)
        {
            pawn.health.AddHediff(timerDef);
            timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
        }

        if (ownerFaction == null)
        {
            return;
        }

        if (pawn.Faction != ownerFaction)
        {
            try
            {
                pawn.SetFaction(ownerFaction, source);
            }
            catch
            {
            }
        }

        HediffComp_VoidReturn comp = timer?.TryGetComp<HediffComp_VoidReturn>();
        comp?.SetOwnerFaction(ownerFaction);
    }

    public static bool TryGetControlledFaction(Thing thing, out Faction faction)
    {
        faction = null;
        if (thing is not Pawn pawn || !HasVoidTimer(pawn))
        {
            return false;
        }

        if (TryGetOwnerFaction(pawn, out faction))
        {
            return true;
        }

        faction = pawn.Faction;
        return faction != null;
    }
}

[HarmonyPatch(typeof(CompCauseHediff_AoE), "IsPawnAffected")]
public static class Patch_CompCauseHediff_AoE_IsPawnAffected
{
    public static bool Prefix(CompCauseHediff_AoE __instance, Pawn target, ref bool __result)
    {
        Pawn source = __instance?.parent as Pawn;
        if (source == null || target == null)
        {
            return true;
        }

        if (source.kindDef?.defName != "Nociosphere")
        {
            return true;
        }

        if (!NociosphereControllerUtility.IsFriendlyNociosphere(source))
        {
            return true;
        }

        // Source-level filter: friendly controlled nociosphere only affects controller-hostile targets.
        if (!NociosphereControllerUtility.IsHostileToController(source, target))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(JobGiver_NociosphereFight), "TryGiveJob")]
public static class Patch_JobGiver_NociosphereFight_TryGiveJob
{
    public static void Postfix(Pawn pawn, ref Job __result)
    {
        if (!NociosphereControllerUtility.IsFriendlyNociosphere(pawn) || __result == null)
        {
            return;
        }

        Thing target = __result.targetA.Thing;
        if (target != null && !NociosphereControllerUtility.IsHostileToController(pawn, target))
        {
            __result = null;
        }
    }
}

[HarmonyPatch(typeof(JobGiver_NociosphereSkip), "TryGiveJob")]
public static class Patch_JobGiver_NociosphereSkip_TryGiveJob
{
    public static void Postfix(Pawn pawn, ref Job __result)
    {
        if (!NociosphereControllerUtility.IsFriendlyNociosphere(pawn) || __result == null)
        {
            return;
        }

        Thing targetA = __result.targetA.Thing;
        Thing targetB = __result.targetB.Thing;
        if ((targetA != null && !NociosphereControllerUtility.IsHostileToController(pawn, targetA)) ||
            (targetB != null && !NociosphereControllerUtility.IsHostileToController(pawn, targetB)))
        {
            __result = null;
        }
    }
}

[HarmonyPatch(typeof(CompDreadmeld), "SpawnPawn")]
public static class Patch_CompDreadmeld_SpawnPawn
{
    public static void Postfix(CompDreadmeld __instance, Pawn pawn)
    {
        Pawn source = __instance?.parent as Pawn;
        if (pawn == null || source == null)
        {
            return;
        }

        if (!NociosphereControllerUtility.TryGetOwnerFaction(source, out Faction ownerFaction))
        {
            ownerFaction = source.Faction;
        }

        NociosphereControllerUtility.EnsureTimerAndOwner(pawn, ownerFaction, source);
    }
}

[HarmonyPatch(typeof(DeathActionWorker_Divide), "SpawnPawn")]
public static class Patch_DeathActionWorker_Divide_SpawnPawn
{
    public static void Postfix(Pawn child, Pawn parent)
    {
        if (child == null || parent == null)
        {
            return;
        }

        // Only inherit for summoned entities lineage (parent has our tether).
        if (!NociosphereControllerUtility.HasVoidTimer(parent))
        {
            return;
        }

        NociosphereControllerUtility.TryGetOwnerFaction(parent, out Faction ownerFaction);
        ownerFaction ??= parent.Faction;
        NociosphereControllerUtility.EnsureTimerAndOwner(child, ownerFaction, parent);
    }
}

[HarmonyPatch(typeof(GenHostility), "HostileTo", new[] { typeof(Thing), typeof(Thing) })]
public static class Patch_GenHostility_HostileTo_ThingThing
{
    public static void Postfix(Thing a, Thing b, ref bool __result)
    {
        if (!__result || a == null || b == null)
        {
            return;
        }

        bool aControlled = NociosphereControllerUtility.TryGetControlledFaction(a, out Faction aController);
        bool bControlled = NociosphereControllerUtility.TryGetControlledFaction(b, out Faction bController);
        if (!aControlled && !bControlled)
        {
            return;
        }

        Faction effectiveA = aControlled ? aController : a.Faction;
        Faction effectiveB = bControlled ? bController : b.Faction;
        if (effectiveA != null && effectiveB != null)
        {
            __result = effectiveA.HostileTo(effectiveB);
            return;
        }

        if (aControlled && bControlled && aController == bController)
        {
            __result = false;
            return;
        }

        if (aControlled && b.Faction == aController)
        {
            __result = false;
            return;
        }

        if (bControlled && a.Faction == bController)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(GenHostility), "HostileTo", new[] { typeof(Thing), typeof(Faction) })]
public static class Patch_GenHostility_HostileTo_ThingFaction
{
    public static void Postfix(Thing t, Faction fac, ref bool __result)
    {
        if (!__result || t == null || fac == null)
        {
            return;
        }

        if (!NociosphereControllerUtility.TryGetControlledFaction(t, out Faction controller) || controller == null)
        {
            return;
        }

        __result = controller.HostileTo(fac);
        if (controller == fac)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(NociosphereUtility), "FindTarget")]
public static class Patch_NociosphereUtility_FindTarget
{
    public static void Postfix(Pawn pawn, ref Thing __result)
    {
        if (pawn == null || !NociosphereControllerUtility.IsFriendlyNociosphere(pawn))
        {
            return;
        }

        if (__result != null && NociosphereControllerUtility.IsHostileToController(pawn, __result))
        {
            return;
        }

        Thing best = FindClosestHostilePawn(pawn);
        __result = best;
    }

    private static Thing FindClosestHostilePawn(Pawn source)
    {
        Map map = source?.Map;
        if (map == null)
        {
            return null;
        }

        Thing best = null;
        float bestDistSq = float.MaxValue;
        IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return null;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == null || p == source || p.Dead || !p.Spawned)
            {
                continue;
            }

            if (!NociosphereControllerUtility.IsHostileToController(source, p))
            {
                continue;
            }

            float d = (p.Position - source.Position).LengthHorizontalSquared;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = p;
            }
        }

        return best;
    }
}

[HarmonyPatch(typeof(CompDevourer), "GetDigestionTicks")]
public static class Patch_CompDevourer_GetDigestionTicks
{
    private const int WailingDigestTicks = 900; // 15s

    public static bool Prefix(CompDevourer __instance, ref int __result)
    {
        Pawn pawn = __instance?.Pawn;
        if (pawn?.kindDef?.defName != "VPEMYX_WailingFleshMound")
        {
            return true;
        }

        __result = WailingDigestTicks;
        return false;
    }
}
