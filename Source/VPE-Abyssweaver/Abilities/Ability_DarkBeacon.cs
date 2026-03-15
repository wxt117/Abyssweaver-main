using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_DarkBeacon : VEF.Abilities.Ability
{
    private AbilityExtension_DarkBeacon Config => def.GetModExtension<AbilityExtension_DarkBeacon>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        Pawn caster = CasterPawn;
        if (caster?.health?.hediffSet == null)
        {
            return;
        }

        HediffDef channelDef = Config?.channelHediff ?? MyExtensionDefOf.VPEMYX_DarkBeaconChannel;
        if (channelDef == null)
        {
            Messages.Message("VPEMYX_Message_DarkBeacon_MissingChannel".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(channelDef);
        if (existing != null)
        {
            caster.health.RemoveHediff(existing);
            Messages.Message("VPEMYX_Message_DarkBeacon_Closed".Translate(), caster, MessageTypeDefOf.NeutralEvent);
            return;
        }

        base.Cast(targets);
        caster.health.AddHediff(channelDef);
        Messages.Message("VPEMYX_Message_DarkBeacon_Opened".Translate(), caster, MessageTypeDefOf.PositiveEvent);
    }
}
