using System.Collections.Generic;
using Verse;

namespace VPE_MyExtension;

public class HediffCompProperties_AbyssalDamageTransferLink : HediffCompProperties
{
    public HediffCompProperties_AbyssalDamageTransferLink()
    {
        compClass = typeof(HediffComp_AbyssalDamageTransferLink);
    }
}

public class HediffComp_AbyssalDamageTransferLink : HediffComp
{
    private int linkedAbyssalId = -1;

    public static void EnsureLink(Pawn owner, Pawn abyssal)
    {
        if (owner?.health?.hediffSet == null || abyssal == null)
        {
            return;
        }

        HediffDef linkDef = MyExtensionDefOf.VPEMYX_AbyssalDamageTransferLink ??
                            DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_AbyssalDamageTransferLink");
        if (linkDef == null)
        {
            return;
        }

        Hediff link = owner.health.hediffSet.GetFirstHediffOfDef(linkDef);
        if (link == null)
        {
            owner.health.AddHediff(linkDef);
            link = owner.health.hediffSet.GetFirstHediffOfDef(linkDef);
        }

        HediffComp_AbyssalDamageTransferLink comp = link?.TryGetComp<HediffComp_AbyssalDamageTransferLink>();
        comp?.SetLinkedAbyssal(abyssal);
    }

    public void SetLinkedAbyssal(Pawn abyssal)
    {
        linkedAbyssalId = abyssal?.thingIDNumber ?? -1;
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref linkedAbyssalId, "linkedAbyssalId", -1);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn owner = Pawn;
        if (owner == null || owner.Dead || owner.health?.hediffSet == null)
        {
            return;
        }

        if (!owner.IsHashIntervalTick(120))
        {
            return;
        }

        if (!TryResolveLinkedAbyssal(owner, out Pawn linked))
        {
            owner.health.RemoveHediff(parent);
            return;
        }

        if (linked == null || linked.Dead || !linked.Spawned || linked.Map != owner.Map)
        {
            owner.health.RemoveHediff(parent);
        }
    }

    public bool TryGetLinkedAbyssal(out Pawn abyssal)
    {
        return TryResolveLinkedAbyssal(Pawn, out abyssal);
    }

    private bool TryResolveLinkedAbyssal(Pawn owner, out Pawn abyssal)
    {
        abyssal = null;
        if (owner == null || linkedAbyssalId < 0 || Current.Game == null)
        {
            return false;
        }

        List<Map> maps = Current.Game.Maps;
        if (maps == null)
        {
            return false;
        }

        for (int m = 0; m < maps.Count; m++)
        {
            IReadOnlyList<Pawn> pawns = maps[m]?.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                continue;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate != null && candidate.thingIDNumber == linkedAbyssalId)
                {
                    abyssal = candidate;
                    return true;
                }
            }
        }

        return false;
    }
}
