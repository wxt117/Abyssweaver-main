using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VPE_MyExtension;

public class HediffCompProperties_FleshMoldingTier1 : HediffCompProperties
{
    public int checkIntervalTicks = 90;
    public float injuryHealAmount = 1.2f;
    public float bloodLossReduction = 0.03f;
    public int corpseCheckIntervalTicks = 420;
    public float corpseSearchRadius = 2.9f;
    public float corpseBonusHealAmount = 4f;

    public HediffCompProperties_FleshMoldingTier1()
    {
        compClass = typeof(HediffComp_FleshMoldingTier1);
    }
}

public class HediffComp_FleshMoldingTier1 : HediffComp
{
    private HediffCompProperties_FleshMoldingTier1 Props => (HediffCompProperties_FleshMoldingTier1)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (pawn.IsHashIntervalTick(Props.checkIntervalTicks))
        {
            HealInjuries(pawn, Props.injuryHealAmount);
            ReduceBloodLoss(pawn, Props.bloodLossReduction);
        }

        if (pawn.Spawned && pawn.Map != null && pawn.IsHashIntervalTick(Props.corpseCheckIntervalTicks))
        {
            Corpse corpse = FindNearbyFreshCorpse(pawn, Props.corpseSearchRadius);
            if (corpse != null)
            {
                corpse.Destroy(DestroyMode.Vanish);
                HealInjuries(pawn, Props.corpseBonusHealAmount);
            }
        }
    }

    private static void HealInjuries(Pawn pawn, float amountPerInjury)
    {
        if (amountPerInjury <= 0f || pawn?.health?.hediffSet == null)
        {
            return;
        }

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            if (pawn.health.hediffSet.hediffs[i] is Hediff_Injury injury && injury.Severity > 0f)
            {
                injury.Heal(amountPerInjury);
            }
        }
    }

    private static void ReduceBloodLoss(Pawn pawn, float amount)
    {
        if (amount <= 0f || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null)
        {
            return;
        }

        bloodLoss.Severity = Math.Max(0f, bloodLoss.Severity - amount);
        if (bloodLoss.Severity <= 0.001f)
        {
            pawn.health.RemoveHediff(bloodLoss);
        }
    }

    private static Corpse FindNearbyFreshCorpse(Pawn pawn, float radius)
    {
        IEnumerable<Thing> things = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, radius, useCenter: true);
        foreach (Thing thing in things)
        {
            Corpse corpse = thing as Corpse;
            if (corpse?.InnerPawn == null || corpse.Destroyed)
            {
                continue;
            }

            if (!(corpse.InnerPawn.RaceProps?.IsFlesh ?? false))
            {
                continue;
            }

            if (corpse.GetRotStage() != RotStage.Fresh)
            {
                continue;
            }

            return corpse;
        }

        return null;
    }
}

public class HediffCompProperties_WailingAura : HediffCompProperties
{
    public float radius = 8f;
    public int checkIntervalTicks = 60;
    public float severityGainPerPulse = 0.08f;
    public float severityDecayPerPulse = 0.06f;
    public int thoughtRefreshIntervalTicks = 300;

    public HediffCompProperties_WailingAura()
    {
        compClass = typeof(HediffComp_WailingAura);
    }
}

public class HediffComp_WailingAura : HediffComp
{
    private HediffCompProperties_WailingAura Props => (HediffCompProperties_WailingAura)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn caster = Pawn;
        if (caster == null || caster.Dead || caster.Map == null || !caster.IsHashIntervalTick(Props.checkIntervalTicks))
        {
            return;
        }

        HediffDef victimDef = MyExtensionDefOf.VPEMYX_WailingAuraVictim ??
                              DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_WailingAuraVictim");
        ThoughtDef moodThought = MyExtensionDefOf.VPEMYX_WailingAuraThought ??
                                 DefDatabase<ThoughtDef>.GetNamedSilentFail("VPEMYX_WailingAuraThought");
        if (victimDef == null)
        {
            return;
        }

        IReadOnlyList<Pawn> pawns = caster.Map.mapPawns?.AllPawnsSpawned;
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

            bool affected = other.HostileTo(caster) &&
                            other.Position.InHorDistOf(caster.Position, Props.radius);
            Hediff current = other.health.hediffSet.GetFirstHediffOfDef(victimDef);
            if (affected)
            {
                if (current == null)
                {
                    other.health.AddHediff(victimDef);
                    current = other.health.hediffSet.GetFirstHediffOfDef(victimDef);
                }

                if (current != null)
                {
                    current.Severity = Math.Min(1f, current.Severity + Props.severityGainPerPulse);
                }

                if (moodThought != null &&
                    other.needs?.mood?.thoughts?.memories != null &&
                    other.IsHashIntervalTick(Props.thoughtRefreshIntervalTicks))
                {
                    other.needs.mood.thoughts.memories.RemoveMemoriesOfDef(moodThought);
                    other.needs.mood.thoughts.memories.TryGainMemory(moodThought, caster);
                }
            }
            else if (current != null)
            {
                current.Severity = Math.Max(0f, current.Severity - Props.severityDecayPerPulse);
                if (current.Severity <= 0.001f)
                {
                    other.health.RemoveHediff(current);
                }
            }
        }
    }
}

public class HediffCompProperties_FleshMoundShockwave : HediffCompProperties
{
    public int pulseIntervalTicks = 420;
    public float radius = 3.9f;
    public float damageAmount = 11f;
    public float armorPenetration = 0.12f;
    public float stunChance = 0.35f;
    public int stunTicks = 90;

    public HediffCompProperties_FleshMoundShockwave()
    {
        compClass = typeof(HediffComp_FleshMoundShockwave);
    }
}

public class HediffComp_FleshMoundShockwave : HediffComp
{
    private HediffCompProperties_FleshMoundShockwave Props => (HediffCompProperties_FleshMoundShockwave)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn caster = Pawn;
        if (caster == null || caster.Dead || caster.Map == null || !caster.IsHashIntervalTick(Props.pulseIntervalTicks))
        {
            return;
        }

        IReadOnlyList<Pawn> pawns = caster.Map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return;
        }

        bool hitAny = false;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == caster || other.Dead || !other.Spawned)
            {
                continue;
            }

            if (!other.HostileTo(caster) || !other.Position.InHorDistOf(caster.Position, Props.radius))
            {
                continue;
            }

            DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, Props.damageAmount, Props.armorPenetration, -1f, caster);
            other.TakeDamage(dinfo);
            hitAny = true;

            if (Rand.Chance(Props.stunChance))
            {
                try
                {
                    other.stances?.stunner?.StunFor(Props.stunTicks, caster, addBattleLog: false, showMote: true);
                }
                catch
                {
                }
            }
        }

        if (hitAny)
        {
            FleckMaker.Static(caster.Position, caster.Map, FleckDefOf.PsycastAreaEffect, 1.2f);
        }
    }
}

public class HediffCompProperties_WailingRegen : HediffCompProperties
{
    public int checkIntervalTicks = 24;
    public float healAmount = 2.6f;
    public int injuriesPerPulse = 3;
    public float bloodLossReduction = 0.12f;
    public float scarHealChance = 0.3f;

    public HediffCompProperties_WailingRegen()
    {
        compClass = typeof(HediffComp_WailingRegen);
    }
}

public class HediffComp_WailingRegen : HediffComp
{
    private HediffCompProperties_WailingRegen Props => (HediffCompProperties_WailingRegen)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null || !pawn.IsHashIntervalTick(Math.Max(15, Props.checkIntervalTicks)))
        {
            return;
        }

        HealMostSevereInjuries(pawn, Props.healAmount, Props.injuriesPerPulse);
        ReduceBloodLoss(pawn, Props.bloodLossReduction);
        TryHealWorstPermanentInjury(pawn, Props.scarHealChance);
    }

    private static void HealMostSevereInjuries(Pawn pawn, float healAmount, int injuriesToHeal)
    {
        if (healAmount <= 0f || injuriesToHeal <= 0 || pawn?.health?.hediffSet == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int healed = 0; healed < injuriesToHeal; healed++)
        {
            Hediff_Injury worst = null;
            float severity = 0f;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is not Hediff_Injury injury || injury.Severity <= 0f)
                {
                    continue;
                }

                if (injury.Severity > severity)
                {
                    severity = injury.Severity;
                    worst = injury;
                }
            }

            if (worst == null)
            {
                break;
            }

            worst.Heal(healAmount);
        }
    }

    private static void ReduceBloodLoss(Pawn pawn, float amount)
    {
        if (amount <= 0f || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null)
        {
            return;
        }

        bloodLoss.Severity = Math.Max(0f, bloodLoss.Severity - amount);
        if (bloodLoss.Severity <= 0.001f)
        {
            pawn.health.RemoveHediff(bloodLoss);
        }
    }

    private static void TryHealWorstPermanentInjury(Pawn pawn, float chance)
    {
        if (pawn?.health?.hediffSet == null || chance <= 0f || !Rand.Chance(Mathf.Clamp01(chance)))
        {
            return;
        }

        Hediff_Injury worstPermanent = null;
        float severity = 0f;
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i] is not Hediff_Injury injury || !injury.IsPermanent() || injury.Severity <= 0f)
            {
                continue;
            }

            if (injury.Severity > severity)
            {
                severity = injury.Severity;
                worstPermanent = injury;
            }
        }

        if (worstPermanent == null)
        {
            return;
        }

        try
        {
            pawn.health.RemoveHediff(worstPermanent);
        }
        catch
        {
        }
    }
}

public class HediffCompProperties_WailingDevourerPassive : HediffCompProperties
{
    public int checkIntervalTicks = 5;
    public int targetDigestDurationTicks = 900;
    public string digestingBuffHediffDefName = "VPEMYX_WailingDevourPredator";

    public HediffCompProperties_WailingDevourerPassive()
    {
        compClass = typeof(HediffComp_WailingDevourerPassive);
    }
}

public class HediffComp_WailingDevourerPassive : HediffComp
{
    private static readonly FieldInfo DevourerTicksDigestingField = typeof(CompDevourer).GetField("ticksDigesting", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DevourerTicksToDigestFullyField = typeof(CompDevourer).GetField("ticksToDigestFully", BindingFlags.Instance | BindingFlags.NonPublic);

    private HediffCompProperties_WailingDevourerPassive Props => (HediffCompProperties_WailingDevourerPassive)props;
    private int digestStartTick = -1;
    private bool wasDigesting;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null || !pawn.IsHashIntervalTick(Math.Max(2, Props.checkIntervalTicks)))
        {
            return;
        }

        CompDevourer devourer = pawn.TryGetComp<CompDevourer>();
        if (devourer == null)
        {
            return;
        }

        int now = Find.TickManager?.TicksGame ?? 0;
        if (devourer.Digesting)
        {
            if (!wasDigesting || digestStartTick < 0)
            {
                int current = ReadDevourerTicksDigesting(devourer);
                digestStartTick = now - Mathf.Max(0, current);
                ForceDigestWindow(devourer, Math.Max(120, Props.targetDigestDurationTicks));
            }

            wasDigesting = true;
            EnsureDigestBuff(pawn);
            EnforceDigestProgress(devourer, now - digestStartTick, Math.Max(120, Props.targetDigestDurationTicks));
            return;
        }

