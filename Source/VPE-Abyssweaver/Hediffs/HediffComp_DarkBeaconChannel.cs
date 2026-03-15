using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class HediffCompProperties_DarkBeaconChannel : HediffCompProperties
{
    public int spawnIntervalTicks = 300;
    public float spawnRadius = 5.9f;

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

    public bool spawnAsCasterFaction = true;
    public bool spawnAsEntitiesFaction = false;
    public FactionDef factionDef;

    public int visualIntervalTicks = 20;
    public string startupFleckDefName = "VoidStructureIncomingSlow";
    public string ringFleckDefName = "DarkHighlightRing";
    public string sustainedFleckDefName = "PsychicRitual_Sustained";
    public string startupSoundDefName = "VoidStructure_Emerge";
    public string intenseLightFleckDefName = "TwistingMonolithLightsIntense";
    public string distortionRingFleckDefName = "PulsingDistortionRing";
    public string monolithRingFleckDefName = "MonolithTwistingRingSlow";
    public string incomingFleckDefName = "VoidStructureIncomingSlow";
    public string climaxSoundDefName = "VoidMonolith_ActivateL2L3";
    public int intensePulseIntervalTicks = 30;
    public int climaxPulseIntervalTicks = 120;

    public HediffCompProperties_DarkBeaconChannel()
    {
        compClass = typeof(HediffComp_DarkBeaconChannel);
    }
}

public class HediffComp_DarkBeaconChannel : HediffComp
{
    private IntVec3 anchorCell = IntVec3.Invalid;
    private bool initialized;
    private bool stopping;
    private int lastSpawnTick = -999999;
    private List<int> spawnedPawnIds = new List<int>();
    private List<PawnKindDef> cachedHighCandidates;
    private List<PawnKindDef> cachedLowCandidates;

