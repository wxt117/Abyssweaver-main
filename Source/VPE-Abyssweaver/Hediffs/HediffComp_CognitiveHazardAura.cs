using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VPE_MyExtension;

public class HediffCompProperties_CognitiveHazardAura : HediffCompProperties
{
    public float radius = 55f;
    public int checkIntervalTicks = 30;
    public float severityGainPerPulse = 0.03f;
    public float severityDecayPerPulse = 0.02f;
    public float maxSeverity = 1.2f;
    public int thoughtRefreshIntervalTicks = 150;

    public string victimHediffDefName = "VPEMYX_CognitiveHazardVictim";
    public string moodThoughtDefName = "VPEMYX_CognitiveHazardThought";

    public HediffCompProperties_CognitiveHazardAura()
    {
        compClass = typeof(HediffComp_CognitiveHazardAura);
    }
}

public class HediffComp_CognitiveHazardAura : HediffComp
{
    private HediffCompProperties_CognitiveHazardAura Props => (HediffCompProperties_CognitiveHazardAura)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn caster = Pawn;
        if (caster == null || caster.Dead || caster.Map == null || !caster.IsHashIntervalTick(Props.checkIntervalTicks))
        {
            return;
        }

        HediffDef victimDef = MyExtensionDefOf.VPEMYX_CognitiveHazardVictim ??
                              DefDatabase<HediffDef>.GetNamedSilentFail(Props.victimHediffDefName);
        ThoughtDef moodThought = MyExtensionDefOf.VPEMYX_CognitiveHazardThought ??
                                 DefDatabase<ThoughtDef>.GetNamedSilentFail(Props.moodThoughtDefName);
        if (victimDef == null)
        {
            return;
        }

        var pawns = caster.Map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == caster || other.Dead || other.health?.hediffSet == null)
            {
                continue;
            }

            Hediff victim = other.health.hediffSet.GetFirstHediffOfDef(victimDef);
            bool targetingCaster = IsTargetingCaster(other, caster) &&
                                   other.Position.InHorDistOf(caster.Position, Props.radius) &&
                                   other.HostileTo(caster);

            if (targetingCaster)
            {
                if (victim == null)
                {
                    other.health.AddHediff(victimDef);
                    victim = other.health.hediffSet.GetFirstHediffOfDef(victimDef);
                }

                if (victim != null)
                {
                    victim.Severity = Math.Min(Props.maxSeverity, victim.Severity + Props.severityGainPerPulse);
                }

                if (moodThought != null &&
                    other.needs?.mood?.thoughts?.memories != null &&
                    other.IsHashIntervalTick(Props.thoughtRefreshIntervalTicks))
                {
                    other.needs.mood.thoughts.memories.RemoveMemoriesOfDef(moodThought);
                    other.needs.mood.thoughts.memories.TryGainMemory(moodThought, caster);
                }
            }
            else if (victim != null)
            {
                victim.Severity = Math.Max(0f, victim.Severity - Props.severityDecayPerPulse);
                if (victim.Severity <= 0.001f)
                {
                    other.health.RemoveHediff(victim);
                }
            }
        }
    }

    private static bool IsTargetingCaster(Pawn pawn, Pawn caster)
    {
        if (pawn == null || caster == null)
        {
            return false;
        }

        if (pawn.mindState?.enemyTarget == caster)
        {
            return true;
        }

        Job curJob = pawn.CurJob;
        if (curJob != null &&
            (curJob.targetA.Thing == caster || curJob.targetB.Thing == caster || curJob.targetC.Thing == caster))
        {
            return true;
        }

        if (pawn.stances?.curStance is Stance_Warmup warmup && warmup.focusTarg.Thing == caster)
        {
            return true;
        }

        return false;
    }
}