        wasDigesting = false;
        digestStartTick = -1;
        RemoveDigestBuff(pawn);
    }

    private void EnsureDigestBuff(Pawn pawn)
    {
        HediffDef buffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.digestingBuffHediffDefName);
        if (buffDef == null || pawn?.health?.hediffSet == null)
        {
            return;
        }

        if (pawn.health.hediffSet.GetFirstHediffOfDef(buffDef) == null)
        {
            pawn.health.AddHediff(buffDef);
        }
    }

    private void RemoveDigestBuff(Pawn pawn)
    {
        HediffDef buffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.digestingBuffHediffDefName);
        if (buffDef == null || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff current = pawn.health.hediffSet.GetFirstHediffOfDef(buffDef);
        if (current != null)
        {
            pawn.health.RemoveHediff(current);
        }
    }

    private static int ReadDevourerTicksDigesting(CompDevourer devourer)
    {
        if (devourer == null || DevourerTicksDigestingField == null)
        {
            return 0;
        }

        try
        {
            return (int)DevourerTicksDigestingField.GetValue(devourer);
        }
        catch
        {
            return 0;
        }
    }

    private static void ForceDigestWindow(CompDevourer devourer, int targetDurationTicks)
    {
        if (devourer == null || DevourerTicksDigestingField == null || DevourerTicksToDigestFullyField == null)
        {
            return;
        }

        try
        {
            int current = Mathf.Max(0, (int)DevourerTicksDigestingField.GetValue(devourer));
            int total = Mathf.Max(1, (int)DevourerTicksToDigestFullyField.GetValue(devourer));
            int target = Math.Max(120, targetDurationTicks);
            if (total == target)
            {
                return;
            }

            float pct = Mathf.Clamp01(current / (float)total);
            int remappedCurrent = Mathf.Clamp(Mathf.RoundToInt(target * pct), 0, target);
            DevourerTicksToDigestFullyField.SetValue(devourer, target);
            DevourerTicksDigestingField.SetValue(devourer, remappedCurrent);
        }
        catch
        {
        }
    }

    private static void EnforceDigestProgress(CompDevourer devourer, int elapsedTicks, int targetDurationTicks)
    {
        if (devourer == null || DevourerTicksDigestingField == null || DevourerTicksToDigestFullyField == null)
        {
            return;
        }

        try
        {
            int target = Math.Max(120, targetDurationTicks);
            // Some versions/mod interactions can rewrite total digest ticks mid-digestion;
            // keep the window fixed so digestion duration stays deterministic.
            DevourerTicksToDigestFullyField.SetValue(devourer, target);

            int current = (int)DevourerTicksDigestingField.GetValue(devourer);
            int total = (int)DevourerTicksToDigestFullyField.GetValue(devourer);
            if (total <= 0 || current >= total)
            {
                return;
            }

            int desired = Mathf.Clamp(elapsedTicks, 0, total);
            if (desired <= current)
            {
                return;
            }

            DevourerTicksDigestingField.SetValue(devourer, desired);
        }
        catch
        {
        }
    }
}

public class HediffCompProperties_LegionAssault : HediffCompProperties
{
    public int checkIntervalTicks = 60;
    public int sweepIntervalTicks = 150;
    public int sweepTelegraphTicks = 24;
    public int sweepStageGapTicks = 8;
    public float sweepRadius = 4.6f;
    public float sweepDamage = 20f;
    public float sweepArmorPenetration = 0.22f;
    public float sweepStunChance = 0.35f;
    public int sweepStunTicks = 60;
    public float sweepStage1RadiusFactor = 0.45f;
    public float sweepStage2RadiusFactor = 0.75f;
    public float sweepStage3RadiusFactor = 1f;
    public float sweepStage1DamageFactor = 0.55f;
    public float sweepStage2DamageFactor = 0.9f;
    public float sweepStage3DamageFactor = 1.25f;
    public int dreadPulseIntervalTicks = 75;
    public float dreadRadius = 11.4f;
    public float dreadSeverityGain = 0.12f;
    public float dreadSeverityDecay = 0.08f;
    public int dreadThoughtRefreshIntervalTicks = 300;
    public int ultimateCooldownTicks = 2400;
    public float ultimateHealthThreshold = 0.4f;
    public float ultimateRadius = 55f;
    public int ultimateStunTicks = 120;
    public float ultimateShockSeverity = 1f;
    public int damageMomentumDurationTicks = 300;
    public float damageMomentumGainPerHit = 1f;
    public float damageMomentumMaxSeverity = 5f;
    public int damageMomentumDecayIntervalTicks = 30;
    public float damageMomentumDecayPerPulse = 1f;
    public int meleeTargetLockTicks = 240;
    public int fearRoarIntervalTicks = 2700;
    public float fearRoarRadius = 13.5f;
    public int fearRoarStunTicks = 300;

    public HediffCompProperties_LegionAssault()
    {
        compClass = typeof(HediffComp_LegionAssault);
    }
}

public class HediffCompProperties_LegionPassiveRegen : HediffCompProperties
{
    public int checkIntervalTicks = 90;
    public int unstoppableCheckIntervalTicks = 15;
    public float combatRadius = 8f;
    public float healAmountInCombat = 0.85f;
    public float healAmountOutOfCombat = 0.35f;
    public int injuriesPerPulseInCombat = 3;
    public int injuriesPerPulseOutOfCombat = 1;
    public float bloodLossReductionInCombat = 0.03f;
    public float bloodLossReductionOutOfCombat = 0.015f;
    public float scarHealChanceInCombat = 0.35f;
    public float scarHealChanceOutOfCombat = 0.12f;

    public HediffCompProperties_LegionPassiveRegen()
    {
        compClass = typeof(HediffComp_LegionPassiveRegen);
    }
}

public class HediffComp_LegionPassiveRegen : HediffComp
{
    private HediffCompProperties_LegionPassiveRegen Props => (HediffCompProperties_LegionPassiveRegen)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (pawn.IsHashIntervalTick(Math.Max(6, Props.unstoppableCheckIntervalTicks)))
        {
            ApplyUnstoppable(pawn);
        }

        if (!pawn.IsHashIntervalTick(Math.Max(15, Props.checkIntervalTicks)))
        {
            return;
        }

        bool inCombat = HasHostileInRadius(pawn, Props.combatRadius);
        float heal = inCombat ? Props.healAmountInCombat : Props.healAmountOutOfCombat;
        int injuriesToHeal = inCombat ? Props.injuriesPerPulseInCombat : Props.injuriesPerPulseOutOfCombat;
        float bloodLossReduction = inCombat ? Props.bloodLossReductionInCombat : Props.bloodLossReductionOutOfCombat;
        float scarHealChance = inCombat ? Props.scarHealChanceInCombat : Props.scarHealChanceOutOfCombat;

        HealMostSevereInjuries(pawn, heal, injuriesToHeal);
        ReduceBloodLoss(pawn, bloodLossReduction);
        TryHealWorstPermanentInjury(pawn, scarHealChance);
    }

    private static bool HasHostileInRadius(Pawn pawn, float radius)
    {
        IReadOnlyList<Pawn> pawns = pawn.Map?.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return false;
        }

        float radiusSq = radius * radius;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == pawn || other.Dead || !other.Spawned || !other.HostileTo(pawn))
            {
                continue;
            }

            if ((other.Position - pawn.Position).LengthHorizontalSquared <= radiusSq)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyUnstoppable(Pawn pawn)
    {
        if (pawn?.stances?.stunner == null)
        {
            return;
        }

        try
        {
            if (pawn.stances.stunner.Stunned)
            {
                pawn.stances.stunner.StopStun();
            }
        }
        catch
        {
        }
    }

    private static void HealMostSevereInjuries(Pawn pawn, float healAmount, int injuriesToHeal)
    {
        if (healAmount <= 0f || injuriesToHeal <= 0 || pawn?.health?.hediffSet == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int healed = 0; healed < injuriesToHeal; healed++)
        {
            Hediff_Injury worst = null;
            float severity = 0f;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is not Hediff_Injury injury || injury.Severity <= 0f)
                {
                    continue;
                }

                if (injury.Severity > severity)
                {
                    severity = injury.Severity;
                    worst = injury;
                }
            }

            if (worst == null)
            {
                break;
            }

            worst.Heal(healAmount);
        }
    }

    private static void ReduceBloodLoss(Pawn pawn, float amount)
    {
        if (amount <= 0f || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null)
        {
            return;
        }

        bloodLoss.Severity = Math.Max(0f, bloodLoss.Severity - amount);
        if (bloodLoss.Severity <= 0.001f)
        {
            pawn.health.RemoveHediff(bloodLoss);
        }
    }

    private static void TryHealWorstPermanentInjury(Pawn pawn, float chance)
    {
        if (pawn?.health?.hediffSet == null || chance <= 0f || !Rand.Chance(Mathf.Clamp01(chance)))
        {
            return;
        }

        Hediff_Injury worstPermanent = null;
        float severity = 0f;
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i] is not Hediff_Injury injury || !injury.IsPermanent() || injury.Severity <= 0f)
            {
                continue;
            }

            if (injury.Severity > severity)
            {
                severity = injury.Severity;
                worstPermanent = injury;
            }
        }

        if (worstPermanent == null)
        {
            return;
        }

        try
        {
            pawn.health.RemoveHediff(worstPermanent);
        }
        catch
        {
        }
    }
}

public class HediffComp_LegionAssault : HediffComp
{
    private const float BoneSpikeVolleyRange = 24f;
    private const float BoneSpikeVolleyHalfAngle = 48f;
    private const int BoneSpikeVolleyMaxTargets = 22;
    private const int BoneSpikeVolleyExtraProjectiles = 28;

    private HediffCompProperties_LegionAssault Props => (HediffCompProperties_LegionAssault)props;
    private int lastSweepTick = -99999;
    private int lastDreadPulseTick = -99999;
    private int lastFearRoarTick = -99999;
    private int pendingSweepExecuteTick = -1;
    private Vector2 pendingSweepDirection = Vector2.zero;
    private readonly HashSet<Pawn> stageHitCache = new HashSet<Pawn>();
    private int momentumExpireTick = -1;
    private int lockedMeleeTargetId = -1;
    private int meleeTargetLockUntilTick = -1;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
        {
            return;
        }

        int now = Find.TickManager?.TicksGame ?? 0;
        TickPendingActions(pawn, now);
        TickDamageMomentum(pawn, now);

        if (pawn.Downed)
        {
            CancelCurrentAssaultJobIfNeeded(pawn);
            ClearPendingActions();
            return;
        }

        if (!pawn.IsHashIntervalTick(Props.checkIntervalTicks))
        {
            return;
        }

        if (now - lastDreadPulseTick >= Props.dreadPulseIntervalTicks)
        {
            PulseDreadField(pawn);
            lastDreadPulseTick = now;
        }

        TryFearRoar(pawn, now);

        EnsureAdjacentMeleePressure(pawn, now);

