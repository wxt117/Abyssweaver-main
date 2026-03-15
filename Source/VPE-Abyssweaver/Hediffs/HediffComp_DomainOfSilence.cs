using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class HediffCompProperties_DomainOfSilenceChannel : HediffCompProperties
{
    public float radius = 30f;
    public int pulseIntervalTicks = 90;
    public int entropyIntervalTicks = 60;
    public float entropyBasePerSecond = 5f;
    public float entropyRampPerSecond = 1.2f;
    public float entropyStopPercent = 0.8f;
    public float brainDamagePerPulse = 0.15f;
    public float brainSeverityPerPulse = 0.006f;
    public string brainWitherHediffDefName = "VPEMYX_BrainWithering";

    public int visualIntervalTicks = 12;
    public float visualReferenceRadius = 30f;
    public string sustainedFleckDefName = "PsychicRitual_Sustained";
    public string ringFleckDefName = "DarkHighlightRing";
    public string distortionFleckDefName = "PsychicDistortionRingContractingQuick";
    public string startupSoundDefName = "VoidTerrorCast";

    public HediffCompProperties_DomainOfSilenceChannel()
    {
        compClass = typeof(HediffComp_DomainOfSilenceChannel);
    }
}

public class HediffComp_DomainOfSilenceChannel : HediffComp
{
    private IntVec3 anchorCell = IntVec3.Invalid;
    private int startedTick = -1;
    private bool initialized;
    private bool stopping;

    private HediffCompProperties_DomainOfSilenceChannel Props => (HediffCompProperties_DomainOfSilenceChannel)props;

