using System.Collections.Generic;
using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace VPE_MyExtension;

public static class EntitySummonUtility
{
    public static bool IsFleshMoldingConstruct(Pawn pawn)
    {
        return IsFleshMoldingConstruct(pawn?.kindDef);
    }

    public static bool IsFleshMoldingConstruct(PawnKindDef kindDef)
    {
        string defName = kindDef?.defName;
        if (string.IsNullOrEmpty(defName))
        {
            return false;
        }

        return defName == "VPEMYX_GraftedAberration" ||
               defName == "VPEMYX_WailingFleshMound" ||
               defName == "VPEMYX_LordOfLegion" ||
               defName == "VPEMYX_AbyssalAtrocity";
    }

    public static List<PawnKindDef> ResolveCandidates(
        List<PawnKindDef> explicitPawnKinds,
        bool useAllEntitiesIfEmpty,
        float minCombatPower,
        float maxCombatPower,
        List<PawnKindDef> excludedPawnKinds,
        bool allowPainSphere = false)
    {
        List<PawnKindDef> result = new List<PawnKindDef>();
        if (explicitPawnKinds != null && explicitPawnKinds.Count > 0)
        {
            for (int i = 0; i < explicitPawnKinds.Count; i++)
            {
                PawnKindDef def = explicitPawnKinds[i];
                if (def != null &&
                    !IsExcluded(def, excludedPawnKinds, allowPainSphere) &&
                    IsWithinPowerRange(def, minCombatPower, maxCombatPower))
                {
                    result.Add(def);
                }
            }

            return result;
        }

        if (!useAllEntitiesIfEmpty)
        {
            return result;
        }

        List<PawnKindDef> allDefs = DefDatabase<PawnKindDef>.AllDefsListForReading;
        for (int i = 0; i < allDefs.Count; i++)
        {
            PawnKindDef def = allDefs[i];
            if (def != null &&
                IsEntityLike(def) &&
                !IsExcluded(def, excludedPawnKinds, allowPainSphere) &&
                IsWithinPowerRange(def, minCombatPower, maxCombatPower))
            {
                result.Add(def);
            }
        }

        return result;
    }

    public static Faction ResolveGenerationFaction(
        bool spawnAsCasterFaction,
        bool spawnAsEntitiesFaction,
        FactionDef factionDef,
        Pawn casterPawn)
    {
        if (spawnAsCasterFaction && casterPawn?.Faction != null)
        {
            return casterPawn.Faction;
        }

        if (spawnAsEntitiesFaction && FactionDefOf.Entities != null)
        {
            return Faction.OfEntities;
        }

        if (factionDef != null)
        {
            return Find.FactionManager.FirstFactionOfDef(factionDef);
        }

        return null;
    }

    public static Pawn GeneratePawnSafe(PawnKindDef kind, Faction generationFaction)
    {
        try
        {
            return PawnGenerator.GeneratePawn(kind, generationFaction, (PlanetTile?)null);
        }
        catch
        {
            if (FactionDefOf.Entities == null)
            {
                return null;
            }

            try
            {
                return PawnGenerator.GeneratePawn(kind, Faction.OfEntities, (PlanetTile?)null);
            }
            catch
            {
                return null;
            }
        }
    }

    public static void PostProcessSpawnedPawn(
        Pawn spawned,
        Pawn casterPawn,
        bool spawnAsCasterFaction,
        Map map,
        IntVec3 cell,
        bool applyVoidLifetime = true)
    {
        EnsureCasterFaction(spawned, casterPawn, spawnAsCasterFaction);
        EnsureCombatBehavior(spawned, map, cell);
        EnsureNociosphereCasterFaction(spawned, casterPawn);
        if (applyVoidLifetime)
        {
            ApplyVoidLifetime(spawned, casterPawn);
        }

        PlayVoidEffect(cell, map);
    }

    public static void PlayVoidEffect(IntVec3 cell, Map map)
    {
        FleckDef fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (fleckDef != null)
        {
            FleckMaker.Static(cell, map, fleckDef, 1.5f);
        }

        SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        soundDef?.PlayOneShot(new TargetInfo(cell, map));
    }