        if (pendingSweepExecuteTick < 0 && now - lastSweepTick >= Props.sweepIntervalTicks)
        {
            if (ScheduleSweep(pawn, now))
            {
                lastSweepTick = now;
            }
        }
    }

    private void TickPendingActions(Pawn pawn, int now)
    {
        if (pendingSweepExecuteTick >= 0 && now >= pendingSweepExecuteTick)
        {
            ExecuteBoneSpikeVolley(pawn);
            pendingSweepExecuteTick = -1;
        }
    }

    public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
    {
        base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null || totalDamageDealt <= 0.01f)
        {
            return;
        }

        HediffDef momentumDef = MyExtensionDefOf.VPEMYX_LegionMomentum ??
                                DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_LegionMomentum");
        if (momentumDef == null)
        {
            return;
        }

        Hediff momentum = pawn.health.hediffSet.GetFirstHediffOfDef(momentumDef);
        if (momentum == null)
        {
            pawn.health.AddHediff(momentumDef);
            momentum = pawn.health.hediffSet.GetFirstHediffOfDef(momentumDef);
        }

        if (momentum == null)
        {
            return;
        }

        float maxFromDef = momentumDef.maxSeverity > 0f ? momentumDef.maxSeverity : 5f;
        float maxSeverity = Math.Max(1f, Math.Min(Props.damageMomentumMaxSeverity, maxFromDef));
        momentum.Severity = Math.Min(maxSeverity, momentum.Severity + Math.Max(0.05f, Props.damageMomentumGainPerHit));
        momentumExpireTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.damageMomentumDurationTicks);
    }

    private void TickDamageMomentum(Pawn pawn, int now)
    {
        HediffDef momentumDef = MyExtensionDefOf.VPEMYX_LegionMomentum ??
                                DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_LegionMomentum");
        if (momentumDef == null || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff momentum = pawn.health.hediffSet.GetFirstHediffOfDef(momentumDef);
        if (momentum == null)
        {
            momentumExpireTick = -1;
            return;
        }

        if (momentumExpireTick >= 0 && now < momentumExpireTick)
        {
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(10, Props.damageMomentumDecayIntervalTicks)))
        {
            return;
        }

        momentum.Severity = Math.Max(0f, momentum.Severity - Math.Max(0.05f, Props.damageMomentumDecayPerPulse));
        if (momentum.Severity <= 0.001f)
        {
            pawn.health.RemoveHediff(momentum);
            momentumExpireTick = -1;
        }
    }

    private bool ScheduleSweep(Pawn pawn, int now)
    {
        if (HasHostileInRange(pawn, 2.1f))
        {
            return false;
        }

        if (!HasHostileInRange(pawn, BoneSpikeVolleyRange))
        {
            return false;
        }

        Pawn target = FindClosestHostilePawn(pawn, BoneSpikeVolleyRange);
        pendingSweepDirection = ResolveSweepDirection(pawn, target);
        ExecuteBoneSpikeVolley(pawn);
        return true;
    }

    private void ExecuteBoneSpikeVolley(Pawn pawn)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return;
        }

        float radius = BoneSpikeVolleyRange;
        float halfAngle = BoneSpikeVolleyHalfAngle;

        ThingDef projectileDef = ResolveBoneSpikeProjectileDef();
        if (projectileDef == null)
        {
            return;
        }

        IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return;
        }

        List<Pawn> validTargets = new List<Pawn>();
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == pawn || other.Dead || !other.Spawned)
            {
                continue;
            }

            if (!other.HostileTo(pawn) ||
                !other.Position.InHorDistOf(pawn.Position, radius) ||
                !IsInFrontCone(pawn, other.Position, radius, pendingSweepDirection, halfAngle))
            {
                continue;
            }

            validTargets.Add(other);
        }

        validTargets.Sort((a, b) =>
        {
            float ad = (a.Position - pawn.Position).LengthHorizontalSquared;
            float bd = (b.Position - pawn.Position).LengthHorizontalSquared;
            return ad.CompareTo(bd);
        });

        int count = Math.Min(validTargets.Count, BoneSpikeVolleyMaxTargets);
        for (int i = 0; i < count; i++)
        {
            Pawn target = validTargets[i];
            LaunchBoneSpikeProjectile(pawn, new LocalTargetInfo(target), projectileDef);
            if (Rand.Chance(0.45f))
            {
                LaunchBoneSpikeProjectile(pawn, new LocalTargetInfo(target), projectileDef);
            }
            ApplySeverityHediff(target, GetLegionDreadDef(), Props.dreadSeverityGain);
            ShowSpikeTrail(pawn, target);

            if (Rand.Chance(Props.sweepStunChance))
            {
                try
                {
                    target.stances?.stunner?.StunFor(Props.sweepStunTicks, pawn, addBattleLog: false, showMote: true);
                }
                catch
                {
                }
            }
        }

        for (int i = 0; i < BoneSpikeVolleyExtraProjectiles; i++)
        {
            IntVec3 cell = RandomConeCell(pawn, pendingSweepDirection, radius, halfAngle);
            if (!cell.IsValid || !cell.InBounds(map))
            {
                continue;
            }

            LaunchBoneSpikeProjectile(pawn, cell, projectileDef);
        }

        PlayOneShot(map, pawn.Position, "Gorehulk_Spine_Launch", "Ability_SpineLaunch", "Pawn_Dreadmeld_Attack_Spike");
    }

    private void TryFearRoar(Pawn pawn, int now)
    {
        if (pawn == null || pawn.Dead || pawn.Downed || pawn.Map == null)
        {
            return;
        }

        if (now - lastFearRoarTick < Math.Max(60, Props.fearRoarIntervalTicks))
        {
            return;
        }

        float radius = Math.Max(1.9f, Props.fearRoarRadius);
        IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return;
        }

        bool hasHostile = false;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == pawn || other.Dead || !other.Spawned || !other.HostileTo(pawn))
            {
                continue;
            }

            if (!other.Position.InHorDistOf(pawn.Position, radius))
            {
                continue;
            }

            hasHostile = true;
            TryStunTarget(other, pawn, Math.Max(30, Props.fearRoarStunTicks), showMote: true);
        }

        if (!hasHostile)
        {
            return;
        }

        lastFearRoarTick = now;
        SpawnStaticFleck(pawn.Map, pawn.Position, 3.0f, "FlashRed", "ExplosionFlash", "PsycastAreaEffect");
        ShowLargeRadiusRingEffect(pawn, radius * 0.55f, 1.1f, "PsycastAreaEffect", "PsycastPsychicEffect");
        ShowLargeRadiusRingEffect(pawn, radius, 1.45f, "PsycastSkipOuterRingEntry", "PsycastAreaEffect", "PsycastPsychicEffect");
        PlayOneShot(pawn.Map, pawn.Position, "PsychicBanshee", "PsychicRitual_Interrupted", "PsycastPsychicEffect");
    }

    private void PulseDreadField(Pawn caster)
    {
        Map map = caster?.Map;
        if (map == null)
        {
            return;
        }

        HediffDef dreadDef = GetLegionDreadDef();
        ThoughtDef dreadThought = MyExtensionDefOf.VPEMYX_LegionDreadThought ??
                                 DefDatabase<ThoughtDef>.GetNamedSilentFail("VPEMYX_LegionDreadThought");
        if (dreadDef == null)
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
            Pawn other = pawns[i];
            if (other == null || other == caster || other.Dead || other.health?.hediffSet == null)
            {
                continue;
            }

            bool inRange = other.HostileTo(caster) && other.Position.InHorDistOf(caster.Position, Props.dreadRadius);
            Hediff current = other.health.hediffSet.GetFirstHediffOfDef(dreadDef);
            if (inRange)
            {
                if (current == null)
                {
                    other.health.AddHediff(dreadDef);
                    current = other.health.hediffSet.GetFirstHediffOfDef(dreadDef);
                }

                if (current != null)
                {
                    current.Severity = Math.Min(1f, current.Severity + Props.dreadSeverityGain);
                }

                if (dreadThought != null &&
                    other.needs?.mood?.thoughts?.memories != null &&
                    other.IsHashIntervalTick(Props.dreadThoughtRefreshIntervalTicks))
                {
                    other.needs.mood.thoughts.memories.RemoveMemoriesOfDef(dreadThought);
                    other.needs.mood.thoughts.memories.TryGainMemory(dreadThought, caster);
                }
            }
            else if (current != null)
            {
                current.Severity = Math.Max(0f, current.Severity - Props.dreadSeverityDecay);
                if (current.Severity <= 0.001f)
                {
                    other.health.RemoveHediff(current);
                }
            }
        }

        // No visual effect here by request: keep pressure debuff gameplay-only.
    }

    private void ShowDreadPulseEffect(Pawn pawn)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return;
        }

        SpawnStaticFleck(map, pawn.Position, 2.2f, "VoidNodeHighLightningRing", "LightningGlow", "PsycastPsychicEffect");
        SpawnStaticFleck(map, pawn.Position, 1.3f, "FlashRed", "ExplosionFlash");
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, Props.dreadRadius, useCenter: true))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            float dist = pawn.Position.DistanceTo(cell);
            if (dist < Props.dreadRadius - 0.8f || dist > Props.dreadRadius + 0.25f)
            {
                continue;
            }

            if ((cell.x + cell.z) % 4 == 0)
            {
                SpawnStaticFleck(map, cell, 0.95f, "VoidNodeLowLightningRing", "PsycastPsychicEffect");
            }
            else if ((cell.x + cell.z) % 5 == 0)
            {
                SpawnStaticFleck(map, cell, 0.65f, "LightningGlow", "ShotFlash");
            }
        }

        PlayOneShot(map, pawn.Position, "PsychicBanshee", "PsycastPsychicEffect", "Psycast_Skip_Entry");
    }

    private void ShowChargeTelegraph(Pawn pawn, IntVec3 targetCell)
    {
        if (pawn?.Map == null || !targetCell.IsValid || !targetCell.InBounds(pawn.Map))
        {
            return;
        }

        Map map = pawn.Map;
        Vector3 start = pawn.DrawPos;
        Vector3 end = targetCell.ToVector3Shifted();
        int points = Math.Max(6, (int)(pawn.Position.DistanceTo(targetCell) * 2.2f));

        SpawnStaticFleck(map, pawn.Position, 1.6f, "PsycastSkipOuterRingEntry", "PsycastSkipFlashEntry", "ExplosionFlash");
        SpawnStaticFleck(map, targetCell, 2.2f, "PsycastSkipFlashEntry", "PsycastPsychicEffect");
        for (int i = 1; i < points; i++)
        {
            IntVec3 cell = IntVec3.FromVector3(Vector3.Lerp(start, end, i / (float)points));
            if (!cell.InBounds(map))
            {
                continue;
            }

            if ((i % 3) == 0)
            {
                SpawnStaticFleck(map, cell, 0.95f, "PsycastPsychicLine", "ShotFlash", "PsycastAreaEffect");
            }
            else
            {
                SpawnStaticFleck(map, cell, 0.65f, "SparkFlash", "PsycastPsychicLine");
            }
        }

        PlayOneShot(map, pawn.Position, "Pawn_Devourer_Jump", "Psycast_Skip_Entry");
    }

    private void ShowSweepTelegraph(Pawn pawn, Vector2 direction)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return;
        }

        SpawnStaticFleck(map, pawn.Position, 1.9f, "PsycastSkipOuterRingEntry", "FlashRed", "ExplosionFlash");
        ShowSweepConeArc(pawn, BoneSpikeVolleyRange, direction, BoneSpikeVolleyHalfAngle, 1.0f, "PsycastSkipOuterRingEntry", "PsycastAreaEffect");
        PlayOneShot(map, pawn.Position, "PsychicRitual_Interrupted", "PsycastPsychicEffect");
    }

    private void ShowBoneSpikeVolleyEffect(Pawn pawn, float radius, Vector2 direction, float halfAngle)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return;
        }

        SpawnStaticFleck(map, pawn.Position, 2.0f, "DustPuffThick", "FlashRed", "ExplosionFlash");
        ShowSweepConeArc(pawn, radius * 0.78f, direction, halfAngle - 12f, 0.95f, "SparkFlash", "ShotFlash");
        ShowSweepConeArc(pawn, radius, direction, halfAngle, 1.2f, "PsycastPsychicLine", "PsycastPsychicEffect", "PsycastAreaEffect");

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, useCenter: true))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            float dist = pawn.Position.DistanceTo(cell);
            if (dist < 1.2f || dist > radius)
            {
                continue;
            }

            if (!IsInFrontCone(pawn, cell, radius, direction, halfAngle))
            {
                continue;
            }

            if ((cell.x + (cell.z * 2)) % 3 == 0)
            {
                SpawnStaticFleck(map, cell, 0.85f, "PsycastPsychicLine", "SparkFlash", "ShotFlash");
            }
        }
    }

    private static ThingDef ResolveBoneSpikeProjectileDef()
    {
        return DefDatabase<ThingDef>.GetNamedSilentFail("Spine_Gorehulk")
               ?? DefDatabase<ThingDef>.GetNamedSilentFail("Spike_Fingerspike")
               ?? DefDatabase<ThingDef>.GetNamedSilentFail("Spike_Toughspike");
    }

    private static void LaunchBoneSpikeProjectile(Pawn caster, IntVec3 targetCell, ThingDef projectileDef)
    {
        LaunchBoneSpikeProjectile(caster, new LocalTargetInfo(targetCell), projectileDef);
    }

    private static void LaunchBoneSpikeProjectile(Pawn caster, LocalTargetInfo targetInfo, ThingDef projectileDef)
    {
        if (caster?.Map == null || projectileDef == null || !targetInfo.IsValid || !targetInfo.Cell.InBounds(caster.Map))
        {
            return;
        }

        try
        {
            Projectile projectile = GenSpawn.Spawn(projectileDef, caster.Position, caster.Map, WipeMode.Vanish) as Projectile;
            if (projectile == null)
            {
                return;
            }

            projectile.Launch(caster, caster.DrawPos, targetInfo, targetInfo, ProjectileHitFlags.IntendedTarget, preventFriendlyFire: true, equipment: null, targetCoverDef: null);
        }
        catch
        {
        }
    }

    private static IntVec3 RandomConeCell(Pawn pawn, Vector2 direction, float range, float halfAngle)
    {
        if (pawn?.Map == null)
        {
            return IntVec3.Invalid;
        }

        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        float angle = Rand.Range(-halfAngle, halfAngle);
        float distance = Rand.Range(range * 0.4f, range);
        Vector2 rotated = Rotate(dir, angle);

        IntVec3 cell = new IntVec3(
            pawn.Position.x + Mathf.RoundToInt(rotated.x * distance),
            0,
            pawn.Position.z + Mathf.RoundToInt(rotated.y * distance));

        if (!cell.InBounds(pawn.Map))
        {
            return CellFinder.StandableCellNear(cell.ClampInsideMap(pawn.Map), pawn.Map, 2f);
        }

        return cell;
    }

    private static Vector2 Rotate(Vector2 v, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2((v.x * cos) - (v.y * sin), (v.x * sin) + (v.y * cos));
    }

    private void ShowSpikeTrail(Pawn pawn, Pawn target)
    {
        Map map = pawn?.Map;
        if (map == null || target == null || !target.Spawned || target.Map != map)
        {
            return;
        }

        Vector3 start = pawn.DrawPos;
        Vector3 end = target.DrawPos;
        int points = Math.Max(4, (int)(pawn.Position.DistanceTo(target.Position) * 1.8f));
        for (int i = 1; i < points; i++)
        {
            IntVec3 cell = IntVec3.FromVector3(Vector3.Lerp(start, end, i / (float)points));
            if (!cell.InBounds(map))
            {
                continue;
            }

            if ((i % 2) == 0)
            {
                SpawnStaticFleck(map, cell, 0.62f, "PsycastPsychicLine", "ShotFlash");
            }
        }

        SpawnStaticFleck(map, target.Position, 1.0f, "SparkFlash", "ShotFlash", "PsycastPsychicEffect");
    }

    private static void ShowSweepConeArc(Pawn pawn, float radius, Vector2 direction, float halfAngleDeg, float fleckSize, params string[] fleckDefs)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return;
        }

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, useCenter: true))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            float dist = pawn.Position.DistanceTo(cell);
            if (dist < radius - 0.8f || dist > radius + 0.25f)
            {
                continue;
            }

            if (!IsInFrontCone(pawn, cell, radius + 0.2f, direction, halfAngleDeg))
            {
                continue;
            }

            if ((cell.x + cell.z) % 2 == 0)
            {
                SpawnStaticFleck(map, cell, fleckSize, fleckDefs);
            }
        }
    }

    private static HediffDef GetLegionDreadDef()
    {
        return MyExtensionDefOf.VPEMYX_LegionDreadField ??
               DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_LegionDreadField");
    }

    private static void ApplySeverityHediff(Pawn pawn, HediffDef hediffDef, float gain, bool clampToOne = false)
    {
        if (pawn?.health?.hediffSet == null || hediffDef == null || gain <= 0f)
        {
            return;
        }

        Hediff current = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
        if (current == null)
        {
            pawn.health.AddHediff(hediffDef);
            current = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
        }

        if (current == null)
        {
            return;
        }

        float maxSeverity = clampToOne ? 1f : Math.Max(1f, current.def.maxSeverity);
        current.Severity = Math.Min(maxSeverity, current.Severity + gain);
    }


    private static Vector2 ResolveSweepDirection(Pawn pawn, Pawn preferredTarget)
    {
        if (pawn == null)
        {
            return Vector2.right;
        }

        if (preferredTarget != null && preferredTarget.Spawned && preferredTarget.Map == pawn.Map)
        {
            Vector2 toTarget = new Vector2(preferredTarget.Position.x - pawn.Position.x, preferredTarget.Position.z - pawn.Position.z);
            if (toTarget.sqrMagnitude > 0.01f)
            {
                return toTarget.normalized;
            }
        }

        IntVec3 facingCell = pawn.Rotation.FacingCell;
        Vector2 facing = new Vector2(facingCell.x, facingCell.z);
        return facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.right;
    }

    private static bool IsInFrontCone(Pawn pawn, IntVec3 targetCell, float radius, Vector2 direction, float halfAngleDeg)
    {
        if (pawn == null)
        {
            return false;
        }

        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        Vector2 delta = new Vector2(targetCell.x - pawn.Position.x, targetCell.z - pawn.Position.z);
        float distSq = delta.sqrMagnitude;
        if (distSq > radius * radius)
        {
            return false;
        }

        if (distSq <= 0.01f)
        {
            return true;
        }

        float dot = Vector2.Dot(dir, delta.normalized);
        float minDot = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        return dot >= minDot;
    }

    private static void ShowLargeRadiusRingEffect(Pawn pawn, float radius, float size, params string[] fleckDefs)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return;
        }

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, useCenter: true))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            float dist = pawn.Position.DistanceTo(cell);
            if (dist < radius - 0.95f || dist > radius + 0.55f)
            {
                continue;
            }

            if ((cell.x + cell.z) % 2 == 0)
            {
                SpawnStaticFleck(map, cell, size, fleckDefs);
            }
        }
    }

    private static void SpawnStaticFleck(Map map, IntVec3 cell, float size, params string[] preferredDefs)
    {
        if (map == null || !cell.InBounds(map))
        {
            return;
        }

        FleckDef def = ResolveFleck(preferredDefs);
        if (def != null)
        {
            FleckMaker.Static(cell, map, def, size);
            return;
        }

        FleckMaker.Static(cell, map, FleckDefOf.PsycastAreaEffect, size);
    }

    private static FleckDef ResolveFleck(params string[] preferredDefs)
    {
        if (preferredDefs != null)
        {
            for (int i = 0; i < preferredDefs.Length; i++)
            {
                string name = preferredDefs[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                FleckDef def = DefDatabase<FleckDef>.GetNamedSilentFail(name);
                if (def != null)
                {
                    return def;
                }
            }
        }

        return DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
    }

    private static void PlayOneShot(Map map, IntVec3 cell, params string[] preferredSounds)
    {
        if (map == null)
        {
            return;
        }

        SoundDef soundDef = ResolveSound(preferredSounds);
        soundDef?.PlayOneShot(new TargetInfo(cell, map));
    }

    private static SoundDef ResolveSound(params string[] preferredDefs)
    {
        if (preferredDefs != null)
        {
            for (int i = 0; i < preferredDefs.Length; i++)
            {
                string name = preferredDefs[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail(name);
                if (def != null)
                {
                    return def;
                }
            }
        }

        return DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
    }

    private static Pawn FindClosestHostilePawn(Pawn source, float radius)
    {
        IReadOnlyList<Pawn> pawns = source?.Map?.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return null;
        }

        Pawn best = null;
        float bestDistSq = float.MaxValue;
        float maxDistSq = radius * radius;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == source || other.Dead || !other.Spawned || !other.HostileTo(source))
            {
                continue;
            }

            float distSq = (other.Position - source.Position).LengthHorizontalSquared;
            if (distSq > maxDistSq || distSq >= bestDistSq)
            {
                continue;
            }

            bestDistSq = distSq;
            best = other;
        }

        return best;
    }


    private static bool HasHostileInRange(Pawn source, float radius)
    {
        IReadOnlyList<Pawn> pawns = source?.Map?.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return false;
        }

        float maxDistSq = radius * radius;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other == source || other.Dead || !other.Spawned || !other.HostileTo(source))
            {
                continue;
            }

            float distSq = (other.Position - source.Position).LengthHorizontalSquared;
            if (distSq <= maxDistSq)
            {
                return true;
            }
        }

        return false;
    }

    private bool EnsureAdjacentMeleePressure(Pawn pawn, int now)
    {
        if (pawn == null || pawn.jobs == null || pawn.Downed || pawn.Dead)
        {
            return false;
        }

        Job cur = pawn.CurJob;
        Pawn curTarget = cur?.def == JobDefOf.AttackMelee ? cur.targetA.Thing as Pawn : null;
        if (IsValidLockedMeleeTarget(pawn, curTarget))
        {
            lockedMeleeTargetId = curTarget.thingIDNumber;
            meleeTargetLockUntilTick = now + Math.Max(60, Props.meleeTargetLockTicks);
            return true;
        }

        Pawn locked = FindLockedMeleeTarget(pawn);
        if (IsValidLockedMeleeTarget(pawn, locked) && now < meleeTargetLockUntilTick)
        {
            return TryStartMeleeJob(pawn, locked, now);
        }

        Pawn adjacent = FindClosestHostilePawn(pawn, 2.1f);
        if (!IsValidLockedMeleeTarget(pawn, adjacent))
        {
            if (now >= meleeTargetLockUntilTick)
            {
                lockedMeleeTargetId = -1;
                meleeTargetLockUntilTick = -1;
            }

            return false;
        }

        return TryStartMeleeJob(pawn, adjacent, now);
    }

    private bool TryStartMeleeJob(Pawn pawn, Pawn target, int now)
    {
        if (!IsValidLockedMeleeTarget(pawn, target))
        {
            return false;
        }

        Job cur = pawn.CurJob;
        if (cur != null && cur.def == JobDefOf.AttackMelee && cur.targetA.Thing == target)
        {
            lockedMeleeTargetId = target.thingIDNumber;
            meleeTargetLockUntilTick = now + Math.Max(60, Props.meleeTargetLockTicks);
            return true;
        }

        try
        {
            Job melee = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            melee.expiryInterval = 220;
            melee.checkOverrideOnExpire = true;
            pawn.jobs.StartJob(melee, JobCondition.InterruptForced);
            lockedMeleeTargetId = target.thingIDNumber;
            meleeTargetLockUntilTick = now + Math.Max(60, Props.meleeTargetLockTicks);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Pawn FindLockedMeleeTarget(Pawn pawn)
    {
        if (pawn?.Map == null || lockedMeleeTargetId < 0)
        {
            return null;
        }

        IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return null;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other != null && other.thingIDNumber == lockedMeleeTargetId)
            {
                return other;
            }
        }

        return null;
    }

    private static bool IsValidLockedMeleeTarget(Pawn source, Pawn target)
    {
        return source != null &&
               target != null &&
               !target.Dead &&
               target.Spawned &&
               target.Map == source.Map &&
               target.HostileTo(source) &&
               target.Position.InHorDistOf(source.Position, 5.5f);
    }

    private static void TryStunTarget(Pawn target, Pawn source, int ticks, bool showMote)
    {
        if (target == null || source == null || ticks <= 0)
        {
            return;
        }

        try
        {
            target.stances?.stunner?.StunFor(ticks, source, addBattleLog: false, showMote: showMote);
        }
        catch
        {
        }
    }

    private static void CancelCurrentAssaultJobIfNeeded(Pawn pawn)
    {
        Job curJob = pawn?.CurJob;
        if (curJob == null)
        {
            return;
        }

        if (curJob.def != JobDefOf.AttackMelee)
        {
            return;
        }

        try
        {
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);
        }
        catch
        {
        }
    }

    private void ClearPendingActions()
    {
        pendingSweepExecuteTick = -1;
        pendingSweepDirection = Vector2.zero;
        lockedMeleeTargetId = -1;
        meleeTargetLockUntilTick = -1;
    }
}

