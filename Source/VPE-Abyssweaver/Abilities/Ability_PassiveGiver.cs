using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_PassiveGiver : Ability
{
    private AbilityExtension_PassiveGiver PassiveExt => def?.GetModExtension<AbilityExtension_PassiveGiver>();

    public override void Init()
    {
        base.Init();
        AddPassives();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            AddPassives();
        }
    }

    public override void Tick()
    {
        base.Tick();
        if (pawn != null && pawn.IsHashIntervalTick(120))
        {
            AddPassives();
        }
    }

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        // Passive only; no active cast behavior.
    }

    public override bool AICanUseOn(Thing target)
    {
        return false;
    }

    public override bool ShowGizmoOnPawn()
    {
        return false;
    }

    private void AddPassives()
    {
        if (pawn?.health?.hediffSet == null || PassiveExt?.hediffsToAdd == null)
        {
            return;
        }

        for (int i = 0; i < PassiveExt.hediffsToAdd.Count; i++)
        {
            HediffDef hediffDef = PassiveExt.hediffsToAdd[i];
            if (hediffDef == null)
            {
                continue;
            }

            if (!pawn.health.hediffSet.HasHediff(hediffDef))
            {
                pawn.health.AddHediff(hediffDef);
            }
        }
    }
}
