using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_VoidAscension : DefModExtension
{
    public int comaDurationTicks = 2500;
    public HediffDef cocoonHediff;
    public HediffDef avatarHediff;
    public bool triggerEntityAssaultOnCast = true;
    public float fixedAssaultPoints = 3000f;
}