public class HediffCompProperties_AbyssalAtrocityCore : HediffCompProperties
{
    public int thinkIntervalTicks = 30;

    // Skill 1: gravity black hole
    public int gravityPulseCooldownTicks = 2700;
    public float gravitySearchRadius = 45f;
    public float gravityPulseRadius = 14f;
    public int gravityPulseMaxTargets = 14;
    public float gravityPulseDamage = 26f;
    public float gravityPulseArmorPenetration = 0.35f;
    public int gravityPulseStunTicks = 180;
    public float gravityPullDistanceFromHole = 1.6f;
    public float gravityCorruptionSeverity = 0.22f;

    // Summoner-follow binding
    public int followCheckIntervalTicks = 45;
    public float followDesiredDistance = 8f;
    public float followTeleportDistance = 28f;
    public float followTeleportRadius = 2.8f;

    // Skill 2: abyssal terror roar
    public int terrorRoarCooldownTicks = 3600;
    public float terrorRoarRadius = 0f;
    public float terrorRoarMapRadiusFactor = 0.5f;
    public float terrorRoarDamage = 34f;
    public float terrorRoarArmorPenetration = 0.35f;
    public int terrorRoarStunTicks = 420;
    public float terrorRoarDreadSeverityGain = 0.45f;

    // Skill 3: flesh distortion
    public int fleshDistortionCooldownTicks = 900;
    public float fleshDistortionSearchRadius = 42f;
    public float fleshDistortionRadius = 9f;
    public int fleshDistortionMaxTargets = 20;
    public float fleshDistortionInitialSeverity = 0.2f;

