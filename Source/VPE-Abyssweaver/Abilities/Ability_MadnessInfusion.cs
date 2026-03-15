using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using Verse.AI;

namespace VPE_MyExtension;

public class Ability_MadnessInfusion : VEF.Abilities.Ability
{
    private AbilityExtension_MadnessInfusion Config => def.GetModExtension<AbilityExtension_MadnessInfusion>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (targets == null || targets.Length == 0)
        {
            return;
        }

        Pawn target = targets[0].Thing as Pawn;
        if (target == null || target.Dead)
        {
            return;
        }

        if (CasterPawn != null && target.HostileTo(CasterPawn))
        {
            ApplyEnemyEffect(target);
        }
        else
        {
            ApplyAllyEffect(target);
        }
    }

    private void ApplyEnemyEffect(Pawn target)
    {
        MentalStateHandler handler = target.mindState?.mentalStateHandler;
        if (handler == null)
        {
            return;
        }

        List<MentalStateDef> candidates = new List<MentalStateDef>
        {
            MentalStateDefOf.BerserkPermanent,
            MentalStateDefOf.Berserk,
            MentalStateDefOf.PanicFlee,
            MentalStateDefOf.Terror,
            DefDatabase<MentalStateDef>.GetNamedSilentFail("HumanityBreak"),
            DefDatabase<MentalStateDef>.GetNamedSilentFail("EntityKiller"),
            DefDatabase<MentalStateDef>.GetNamedSilentFail("Wander_Psychotic")
        };

        while (candidates.Count > 0)
        {
            MentalStateDef state = candidates.RandomElement();
            candidates.Remove(state);
            if (state == null)
            {
                continue;
            }

            if (handler.TryStartMentalState(
                    state,
                    "Void delusion backflow",
                    forced: true,
                    forceWake: true,
                    causedByMood: false,
                    otherPawn: CasterPawn,
                    transitionSilently: false,
                    causedByDamage: false,
                    causedByPsycast: true))
            {
                return;
            }
        }

        handler.TryStartMentalState(
            MentalStateDefOf.Berserk,
            "Void delusion backflow",
            forced: true,
            forceWake: true,
            causedByMood: false,
            otherPawn: CasterPawn,
            transitionSilently: false,
            causedByDamage: false,
            causedByPsycast: true);
    }

    private void ApplyAllyEffect(Pawn target)
    {
        ThoughtDef thought = Config?.allyMoodThought ??
                            MyExtensionDefOf.VPEMYX_DelusionBackflowThought;
        if (thought != null && target.needs?.mood?.thoughts?.memories != null)
        {
            target.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thought);
            target.needs.mood.thoughts.memories.TryGainMemory(thought, CasterPawn);
        }

        HediffDef buff = Config?.allyBuffHediff ?? MyExtensionDefOf.VPEMYX_DelusionBackflowBuff;
        if (buff == null || target.health?.hediffSet == null)
        {
            return;
        }

        Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(buff);
        if (existing != null)
        {
            target.health.RemoveHediff(existing);
        }

        target.health.AddHediff(buff);
    }
}
