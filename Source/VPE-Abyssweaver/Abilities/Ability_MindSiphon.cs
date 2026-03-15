using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using VEF.Abilities;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class Ability_MindSiphon : VEF.Abilities.Ability
{
    private AbilityExtension_MindSiphon Config => def.GetModExtension<AbilityExtension_MindSiphon>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);

        Pawn caster = CasterPawn;
        Pawn target = targets != null && targets.Length > 0 ? targets[0].Thing as Pawn : null;
        if (!CanSiphon(caster, target))
        {
            Messages.Message("VPEMYX_Message_MindSiphon_InvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        SkillRecord stolenSkill = ChooseRandomSkillToDrain(target);
        if (stolenSkill != null)
        {
            DrainAndTransferSkill(target, caster, stolenSkill);
            TryStealPassion(target, caster, stolenSkill.def);
        }

        ApplyMindDrained(target);
        TryGrantSensitivityFromPsylink(target, caster);
        PlayEffects(caster, target);
    }

    private static bool CanSiphon(Pawn caster, Pawn target)
    {
        if (caster == null || target == null || target.Dead || !target.Downed)
        {
            return false;
        }

        if (!target.RaceProps.Humanlike)
        {
            return false;
        }

        return target.HostileTo(caster);
    }

    private static SkillRecord ChooseRandomSkillToDrain(Pawn target)
    {
        if (target?.skills == null)
        {
            return null;
        }

        List<SkillRecord> candidates = new List<SkillRecord>();
        List<SkillRecord> all = target.skills.skills;
        for (int i = 0; i < all.Count; i++)
        {
            SkillRecord skill = all[i];
            if (skill != null && !skill.TotallyDisabled && skill.Level > 0)
            {
                candidates.Add(skill);
            }
        }

        return candidates.Count > 0 ? candidates.RandomElement() : null;
    }

    private static void DrainAndTransferSkill(Pawn target, Pawn caster, SkillRecord targetSkill)
    {
        targetSkill.Level = Mathf.Max(0, targetSkill.Level - 1);

        SkillRecord casterSkill = caster?.skills?.GetSkill(targetSkill.def);
        if (casterSkill == null || casterSkill.TotallyDisabled)
        {
            return;
        }

        casterSkill.Level = Mathf.Min(20, casterSkill.Level + 1);
    }

    private void TryStealPassion(Pawn target, Pawn caster, SkillDef skillDef)
    {
        if (skillDef == null || !Rand.Chance(Config?.passionStealChance ?? 0.10f))
        {
            return;
        }

        SkillRecord targetSkill = target.skills?.GetSkill(skillDef);
        SkillRecord casterSkill = caster.skills?.GetSkill(skillDef);
        if (targetSkill == null || casterSkill == null)
        {
            return;
        }

        if (targetSkill.passion <= Passion.None || casterSkill.passion >= Passion.Major)
        {
            return;
        }

        targetSkill.passion -= 1;
        casterSkill.passion += 1;
    }

    private void ApplyMindDrained(Pawn target)
    {
        HediffDef mindDrained = Config?.mindDrainedHediff ?? MyExtensionDefOf.VPEMYX_MindDrained;
        if (mindDrained == null || target?.health?.hediffSet == null)
        {
            return;
        }

        if (!target.health.hediffSet.HasHediff(mindDrained))
        {
            target.health.AddHediff(mindDrained);
        }
    }

    private void TryGrantSensitivityFromPsylink(Pawn target, Pawn caster)
    {
        if (caster?.health?.hediffSet == null || target?.health?.hediffSet == null)
        {
            return;
        }

        if (!target.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
        {
            return;
        }

        HediffDef sensitivityDef = Config?.sensitivityHediff ?? MyExtensionDefOf.VPEMYX_MindSiphonSensitivity;
        if (sensitivityDef == null)
        {
            return;
        }

        Hediff sensitivity = caster.health.hediffSet.GetFirstHediffOfDef(sensitivityDef);
        if (sensitivity == null)
        {
            caster.health.AddHediff(sensitivityDef);
            sensitivity = caster.health.hediffSet.GetFirstHediffOfDef(sensitivityDef);
        }

        if (sensitivity == null)
        {
            return;
        }

        float gain = Mathf.Max(0f, Config?.sensitivityGainPerPsylink ?? 1f);
        float maxSeverity = Mathf.Max(0f, Config?.sensitivityMaxSeverity ?? 10f);
        sensitivity.Severity = Mathf.Min(maxSeverity, sensitivity.Severity + gain);
    }

    private static void PlayEffects(Pawn caster, Pawn target)
    {
        if (caster?.Map == null)
        {
            return;
        }

        FleckDef aoe = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (aoe != null)
        {
            FleckMaker.Static(target.Position, caster.Map, aoe, 1.2f);
        }

        SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal") ??
                         DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        sound?.PlayOneShot(new TargetInfo(target.Position, caster.Map));
    }
}
