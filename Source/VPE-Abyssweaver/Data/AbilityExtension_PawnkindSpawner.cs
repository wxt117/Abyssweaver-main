using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_PawnkindSpawner : DefModExtension
{
    public PawnKindDef pawnkind;
    public int spawnCount = 1;
    public bool spawnAsAlly = true;
    public List<HediffDef> hediffs;
}
