using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VPE_MyExtension;

public class Ability_GlobalLobotomy : VEF.Abilities.Ability
{
    private AbilityExtension_GlobalLobotomy Config => def.GetModExtension<AbilityExtension_GlobalLobotomy>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);

        Pawn caster = CasterPawn;
        Map map = caster?.Map;
        if (caster == null || map == null)
        {
            return;
        }

        int affected = 0;
        IReadOnlyList<Pawn> all = map.mapPawns?.AllPawnsSpawned;
        if (all == null)
        {
            return;
        }

        for (int i = 0; i < all.Count; i++)
        {
            Pawn pawn = all[i];
            if (pawn == null || pawn == caster || pawn.Dead)
            {
                continue;
            }

            if (!ShouldAffect(pawn))
            {
                continue;
            }

            bool broke = ForceBreak(pawn, caster);
            ApplyMoodMemory(pawn, caster);
            PlayTargetEffect(pawn);
            if (broke)
            {
                affected++;
            }
        }

        PlayCasterEffect(caster);
        Messages.Message("VPEMYX_Message_GlobalLobotomy_Affected".Translate(affected), caster, MessageTypeDefOf.PositiveEvent);
    }

    private bool ShouldAffect(Pawn pawn)
    {
        if (pawn == null || pawn.Downed || IsEntityLike(pawn))
        {
            return false;
        }

        if (pawn.RaceProps.IsMechanoid && Config?.affectMechs == false)
        {
            return false;
        }

        if (pawn.RaceProps.Animal && Config?.affectAnimals == false)
        {
            return false;
        }

        return true;
    }

    private static bool ForceBreak(Pawn target, Pawn caster)
    {
        MentalStateHandler handler = target.mindState?.mentalStateHandler;
        if (handler == null)
        {
            return false;
        }

        List<MentalStateDef> states = BuildStatePool(target);
        while (states.Count > 0)
        {
            MentalStateDef state = states.RandomElement();
            states.Remove(state);
            if (state == null)
            {
                continue;
            }

            if (handler.TryStartMentalState(
                    state,
                    "Global void lobotomy",
                    forced: true,
                    forceWake: true,
                    causedByMood: false,
                    otherPawn: caster,
                    transitionSilently: false,
                    causedByDamage: false,
                    causedByPsycast: true))
            {
                return true;
            }
        }

        return false;
    }

    private static List<MentalStateDef> BuildStatePool(Pawn target)
    {
        List<MentalStateDef> result = new List<MentalStateDef>();
        if (target.RaceProps.IsMechanoid)
        {
            AddState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("BerserkMechanoid"));
            AddState(result, MentalStateDefOf.Berserk);
            return result;
        }

        if (target.RaceProps.Animal)
        {
            AddState(result, MentalStateDefOf.ManhunterPermanent);
            AddState(result, MentalStateDefOf.Manhunter);
            AddState(result, MentalStateDefOf.PanicFlee);
            return result;
        }

        // Keep to stable human mental states; exclude EntityKiller (can spam NRE in JobGiver_SlaughterEntity).
        AddState(result, DefDatabase<MentalStateDef>.GetNamedSilentFail("Catatonic"));
        AddState(result, MentalStateDefOf.BerserkPermanent);
        AddState(result, MentalStateDefOf.Berserk);
        AddState(result, MentalStateDefOf.PanicFlee);
        AddState(result, MentalStateDefOf.Wander_Psychotic);
        return result;
    }

    private static void AddState(List<MentalStateDef> list, MentalStateDef def)
    {
        if (def != null)
        {
            list.Add(def);
        }
    }

    private static bool IsEntityLike(Pawn pawn)
    {
        if (pawn?.RaceProps?.IsAnomalyEntity == true)
        {
            return true;
        }

        FactionDef defaultFaction = pawn?.kindDef?.defaultFactionDef;
        if (defaultFaction == FactionDefOf.Entities ||
            string.Equals(defaultFaction?.defName, "Entities", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pawn?.Faction == Faction.OfEntities || pawn?.Faction?.def == FactionDefOf.Entities)
        {
            return true;
        }

        string category = pawn?.kindDef?.overrideDebugActionCategory;
        if (!string.IsNullOrEmpty(category) && category.IndexOf("Entity", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private void ApplyMoodMemory(Pawn target, Pawn caster)
    {
        ThoughtDef thought = Config?.moodThought ?? MyExtensionDefOf.VPEMYX_GlobalLobotomyThought;
        if (thought == null || target.needs?.mood?.thoughts?.memories == null)
        {
            return;
        }

        target.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thought);
        target.needs.mood.thoughts.memories.TryGainMemory(thought, caster);
    }

    private static void PlayTargetEffect(Pawn target)
    {
        if (target?.Map == null)
        {
            return;
        }

        FleckDef aoe = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (aoe != null)
        {
            FleckMaker.Static(target.Position, target.Map, aoe, 0.9f);
        }
    }

    private static void PlayCasterEffect(Pawn caster)
    {
        if (caster?.Map == null)
        {
            return;
        }

        FleckDef aoe = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
        if (aoe != null)
        {
            FleckMaker.Static(caster.Position, caster.Map, aoe, 2.4f);
        }

        SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("PsychicPulseGlobal") ??
                         DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry");
        sound?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
    }
}
