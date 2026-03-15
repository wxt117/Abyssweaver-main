using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VPE_MyExtension;

public class Ability_AbyssalBirth : VEF.Abilities.Ability
{
    private AbilityExtension_AbyssalBirth Config => def.GetModExtension<AbilityExtension_AbyssalBirth>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);

        Pawn caster = CasterPawn;
        if (caster?.Map == null || caster.Faction == null)
        {
            return;
        }

        AbilityExtension_AbyssalBirth cfg = Config;
        PawnKindDef sacrificeKind = cfg?.sacrificePawnkind ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("VPEMYX_LordOfLegion");
        PawnKindDef resultKind = cfg?.resultPawnkind ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("VPEMYX_AbyssalAtrocity");
        int required = Math.Max(1, cfg?.requiredSacrifices ?? 7);
        float spawnRadius = Math.Max(1.9f, cfg?.spawnRadius ?? 4f);
        if (sacrificeKind == null || resultKind == null)
        {
            Messages.Message("VPEMYX_Message_AbyssalBirth_MissingDefs".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        List<Pawn> candidates = CollectCandidates(caster, sacrificeKind);
        if (candidates.Count < required)
        {
            Messages.Message("VPEMYX_Message_AbyssalBirth_NotEnoughSacrifices".Translate(required, candidates.Count), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        candidates.Sort((a, b) =>
        {
            float da = (a.Position - caster.Position).LengthHorizontalSquared;
            float db = (b.Position - caster.Position).LengthHorizontalSquared;
            return da.CompareTo(db);
        });

        for (int i = 0; i < required; i++)
        {
            Pawn sacrifice = candidates[i];
            if (sacrifice == null || sacrifice.Destroyed)
            {
                continue;
            }

            if (sacrifice.Spawned && sacrifice.Map == caster.Map)
            {
                EntitySummonUtility.PlayVoidEffect(sacrifice.Position, caster.Map);
            }

            try
            {
                sacrifice.Destroy(DestroyMode.Vanish);
            }
            catch
            {
            }
        }

        IntVec3 spawnCell = CellFinder.StandableCellNear(caster.Position, caster.Map, spawnRadius);
        Pawn generated = EntitySummonUtility.GeneratePawnSafe(resultKind, caster.Faction);
        if (generated == null)
        {
            Messages.Message("VPEMYX_Message_AbyssalBirth_SpawnFailed".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        Pawn spawned = (Pawn)GenSpawn.Spawn(generated, spawnCell, caster.Map, WipeMode.Vanish);
        EntitySummonUtility.PostProcessSpawnedPawn(
            spawned,
            caster,
            spawnAsCasterFaction: true,
            caster.Map,
            spawnCell,
            applyVoidLifetime: false);

        RemoveTransientVoidHediffsIfAny(spawned);
        MyExtensionGameComponent gameComp = Current.Game?.GetComponent<MyExtensionGameComponent>();
        gameComp?.RegisterAbyssalOwner(spawned, caster);
        EntitySummonUtility.PlayVoidEffect(spawnCell, caster.Map);
        Messages.Message("VPEMYX_Message_AbyssalBirth_Success".Translate(), caster, MessageTypeDefOf.PositiveEvent);
    }

    private static List<Pawn> CollectCandidates(Pawn caster, PawnKindDef sacrificeKind)
    {
        List<Pawn> result = new List<Pawn>();
        IReadOnlyList<Pawn> all = caster.Map?.mapPawns?.AllPawnsSpawned;
        if (all == null)
        {
            return result;
        }

        for (int i = 0; i < all.Count; i++)
        {
            Pawn pawn = all[i];
            if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned)
            {
                continue;
            }

            if (pawn.kindDef != sacrificeKind)
            {
                continue;
            }

            if (pawn.Faction != caster.Faction)
            {
                continue;
            }

            result.Add(pawn);
        }

        return result;
    }

    private static void RemoveTransientVoidHediffsIfAny(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        if (hediffs == null || hediffs.Count == 0)
        {
            return;
        }

        for (int i = hediffs.Count - 1; i >= 0; i--)
        {
            Hediff hediff = hediffs[i];
            if (!ShouldStripFromAbyssal(hediff))
            {
                continue;
            }

            try
            {
                pawn.health.RemoveHediff(hediff);
            }
            catch
            {
            }
        }
    }

    private static bool ShouldStripFromAbyssal(Hediff hediff)
    {
        if (hediff?.def == null)
        {
            return false;
        }

        string defName = hediff.def.defName ?? string.Empty;
        if (defName == "VPEMYX_VoidReturnTimer" ||
            defName == "VoidTouched" ||
            defName.IndexOf("VoidTensor", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return hediff.TryGetComp<HediffComp_VoidReturn>() != null;
    }
}
