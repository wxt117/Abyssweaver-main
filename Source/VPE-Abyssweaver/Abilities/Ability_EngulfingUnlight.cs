using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VPE_MyExtension;

public class Ability_EngulfingUnlight : VEF.Abilities.Ability
{
    private const int DefaultDurationTicks = 30000;

    private AbilityExtension_EngulfingUnlight Config => def.GetModExtension<AbilityExtension_EngulfingUnlight>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);

        Pawn caster = CasterPawn;
        Map map = caster?.Map;
        if (caster == null || map == null)
        {
            return;
        }

        int duration = Config?.darknessDurationTicks > 0 ? Config.darknessDurationTicks : DefaultDurationTicks;
        ApplyUnnaturalDarkness(map, duration);
        ApplyVoidSight(caster, map, duration);
        SpawnNoctols(caster, map, duration);
        PlayCastEffects(caster);
    }

    private void ApplyUnnaturalDarkness(Map map, int durationTicks)
    {
        if (map?.gameConditionManager == null)
        {
            return;
        }

        string conditionName = Config?.conditionDefName ?? "UnnaturalDarkness";
        GameConditionDef def = DefDatabase<GameConditionDef>.GetNamedSilentFail(conditionName);
        if (def == null)
        {
            return;
        }

        List<GameCondition> active = map.gameConditionManager.ActiveConditions;
        for (int i = 0; i < active.Count; i++)
        {
            GameCondition existing = active[i];
            if (existing.def != def)
            {
                continue;
            }

            existing.TicksLeft = Math.Max(existing.TicksLeft, durationTicks);
            return;
        }

        GameCondition condition = GameConditionMaker.MakeCondition(def, durationTicks);
        map.gameConditionManager.RegisterCondition(condition);
    }

    private void ApplyVoidSight(Pawn caster, Map map, int durationTicks)
    {
        HediffDef voidSightDef = Config?.allyVoidSightHediff ?? MyExtensionDefOf.VPEMYX_VoidSight;
        if (voidSightDef == null)
        {
            return;
        }

        IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
            {
                continue;
            }

            if (!ShouldReceiveVoidSight(caster, pawn))
            {
                continue;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(voidSightDef);
            if (existing == null)
            {
                pawn.health.AddHediff(voidSightDef);
                existing = pawn.health.hediffSet.GetFirstHediffOfDef(voidSightDef);
            }

            HediffComp_Disappears disappears = existing?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = durationTicks;
            }
        }
    }

    private bool ShouldReceiveVoidSight(Pawn caster, Pawn pawn)
    {
        if (pawn == caster)
        {
            return true;
        }

        if (Config?.applyToAllFactionAllies != false && caster.Faction != null && pawn.Faction == caster.Faction)
        {
            return true;
        }

        return false;
    }

    private void SpawnNoctols(Pawn caster, Map map, int durationTicks)
    {
        int spawnCount = Math.Max(0, Config?.noctolSpawnCount ?? 0);
        if (spawnCount <= 0)
        {
            return;
        }

        string pawnKindName = Config?.noctolPawnkindDefName ?? "Noctol";
        PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindName);
        if (kindDef == null)
        {
            Messages.Message("VPEMYX_Message_EngulfingUnlight_NoNoctolDef".Translate(pawnKindName), MessageTypeDefOf.RejectInput, false);
            return;
        }

        List<Pawn> hostiles = new List<Pawn>();
        IReadOnlyList<Pawn> all = map.mapPawns?.AllPawnsSpawned;
        if (all != null)
        {
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p != null && !p.Dead && p.HostileTo(caster))
                {
                    hostiles.Add(p);
                }
            }
        }

        int radius = Math.Max(2, Config?.spawnRadius ?? 8);
        for (int i = 0; i < spawnCount; i++)
        {
            IntVec3 anchor = hostiles.Count > 0 ? hostiles.RandomElement().Position : caster.Position;
            IntVec3 spawnCell = CellFinder.StandableCellNear(anchor, map, radius);

            Pawn spawned = EntitySummonUtility.GeneratePawnSafe(kindDef, caster.Faction);
            if (spawned == null)
            {
                continue;
            }

            GenSpawn.Spawn(spawned, spawnCell, map, WipeMode.VanishOrMoveAside);
            if (caster.Faction != null && spawned.Faction != caster.Faction)
            {
                spawned.SetFaction(caster.Faction, caster);
            }

            spawned.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
            spawned.jobs?.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);

            EntitySummonUtility.PlayVoidEffect(spawnCell, map);
        }
    }

    private static void PlayCastEffects(Pawn caster)
    {
        if (caster?.Map == null)
        {
            return;
        }

        FleckDef aoe = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (aoe != null)
        {
            FleckMaker.Static(caster.Position, caster.Map, aoe, 3f);
        }

        SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal") ??
                         DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        sound?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
    }
}
