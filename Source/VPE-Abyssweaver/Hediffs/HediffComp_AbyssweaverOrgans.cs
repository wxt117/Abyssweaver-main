using System;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class HediffCompProperties_AbyssweaverBloodCoagulation : HediffCompProperties
{
    public int checkIntervalTicks = 60;
    public float bloodLossReductionPerPulse = 0.007f;

    public HediffCompProperties_AbyssweaverBloodCoagulation()
    {
        compClass = typeof(HediffComp_AbyssweaverBloodCoagulation);
    }
}

public class HediffComp_AbyssweaverBloodCoagulation : HediffComp
{
    private HediffCompProperties_AbyssweaverBloodCoagulation Props =>
        (HediffCompProperties_AbyssweaverBloodCoagulation)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(30, Props.checkIntervalTicks)))
        {
            return;
        }

        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null || Props.bloodLossReductionPerPulse <= 0f)
        {
            return;
        }

        bloodLoss.Severity = Math.Max(0f, bloodLoss.Severity - Props.bloodLossReductionPerPulse);
        if (bloodLoss.Severity <= 0.0001f)
        {
            pawn.health.RemoveHediff(bloodLoss);
        }
    }
}

public class HediffCompProperties_AbyssweaverNoseFilter : HediffCompProperties
{
    public int checkIntervalTicks = 180;
    public string rotStinkExposureDefName = "RotStinkExposure";
    public string[] corpseThoughtDefs =
    {
        "ObservedLayingCorpse",
        "ObservedLayingRottingCorpse",
        "RotStinkLingering"
    };

    public HediffCompProperties_AbyssweaverNoseFilter()
    {
        compClass = typeof(HediffComp_AbyssweaverNoseFilter);
    }
}

public class HediffComp_AbyssweaverNoseFilter : HediffComp
{
    private HediffCompProperties_AbyssweaverNoseFilter Props =>
        (HediffCompProperties_AbyssweaverNoseFilter)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(60, Props.checkIntervalTicks)))
        {
            return;
        }

        if (!string.IsNullOrEmpty(Props.rotStinkExposureDefName))
        {
            HediffDef stinkDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.rotStinkExposureDefName);
            if (stinkDef != null)
            {
                RemoveAllHediffsOfDef(pawn, stinkDef);
            }
        }

        if (pawn.needs?.mood?.thoughts?.memories == null || Props.corpseThoughtDefs == null)
        {
            return;
        }

        for (int i = 0; i < Props.corpseThoughtDefs.Length; i++)
        {
            string thoughtName = Props.corpseThoughtDefs[i];
            if (string.IsNullOrEmpty(thoughtName))
            {
                continue;
            }

            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
            if (thought != null)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thought);
            }
        }
    }

    private static void RemoveAllHediffsOfDef(Pawn pawn, HediffDef def)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
        {
            return;
        }

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            Hediff h = pawn.health.hediffSet.hediffs[i];
            if (h != null && h.def == def)
            {
                pawn.health.RemoveHediff(h);
            }
        }
    }
}

public class HediffCompProperties_AbyssweaverStomachFilter : HediffCompProperties
{
    public int checkIntervalTicks = 60;
    public string[] blockedHediffDefs = { "FoodPoisoning" };
    public string[] blockedThoughtDefs = { "AteRawFood", "AteCorpse" };

    public HediffCompProperties_AbyssweaverStomachFilter()
    {
        compClass = typeof(HediffComp_AbyssweaverStomachFilter);
    }
}

public class HediffComp_AbyssweaverStomachFilter : HediffComp
{
    private HediffCompProperties_AbyssweaverStomachFilter Props =>
        (HediffCompProperties_AbyssweaverStomachFilter)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(30, Props.checkIntervalTicks)))
        {
            return;
        }

        if (Props.blockedHediffDefs != null)
        {
            for (int i = 0; i < Props.blockedHediffDefs.Length; i++)
            {
                string defName = Props.blockedHediffDefs[i];
                if (string.IsNullOrEmpty(defName))
                {
                    continue;
                }

                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                if (def != null)
                {
                    RemoveAllHediffsOfDef(pawn, def);
                }
            }
        }

        if (pawn.needs?.mood?.thoughts?.memories == null || Props.blockedThoughtDefs == null)
        {
            return;
        }

        for (int i = 0; i < Props.blockedThoughtDefs.Length; i++)
        {
            string thoughtName = Props.blockedThoughtDefs[i];
            if (string.IsNullOrEmpty(thoughtName))
            {
                continue;
            }

            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
            if (thought != null)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thought);
            }
        }
    }

    private static void RemoveAllHediffsOfDef(Pawn pawn, HediffDef def)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
        {
            return;
        }

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            Hediff h = pawn.health.hediffSet.hediffs[i];
            if (h != null && h.def == def)
            {
                pawn.health.RemoveHediff(h);
            }
        }
    }
}

