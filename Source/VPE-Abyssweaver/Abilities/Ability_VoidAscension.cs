using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class Ability_VoidAscension : VEF.Abilities.Ability
{
    private AbilityExtension_VoidAscension Config => def.GetModExtension<AbilityExtension_VoidAscension>();

    public override bool ShowGizmoOnPawn()
    {
        return pawn != null && !pawn.Drafted;
    }

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        Pawn caster = CasterPawn;
        if (caster?.Drafted == true)
        {
            Messages.Message("VPEMYX_Message_VoidAscension_UndraftedOnly".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        base.Cast(targets);

        caster = CasterPawn;
        if (caster == null || caster.RaceProps == null || !caster.RaceProps.Humanlike || caster.health?.hediffSet == null)
        {
            return;
        }

        HediffDef avatarDef = Config?.avatarHediff ?? MyExtensionDefOf.VPEMYX_VoidAvatar;
        HediffDef cocoonDef = Config?.cocoonHediff ?? MyExtensionDefOf.VPEMYX_VoidAscensionCocoon;
        if (avatarDef == null || cocoonDef == null)
        {
            return;
        }

        if (caster.health.hediffSet.HasHediff(avatarDef))
        {
            Messages.Message("VPEMYX_Message_VoidAscension_AlreadyCompleted".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        if (caster.health.hediffSet.HasHediff(cocoonDef))
        {
            Messages.Message("VPEMYX_Message_VoidAscension_AlreadyInProgress".Translate(), caster, MessageTypeDefOf.RejectInput);
            return;
        }

        Hediff cocoon = caster.health.AddHediff(cocoonDef);
        HediffComp_VoidAscensionCocoon cocoonComp = cocoon?.TryGetComp<HediffComp_VoidAscensionCocoon>();
        if (cocoonComp != null && Config != null)
        {
            cocoonComp.OverrideDuration(Config.comaDurationTicks);
        }

        if (Config?.triggerEntityAssaultOnCast ?? true)
        {
            bool triggered = VoidAscensionIncidentUtility.TryTriggerEntityAssault(
                caster.Map,
                Config?.fixedAssaultPoints ?? 3000f);
            if (!triggered)
            {
                Messages.Message("VPEMYX_Message_VoidAscension_NoAssault".Translate(), caster, MessageTypeDefOf.NeutralEvent);
            }
        }

        FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (fleck != null && caster.Map != null)
        {
            FleckMaker.Static(caster.Position, caster.Map, fleck, 2.4f);
        }

        SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal") ??
                         DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        if (caster.Map != null)
        {
            sound?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
        }

        Messages.Message("VPEMYX_Message_VoidAscension_Began".Translate(), caster, MessageTypeDefOf.PositiveEvent);
    }
}
