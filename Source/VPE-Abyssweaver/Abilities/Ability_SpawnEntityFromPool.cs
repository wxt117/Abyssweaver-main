using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_SpawnEntityFromPool : VEF.Abilities.Ability
{
    private AbilityExtension_EntitySpawner Config => def.GetModExtension<AbilityExtension_EntitySpawner>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (Config == null || targets == null || targets.Length == 0)
        {
            return;
        }

        List<PawnKindDef> candidates = EntitySummonUtility.ResolveCandidates(
            Config.pawnkinds,
            Config.useAllEntitiesIfEmpty,
            Config.minCombatPower,
            Config.maxCombatPower,
            Config.excludedPawnkinds);

        if (candidates.Count == 0)
        {
            return;
        }

        Faction generationFaction = EntitySummonUtility.ResolveGenerationFaction(
            Config.spawnAsCasterFaction,
            Config.spawnAsEntitiesFaction,
            Config.factionDef,
            CasterPawn);

        int count = Config.spawnCount < 1 ? 1 : Config.spawnCount;
        for (int i = 0; i < targets.Length; i++)
        {
            SpawnAt(targets[i], candidates, count, generationFaction);
        }
    }

    private void SpawnAt(GlobalTargetInfo target, List<PawnKindDef> candidates, int count, Faction generationFaction)
    {
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
