using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_EntityMixedSpawner : DefModExtension
{
    public List<PawnKindDef> highPawnkinds;
    public List<PawnKindDef> lowPawnkinds;
    public List<PawnKindDef> excludedPawnkinds;
    public bool useAllEntitiesIfEmpty = true;
    public float highMinCombatPower = 301f;
    public float highMaxCombatPower = 99999f;
    public float lowMinCombatPower = 0f;
    public float lowMaxCombatPower = 300f;
    public int highCount = 1;
    public int lowCount = 2;
    public bool spawnAsCasterFaction = true;
    public bool spawnAsEntitiesFaction = false;
    public FactionDef factionDef;
}