    public float Radius => Props.radius;
    public IntVec3 AnchorCell => anchorCell;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref anchorCell, "anchorCell", IntVec3.Invalid);
        Scribe_Values.Look(ref startedTick, "startedTick", -1);
        Scribe_Values.Look(ref initialized, "initialized");
        Scribe_Values.Look(ref stopping, "stopping");
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        InitializeChannel();
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.Map == null || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!initialized)
        {
            InitializeChannel();
        }

        if (!pawn.Spawned)
        {
            return;
        }

        if (!pawn.Position.Equals(anchorCell))
        {
            StopChannel("VPEMYX_Message_DomainOfSilence_MovedCollapse".Translate(), MessageTypeDefOf.RejectInput);
            return;
        }

        if (pawn.Downed)
        {
            StopChannel("VPEMYX_Message_DomainOfSilence_DownedInterrupted".Translate(), MessageTypeDefOf.RejectInput);
            return;
        }

        if (pawn.IsHashIntervalTick(Props.entropyIntervalTicks) && !TryConsumeEntropy())
        {
            return;
        }

        if (pawn.IsHashIntervalTick(Props.pulseIntervalTicks))
        {
            ApplyBrainWitheringPulse();
        }

        if (pawn.IsHashIntervalTick(Props.visualIntervalTicks))
        {
            PlayLoopVisuals();
        }
    }

    private void InitializeChannel()
    {
        Pawn pawn = Pawn;
        if (pawn == null || pawn.Map == null)
        {
            return;
        }

        initialized = true;
        anchorCell = pawn.Position;
        startedTick = Find.TickManager?.TicksGame ?? 0;
        stopping = false;

        PlayStartupVisuals();
        DomainOfSilenceDomainUtility.InvalidateMap(pawn.Map);
    }

    private bool TryConsumeEntropy()
    {
        Pawn pawn = Pawn;
        Pawn_PsychicEntropyTracker entropy = pawn?.psychicEntropy;
        if (pawn == null || entropy == null)
        {
            StopChannel("VPEMYX_Message_DomainOfSilence_NoEntropyCircuit".Translate(), MessageTypeDefOf.RejectInput);
            return false;
        }

        float relative = GetEntropyRelative(entropy);
        if (relative >= Props.entropyStopPercent)
        {
            StopChannel("VPEMYX_Message_DomainOfSilence_EntropyOver80".Translate(), MessageTypeDefOf.NegativeEvent);
            return false;
        }

        int nowTick = Find.TickManager?.TicksGame ?? startedTick;
        float elapsedSeconds = Mathf.Max(0f, (nowTick - startedTick) / 60f);
        float cost = Props.entropyBasePerSecond + elapsedSeconds * Props.entropyRampPerSecond;

        if (entropy.WouldOverflowEntropy(cost) || !entropy.TryAddEntropy(cost, pawn, scale: false, overLimit: false))
        {
            StopChannel("VPEMYX_Message_DomainOfSilence_EntropyOverload".Translate(), MessageTypeDefOf.NegativeEvent);
            return false;
        }

        return true;
    }

    private void ApplyBrainWitheringPulse()
    {
        Pawn caster = Pawn;
        Map map = caster?.Map;
        if (caster == null || map?.mapPawns?.AllPawnsSpawned == null)
        {
            return;
        }

        HediffDef witherDef = MyExtensionDefOf.VPEMYX_BrainWithering ??
                              DefDatabase<HediffDef>.GetNamedSilentFail(Props.brainWitherHediffDefName);

        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn target = pawns[i];
            if (target == null || target == caster || target.Dead || target.health?.hediffSet == null)
            {
                continue;
            }

            if (!target.Position.InHorDistOf(anchorCell, Props.radius) || !target.HostileTo(caster))
            {
                continue;
            }

            if (witherDef != null)
            {
                Hediff wither = target.health.hediffSet.GetFirstHediffOfDef(witherDef);
                if (wither == null)
                {
                    target.health.AddHediff(witherDef);
                    wither = target.health.hediffSet.GetFirstHediffOfDef(witherDef);
                }

                if (wither != null)
                {
                    float maxSeverity = wither.def?.maxSeverity > 0f ? wither.def.maxSeverity : 1f;
                    wither.Severity = Math.Min(maxSeverity, wither.Severity + Props.brainSeverityPerPulse);
                }
            }

            ApplyBrainDamage(target, caster);
        }
    }

    private void ApplyBrainDamage(Pawn target, Pawn caster)
    {
        BodyPartRecord part = target.health?.hediffSet?.GetBrain() ?? target.RaceProps?.body?.corePart;
        if (part == null)
        {
            return;
        }

        float damageAmount = Math.Max(0.01f, Props.brainDamagePerPulse);
        DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, damageAmount, 999f, -1f, caster, part);
        target.TakeDamage(dinfo);
    }

    private void PlayStartupVisuals()
    {
        Pawn pawn = Pawn;
        if (pawn?.Map == null)
        {
            return;
        }

        float ringScale = GetVisualRingScale();
        SpawnFleck(Props.ringFleckDefName, ringScale);
        SpawnFleck(Props.distortionFleckDefName, ringScale * 0.8f);
        SpawnFleck(Props.sustainedFleckDefName, 1.7f);

        SoundDef startSound = DefDatabase<SoundDef>.GetNamedSilentFail(Props.startupSoundDefName) ??
                              DefDatabase<SoundDef>.GetNamedSilentFail("PsychicRitual_Complete") ??
                              DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal");
        startSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
    }

    private void PlayLoopVisuals()
    {
        Pawn pawn = Pawn;
        if (pawn?.Map == null)
        {
            return;
        }

        float ringScale = GetVisualRingScale();
        SpawnFleck(Props.sustainedFleckDefName, 1.05f);

        if (pawn.IsHashIntervalTick(24))
        {
            SpawnFleck(Props.ringFleckDefName, ringScale);
        }

        if (pawn.IsHashIntervalTick(36))
        {
            SpawnFleck(Props.distortionFleckDefName, ringScale * 0.85f);
        }
    }

    private void SpawnFleck(string fleckName, float scale)
    {
        Pawn pawn = Pawn;
        if (pawn?.Map == null || string.IsNullOrEmpty(fleckName))
        {
            return;
        }

        FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail(fleckName) ??
                         DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (fleck != null)
        {
            FleckMaker.Static(pawn.Position, pawn.Map, fleck, scale);
        }
    }

    private float GetVisualRingScale()
    {
        float refRadius = Props.visualReferenceRadius > 0.1f ? Props.visualReferenceRadius : Props.radius;
        return Mathf.Clamp(refRadius / 7f, 1.8f, 8f);
    }

    private void StopChannel(string message, MessageTypeDef messageType)
    {
        if (stopping)
        {
            return;
        }

        stopping = true;
        Pawn pawn = Pawn;
        if (pawn?.health?.hediffSet == null)
        {
            return;
        }

        if (!message.NullOrEmpty())
        {
            Messages.Message(message, pawn, messageType);
        }

        pawn.health.RemoveHediff(parent);
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
        DomainOfSilenceDomainUtility.InvalidateMap(Pawn?.Map);
    }

    private static float GetEntropyRelative(Pawn_PsychicEntropyTracker entropy)
    {
        if (entropy == null)
        {
            return 0f;
        }

        try
        {
            PropertyInfo relativeProp = entropy.GetType().GetProperty("EntropyRelativeValue", BindingFlags.Instance | BindingFlags.Public);
            if (relativeProp?.GetValue(entropy) is float relative)
            {
                return relative;
            }

            PropertyInfo valueProp = entropy.GetType().GetProperty("EntropyValue", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo maxProp = entropy.GetType().GetProperty("MaxEntropy", BindingFlags.Instance | BindingFlags.Public);
            if (valueProp?.GetValue(entropy) is float value &&
                maxProp?.GetValue(entropy) is float max &&
                max > 0.001f)
            {
                return value / max;
            }
        }
        catch
        {
        }

        return 0f;
    }
}

