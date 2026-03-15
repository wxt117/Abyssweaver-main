using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class HediffCompProperties_VoidReturn : HediffCompProperties
{
    public int durationTicks = 5400;
    public FleckDef fleckDef;
    public SoundDef soundDef;

    public HediffCompProperties_VoidReturn()
    {
        compClass = typeof(HediffComp_VoidReturn);
    }
}

public class HediffComp_VoidReturn : HediffComp
{
    private HediffCompProperties_VoidReturn Props => (HediffCompProperties_VoidReturn)props;
    private int lastDreadmeldPropagationTick = -99999;
    private int ownerFactionLoadId = -1;
    private Faction ownerFaction;

    private const float DreadmeldPropagationRadius = 40f;
    private const float DreadmeldDeathCatchRadius = 14f;

    public void SetOwnerFaction(Faction faction)
    {
        ownerFaction = faction;
        ownerFactionLoadId = faction?.loadID ?? -1;
    }

    public Faction GetOwnerFaction()
    {
        if (ownerFaction != null)
        {
            return ownerFaction;
        }

        if (ownerFactionLoadId < 0 || Find.FactionManager == null)
        {
            return null;
        }

        List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
        for (int i = 0; i < factions.Count; i++)
        {
            Faction faction = factions[i];
            if (faction != null && faction.loadID == ownerFactionLoadId)
            {
                ownerFaction = faction;
                return ownerFaction;
            }
        }

        return null;
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref ownerFactionLoadId, "ownerFactionLoadId", -1);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || parent == null)
        {
            return;
        }

        if (EntitySummonUtility.IsFleshMoldingConstruct(pawn))
        {
            pawn.health?.RemoveHediff(parent);
            return;
        }

        TryEnforceOwnerFaction(pawn);
        TryPropagateFromDreadmeld(pawn);

        if (pawn.Dead)
        {
            return;
        }

        if (parent.ageTicks < Props.durationTicks)
        {
            return;
        }

        if (pawn.Spawned && pawn.Map != null)
        {
            if (Props.fleckDef != null || Props.soundDef != null)
            {
                if (Props.fleckDef != null)
                {
                    FleckMaker.Static(pawn.Position, pawn.Map, Props.fleckDef, 1.5f);
                }

                Props.soundDef?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
            else
            {
                EntitySummonUtility.PlayVoidEffect(pawn.Position, pawn.Map);
            }
        }

        pawn.Destroy(DestroyMode.Vanish);
    }

    public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
    {
        base.Notify_PawnDied(dinfo, culprit);

        Pawn pawn = Pawn;
        if (pawn?.kindDef?.defName != "Dreadmeld")
        {
            return;
        }

        Map map = pawn.MapHeld;
        if (map == null)
        {
            return;
        }

        TryApplyVoidTimerAround(map, pawn.PositionHeld, DreadmeldDeathCatchRadius);
    }

    private void TryPropagateFromDreadmeld(Pawn pawn)
    {
        if (pawn.kindDef?.defName != "Dreadmeld" || pawn.Map == null || !pawn.Spawned)
        {
            return;
        }

        int now = Find.TickManager.TicksGame;
        if (now - lastDreadmeldPropagationTick < 120)
        {
            return;
        }

        lastDreadmeldPropagationTick = now;
        TryApplyVoidTimerAround(pawn.Map, pawn.Position, DreadmeldPropagationRadius);
    }

    private void TryApplyVoidTimerAround(Map map, IntVec3 center, float radius)
    {
        HediffDef timerDef = GetTimerDef();
        if (map == null || timerDef == null)
        {
            return;
        }

        IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
        if (pawns == null || pawns.Count == 0)
        {
            return;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn other = pawns[i];
            if (other == null || other.Dead || !other.Spawned)
            {
                continue;
            }

            if (!IsEntityLike(other))
            {
                continue;
            }

            if (EntitySummonUtility.IsFleshMoldingConstruct(other))
            {
                continue;
            }

            if (other.health?.hediffSet == null || other.health.hediffSet.HasHediff(timerDef))
            {
                continue;
            }

            if (other.Position.DistanceTo(center) > radius)
            {
                continue;
            }

            other.health.AddHediff(timerDef);
        }
    }

    private static HediffDef GetTimerDef()
    {
        return MyExtensionDefOf.VPEMYX_VoidReturnTimer ??
               DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_VoidReturnTimer");
    }

    private static bool IsEntityLike(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        if (pawn.kindDef?.defaultFactionDef == FactionDefOf.Entities || pawn.kindDef?.defaultFactionDef?.defName == "Entities")
        {
            return true;
        }

        return pawn.RaceProps?.IsAnomalyEntity ?? false;
    }

    private void TryEnforceOwnerFaction(Pawn pawn)
    {
        Faction owner = GetOwnerFaction();
        if (pawn == null || owner == null || pawn.Faction == owner)
        {
            return;
        }

        try
        {
            pawn.SetFaction(owner);
        }
        catch
        {
        }
    }
}
