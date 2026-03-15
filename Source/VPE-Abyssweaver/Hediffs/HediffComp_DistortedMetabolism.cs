using System;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class HediffCompProperties_DistortedMetabolism : HediffCompProperties
{
    public int checkIntervalTicks = 60;
    public float entropyCostPerPulse = 0.02f;
    public float injuryHealAmount = 2f;
    public float permanentInjuryHealAmount = 0.3f;
    public float bloodLossReduction = 0.03f;

    public HediffCompProperties_DistortedMetabolism()
    {
        compClass = typeof(HediffComp_DistortedMetabolism);
    }
}

public class HediffComp_DistortedMetabolism : HediffComp
{
    private HediffCompProperties_DistortedMetabolism Props => (HediffCompProperties_DistortedMetabolism)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.IsHashIntervalTick(Props.checkIntervalTicks) == false)
        {
            return;
        }

        Pawn_PsychicEntropyTracker entropy = pawn.psychicEntropy;
        if (entropy == null || pawn.health?.hediffSet == null)
        {
            return;
        }

        bool hasInjury = false;
        for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
        {
            if (pawn.health.hediffSet.hediffs[i] is Hediff_Injury injury && injury.Severity > 0.01f)
            {
                hasInjury = true;
                break;
            }
        }

        if (!hasInjury || entropy.WouldOverflowEntropy(Props.entropyCostPerPulse))
        {
            return;
        }

        if (!entropy.TryAddEntropy(Props.entropyCostPerPulse, pawn, scale: false, overLimit: false))
        {
            return;
        }

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            if (pawn.health.hediffSet.hediffs[i] is not Hediff_Injury injury || injury.Severity <= 0f)
            {
                continue;
            }

            float healAmount = injury.IsPermanent() ? Props.permanentInjuryHealAmount : Props.injuryHealAmount;
            if (healAmount > 0f)
            {
                injury.Heal(healAmount);
            }
        }

        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss != null && Props.bloodLossReduction > 0f)
        {
            bloodLoss.Severity = Math.Max(0f, bloodLoss.Severity - Props.bloodLossReduction);
            if (bloodLoss.Severity <= 0.0001f)
            {
                pawn.health.RemoveHediff(bloodLoss);
            }
        }
    }
}