    // Basic attack-like corruption pulse
    public int corruptionPulseIntervalTicks = 120;
    public float corruptionPulseSearchRadius = 52f;
    public float corruptionPulseRadius = 3.4f;
    public int corruptionPulseMaxTargets = 6;
    public float corruptionPulseSeverityGain = 0.08f;
    public float corruptionPulseBonusIfAlreadyCorrupted = 0.14f;

    // Passive sustain
    public int regenIntervalTicks = 20;
    public float regenHealPerPulse = 5.2f;
    public int regenInjuriesPerPulse = 5;
    public float regenBloodLossReduction = 0.18f;

    // Passive: void shield
    public int voidShieldMaxCharges = 15;
    public int voidShieldHitThreshold = 20;
    public int voidShieldWindowTicks = 60;
    public float voidShieldDamageFactor = 0.08f;
    public int voidShieldRechargeIntervalTicks = 240;
    public int voidShieldRechargeAmount = 1;
    public int voidShieldRechargeDelayAfterHitTicks = 300;

    // Passive: damage transfer to abyssal
    public bool enableDamageTransfer = true;

    public HediffCompProperties_AbyssalAtrocityCore()
    {
        compClass = typeof(HediffComp_AbyssalAtrocityCore);
    }
}

public class HediffComp_AbyssalAtrocityCore : HediffComp
{
    private HediffCompProperties_AbyssalAtrocityCore Props => (HediffCompProperties_AbyssalAtrocityCore)props;

