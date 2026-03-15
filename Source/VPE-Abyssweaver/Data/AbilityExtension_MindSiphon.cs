using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_MindSiphon : DefModExtension
{
    public float passionStealChance = 0.10f;
    public float sensitivityGainPerPsylink = 1f;
    public float sensitivityMaxSeverity = 10f;

    public HediffDef mindDrainedHediff;
    public HediffDef sensitivityHediff;
}
