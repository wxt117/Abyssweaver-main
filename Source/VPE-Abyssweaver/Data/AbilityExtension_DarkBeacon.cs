using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_DarkBeacon : DefModExtension
{
    public HediffDef channelHediff;

    public List<PawnKindDef> highPawnkinds;
    public List<PawnKindDef> lowPawnkinds;
    public List<PawnKindDef> excludedPawnkinds;

    public bool useAllEntitiesIfEmpty = true;
    public float highMinCombatPower = 301f;
    public float highMaxCombatPower = 99999f;
    public float lowMinCombatPower = 0f;
    public float lowMaxCombatPower = 300f;
    public int highCountPerWave = 1;
    public int lowCountPerWave = 4;
    public int spawnIntervalTicks = 300;
    public float spawnRadius = 5.9f;

    public bool spawnAsCasterFaction = true;
    public bool spawnAsEntitiesFaction = false;
    public FactionDef factionDef;
}
