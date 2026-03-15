using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_SpawnEntityMixedTier : VEF.Abilities.Ability
{
    private AbilityExtension_EntityMixedSpawner Config => def.GetModExtension<AbilityExtension_EntityMixedSpawner>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (Config == null || targets == null || targets.Length == 0)
        {
            return;
        }

        List<PawnKindDef> highCandidates = EntitySummonUtility.ResolveCandidates(
            Config.highPawnkinds,
            Config.useAllEntitiesIfEmpty,
            Config.highMinCombatPower,
            Config.highMaxCombatPower,
            Config.excludedPawnkinds);

        List<PawnKindDef> lowCandidates = EntitySummonUtility.ResolveCandidates(
            Config.lowPawnkinds,
            Config.useAllEntitiesIfEmpty,
            Config.lowMinCombatPower,
            Config.lowMaxCombatPower,
            Config.excludedPawnkinds);

        if (highCandidates.Count == 0 && lowCandidates.Count == 0)
        {
            return;
        }

        Faction generationFaction = EntitySummonUtility.ResolveGenerationFaction(
            Config.spawnAsCasterFaction,
            Config.spawnAsEntitiesFaction,
            Config.factionDef,
            CasterPawn);

        int highCount = Config.highCount < 1 ? 1 : Config.highCount;
        int lowCount = Config.lowCount < 0 ? 0 : Config.lowCount;

        for (int i = 0; i < targets.Length; i++)
        {
            SpawnGroup(targets[i], highCandidates, highCount, generationFaction);
            SpawnGroup(targets[i], lowCandidates, lowCount, generationFaction);
        }
    }

    private void SpawnGroup(GlobalTargetInfo target, List<PawnKindDef> candidates, int count, Faction generationFaction)
    {
        if (count <= 0 || candidates == null || candidates.Count == 0)
        {
            return;
        }

        if (target.Map == null || !target.Cell.InBounds(target.Map))
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            PawnKindDef kind = candidates.RandomElement();
            Pawn pawnToSpawn = EntitySummonUtility.GeneratePawnSafe(kind, generationFaction);
            if (pawnToSpawn == null)
            {
                continue;
            }

            IntVec3 cell = CellFinder.StandableCellNear(target.Cell, target.Map, 2.9f);
            Pawn spawned = (Pawn)GenSpawn.Spawn(pawnToSpawn, cell, target.Map, WipeMode.Vanish);
            EntitySummonUtility.PostProcessSpawnedPawn(
                spawned,
                CasterPawn,
                Config.spawnAsCasterFaction,
                target.Map,
                cell);
        }
    }
}
