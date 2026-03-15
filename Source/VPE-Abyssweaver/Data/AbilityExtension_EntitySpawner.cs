using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_EntitySpawner : DefModExtension
{
    public List<PawnKindDef> pawnkinds;
    public List<PawnKindDef> excludedPawnkinds;
    public int spawnCount = 1;
    public bool useAllEntitiesIfEmpty = true;
    public float minCombatPower = 0f;
    public float maxCombatPower = 99999f;
    public bool spawnAsCasterFaction = true;
    public bool spawnAsEntitiesFaction = true;
    public FactionDef factionDef;
}