    private int lastGravityPulseTick = -99999;
    private int lastTerrorRoarTick = -99999;
    private int lastFleshDistortionTick = -99999;
    private int lastCorruptionPulseTick = -99999;
    private int lastFollowTick = -99999;
    private int nextRoarWaveTick = -1;
    private int roarWaveIndex = -1;
    private float roarWaveMaxRadius = 0f;
    private IntVec3 blackHoleVisualCenter = IntVec3.Invalid;
    private int blackHoleVisualUntilTick = -1;
    private int nextBlackHoleVisualTick = -1;
    private int voidShieldCharges = -1;
    private int voidShieldWindowStartTick = -99999;
    private int voidShieldHitsInWindow = 0;
    private int nextVoidShieldRechargeTick = -1;
    private int lastVoidShieldHitTick = -99999;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref voidShieldCharges, "voidShieldCharges", -1);
        Scribe_Values.Look(ref voidShieldWindowStartTick, "voidShieldWindowStartTick", -99999);
        Scribe_Values.Look(ref voidShieldHitsInWindow, "voidShieldHitsInWindow", 0);
        Scribe_Values.Look(ref nextVoidShieldRechargeTick, "nextVoidShieldRechargeTick", -1);
        Scribe_Values.Look(ref lastVoidShieldHitTick, "lastVoidShieldHitTick", -99999);
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        EnsureVoidShieldInitialized();
    }

    public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
    {
        base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
        if (totalDamageDealt <= 0f)
        {
            return;
        }

        EnsureVoidShieldInitialized();
        if (voidShieldCharges <= 0)
        {
            return;
        }

        int now = Find.TickManager?.TicksGame ?? 0;
        lastVoidShieldHitTick = now;
        nextVoidShieldRechargeTick = now + Math.Max(30, Props.voidShieldRechargeDelayAfterHitTicks);

        float factor = Mathf.Clamp(Props.voidShieldDamageFactor, 0.01f, 1f);
        float restored = Math.Max(0f, totalDamageDealt * (1f - factor));
        if (restored > 0f && Pawn?.health?.hediffSet != null)
        {
            HealMostSevereInjuries(Pawn, restored, 8);
            ReduceBloodLoss(Pawn, restored * 0.03f);
        }

        int windowTicks = Math.Max(1, Props.voidShieldWindowTicks);
        if (now - voidShieldWindowStartTick >= windowTicks)
        {
            voidShieldWindowStartTick = now;
            voidShieldHitsInWindow = 0;
        }

        voidShieldHitsInWindow++;
        if (voidShieldHitsInWindow < Math.Max(1, Props.voidShieldHitThreshold))
        {
            return;
        }

        voidShieldCharges = Math.Max(0, voidShieldCharges - 1);
        voidShieldHitsInWindow = 0;
        voidShieldWindowStartTick = now;
        if (Pawn?.Spawned == true && Pawn.Map != null)
        {
            SpawnStaticFleck(Pawn.Map, Pawn.Position, 1.35f, "VoidNodeLowLightningRing", "PulsingDistortionRing", "DarkHighlightRing");
            PlayOneShot(Pawn.Map, Pawn.Position, "VoidTerrorCast", "PsychicBanshee");
        }
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
        {
            return;
        }

        int now = Find.TickManager?.TicksGame ?? 0;
        EnsureVoidShieldInitialized();
        TickVoidShieldRecharge(pawn, now);
        TickRoarWaveEffects(pawn, now);
        TickBlackHoleVisuals(pawn.Map, now);
        if (pawn.IsHashIntervalTick(120))
        {
            StripTransientVoidHediffs(pawn);
        }

        if (pawn.IsHashIntervalTick(Math.Max(8, Props.regenIntervalTicks)))
        {
            TickSustain(pawn);
        }

        if (pawn.Downed)
        {
            StopStunIfAny(pawn);
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(12, Props.thinkIntervalTicks)))
        {
            return;
        }

        CancelMeleeIfAny(pawn);
        MaintainSummonerFollow(pawn, now);
        TryCastCorruptionPulse(pawn, now);

        if (TryCastTerrorRoar(pawn, now))
        {
            return;
        }

        if (TryCastGravityPulse(pawn, now))
        {
            return;
        }

        if (TryCastFleshDistortion(pawn, now))
        {
            return;
        }
    }

    private bool TryCastCorruptionPulse(Pawn caster, int now)
    {
        if (now - lastCorruptionPulseTick < Math.Max(60, Props.corruptionPulseIntervalTicks))
        {
            return false;
        }

        float searchRadius = Math.Max(8f, Props.corruptionPulseSearchRadius);
        Pawn primary = FindClosestHostilePawn(caster, searchRadius);
        if (primary == null || !primary.Spawned || primary.Map != caster.Map)
        {
            return false;
        }

        IntVec3 center = primary.Position;
        float radius = Math.Max(1.2f, Props.corruptionPulseRadius);
        List<Pawn> hostiles = CollectHostilesInRange(caster, searchRadius);
        if (hostiles.Count == 0)
        {
            return false;
        }

        List<Pawn> targets = new List<Pawn>();
        for (int i = 0; i < hostiles.Count; i++)
        {
            Pawn target = hostiles[i];
            if (target != null && target.Position.InHorDistOf(center, radius))
            {
                targets.Add(target);
            }
        }

        if (targets.Count == 0)
        {
            targets.Add(primary);
        }

        targets.Sort((a, b) =>
        {
            float ad = (a.Position - center).LengthHorizontalSquared;
            float bd = (b.Position - center).LengthHorizontalSquared;
            return ad.CompareTo(bd);
        });

        int cap = Math.Min(targets.Count, Math.Max(1, Props.corruptionPulseMaxTargets));
        SpawnStaticFleck(caster.Map, center, 1.7f, "MonolithShadow", "NociosphereDepartCompleteDistortion", "DarkHighlightRing");
        SpawnStaticFleck(caster.Map, center, 1.2f, "SputteringBlackPuff", "PulsingDistortionRing", "PsychicDistortionRingContractingQuick");
        ShowRingAt(caster.Map, center, radius, 0.95f, "DarkHighlightRing", "SputteringBlackPuff", "PulsingDistortionRing");

        HediffDef corruptionDef = MyExtensionDefOf.VPEMYX_Corruption ??
                                  DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_Corruption");
        if (corruptionDef == null)
        {
            return false;
        }

        float baseGain = Math.Max(0.01f, Props.corruptionPulseSeverityGain);
        float bonusGain = Math.Max(0f, Props.corruptionPulseBonusIfAlreadyCorrupted);
        for (int i = 0; i < cap; i++)
        {
            Pawn target = targets[i];
            if (target == null || target.Dead || !target.Spawned || target.Map != caster.Map)
            {
                continue;
            }

            BodyPartRecord origin = ResolveCorruptionOriginPart(target);
            bool hadCorruption = target.health?.hediffSet?.GetFirstHediffOfDef(corruptionDef) != null;
            AddOrIncreaseHediff(target, corruptionDef, origin, baseGain, clampToOne: false);
            if (hadCorruption && bonusGain > 0f)
            {
                AddOrIncreaseHediff(target, corruptionDef, origin, bonusGain, clampToOne: false);
            }

            SpawnStaticFleck(caster.Map, target.Position, 0.95f, "SputteringBlackPuff", "DarkHighlightRing", "PsycastAreaEffect");
            if (target != primary)
            {
                ShowLineEffect(caster.Map, center, target.Position, 0.7f, "PsycastPsychicLine", "SputteringBlackPuff", "ShotFlash");
            }
        }

        PlayOneShot(caster.Map, center, "VoidTerrorCast", "PsychicBanshee");
        lastCorruptionPulseTick = now;
        return true;
    }

    private void TickSustain(Pawn pawn)
    {
        StopStunIfAny(pawn);
        HealMostSevereInjuries(pawn, Props.regenHealPerPulse, Props.regenInjuriesPerPulse);
        ReduceBloodLoss(pawn, Props.regenBloodLossReduction);
    }

    private bool TryCastGravityPulse(Pawn caster, int now)
    {
        if (now - lastGravityPulseTick < Math.Max(120, Props.gravityPulseCooldownTicks))
        {
            return false;
        }

        float searchRadius = Math.Max(6f, Props.gravitySearchRadius);
        float holeRadius = Math.Max(3f, Props.gravityPulseRadius);
        List<Pawn> hostiles = CollectHostilesInRange(caster, searchRadius);
        if (hostiles.Count == 0)
        {
            return false;
        }

        IntVec3 holeCenter = ResolveBlackHoleCenter(caster, hostiles, holeRadius);
        if (!holeCenter.IsValid || !holeCenter.InBounds(caster.Map))
        {
            return false;
        }

        List<Pawn> targets = new List<Pawn>();
        for (int i = 0; i < hostiles.Count; i++)
        {
            Pawn pawn = hostiles[i];
            if (pawn != null && pawn.Position.InHorDistOf(holeCenter, holeRadius * 1.45f))
            {
                targets.Add(pawn);
            }
        }

        if (targets.Count == 0)
        {
            return false;
        }

        targets.Sort((a, b) =>
        {
            float ad = (a.Position - holeCenter).LengthHorizontalSquared;
            float bd = (b.Position - holeCenter).LengthHorizontalSquared;
            return ad.CompareTo(bd);
        });

        int cap = Math.Min(targets.Count, Math.Max(1, Props.gravityPulseMaxTargets));
        StartBlackHoleVisual(holeCenter, now);
        SpawnStaticFleck(caster.Map, holeCenter, 2.7f, "MonolithShadow", "NociosphereDepartCompleteDistortion", "PulsingDistortionRing");
        SpawnStaticFleck(caster.Map, holeCenter, 3.1f, "VoidStructureIncomingSlow", "TwistingMonolithLightsIntense", "PulsingDistortionRing");
        SpawnStaticFleck(caster.Map, holeCenter, 2.6f, "PsychicDistortionRingContractingQuick", "PulsingDistortionRing", "DarkHighlightRing");
        ShowRingAt(caster.Map, holeCenter, holeRadius * 0.5f, 1.1f, "DarkHighlightRing", "PulsingDistortionRing", "PsycastAreaEffect");
        ShowRingAt(caster.Map, holeCenter, holeRadius * 0.78f, 1.3f, "MonolithTwistingRingSlow", "PulsingDistortionRing", "DarkHighlightRing");
        ShowRingAt(caster.Map, holeCenter, holeRadius, 1.5f, "VoidNodeHighLightningRing", "DarkHighlightRing", "PulsingDistortionRing");

        for (int i = 0; i < cap; i++)
        {
            Pawn target = targets[i];
            if (target == null || target.Dead || !target.Spawned || target.Map != caster.Map)
            {
                continue;
            }

            IntVec3 from = target.Position;
            IntVec3 destination = ResolvePullDestinationToHole(caster.Map, holeCenter, target, Math.Max(0.9f, Props.gravityPullDistanceFromHole));
            if (destination.IsValid && destination.InBounds(caster.Map))
            {
                try
                {
                    target.pather?.StopDead();
                    target.Position = destination;
                }
                catch
                {
                }
            }

            DamageInfo dinfo = new DamageInfo(
                DamageDefOf.Blunt,
                Math.Max(1f, Props.gravityPulseDamage),
                Math.Max(0f, Props.gravityPulseArmorPenetration),
                -1f,
                caster);
            target.TakeDamage(dinfo);
            TryStun(target, caster, Math.Max(30, Props.gravityPulseStunTicks), showMote: true);
            ApplyOrIncreaseCorruption(target, Math.Max(0.02f, Props.gravityCorruptionSeverity));
            SpawnStaticFleck(caster.Map, from, 1.0f, "SputteringBlackPuff", "VoidStructureIncomingSlow", "PsychicDistortionRingContractingQuick");
            SpawnStaticFleck(caster.Map, target.Position, 1.2f, "PulsingDistortionRing", "VoidNodeLowLightningRing", "SputteringBlackPuff");
            ShowLineEffect(caster.Map, from, holeCenter, 0.95f, "PsycastPsychicLine", "VoidNodeLowLightningRing", "ShotFlash");
        }

        PlayOneShot(caster.Map, holeCenter, "VoidMonolith_ActivateL2L3", "VoidTerrorCast", "PsychicBanshee");
        lastGravityPulseTick = now;
        return true;
    }

    private bool TryCastTerrorRoar(Pawn caster, int now)
    {
        if (now - lastTerrorRoarTick < Math.Max(240, Props.terrorRoarCooldownTicks))
        {
            return false;
        }

        float radius = ResolveTerrorRoarRadius(caster);
        List<Pawn> targets = CollectHostilesInRange(caster, radius);
        if (targets.Count == 0)
        {
            return false;
        }

        HediffDef dreadDef = MyExtensionDefOf.VPEMYX_LegionDreadField ??
                             DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_LegionDreadField");
        for (int i = 0; i < targets.Count; i++)
        {
            Pawn target = targets[i];
            if (target == null || target.Dead || !target.Spawned || target.Map != caster.Map)
            {
                continue;
            }

            DamageInfo dinfo = new DamageInfo(
                DamageDefOf.Blunt,
                Math.Max(1f, Props.terrorRoarDamage),
                Math.Max(0f, Props.terrorRoarArmorPenetration),
                -1f,
                caster);
            target.TakeDamage(dinfo);
            TryStun(target, caster, Math.Max(30, Props.terrorRoarStunTicks), showMote: true);
            ApplySeverityHediff(target, dreadDef, Math.Max(0.05f, Props.terrorRoarDreadSeverityGain), clampToOne: true);
            if (CanReceiveForcedMentalBreak(target) && !TryTriggerSevereMentalBreak(target, caster))
            {
                TryStun(target, caster, Math.Max(60, Props.terrorRoarStunTicks / 2), showMote: false);
            }
        }

        StartRoarRipple(now, radius);
        SpawnStaticFleck(caster.Map, caster.Position, 3.2f, "TwistingMonolithLightsIntense", "FlashRed", "PsycastAreaEffect");
        PlayOneShot(caster.Map, caster.Position, "Pawn_Fleshbeast_Dreadmeld_Call", "PsychicBanshee", "VoidTerrorCast");

        lastTerrorRoarTick = now;
        return true;
    }

    private bool TryCastFleshDistortion(Pawn caster, int now)
    {
        if (now - lastFleshDistortionTick < Math.Max(180, Props.fleshDistortionCooldownTicks))
        {
            return false;
        }

        float searchRadius = Math.Max(8f, Props.fleshDistortionSearchRadius);
        float radius = Math.Max(2.8f, Props.fleshDistortionRadius);
        List<Pawn> hostiles = CollectHostilesInRange(caster, searchRadius);
        if (hostiles.Count == 0)
        {
            return false;
        }

        IntVec3 center = ResolveBlackHoleCenter(caster, hostiles, radius);
        if (!center.IsValid || !center.InBounds(caster.Map))
        {
            return false;
        }

        List<Pawn> targets = new List<Pawn>();
        for (int i = 0; i < hostiles.Count; i++)
        {
            Pawn target = hostiles[i];
            if (target != null && target.Position.InHorDistOf(center, radius))
            {
                targets.Add(target);
            }
        }

        if (targets.Count == 0)
        {
            return false;
        }

        int cap = Math.Min(targets.Count, Math.Max(1, Props.fleshDistortionMaxTargets));
        HediffDef malignancyDef = MyExtensionDefOf.VPEMYX_CellularMalignancy ??
                                  DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_CellularMalignancy");

        SpawnStaticMote(caster.Map, center, "BiomutationWarmup", 1.45f);
        SpawnStaticMote(caster.Map, center, "Mote_FleshmelterBolt_Target", 0.85f);
        SpawnStaticFleck(caster.Map, center, 2.0f, "Fleck_AcidSpitImpact", "SputteringBlackPuff", "VoidNodeLowLightningRing");
        SpawnStaticFleck(caster.Map, center, 2.2f, "VoidNodeLowLightningRing", "PulsingDistortionRing", "DarkHighlightRing");
        ShowRingAt(caster.Map, center, radius * 0.45f, 1.0f, "Fleck_AcidSpitLaunchedGlobFast", "SputteringBlackPuff", "DarkHighlightRing");
        ShowRingAt(caster.Map, center, radius * 0.75f, 1.3f, "Fleck_AcidSpitLaunchedDenseMist", "PulsingDistortionRing", "DarkHighlightRing");
        ShowRingAt(caster.Map, center, radius, 1.5f, "Fleck_AcidSpitLaunchedMist", "VoidNodeLowLightningRing", "PulsingDistortionRing");

        for (int i = 0; i < cap; i++)
        {
            Pawn target = targets[i];
            if (target == null || target.Dead || !target.Spawned || target.Map != caster.Map)
            {
                continue;
            }

            SpawnStaticMote(caster.Map, target.Position, "Mote_FleshmelterBolt_Charge", 0.62f);
            SpawnStaticFleck(caster.Map, target.Position, 1.0f, "Fleck_AcidSpitImpact", "SputteringBlackPuff", "PsycastAreaEffect");
            ShowLineEffect(caster.Map, center, target.Position, 0.85f, "Fleck_AcidSpitLaunchedDenseMist", "VoidNodeLowLightningRing", "ShotFlash");

            if (malignancyDef != null)
            {
                BodyPartRecord origin = ResolveCorruptionOriginPart(target);
                AddOrIncreaseHediff(target, malignancyDef, origin, Math.Max(0.03f, Props.fleshDistortionInitialSeverity), clampToOne: false);
            }
        }

        PlayOneShot(caster.Map, center, "Fleshbeast_Bulbfreak_Spit_Acid_Impact", "VoidTerrorCast", "PsychicBanshee");
        lastFleshDistortionTick = now;
        return true;
    }

    private void StartRoarRipple(int now, float maxRadius)
    {
        roarWaveMaxRadius = Math.Max(3f, maxRadius);
        roarWaveIndex = 0;
        nextRoarWaveTick = now;
    }

    private void TickRoarWaveEffects(Pawn caster, int now)
    {
        if (roarWaveIndex < 0 || now < nextRoarWaveTick || caster?.Map == null)
        {
            return;
        }

        float[] factors = { 0.20f, 0.38f, 0.56f, 0.74f, 0.90f, 1.00f };
        if (roarWaveIndex >= factors.Length)
        {
            roarWaveIndex = -1;
            nextRoarWaveTick = -1;
            return;
        }

        float radius = Math.Max(3f, roarWaveMaxRadius * factors[roarWaveIndex]);
        float size = 0.95f + (roarWaveIndex * 0.15f);
        string[] flecks = ResolveRoarWaveFlecks(roarWaveIndex);
        ShowRing(caster, radius, size, flecks);

        if (roarWaveIndex == 0)
        {
            SpawnStaticFleck(caster.Map, caster.Position, 2.0f, "PulsingDistortionRing", "PsychicDistortionRingContractingQuick", "PsycastAreaEffect");
        }
        else if (roarWaveIndex == factors.Length - 1)
        {
            SpawnStaticFleck(caster.Map, caster.Position, 2.4f, "VoidNodeHighLightningRing", "TwistingMonolithLightsIntense", "PsycastAreaEffect");
            PlayOneShot(caster.Map, caster.Position, "VoidMonolith_ActivateL2L3", "PsychicBanshee", "VoidTerrorCast");
        }

        roarWaveIndex++;
        nextRoarWaveTick = now + 7;
    }

    private void StartBlackHoleVisual(IntVec3 center, int now)
    {
        blackHoleVisualCenter = center;
        blackHoleVisualUntilTick = now + 120;
        nextBlackHoleVisualTick = now;
    }

    private void TickBlackHoleVisuals(Map map, int now)
    {
        if (map == null || !blackHoleVisualCenter.IsValid || now > blackHoleVisualUntilTick || now < nextBlackHoleVisualTick)
        {
            return;
        }

        nextBlackHoleVisualTick = now + 8;
        SpawnVoidSphereMote(map, blackHoleVisualCenter, 2.0f);
        SpawnStaticFleck(map, blackHoleVisualCenter, 2.8f, "NociosphereDepartCompleteDistortion", "MonolithShadow", "PulsingDistortionRing");
        SpawnStaticFleck(map, blackHoleVisualCenter, 2.4f, "MonolithShadow", "DarkHighlightRing", "PsychicDistortionRingContractingQuick");
        SpawnStaticFleck(map, blackHoleVisualCenter, 1.9f, "VoidStructureActivatingRing", "NociosphereDepartingRing", "DarkHighlightRing");
        SpawnStaticFleck(map, blackHoleVisualCenter, 1.7f, "HoraxianHugeSpellDarkWarmup", "SputteringBlackPuff", "NociosphereDepartCompleteDistortion");
    }

    private static void SpawnVoidSphereMote(Map map, IntVec3 center, float scale)
    {
        if (map == null || !center.IsValid || !center.InBounds(map))
        {
            return;
        }

        ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_ActivatedVoidStructure");
        if (moteDef == null)
        {
            return;
        }

        try
        {
            MoteMaker.MakeStaticMote(center.ToVector3Shifted(), map, moteDef, Mathf.Max(0.8f, scale));
        }
        catch
        {
        }
    }

    private void EnsureVoidShieldInitialized()
    {
        if (voidShieldCharges >= 0)
        {
            return;
        }

        int now = Find.TickManager?.TicksGame ?? 0;
        voidShieldCharges = Math.Max(0, Props.voidShieldMaxCharges);
        voidShieldWindowStartTick = now;
        voidShieldHitsInWindow = 0;
        lastVoidShieldHitTick = -99999;
        nextVoidShieldRechargeTick = now + Math.Max(60, Props.voidShieldRechargeIntervalTicks);
    }

    private void TickVoidShieldRecharge(Pawn pawn, int now)
    {
        if (pawn == null || voidShieldCharges < 0)
        {
            return;
        }

        int maxCharges = Math.Max(1, Props.voidShieldMaxCharges);
        if (voidShieldCharges >= maxCharges)
        {
            return;
        }

        int interval = Math.Max(30, Props.voidShieldRechargeIntervalTicks);
        int delay = Math.Max(30, Props.voidShieldRechargeDelayAfterHitTicks);
        if (now - lastVoidShieldHitTick < delay)
        {
            if (nextVoidShieldRechargeTick < now + delay)
            {
                nextVoidShieldRechargeTick = now + delay;
            }
            return;
        }

        if (nextVoidShieldRechargeTick < 0)
        {
            nextVoidShieldRechargeTick = now + interval;
            return;
        }

        if (now < nextVoidShieldRechargeTick)
        {
            return;
        }

        int gain = Math.Max(1, Props.voidShieldRechargeAmount);
        voidShieldCharges = Math.Min(maxCharges, voidShieldCharges + gain);
        nextVoidShieldRechargeTick = now + interval;
        if (pawn.Spawned && pawn.Map != null)
        {
            SpawnStaticFleck(pawn.Map, pawn.Position, 0.9f, "VoidNodeLowLightningRing", "DarkHighlightRing", "PulsingDistortionRing");
        }
    }

    private void EnsureDamageTransferLink(Pawn abyssal, Pawn owner)
    {
        if (!Props.enableDamageTransfer || abyssal == null || owner?.health?.hediffSet == null)
        {
            return;
        }

        HediffDef linkDef = MyExtensionDefOf.VPEMYX_AbyssalDamageTransferLink ??
                            DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_AbyssalDamageTransferLink");
        if (linkDef == null)
        {
            return;
        }

        Hediff link = owner.health.hediffSet.GetFirstHediffOfDef(linkDef);
        if (link == null)
        {
            owner.health.AddHediff(linkDef);
            link = owner.health.hediffSet.GetFirstHediffOfDef(linkDef);
        }

        HediffComp_AbyssalDamageTransferLink comp = link?.TryGetComp<HediffComp_AbyssalDamageTransferLink>();
        comp?.SetLinkedAbyssal(abyssal);
    }

    private void MaintainSummonerFollow(Pawn pawn, int now)
    {
        if (now - lastFollowTick < Math.Max(15, Props.followCheckIntervalTicks))
        {
            return;
        }

        lastFollowTick = now;
        Pawn owner = ResolveSummonerPawn(pawn);
        if (owner == null || owner.Dead || !owner.Spawned || owner.Map != pawn.Map)
        {
            return;
        }

        EnsureDamageTransferLink(pawn, owner);

        float dist = pawn.Position.DistanceTo(owner.Position);
        float teleportDist = Math.Max(6f, Props.followTeleportDistance);
        float followDist = Math.Max(1.5f, Props.followDesiredDistance);
        if (dist > teleportDist)
        {
            IntVec3 near = CellFinder.StandableCellNear(owner.Position, owner.Map, Math.Max(1.5f, Props.followTeleportRadius));
            if (near.IsValid && near.InBounds(owner.Map))
            {
                IntVec3 oldPos = pawn.Position;
                try
                {
                    pawn.pather?.StopDead();
                    pawn.Position = near;
                    SpawnStaticFleck(owner.Map, oldPos, 1.4f, "PsycastSkipOuterRingEntry", "PulsingDistortionRing");
                    SpawnStaticFleck(owner.Map, near, 1.8f, "PsycastSkipFlashEntry", "PulsingDistortionRing", "TwistingMonolithLightsIntense");
                    PlayOneShot(owner.Map, near, "Psycast_Skip_Entry", "VoidTerrorCast");
                }
                catch
                {
                }
            }

            return;
        }

        if (dist <= followDist || pawn.jobs == null)
        {
            return;
        }

        try
        {
            IntVec3 near = CellFinder.StandableCellNear(owner.Position, owner.Map, followDist);
            if (near.IsValid && near.InBounds(owner.Map))
            {
                Job gotoOwner = JobMaker.MakeJob(JobDefOf.Goto, near);
                gotoOwner.expiryInterval = 160;
                gotoOwner.checkOverrideOnExpire = true;
                pawn.jobs.StartJob(gotoOwner, JobCondition.InterruptForced);
            }
        }
        catch
        {
        }
    }

    private static Pawn ResolveSummonerPawn(Pawn abyssal)
    {
        if (abyssal?.Map == null || Current.Game == null)
        {
            return null;
        }

        MyExtensionGameComponent comp = Current.Game.GetComponent<MyExtensionGameComponent>();
        if (comp == null || !comp.TryGetAbyssalOwnerId(abyssal, out int ownerId) || ownerId < 0)
        {
            return null;
        }

        IReadOnlyList<Pawn> pawns = abyssal.Map.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return null;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p != null && p.thingIDNumber == ownerId)
            {
                return p;
            }
        }

        return null;
    }

    private static IntVec3 ResolveBlackHoleCenter(Pawn caster, List<Pawn> hostiles, float radius)
    {
        if (caster?.Map == null || hostiles == null || hostiles.Count == 0)
        {
            return caster?.Position ?? IntVec3.Invalid;
        }

        Pawn best = hostiles[0];
        int bestScore = -1;
        float tieBreakDistSq = float.MaxValue;
        float clusterRadiusSq = (radius * 0.72f) * (radius * 0.72f);
        for (int i = 0; i < hostiles.Count; i++)
        {
            Pawn pivot = hostiles[i];
            if (pivot == null || pivot.Dead || !pivot.Spawned || pivot.Map != caster.Map)
            {
                continue;
            }

            int score = 0;
            for (int j = 0; j < hostiles.Count; j++)
            {
                Pawn other = hostiles[j];
                if (other == null || other.Dead || !other.Spawned || other.Map != caster.Map)
                {
                    continue;
                }

                if ((other.Position - pivot.Position).LengthHorizontalSquared <= clusterRadiusSq)
                {
                    score++;
                }
            }

            float distSq = (pivot.Position - caster.Position).LengthHorizontalSquared;
            if (score > bestScore || (score == bestScore && distSq < tieBreakDistSq))
            {
                bestScore = score;
                tieBreakDistSq = distSq;
                best = pivot;
            }
        }

        IntVec3 center = best?.Position ?? caster.Position;
        return CellFinder.StandableCellNear(center, caster.Map, 2.5f);
    }

    private static IntVec3 ResolvePullDestinationToHole(Map map, IntVec3 holeCenter, Pawn target, float distanceFromHole)
    {
        if (map == null || target == null || !holeCenter.IsValid || !holeCenter.InBounds(map))
        {
            return IntVec3.Invalid;
        }

        Vector2 outward = new Vector2(target.Position.x - holeCenter.x, target.Position.z - holeCenter.z);
        if (outward.sqrMagnitude < 0.001f)
        {
            outward = Rand.UnitVector2;
        }
        else
        {
            outward.Normalize();
        }

        IntVec3 rough = new IntVec3(
            holeCenter.x + Mathf.RoundToInt(outward.x * distanceFromHole),
            0,
            holeCenter.z + Mathf.RoundToInt(outward.y * distanceFromHole));
        return CellFinder.StandableCellNear(rough.ClampInsideMap(map), map, 2f);
    }

    private float ResolveTerrorRoarRadius(Pawn caster)
    {
        if (caster?.Map == null)
        {
            return 0f;
        }

        float mapHalf = Mathf.Min(caster.Map.Size.x, caster.Map.Size.z) * 0.5f;
        float factorRadius = Mathf.Max(
            8f,
            Mathf.Min(caster.Map.Size.x, caster.Map.Size.z) * Mathf.Clamp(Props.terrorRoarMapRadiusFactor, 0.15f, 0.95f));
        return Mathf.Max(Props.terrorRoarRadius, factorRadius, mapHalf);
    }

    private static bool CanReceiveForcedMentalBreak(Pawn pawn)
    {
        if (pawn == null || pawn.Dead || pawn.Downed || pawn.InMentalState || pawn.mindState?.mentalStateHandler == null)
        {
            return false;
        }

        if (IsDeathresting(pawn))
        {
            return false;
        }

        return !pawn.WorkTagIsDisabled(WorkTags.Violent) || !pawn.RaceProps.Humanlike || pawn.RaceProps.Animal;
    }

    private static bool IsDeathresting(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        try
        {
            var prop = typeof(Pawn).GetProperty("IsDeathresting");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                return (bool)prop.GetValue(pawn);
            }
        }
        catch
        {
        }

        List<Hediff> hediffs = pawn.health?.hediffSet?.hediffs;
        if (hediffs == null)
        {
            return false;
        }

        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            string name = h?.def?.defName;
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf("Deathrest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTriggerSevereMentalBreak(Pawn target, Pawn caster)
    {
        MentalStateHandler handler = target?.mindState?.mentalStateHandler;
        if (handler == null)
        {
            return false;
        }

        List<MentalStateDef> states = BuildSevereBreakPool(target);
        while (states.Count > 0)
        {
            MentalStateDef state = states.RandomElement();
            states.Remove(state);
            if (state == null)
            {
                continue;
            }

            if (handler.TryStartMentalState(
                    state,
                    "Abyssal terror roar",
                    forced: true,
                    forceWake: true,
                    causedByMood: false,
                    otherPawn: caster,
                    transitionSilently: false,
                    causedByDamage: false,
                    causedByPsycast: true))
            {
                return true;
            }
        }

        return false;
    }

    private static List<MentalStateDef> BuildSevereBreakPool(Pawn target)
    {
        List<MentalStateDef> result = new List<MentalStateDef>();
        if (target == null)
        {
            return result;
        }

        if (target.RaceProps.IsMechanoid)
        {
            AddMentalState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("BerserkMechanoid"));
            AddMentalState(result, MentalStateDefOf.Berserk);
            return result;
        }

        if (target.RaceProps.Animal)
        {
            AddMentalState(result, MentalStateDefOf.ManhunterPermanent);
            AddMentalState(result, MentalStateDefOf.Manhunter);
            AddMentalState(result, MentalStateDefOf.PanicFlee);
            return result;
        }

        AddMentalState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("Catatonic"));
        AddMentalState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("MurderousRage"));
        AddMentalState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("SadisticRage"));
        AddMentalState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("Wander_Psychotic"));
        AddMentalState(result, MentalStateDefOf.BerserkPermanent);
        AddMentalState(result, MentalStateDefOf.Berserk);
        return result;
    }

    private static void AddMentalState(List<MentalStateDef> list, MentalStateDef def)
    {
        if (def != null)
        {
            list.Add(def);
        }
    }

    private static void CancelMeleeIfAny(Pawn pawn)
    {
        Job cur = pawn?.CurJob;
        if (cur == null || cur.def != JobDefOf.AttackMelee)
        {
            return;
        }

        try
        {
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);
        }
        catch
        {
        }
    }

    private static string[] ResolveRoarWaveFlecks(int waveIndex)
    {
        return waveIndex switch
        {
            0 => new[] { "DarkHighlightRing", "PsychicDistortionRingContractingQuick", "PsycastAreaEffect" },
            1 => new[] { "PulsingDistortionRing", "MonolithTwistingRingSlow", "DarkHighlightRing" },
            2 => new[] { "VoidNodeLowLightningRing", "PulsingDistortionRing", "DarkHighlightRing" },
            3 => new[] { "MonolithTwistingRingSlow", "VoidNodeLowLightningRing", "PulsingDistortionRing" },
            4 => new[] { "VoidNodeHighLightningRing", "DarkHighlightRing", "MonolithTwistingRingSlow" },
            _ => new[] { "TwistingMonolithLightsIntense", "VoidNodeHighLightningRing", "DarkHighlightRing" }
        };
    }

    private static List<Pawn> CollectHostilesInRange(Pawn caster, float radius)
    {
        List<Pawn> result = new List<Pawn>();
        IReadOnlyList<Pawn> pawns = caster?.Map?.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return result;
        }

        float radiusSq = radius * radius;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (!IsValidTarget(caster, other, radius))
            {
                continue;
            }

            float distSq = (other.Position - caster.Position).LengthHorizontalSquared;
            if (distSq <= radiusSq)
            {
                result.Add(other);
            }
        }

        return result;
    }

    private static Pawn FindClosestHostilePawn(Pawn caster, float radius)
    {
        IReadOnlyList<Pawn> pawns = caster?.Map?.mapPawns?.AllPawnsSpawned;
        if (pawns == null)
        {
            return null;
        }

        Pawn best = null;
        float bestDistSq = float.MaxValue;
        float maxDistSq = radius * radius;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (!IsValidTarget(caster, other, radius))
            {
                continue;
            }

            float distSq = (other.Position - caster.Position).LengthHorizontalSquared;
            if (distSq > maxDistSq || distSq >= bestDistSq)
            {
                continue;
            }

            bestDistSq = distSq;
            best = other;
        }

        return best;
    }

    private static bool IsValidTarget(Pawn caster, Pawn target, float maxRange)
    {
        return caster != null &&
               target != null &&
               !target.Dead &&
               target.Spawned &&
               target.Map == caster.Map &&
               target.HostileTo(caster) &&
               target.Position.InHorDistOf(caster.Position, maxRange);
    }

    private static void ShowRing(Pawn caster, float radius, float size, params string[] fleckDefs)
    {
        Map map = caster?.Map;
        if (map == null)
        {
            return;
        }

        ShowRingAt(map, caster.Position, radius, size, fleckDefs);
    }

    private static void ShowRingAt(Map map, IntVec3 center, float radius, float size, params string[] fleckDefs)
    {
        if (map == null || !center.IsValid || !center.InBounds(map))
        {
            return;
        }

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, useCenter: true))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            float dist = center.DistanceTo(cell);
            if (dist < radius - 0.95f || dist > radius + 0.55f)
            {
                continue;
            }

            if ((cell.x + cell.z) % 2 == 0)
            {
                SpawnStaticFleck(map, cell, size, fleckDefs);
            }
        }
    }

    private static void ShowLineEffect(Map map, IntVec3 from, IntVec3 to, float size, params string[] fleckDefs)
    {
        if (map == null || !from.IsValid || !to.IsValid || !from.InBounds(map) || !to.InBounds(map))
        {
            return;
        }

        Vector3 start = from.ToVector3Shifted();
        Vector3 end = to.ToVector3Shifted();
        int points = Math.Max(4, (int)(from.DistanceTo(to) * 1.8f));
        for (int i = 1; i < points; i++)
        {
            IntVec3 cell = IntVec3.FromVector3(Vector3.Lerp(start, end, i / (float)points));
            if (!cell.InBounds(map))
            {
                continue;
            }

            if ((i % 2) == 0)
            {
                SpawnStaticFleck(map, cell, size, fleckDefs);
            }
        }
    }

    private static void SpawnStaticFleck(Map map, IntVec3 cell, float size, params string[] preferredDefs)
    {
        if (map == null || !cell.InBounds(map))
        {
            return;
        }

        FleckDef fleck = ResolveFleck(preferredDefs);
        FleckMaker.Static(cell, map, fleck ?? FleckDefOf.PsycastAreaEffect, size);
    }

    private static void SpawnStaticMote(Map map, IntVec3 cell, string moteDefName, float scale)
    {
        if (map == null || !cell.IsValid || !cell.InBounds(map) || string.IsNullOrEmpty(moteDefName))
        {
            return;
        }

        ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail(moteDefName);
        if (moteDef == null)
        {
            return;
        }

        try
        {
            MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, moteDef, Math.Max(0.1f, scale));
        }
        catch
        {
        }
    }

    private static FleckDef ResolveFleck(params string[] preferredDefs)
    {
        if (preferredDefs != null)
        {
            for (int i = 0; i < preferredDefs.Length; i++)
            {
                string name = preferredDefs[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                FleckDef def = DefDatabase<FleckDef>.GetNamedSilentFail(name);
                if (def != null)
                {
                    return def;
                }
            }
        }

        return DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
    }

    private static void PlayOneShot(Map map, IntVec3 cell, params string[] preferredDefs)
    {
        if (map == null)
        {
            return;
        }

        SoundDef sound = ResolveSound(preferredDefs);
        sound?.PlayOneShot(new TargetInfo(cell, map));
    }

    private static SoundDef ResolveSound(params string[] preferredDefs)
    {
        if (preferredDefs != null)
        {
            for (int i = 0; i < preferredDefs.Length; i++)
            {
                string name = preferredDefs[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                SoundDef def = DefDatabase<SoundDef>.GetNamedSilentFail(name);
                if (def != null)
                {
                    return def;
                }
            }
        }

        return DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
    }

    private static void TryStun(Pawn pawn, Pawn source, int ticks, bool showMote)
    {
        if (pawn == null || source == null || ticks <= 0)
        {
            return;
        }

        try
        {
            pawn.stances?.stunner?.StunFor(ticks, source, addBattleLog: false, showMote: showMote);
        }
        catch
        {
        }
    }

    private static void StopStunIfAny(Pawn pawn)
    {
        if (pawn?.stances?.stunner == null)
        {
            return;
        }

        try
        {
            if (pawn.stances.stunner.Stunned)
            {
                pawn.stances.stunner.StopStun();
            }
        }
        catch
        {
        }
    }

    private static void ApplySeverityHediff(Pawn pawn, HediffDef hediffDef, float gain, bool clampToOne)
    {
        if (pawn?.health?.hediffSet == null || hediffDef == null || gain <= 0f)
        {
            return;
        }

        Hediff current = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
        if (current == null)
        {
            pawn.health.AddHediff(hediffDef);
            current = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
        }

        if (current == null)
        {
            return;
        }

        float maxSeverity = clampToOne ? 1f : Math.Max(1f, current.def.maxSeverity);
        current.Severity = Math.Min(maxSeverity, current.Severity + gain);
    }

    private static void ApplyOrIncreaseCorruption(Pawn target, float severityGain)
    {
        HediffDef corruptionDef = MyExtensionDefOf.VPEMYX_Corruption ??
                                  DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_Corruption");
        if (target?.health?.hediffSet == null || corruptionDef == null || severityGain <= 0f)
        {
            return;
        }

        BodyPartRecord origin = ResolveCorruptionOriginPart(target);
        AddOrIncreaseHediff(target, corruptionDef, origin, severityGain, clampToOne: false);
    }

    private static BodyPartRecord ResolveCorruptionOriginPart(Pawn pawn)
    {
        return pawn?.health?.hediffSet?.GetBrain() ?? pawn?.RaceProps?.body?.corePart;
    }

    private static Hediff AddOrIncreaseHediff(Pawn pawn, HediffDef def, BodyPartRecord part, float gain, bool clampToOne)
    {
        if (pawn?.health?.hediffSet == null || def == null || gain <= 0f)
        {
            return null;
        }

        BodyPartRecord targetPart = IsValidPartForHediff(pawn, part) ? part : null;
        Hediff current = FindHediffOnPart(pawn, def, targetPart) ?? pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (current == null)
        {
            try
            {
                if (targetPart != null)
                {
                    pawn.health.AddHediff(def, targetPart);
                }
                else
                {
                    pawn.health.AddHediff(def);
                }
            }
            catch
            {
                return null;
            }

            current = FindHediffOnPart(pawn, def, targetPart) ?? pawn.health.hediffSet.GetFirstHediffOfDef(def);
        }

        if (current == null)
        {
            return null;
        }

        float maxSeverity = clampToOne ? 1f : Math.Max(1f, current.def.maxSeverity);
        current.Severity = Math.Min(maxSeverity, current.Severity + gain);
        return current;
    }

    private static bool IsValidPartForHediff(Pawn pawn, BodyPartRecord part)
    {
        HediffSet set = pawn?.health?.hediffSet;
        if (set == null || part == null)
        {
            return false;
        }

        if (!set.HasBodyPart(part) || set.PartIsMissing(part))
        {
            return false;
        }

        for (BodyPartRecord cursor = part.parent; cursor != null; cursor = cursor.parent)
        {
            if (set.PartIsMissing(cursor))
            {
                return false;
            }
        }

        try
        {
            return set.GetPartHealth(part) > 0.001f;
        }
        catch
        {
            return false;
        }
    }

    private static Hediff FindHediffOnPart(Pawn pawn, HediffDef def, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
        {
            return null;
        }

        if (part == null)
        {
            return pawn.health.hediffSet.GetFirstHediffOfDef(def);
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h != null && h.def == def && h.Part == part)
            {
                return h;
            }
        }

        return null;
    }

    private static void HealMostSevereInjuries(Pawn pawn, float healAmount, int injuriesToHeal)
    {
        if (healAmount <= 0f || injuriesToHeal <= 0 || pawn?.health?.hediffSet == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int healed = 0; healed < injuriesToHeal; healed++)
        {
            Hediff_Injury worst = null;
            float severity = 0f;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is not Hediff_Injury injury || injury.Severity <= 0f)
                {
                    continue;
                }

                if (injury.Severity > severity)
                {
                    severity = injury.Severity;
                    worst = injury;
                }
            }

            if (worst == null)
            {
                break;
            }

            worst.Heal(healAmount);
        }
    }

    private static void ReduceBloodLoss(Pawn pawn, float amount)
    {
        if (amount <= 0f || pawn?.health?.hediffSet == null)
        {
            return;
        }

        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null)
        {
            return;
        }

        bloodLoss.Severity = Math.Max(0f, bloodLoss.Severity - amount);
        if (bloodLoss.Severity <= 0.001f)
        {
            pawn.health.RemoveHediff(bloodLoss);
        }
    }

    private static void StripTransientVoidHediffs(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = hediffs.Count - 1; i >= 0; i--)
        {
            Hediff hediff = hediffs[i];
            if (hediff?.def == null)
            {
                continue;
            }

            string defName = hediff.def.defName ?? string.Empty;
            bool shouldStrip =
                defName == "VPEMYX_VoidReturnTimer" ||
                defName == "VoidTouched" ||
                defName.IndexOf("VoidTensor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hediff.TryGetComp<HediffComp_VoidReturn>() != null;
            if (!shouldStrip)
            {
                continue;
            }

            try
            {
                pawn.health.RemoveHediff(hediff);
            }
            catch
            {
            }
        }
    }
}

