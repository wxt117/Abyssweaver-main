using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_ActivateEmbraceGloom : VEF.Abilities.Ability
{
    // Passive ability: hidden from the pawn command bar.
    public override bool ShowGizmoOnPawn() => false;

    public override void Tick()
    {
        base.Tick();
        EnsurePassive();
    }

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        EnsurePassive();
    }

    private void EnsurePassive()
    {
        Pawn pawn = CasterPawn;
        HediffDef passive = MyExtensionDefOf.VPEMYX_EmbraceGloom;
        if (pawn == null || passive == null || pawn.health?.hediffSet == null || !pawn.IsHashIntervalTick(120))
        {
            return;
        }

        if (!pawn.health.hediffSet.HasHediff(passive))
        {
            pawn.health.AddHediff(passive);
        }
    }
}
