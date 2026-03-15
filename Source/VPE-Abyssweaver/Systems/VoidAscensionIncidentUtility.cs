using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace VPE_MyExtension;

public static class VoidAscensionIncidentUtility
{
    private static readonly string[] AssaultPawnKindDefs =
    {
        // 血肉兽、血棘巨人、惊惧母兽、吞噬兽（不包含嵌合兽）
        "Fingerspike",
        "Gorehulk",
        "Dreadmeld",
        "Devourer",
    };

    public static bool TryTriggerEntityAssault(Map map, float fixedAssaultPoints)
    {
        if (map == null)
        {
            return false;
        }

        Faction entitiesFaction = Faction.OfEntities;
        if (entitiesFaction == null)
        {
            return false;
        }

        List<PawnKindDef> pool = new List<PawnKindDef>(AssaultPawnKindDefs.Length);
        for (int i = 0; i < AssaultPawnKindDefs.Length; i++)
        {
            PawnKindDef def = DefDatabase<PawnKindDef>.GetNamedSilentFail(AssaultPawnKindDefs[i]);
            if (def != null)
            {
                pool.Add(def);
            }
        }

        if (pool.Count == 0)
        {
            return false;
        }

        if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entryCell, map, CellFinder.EdgeRoadChance_Hostile, allowFogged: false, null))
        {
            entryCell = CellFinder.RandomEdgeCell(map);
        }

        float targetPoints = Mathf.Max(100f, fixedAssaultPoints);
        List<Pawn> spawned = SpawnAssaultWave(map, entitiesFaction, pool, entryCell, targetPoints);
        if (spawned.Count == 0)
        {
            return false;
        }

        LordJob_AssaultColony lordJob = new LordJob_AssaultColony(
            entitiesFaction,
            canKidnap: true,
            canTimeoutOrFlee: true,
            sappers: false,
            useAvoidGridSmart: false,
            canSteal: true,
            breachers: false,
            canPickUpOpportunisticWeapons: false);
        LordMaker.MakeNewLord(entitiesFaction, lordJob, map, spawned);

        if (Find.LetterStack != null)
        {
            string label = "Void breach: entities assault";
            string text = $"The ascension cocoon tears reality open. A strike force of anomaly entities ({spawned.Count}) is attacking your colony.";
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, new LookTargets(spawned));
        }

        return true;
    }

    private static List<Pawn> SpawnAssaultWave(Map map, Faction faction, List<PawnKindDef> pool, IntVec3 entryCell, float targetPoints)
    {
        List<Pawn> result = new List<Pawn>();
        float spent = 0f;
        int safety = 0;

        while (spent < targetPoints && safety++ < 64)
        {
            float remaining = targetPoints - spent;
            List<PawnKindDef> candidates = pool.FindAll(k => k.combatPower <= remaining + 80f);
            if (candidates.Count == 0)
            {
                candidates = pool;
            }

            PawnKindDef chosen = candidates.RandomElementByWeight(k => Mathf.Clamp(remaining / Mathf.Max(1f, k.combatPower), 0.35f, 3f));
            Pawn pawn = EntitySummonUtility.GeneratePawnSafe(chosen, faction);
            if (pawn == null)
            {
                continue;
            }

            if (pawn.Faction != faction)
            {
                pawn.SetFaction(faction);
            }

            if (!CellFinder.TryFindRandomSpawnCellForPawnNear(entryCell, map, out IntVec3 spawnCell, 12, c => c.Standable(map) && !c.Fogged(map)))
            {
                spawnCell = CellFinder.StandableCellNear(entryCell, map, 8f);
            }

            Pawn spawned = (Pawn)GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
            result.Add(spawned);
            spent += Mathf.Max(10f, chosen.combatPower);

            if (result.Count >= 2 && spent >= targetPoints * 0.9f)
            {
                break;
            }
        }

        return result;
    }
}