public static class DomainOfSilenceDomainUtility
{
    private const int CacheUpdateIntervalTicks = 15;
    private static readonly Dictionary<int, DomainCache> CacheByMapId = new Dictionary<int, DomainCache>();

    private sealed class DomainCache
    {
        public int lastBuiltTick = -1;
        public readonly List<DomainState> states = new List<DomainState>();
    }

    private readonly struct DomainState
    {
        public readonly IntVec3 center;
        public readonly float radius;

        public DomainState(IntVec3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    public static bool IsThingInsideAnyDomain(Thing thing)
    {
        if (thing?.Map == null || !thing.Spawned)
        {
            return false;
        }

        return IsCellInsideAnyDomain(thing.Map, thing.Position);
    }

    public static bool IsCellInsideAnyDomain(Map map, IntVec3 cell)
    {
        if (map == null)
        {
            return false;
        }

        DomainCache cache = GetOrBuildCache(map);
        for (int i = 0; i < cache.states.Count; i++)
        {
            DomainState state = cache.states[i];
            if (cell.InHorDistOf(state.center, state.radius))
            {
                return true;
            }
        }

        return false;
    }

    public static void InvalidateMap(Map map)
    {
        if (map == null)
        {
            return;
        }

        if (CacheByMapId.TryGetValue(map.uniqueID, out DomainCache cache))
        {
            cache.lastBuiltTick = -1;
        }
    }

    private static DomainCache GetOrBuildCache(Map map)
    {
        int mapId = map.uniqueID;
        if (!CacheByMapId.TryGetValue(mapId, out DomainCache cache))
        {
            cache = new DomainCache();
            CacheByMapId[mapId] = cache;
        }

        int nowTick = Find.TickManager?.TicksGame ?? 0;
        if (cache.lastBuiltTick >= 0 && nowTick - cache.lastBuiltTick < CacheUpdateIntervalTicks)
        {
            return cache;
        }

        cache.states.Clear();
        IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
        if (pawns != null)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.health?.hediffSet == null)
                {
                    continue;
                }

                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MyExtensionDefOf.VPEMYX_DomainOfSilenceChannel);
                HediffComp_DomainOfSilenceChannel comp = hediff?.TryGetComp<HediffComp_DomainOfSilenceChannel>();
                if (comp == null)
                {
                    continue;
                }

                cache.states.Add(new DomainState(comp.AnchorCell.IsValid ? comp.AnchorCell : pawn.Position, comp.Radius));
            }
        }

        cache.lastBuiltTick = nowTick;
        return cache;
    }
}