    private HediffCompProperties_DarkBeaconChannel Props => (HediffCompProperties_DarkBeaconChannel)props;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref anchorCell, "anchorCell", IntVec3.Invalid);
        Scribe_Values.Look(ref initialized, "initialized");
        Scribe_Values.Look(ref stopping, "stopping");
        Scribe_Values.Look(ref lastSpawnTick, "lastSpawnTick", -999999);
        Scribe_Collections.Look(ref spawnedPawnIds, "spawnedPawnIds", LookMode.Value);
        spawnedPawnIds ??= new List<int>();
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        InitializeChannel();
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn caster = Pawn;
        if (caster == null || caster.Dead || caster.Map == null || caster.health?.hediffSet == null || !caster.Spawned)
        {
            return;
        }

        if (!initialized)
        {
            InitializeChannel();
        }

        if (!caster.Position.Equals(anchorCell))
        {
            StopAndClose("VPEMYX_Message_DarkBeacon_InterruptedMoved".Translate());
            return;
        }

        if (caster.Downed)
        {
            StopAndClose("VPEMYX_Message_DarkBeacon_InterruptedDowned".Translate());
            return;
        }

        int now = Find.TickManager?.TicksGame ?? 0;
        if (now - lastSpawnTick >= Props.spawnIntervalTicks)
        {
            lastSpawnTick = now;
            SpawnWave(caster);
        }

        if (caster.IsHashIntervalTick(Props.visualIntervalTicks))
        {
            PlayLoopVisuals(caster);
        }

        if (caster.IsHashIntervalTick(Props.intensePulseIntervalTicks))
        {
            PlayEndgamePulse(caster, heavy: false);
        }

        if (caster.IsHashIntervalTick(Props.climaxPulseIntervalTicks))
        {
            PlayEndgamePulse(caster, heavy: true);
        }
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
        ReturnAllSummons();
    }

    private void InitializeChannel()
    {
        Pawn caster = Pawn;
        if (caster?.Map == null)
        {
            return;
        }

        initialized = true;
        stopping = false;
        anchorCell = caster.Position;
        lastSpawnTick = (Find.TickManager?.TicksGame ?? 0) - Props.spawnIntervalTicks;
        RebuildCandidateCache();
        PlayStartupVisuals(caster);
    }

    private void RebuildCandidateCache()
    {
        cachedHighCandidates = EntitySummonUtility.ResolveCandidates(
            Props.highPawnkinds,
            Props.useAllEntitiesIfEmpty,
            Props.highMinCombatPower,
            Props.highMaxCombatPower,
            Props.excludedPawnkinds,
            allowPainSphere: true);

        cachedLowCandidates = EntitySummonUtility.ResolveCandidates(
            Props.lowPawnkinds,
            Props.useAllEntitiesIfEmpty,
            Props.lowMinCombatPower,
            Props.lowMaxCombatPower,
            Props.excludedPawnkinds,
            allowPainSphere: true);

        RemoveForbiddenConstructs(cachedHighCandidates);
        RemoveForbiddenConstructs(cachedLowCandidates);
    }

    private void SpawnWave(Pawn caster)
    {
        Map map = caster.Map;
        if (map == null)
        {
            return;
        }

        if ((cachedHighCandidates == null || cachedHighCandidates.Count == 0) &&
            (cachedLowCandidates == null || cachedLowCandidates.Count == 0))
        {
            RebuildCandidateCache();
        }

        Faction generationFaction = EntitySummonUtility.ResolveGenerationFaction(
            Props.spawnAsCasterFaction,
            Props.spawnAsEntitiesFaction,
            Props.factionDef,
            caster);

        int spawnedCount = 0;
        spawnedCount += SpawnFromPool(caster, map, cachedHighCandidates, Mathf.Max(0, Props.highCountPerWave), generationFaction);
        spawnedCount += SpawnFromPool(caster, map, cachedLowCandidates, Mathf.Max(0, Props.lowCountPerWave), generationFaction);

        if (spawnedCount <= 0)
        {
            Messages.Message("VPEMYX_Message_DarkBeacon_NoEntitiesAvailable".Translate(), caster, MessageTypeDefOf.NeutralEvent);
        }
    }

    private int SpawnFromPool(Pawn caster, Map map, List<PawnKindDef> pool, int count, Faction generationFaction)
    {
        if (count <= 0 || pool == null || pool.Count == 0)
        {
            return 0;
        }

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            PawnKindDef kind = pool.RandomElement();
            if (EntitySummonUtility.IsFleshMoldingConstruct(kind))
            {
                continue;
            }
            Pawn generated = EntitySummonUtility.GeneratePawnSafe(kind, generationFaction);
            if (generated == null)
            {
                continue;
            }

            IntVec3 spawnCell;
            try
            {
                spawnCell = CellFinder.StandableCellNear(anchorCell, map, Props.spawnRadius);
            }
            catch
            {
                continue;
            }

            if (!spawnCell.IsValid || !spawnCell.InBounds(map))
            {
                continue;
            }

            Pawn pawn;
            try
            {
                pawn = (Pawn)GenSpawn.Spawn(generated, spawnCell, map, WipeMode.Vanish);
            }
            catch
            {
                continue;
            }

            if (pawn == null)
            {
                continue;
            }

            EntitySummonUtility.PostProcessSpawnedPawn(
                pawn,
                caster,
                Props.spawnAsCasterFaction,
                map,
                spawnCell,
                // Nociosphere uses the same control/tether path as Void Command to avoid ally infighting.
                applyVoidLifetime: string.Equals(kind.defName, "Nociosphere", System.StringComparison.OrdinalIgnoreCase));

            spawnedPawnIds.Add(pawn.thingIDNumber);
            spawned++;
        }

        return spawned;
    }

    private static void RemoveForbiddenConstructs(List<PawnKindDef> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            return;
        }

        pool.RemoveAll(EntitySummonUtility.IsFleshMoldingConstruct);
    }

    private void ReturnAllSummons()
    {
        if (spawnedPawnIds == null || spawnedPawnIds.Count == 0)
        {
            return;
        }

        Pawn caster = Pawn;
        Map map = caster?.MapHeld;
        if (map == null || map.mapPawns?.AllPawnsSpawned == null)
        {
            spawnedPawnIds.Clear();
            return;
        }

        HashSet<int> idSet = new HashSet<int>(spawnedPawnIds);
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = pawns.Count - 1; i >= 0; i--)
        {
            Pawn p = pawns[i];
            if (p == null || p.Destroyed || !idSet.Contains(p.thingIDNumber))
            {
                continue;
            }

            if (p.Spawned && p.Map != null)
            {
                EntitySummonUtility.PlayVoidEffect(p.Position, p.Map);
            }

            p.Destroy(DestroyMode.Vanish);
        }

        spawnedPawnIds.Clear();
    }

    private void StopAndClose(string message)
    {
        if (stopping)
        {
            return;
        }

        stopping = true;
        Pawn caster = Pawn;
        if (!message.NullOrEmpty() && caster != null)
        {
            Messages.Message(message, caster, MessageTypeDefOf.RejectInput);
        }

        caster?.health?.RemoveHediff(parent);
    }

    private void PlayStartupVisuals(Pawn caster)
    {
        if (caster?.Map == null)
        {
            return;
        }

        SpawnFleck(caster, Props.startupFleckDefName, 2.1f);
        SpawnFleck(caster, Props.ringFleckDefName, 3.2f);
        SpawnFleck(caster, Props.sustainedFleckDefName, 1.3f);
        SpawnFleck(caster, Props.incomingFleckDefName, 10f);
        SpawnFleck(caster, Props.intenseLightFleckDefName, 1.4f);

        SoundDef sound = GetSafeStartupSound();
        sound?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
    }

    private void PlayLoopVisuals(Pawn caster)
    {
        if (caster?.Map == null)
        {
            return;
        }

        SpawnFleck(caster, Props.sustainedFleckDefName, 1.0f);
        if (caster.IsHashIntervalTick(45))
        {
            SpawnFleck(caster, Props.ringFleckDefName, 2.8f);
        }
    }

    private void PlayEndgamePulse(Pawn caster, bool heavy)
    {
        if (caster?.Map == null)
        {
            return;
        }

        SpawnFleck(caster, Props.intenseLightFleckDefName, heavy ? 1.7f : 1.15f);
        SpawnFleck(caster, Props.monolithRingFleckDefName, heavy ? 1.3f : 0.85f);
        SpawnFleck(caster, Props.distortionRingFleckDefName, heavy ? 1.8f : 1.1f);
        SpawnFleck(caster, Props.incomingFleckDefName, heavy ? 12f : 7f);
        SpawnFleck(caster, Props.ringFleckDefName, heavy ? 3.6f : 2.5f);

        if (heavy)
        {
            SoundDef climax = DefDatabase<SoundDef>.GetNamedSilentFail(Props.climaxSoundDefName) ??
                              DefDatabase<SoundDef>.GetNamedSilentFail("VoidStructure_Activate") ??
                              DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal");
            climax?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
        }
    }

    private static void SpawnFleck(Pawn caster, string fleckDefName, float scale)
    {
        if (caster?.Map == null || string.IsNullOrEmpty(fleckDefName))
        {
            return;
        }

        FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail(fleckDefName) ??
                         DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (fleck != null)
        {
            FleckMaker.Static(caster.Position, caster.Map, fleck, scale);
        }
    }

    private SoundDef GetSafeStartupSound()
    {
        // Avoid sustainers here: PlayOneShot on sustainers throws runtime errors.
        SoundDef chosen = DefDatabase<SoundDef>.GetNamedSilentFail(Props.startupSoundDefName);
        if (chosen != null && !string.Equals(chosen.defName, "VoidStructure_Emerging"))
        {
            return chosen;
        }

        return DefDatabase<SoundDef>.GetNamedSilentFail("VoidStructure_Emerge") ??
               DefDatabase<SoundDef>.GetNamedSilentFail("VoidStructure_Activate") ??
               DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
    }
}
