using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_EngulfingUnlight : DefModExtension
{
    public int darknessDurationTicks = 30000;
    public int noctolSpawnCount = 12;
    public int spawnRadius = 8;
    public string conditionDefName = "UnnaturalDarkness";
    public string noctolPawnkindDefName = "Noctol";
    public bool applyToAllFactionAllies = true;

    public HediffDef allyVoidSightHediff;
}
