using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class HediffCompProperties_VoidSight : HediffCompProperties
{
    public int checkIntervalTicks = 30;
    public string darknessExposureDefName = "DarknessExposure";

    public HediffCompProperties_VoidSight()
    {
        compClass = typeof(HediffComp_VoidSight);
    }
}

public class HediffComp_VoidSight : HediffComp
{
    private HediffCompProperties_VoidSight Props => (HediffCompProperties_VoidSight)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null || !pawn.IsHashIntervalTick(Props.checkIntervalTicks))
        {
            return;
        }

        HediffDef darknessExposure = DefDatabase<HediffDef>.GetNamedSilentFail(Props.darknessExposureDefName);
        if (darknessExposure == null)
        {
            return;
        }

        Hediff exposure = pawn.health.hediffSet.GetFirstHediffOfDef(darknessExposure);
        if (exposure != null)
        {
            pawn.health.RemoveHediff(exposure);
        }
    }
}
