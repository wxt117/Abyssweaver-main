using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_DomainOfSilence : VEF.Abilities.Ability
{
    private AbilityExtension_DomainOfSilence Config => def.GetModExtension<AbilityExtension_DomainOfSilence>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        Pawn caster = CasterPawn;
        if (caster?.health?.hediffSet == null)
        {
            return;
        }

        HediffDef channelDef = Config?.channelHediff ?? MyExtensionDefOf.VPEMYX_DomainOfSilenceChannel;
        if (channelDef == null)
        {
            Messages.Message("VPEMYX_Message_DomainOfSilence_MissingChannel".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(channelDef);
        if (existing != null)
        {
            caster.health.RemoveHediff(existing);
            Messages.Message("VPEMYX_Message_DomainOfSilence_Cancelled".Translate(), caster, MessageTypeDefOf.NeutralEvent);
            return;
        }

        base.Cast(targets);
        caster.health.AddHediff(channelDef);
        Messages.Message("VPEMYX_Message_DomainOfSilence_Started".Translate(), caster, MessageTypeDefOf.PositiveEvent);
    }
}
