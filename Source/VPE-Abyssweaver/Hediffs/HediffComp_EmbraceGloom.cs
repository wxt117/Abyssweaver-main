using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class HediffCompProperties_EmbraceGloom : HediffCompProperties
{
    public int checkIntervalTicks = 60;
    public string darknessConditionDefName = "UnnaturalDarkness";
    public float psyfocusRecoveryPerDayInDarkness = 0.4f;

    public HediffCompProperties_EmbraceGloom()
    {
        compClass = typeof(HediffComp_EmbraceGloom);
    }
}

public class HediffComp_EmbraceGloom : HediffComp
{
    private HediffCompProperties_EmbraceGloom Props => (HediffCompProperties_EmbraceGloom)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.Map == null || !pawn.IsHashIntervalTick(Props.checkIntervalTicks))
        {
            return;
        }

        if (!IsUnnaturalDarknessActive(pawn.Map))
        {
            return;
        }

        Pawn_PsychicEntropyTracker entropy = pawn.psychicEntropy;
        if (entropy == null || !entropy.NeedsPsyfocus)
        {
            return;
        }

        float offset = Props.psyfocusRecoveryPerDayInDarkness * Props.checkIntervalTicks / 60000f;
        if (offset <= 0f || entropy.CurrentPsyfocus >= 1f)
        {
            return;
        }

        entropy.OffsetPsyfocusDirectly(offset);
    }

    private bool IsUnnaturalDarknessActive(Map map)
    {
        if (map?.gameConditionManager == null)
        {
            return false;
        }

        GameConditionDef condition = DefDatabase<GameConditionDef>.GetNamedSilentFail(Props.darknessConditionDefName);
        return condition != null && map.gameConditionManager.ConditionIsActive(condition);
    }
}
