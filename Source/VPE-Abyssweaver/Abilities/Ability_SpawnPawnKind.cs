using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_SpawnPawnKind : VEF.Abilities.Ability
{
    private AbilityExtension_PawnkindSpawner SpawnConfig =>
        def.GetModExtension<AbilityExtension_PawnkindSpawner>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (SpawnConfig == null || SpawnConfig.pawnkind == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            SpawnAt(targets[i]);
        }
    }

    private void SpawnAt(GlobalTargetInfo target)
    {
        if (target.Map == null || !target.Cell.InBounds(target.Map))
        {
            return;
        }

        int count = SpawnConfig.spawnCount < 1 ? 1 : SpawnConfig.spawnCount;
        for (int i = 0; i < count; i++)
        {
            IntVec3 cell = CellFinder.StandableCellNear(target.Cell, target.Map, 2.9f);
            Pawn pawnToSpawn = PawnGenerator.GeneratePawn(SpawnConfig.pawnkind, ResolveFaction());
            ApplyExtraHediffs(pawnToSpawn);
            GenSpawn.Spawn(pawnToSpawn, cell, target.Map, WipeMode.Vanish);
        }
    }

    private Faction ResolveFaction()
    {
        if (SpawnConfig.spawnAsAlly && CasterPawn?.Faction != null)
        {
            return CasterPawn.Faction;
        }

        return null;
    }

    private void ApplyExtraHediffs(Pawn pawn)
    {
        if (SpawnConfig.hediffs == null)
        {
            return;
        }

        for (int i = 0; i < SpawnConfig.hediffs.Count; i++)
        {
            HediffDef hediff = SpawnConfig.hediffs[i];
            if (hediff != null)
            {
                pawn.health.AddHediff(hediff);
            }
        }
    }
}
