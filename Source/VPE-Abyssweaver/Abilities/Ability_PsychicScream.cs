using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using VEF.Abilities;
using Verse;
using Verse.Sound;

namespace VPE_MyExtension;

public class Ability_PsychicScream : VEF.Abilities.Ability
{
    private AbilityExtension_PsychicScream Config => def.GetModExtension<AbilityExtension_PsychicScream>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (Config == null || CasterPawn == null || CasterPawn.Map == null)
        {
            return;
        }

        PlayScreamEffect();
        ScreamResult result = ApplyScream();
        Messages.Message(
            "VPEMYX_Message_PsychicScream_Result".Translate(result.affected, result.hostilesInRange, result.noMoodTargets),
            CasterPawn,
            result.affected > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
    }

    private ScreamResult ApplyScream()
    {
        List<Pawn> pawnsInRange = CasterPawn.Map.mapPawns.AllPawnsSpawned
            .Where(p => p != null && p != CasterPawn && !p.Dead && p.Position.InHorDistOf(CasterPawn.Position, Config.radius))
            .ToList();

        int hostilesInRange = 0;
        int noMoodTargets = 0;
        int affected = 0;

        foreach (Pawn pawn in pawnsInRange)
        {
            if (!pawn.HostileTo(CasterPawn))
            {
                continue;
            }

            hostilesInRange++;

            if (!Config.affectMechs && pawn.RaceProps.IsMechanoid)
            {
                continue;
            }

            if (!Config.affectAnimals && pawn.RaceProps.Animal)
            {
                continue;
            }

            if (pawn.needs?.mood == null)
            {
                noMoodTargets++;
                continue;
            }

            ApplyMoodThought(pawn);
            PlayTargetEffect(pawn);
            affected++;
        }

        return new ScreamResult(hostilesInRange, noMoodTargets, affected);
    }

    private void ApplyMoodThought(Pawn pawn)
    {
        ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("VPEMYX_PsychicScreamThought") ??
                               MyExtensionDefOf.VPEMYX_PsychicScreamThought;
        if (thoughtDef == null)
        {
            return;
        }

        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thoughtDef);
        pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, CasterPawn);
    }

    private void PlayScreamEffect()
    {
        FleckDef aoe = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (aoe != null)
        {
            float scale = Mathf.Clamp(Config.radius / 6f, 1.6f, 6f);
            FleckMaker.Static(CasterPawn.Position, CasterPawn.Map, aoe, scale);
        }

        SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal") ??
                         DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        sound?.PlayOneShot(new TargetInfo(CasterPawn.Position, CasterPawn.Map));
    }

    private static void PlayTargetEffect(Pawn pawn)
    {
        if (pawn?.Map == null)
        {
            return;
        }

        FleckDef aoe = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (aoe != null)
        {
            FleckMaker.Static(pawn.Position, pawn.Map, aoe, 0.8f);
        }
    }

    private readonly struct ScreamResult
    {
        public readonly int hostilesInRange;
        public readonly int noMoodTargets;
        public readonly int affected;

        public ScreamResult(int hostilesInRange, int noMoodTargets, int affected)
        {
            this.hostilesInRange = hostilesInRange;
            this.noMoodTargets = noMoodTargets;
            this.affected = affected;
        }
    }
}