    private static bool IsExcluded(PawnKindDef def, List<PawnKindDef> configExcludedPawnKinds, bool allowPainSphere)
    {
        if (def == null)
        {
            return true;
        }

        if (def.defName != null &&
            def.defName.IndexOf("Shambler", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        // Built-in hard exclusions for unstable/unwanted entities.
        switch (def.defName)
        {
            // Exclude T3 flesh molding constructs from all generic summon pools.
            case "VPEMYX_GraftedAberration":
            case "VPEMYX_WailingFleshMound":
            case "VPEMYX_LordOfLegion":
            case "VPEMYX_AbyssalAtrocity":
                return true;
            // Exclude pain sphere due to unresolved ally hostility edge cases.
            case "Nociosphere":
                if (!allowPainSphere)
                {
                    return true;
                }
                break;
            case "FleshmassNucleus":
            case "Revenant":
            case "ShamblerSwarmer":
            case "ShamblerSoldier":
            case "ShamblerGorehulk":
            case "RP_Spraycyst":
            case "RP_Rustspark":
            // Ratkin Anomaly+: these are scripted event entities and not safe as generic summons.
            case "RA_Obsession":
            case "RA_De_lovely":
            case "RA_Fruiter":
            case "RA_Shadow":
                return true;
        }

        if (configExcludedPawnKinds == null)
        {
            return false;
        }

        for (int i = 0; i < configExcludedPawnKinds.Count; i++)
        {
            if (configExcludedPawnKinds[i] == def)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinPowerRange(PawnKindDef def, float minCombatPower, float maxCombatPower)
    {
        float power = def.combatPower;
        return power >= minCombatPower && power <= maxCombatPower;
    }

    private static bool IsEntityLike(PawnKindDef def)
    {
        if (def == null)
        {
            return false;
        }

        if (def.defaultFactionDef == FactionDefOf.Entities || def.defaultFactionDef?.defName == "Entities")
        {
            return true;
        }

        return def.RaceProps?.IsAnomalyEntity ?? false;
    }

    private static void EnsureCasterFaction(Pawn pawn, Pawn casterPawn, bool spawnAsCasterFaction)
    {
        if (!spawnAsCasterFaction || pawn == null || casterPawn?.Faction == null)
        {
            return;
        }

        if (pawn.Faction != casterPawn.Faction)
        {
            pawn.SetFaction(casterPawn.Faction, casterPawn);
        }
    }

    private static void EnsureCombatBehavior(Pawn pawn, Map map, IntVec3 fallbackCell)
    {
        if (pawn == null || map == null || pawn.kindDef == null)
        {
            return;
        }

        EnsureNociosphereAwake(pawn);

        if (pawn.kindDef.defName != "Chimera")
        {
            return;
        }

        if (pawn.GetLord() != null)
        {
            return;
        }

        try
        {
            LordJob_AssistColony lordJob = new LordJob_AssistColony(pawn.Faction, fallbackCell);
            LordMaker.MakeNewLord(pawn.Faction, lordJob, map, new List<Pawn> { pawn });
        }
        catch
        {
            pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
        }
    }

    private static void EnsureNociosphereAwake(Pawn pawn)
    {
        if (pawn?.kindDef?.defName != "Nociosphere")
        {
            return;
        }

        CompActivity activity = pawn.TryGetComp<CompActivity>();
        if (activity != null)
        {
            try
            {
                activity.SetActivity(1f, true);
                activity.EnterActiveState();
            }
            catch
            {
            }
        }

        CompCanBeDormant dormant = pawn.TryGetComp<CompCanBeDormant>();
        if (dormant != null && !dormant.Awake)
        {
            try
            {
                dormant.WakeUp();
            }
            catch
            {
                try
                {
                    dormant.WakeUpWithDelay();
                }
                catch
                {
                }
            }
        }

        CompWakeUpDormant wake = pawn.TryGetComp<CompWakeUpDormant>();
        if (wake == null)
        {
            return;
        }

        try
        {
            wake.Activate(pawn, true, false, true);
        }
        catch
        {
        }
    }

    private static void EnsureNociosphereCasterFaction(Pawn pawn, Pawn casterPawn)
    {
        if (pawn?.kindDef?.defName != "Nociosphere" || casterPawn?.Faction == null)
        {
            return;
        }

        if (pawn.Faction != casterPawn.Faction)
        {
            pawn.SetFaction(casterPawn.Faction, casterPawn);
        }
    }

    private static void ApplyVoidLifetime(Pawn pawn, Pawn casterPawn)
    {
        if (IsFleshMoldingConstruct(pawn))
        {
            return;
        }

        HediffDef timerDef = MyExtensionDefOf.VPEMYX_VoidReturnTimer ??
                             DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_VoidReturnTimer");
        if (timerDef == null || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
        if (timer == null)
        {
            pawn.health.AddHediff(timerDef);
            timer = pawn.health.hediffSet.GetFirstHediffOfDef(timerDef);
        }

        HediffComp_VoidReturn comp = timer?.TryGetComp<HediffComp_VoidReturn>();
        if (comp != null && casterPawn?.Faction != null)
        {
            comp.SetOwnerFaction(casterPawn.Faction);
        }
    }
}
