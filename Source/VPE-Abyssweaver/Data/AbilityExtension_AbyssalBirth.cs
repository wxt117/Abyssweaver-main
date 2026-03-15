using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_AbyssalBirth : DefModExtension
{
    public int requiredSacrifices = 7;
    public PawnKindDef sacrificePawnkind;
    public PawnKindDef resultPawnkind;
    public float spawnRadius = 4f;
}
