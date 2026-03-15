using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VPE_MyExtension;

public class Ability_FleshMolding : VEF.Abilities.Ability
{
    private AbilityExtension_FleshMolding Config => def.GetModExtension<AbilityExtension_FleshMolding>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (CasterPawn == null || Config == null || targets == null || targets.Length == 0)
        {
            return;
        }

        GlobalTargetInfo target = targets[0];
        if (target.Map == null || !target.Cell.InBounds(target.Map))
        {
            return;
        }

        Map map = target.Map;
        float radius = Config.radius > 0f ? Config.radius : 8f;
        List<Corpse> corpses = CollectFreshCorpses(map, target.Cell, radius);
        List<Pawn> victims = CollectDownedHostiles(map, target.Cell, radius, CasterPawn);
        int materialCount = corpses.Count + victims.Count;

        int tier1Required = Config.tier1Required > 0 ? Config.tier1Required : 5;
        if (materialCount < tier1Required)
        {
            Messages.Message(
                "VPEMYX_Message_FleshMolding_NotEnoughMaterials".Translate(tier1Required, materialCount),
                CasterPawn,
                MessageTypeDefOf.RejectInput,
                historical: false);
            return;
        }

        PawnKindDef resultKind = ResolveResultKind(materialCount);
        if (resultKind == null)
        {
            Messages.Message(
                "VPEMYX_Message_FleshMolding_NoResultKind".Translate(),
                CasterPawn,
                MessageTypeDefOf.RejectInput,
                historical: false);
            return;
        }

        ConsumeCorpses(corpses);
        ConsumeVictims(victims);

        IntVec3 spawnCell = CellFinder.StandableCellNear(target.Cell, map, 3f);
        Pawn generated = EntitySummonUtility.GeneratePawnSafe(resultKind, CasterPawn.Faction);
        if (generated == null)
        {
            Messages.Message(
                "VPEMYX_Message_FleshMolding_GenerationFailed".Translate(),
                CasterPawn,
                MessageTypeDefOf.RejectInput,
                historical: false);
            return;
        }

        Pawn spawned = (Pawn)GenSpawn.Spawn(generated, spawnCell, map, WipeMode.Vanish);
        EntitySummonUtility.PostProcessSpawnedPawn(
            spawned,
            CasterPawn,
            spawnAsCasterFaction: true,
            map,
            spawnCell,
            applyVoidLifetime: false);

        RemoveVoidTetherIfAny(spawned);
        ApplyTierEnhancement(spawned);
    }

    private PawnKindDef ResolveResultKind(int materialCount)
    {
        int tier1 = Config.tier1Required > 0 ? Config.tier1Required : 5;
        int tier2 = Config.tier2Required > 0 ? Config.tier2Required : 15;
        int tier3 = Config.tier3Required > 0 ? Config.tier3Required : 30;

        if (materialCount >= tier3 && Config.tier3Pawnkind != null)
        {
            return Config.tier3Pawnkind;
        }

        if (materialCount >= tier2 && Config.tier2Pawnkind != null)
        {
            return Config.tier2Pawnkind;
        }

        if (materialCount >= tier1 && Config.tier1Pawnkind != null)
        {
            return Config.tier1Pawnkind;
        }

        return null;
    }

    private static void ApplyTierEnhancement(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return;
        }

        HediffDef enhancement = null;
        switch (pawn.kindDef?.defName)
        {
            case "VPEMYX_GraftedAberration":
                enhancement = MyExtensionDefOf.VPEMYX_FleshMoldingTier1;
                break;
            case "VPEMYX_WailingFleshMound":
                enhancement = MyExtensionDefOf.VPEMYX_FleshMoldingTier2;
                break;
            case "VPEMYX_LordOfLegion":
                enhancement = MyExtensionDefOf.VPEMYX_FleshMoldingTier3;
                break;
        }

        if (enhancement != null && !pawn.health.hediffSet.HasHediff(enhancement))
        {
            pawn.health.AddHediff(enhancement);
        }

        RemoveVoidTetherIfAny(pawn);
    }

    private static void RemoveVoidTetherIfAny(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return;
        }

        HediffDef timerDef = MyExtensionDefOf.VPEMYX_VoidReturnTimer ??
                             DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_VoidReturnTimer");
        if (timerDef == null)
        {
            return;
        }

        Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
        if (timer != null)
        {
            pawn.health.RemoveHediff(timer);
        }
    }

    private static List<Corpse> CollectFreshCorpses(Map map, IntVec3 center, float radius)
    {
        List<Corpse> result = new List<Corpse>();
        IEnumerable<Thing> things = GenRadial.RadialDistinctThingsAround(center, map, radius, useCenter: true);
        foreach (Thing thing in things)
        {
            Corpse corpse = thing as Corpse;
            if (corpse?.InnerPawn == null || corpse.Destroyed)
            {
                continue;
            }

            if (!(corpse.InnerPawn.RaceProps?.IsFlesh ?? false))
            {
                continue;
            }

            if (corpse.GetRotStage() != RotStage.Fresh)
            {
                continue;
            }

            result.Add(corpse);
        }

        return result;
    }

    private static List<Pawn> CollectDownedHostiles(Map map, IntVec3 center, float radius, Pawn caster)
    {
        List<Pawn> result = new List<Pawn>();
        IEnumerable<Thing> things = GenRadial.RadialDistinctThingsAround(center, map, radius, useCenter: true);
        foreach (Thing thing in things)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn == caster)
            {
                continue;
            }

            if (!pawn.Downed || pawn.health?.Dead == true)
            {
                continue;
            }

            if (!(pawn.RaceProps?.IsFlesh ?? false))
            {
                continue;
            }

            if (!pawn.HostileTo(caster))
            {
                continue;
            }

            result.Add(pawn);
        }

        return result;
    }

    private static void ConsumeCorpses(List<Corpse> corpses)
    {
        for (int i = 0; i < corpses.Count; i++)
        {
            Corpse corpse = corpses[i];
            if (corpse == null || corpse.Destroyed)
            {
                continue;
            }

            corpse.Destroy(DestroyMode.Vanish);
        }
    }

    private static void ConsumeVictims(List<Pawn> victims)
    {
        for (int i = 0; i < victims.Count; i++)
        {
            Pawn pawn = victims[i];
            if (pawn == null || pawn.Destroyed)
            {
                continue;
            }

            if (!pawn.Dead)
            {
                pawn.Kill(null, null);
            }

            Corpse corpse = pawn.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
