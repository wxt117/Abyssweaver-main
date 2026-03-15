using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_FleshMolding : DefModExtension
{
    public float radius = 8f;
    public int tier1Required = 5;
    public int tier2Required = 15;
    public int tier3Required = 30;
    public PawnKindDef tier1Pawnkind;
    public PawnKindDef tier2Pawnkind;
    public PawnKindDef tier3Pawnkind;
}
