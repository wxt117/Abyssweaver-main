using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_CorpseThreads : VEF.Abilities.Ability
{
    private static readonly List<string> ShamblerKinds = new List<string>
    {
        "ShamblerSwarmer",
        "ShamblerSoldier"
    };

    private AbilityExtension_CorpseThreads Config => def.GetModExtension<AbilityExtension_CorpseThreads>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (CasterPawn == null || targets == null || targets.Length == 0)
        {
            return;
        }

        MyExtensionGameComponent comp = Current.Game.GetComponent<MyExtensionGameComponent>();
        int threshold = Config?.unlockThreshold ?? 100;
        int currentCount = comp?.GetRevivedCorpses(CasterPawn) ?? 0;
        EnsureCounterHediff(CasterPawn, currentCount, threshold);

        GlobalTargetInfo target = targets[0];
        if (target.Map == null || !target.Cell.InBounds(target.Map))
        {
            return;
        }

        Map map = target.Map;
        float radius = Config?.radius ?? 8f;
        List<Corpse> corpses = CollectCorpses(map, target.Cell, radius);
        if (corpses.Count == 0)
        {
            return;
        }

        bool frenzyUnlocked = comp?.IsFleshFrenzyUnlocked(CasterPawn) ?? false;
        HediffDef frenzyBuff = Config?.frenzyBuffHediff ?? MyExtensionDefOf.VPEMYX_FleshFrenzyBuff;

        int revived = 0;
        for (int i = 0; i < corpses.Count; i++)
        {
            Corpse corpse = corpses[i];
            if (corpse == null || corpse.Destroyed || !corpse.Spawned)
            {
                continue;
            }

            PawnKindDef shamblerKind = ResolveShamblerKind();
            if (shamblerKind == null)
            {
                continue;
            }

            Pawn pawnToSpawn = EntitySummonUtility.GeneratePawnSafe(shamblerKind, CasterPawn.Faction);
            if (pawnToSpawn == null)
            {
                continue;
            }

            IntVec3 spawnCell = corpse.Position;
            corpse.Destroy(DestroyMode.Vanish);
            Pawn spawned = (Pawn)GenSpawn.Spawn(pawnToSpawn, spawnCell, map, WipeMode.Vanish);
            EntitySummonUtility.PostProcessSpawnedPawn(
                spawned,
                CasterPawn,
                spawnAsCasterFaction: true,
                map,
                spawnCell);

            if (frenzyUnlocked && frenzyBuff != null)
            {
                spawned.health?.AddHediff(frenzyBuff);
            }

            revived++;
        }

        if (revived <= 0 || comp == null)
        {
            return;
        }

        bool unlockedNow = comp.RegisterRevivedCorpses(CasterPawn, revived, threshold, out int totalRevived);
        EnsureCounterHediff(CasterPawn, totalRevived, threshold);
        if (unlockedNow)
        {
            Messages.Message(
                "VPEMYX_Message_CorpseThreads_FrenzyUnlocked".Translate(totalRevived, threshold),
                CasterPawn,
                MessageTypeDefOf.PositiveEvent);
        }
    }

    private static void EnsureCounterHediff(Pawn pawn, int count, int threshold)
    {
        if (pawn?.health?.hediffSet == null || MyExtensionDefOf.VPEMYX_CorpseThreadsCounter == null)
        {
            return;
        }

        Hediff_CorpseThreadsCounter counterHediff =
            pawn.health.hediffSet.GetFirstHediffOfDef(MyExtensionDefOf.VPEMYX_CorpseThreadsCounter) as Hediff_CorpseThreadsCounter;

        if (count >= threshold && threshold > 0)
        {
            if (counterHediff != null)
            {
                pawn.health.RemoveHediff(counterHediff);
            }

            return;
        }

        if (counterHediff == null)
        {
            counterHediff = HediffMaker.MakeHediff(MyExtensionDefOf.VPEMYX_CorpseThreadsCounter, pawn) as Hediff_CorpseThreadsCounter;
            if (counterHediff == null)
            {
                return;
            }

            pawn.health.AddHediff(counterHediff);
        }

        counterHediff.SetCounter(count, threshold);
    }

    private static List<Corpse> CollectCorpses(Map map, IntVec3 center, float radius)
    {
        List<Corpse> result = new List<Corpse>();
        IEnumerable<Thing> things = GenRadial.RadialDistinctThingsAround(center, map, radius, useCenter: true);
        foreach (Thing thing in things)
        {
            Corpse corpse = thing as Corpse;
            if (corpse?.InnerPawn == null)
            {
                continue;
            }

            result.Add(corpse);
        }

        return result;
    }

    private static PawnKindDef ResolveShamblerKind()
    {
        string defName = Rand.Chance(0.75f) ? ShamblerKinds[0] : ShamblerKinds[1];
        PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
        if (kind != null)
        {
            return kind;
        }

        return DefDatabase<PawnKindDef>.GetNamedSilentFail(ShamblerKinds[0]);
    }
}
