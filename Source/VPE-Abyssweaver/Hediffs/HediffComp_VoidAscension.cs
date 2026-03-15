using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class HediffCompProperties_VoidAscensionCocoon : HediffCompProperties
{
    public int checkIntervalTicks = 30;
    public int durationTicks = 2500;
    public string resultHediffDefName = "VPEMYX_VoidAvatar";

    public HediffCompProperties_VoidAscensionCocoon()
    {
        compClass = typeof(HediffComp_VoidAscensionCocoon);
    }
}

public class HediffComp_VoidAscensionCocoon : HediffComp
{
    private int startTick = -1;
    private int overrideDurationTicks = -1;
    private HediffCompProperties_VoidAscensionCocoon Props => (HediffCompProperties_VoidAscensionCocoon)props;

    public void OverrideDuration(int durationTicks)
    {
        overrideDurationTicks = Math.Max(60, durationTicks);
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref startTick, "startTick", -1);
        Scribe_Values.Look(ref overrideDurationTicks, "overrideDurationTicks", -1);
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        if (startTick < 0)
        {
            startTick = Find.TickManager?.TicksGame ?? 0;
        }
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        int interval = Math.Max(10, Props.checkIntervalTicks);
        if (!pawn.IsHashIntervalTick(interval))
        {
            return;
        }

        if (startTick < 0)
        {
            startTick = Find.TickManager?.TicksGame ?? 0;
        }

        int duration = overrideDurationTicks > 0 ? overrideDurationTicks : Math.Max(60, Props.durationTicks);
        int now = Find.TickManager?.TicksGame ?? 0;
        if (now - startTick < duration)
        {
            return;
        }

        CompleteAscension(pawn);
    }

    private void CompleteAscension(Pawn pawn)
    {
        HediffDef avatarDef = MyExtensionDefOf.VPEMYX_VoidAvatar ??
                              DefDatabase<HediffDef>.GetNamedSilentFail(Props.resultHediffDefName);
        if (avatarDef != null && !pawn.health.hediffSet.HasHediff(avatarDef))
        {
            pawn.health.AddHediff(avatarDef);
        }

        if (parent != null && pawn.health.hediffSet.hediffs.Contains(parent))
        {
            pawn.health.RemoveHediff(parent);
        }

        FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (fleck != null && pawn.Map != null)
        {
            FleckMaker.Static(pawn.Position, pawn.Map, fleck, 3.2f);
        }

        SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal") ??
                         DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        if (sound != null && pawn.Map != null)
        {
            sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }

        Messages.Message("VPEMYX_Message_VoidAscension_Completed".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent);
    }
}

public class HediffCompProperties_VoidAvatar : HediffCompProperties
{
    public int checkIntervalTicks = 60;
    public int bonusSkillLevels = 5;
    public bool forcePsychopath = true;
    public bool clampAnimalsAndPlants = true;
    public bool purgeDiseaseAndToxins = true;

    public HediffCompProperties_VoidAvatar()
    {
        compClass = typeof(HediffComp_VoidAvatar);
    }
}

public class HediffComp_VoidAvatar : HediffComp
{
    private bool initialized;
    private HediffCompProperties_VoidAvatar Props => (HediffCompProperties_VoidAvatar)props;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref initialized, "initialized", false);
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        EnsureInitialized();
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        EnsureInitialized();

        int interval = Math.Max(15, Props.checkIntervalTicks);
        if (!pawn.IsHashIntervalTick(interval))
        {
            return;
        }

        if (Props.clampAnimalsAndPlants)
        {
            ClampForbiddenSkills(pawn);
        }

        if (Props.purgeDiseaseAndToxins)
        {
            PurgeDiseaseAndToxins(pawn);
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        Pawn pawn = Pawn;
        if (pawn == null)
        {
            return;
        }

        ApplySkillTransformation(pawn);
        if (Props.forcePsychopath)
        {
            EnsurePsychopathTrait(pawn);
        }

        initialized = true;
    }

    private void ApplySkillTransformation(Pawn pawn)
    {
        if (pawn.skills?.skills == null)
        {
            return;
        }

        for (int i = 0; i < pawn.skills.skills.Count; i++)
        {
            SkillRecord skill = pawn.skills.skills[i];
            if (skill == null)
            {
                continue;
            }

            if (skill.def == SkillDefOf.Animals || skill.def == SkillDefOf.Plants)
            {
                skill.Level = 0;
                skill.passion = Passion.None;
                skill.xpSinceLastLevel = 0f;
                continue;
            }

            if (skill.TotallyDisabled || Props.bonusSkillLevels == 0)
            {
                continue;
            }

            skill.Level = Mathf.Clamp(skill.Level + Props.bonusSkillLevels, 0, 20);
        }
    }

    private static void EnsurePsychopathTrait(Pawn pawn)
    {
        TraitSet traits = pawn.story?.traits;
        if (traits == null)
        {
            return;
        }

        TraitDef psychopath = TraitDefOf.Psychopath ?? DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
        if (psychopath == null || traits.HasTrait(psychopath))
        {
            return;
        }

        traits.GainTrait(new Trait(psychopath, 0, forced: true));
    }

    private static void ClampForbiddenSkills(Pawn pawn)
    {
        if (pawn.skills == null)
        {
            return;
        }

        ForceSkillOff(pawn.skills.GetSkill(SkillDefOf.Animals));
        ForceSkillOff(pawn.skills.GetSkill(SkillDefOf.Plants));
    }

    private static void ForceSkillOff(SkillRecord skill)
    {
        if (skill == null)
        {
            return;
        }

        if (skill.Level != 0)
        {
            skill.Level = 0;
        }

        skill.passion = Passion.None;
        skill.xpSinceLastLevel = 0f;
    }

    private static void PurgeDiseaseAndToxins(Pawn pawn)
    {
        if (pawn.health?.hediffSet?.hediffs == null)
        {
            return;
        }

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            Hediff h = pawn.health.hediffSet.hediffs[i];
            if (h == null)
            {
                continue;
            }

            if (ShouldPurge(h))
            {
                pawn.health.RemoveHediff(h);
            }
        }
    }

    private static bool ShouldPurge(Hediff h)
    {
        if (h == null)
        {
            return false;
        }

        if (h.def == HediffDefOf.ToxicBuildup || h.def == HediffDefOf.FoodPoisoning)
        {
            return true;
        }

        if (h.TryGetComp<HediffComp_Immunizable>() != null)
        {
            return true;
        }

        string name = h.def?.defName;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.IndexOf("Toxic", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Poison", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flu", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Plague", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Malaria", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Mechanites", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("SleepingSickness", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("GutWorms", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
