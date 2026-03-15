using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;

namespace VPE_MyExtension;

public class Ability_FleshHealing : Ability
{
    private AbilityExtension_FleshHealing Config => def.GetModExtension<AbilityExtension_FleshHealing>();

    public override void Cast(params GlobalTargetInfo[] targets)
    {
        base.Cast(targets);
        if (Config == null || targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Pawn pawn = targets[i].Thing as Pawn;
            if (pawn == null || pawn.Dead)
            {
                continue;
            }

            HealPawn(pawn);
        }
    }

    private void HealPawn(Pawn pawn)
    {
        if (Config.healInjuries)
        {
            HealInjuries(pawn);
        }

        if (Config.replaceMissingParts)
        {
            ReplaceMissingParts(pawn);
        }
    }

    private void HealInjuries(Pawn pawn)
    {
        List<Hediff> hediffs = pawn.health?.hediffSet?.hediffs;
        if (hediffs == null)
        {
            return;
        }

        for (int i = hediffs.Count - 1; i >= 0; i--)
        {
            Hediff_Injury injury = hediffs[i] as Hediff_Injury;
            if (injury == null)
            {
                continue;
            }

            if (!Config.healPermanentInjuries && injury.IsPermanent())
            {
                continue;
            }

            injury.Heal(injury.Severity);
        }
    }

    private void ReplaceMissingParts(Pawn pawn)
    {
        List<Hediff_MissingPart> missingParts = pawn.health?.hediffSet?.hediffs?
            .OfType<Hediff_MissingPart>()
            .OrderBy(m => m.Part?.depth ?? 0)
            .ToList();
        if (missingParts == null || missingParts.Count == 0)
        {
            return;
        }

        HashSet<BodyPartRecord> processedInstallParts = new HashSet<BodyPartRecord>();
        for (int i = 0; i < missingParts.Count; i++)
        {
            BodyPartRecord part = missingParts[i].Part;
            if (part == null)
            {
                continue;
            }

            if (pawn.health.hediffSet.GetMissingPartFor(part) == null)
            {
                continue;
            }

            HediffDef replacement = FindReplacement(part.def);
            if (replacement == null && CanUseFallbackForPart(part))
            {
                replacement = Config.fallbackReplacement;
            }

            if (replacement == null || pawn.health.hediffSet.HasDirectlyAddedPartFor(part))
            {
                continue;
            }

            BodyPartRecord installPart = ResolveInstallPart(part, replacement);
            if (installPart == null || processedInstallParts.Contains(installPart))
            {
                continue;
            }

            TryInstallReplacement(pawn, installPart, replacement);
            processedInstallParts.Add(installPart);
        }
    }

    private void TryInstallReplacement(Pawn pawn, BodyPartRecord part, HediffDef replacement)
    {
        if (pawn.health.hediffSet.HasDirectlyAddedPartFor(part))
        {
            return;
        }

        if (pawn.health.hediffSet.GetMissingPartFor(part) != null || pawn.health.hediffSet.HasMissingPartFor(part))
        {
            pawn.health.RestorePart(part, null, true);
            if (pawn.health.hediffSet.GetMissingPartFor(part) != null || pawn.health.hediffSet.HasMissingPartFor(part))
            {
                return;
            }
        }

        TryAddReplacement(pawn, part, replacement);
    }

    private BodyPartRecord ResolveInstallPart(BodyPartRecord missingPart, HediffDef replacement)
    {
        BodyPartDef installDef = replacement?.defaultInstallPart;
        if (installDef == null)
        {
            return missingPart;
        }

        BodyPartRecord cursor = missingPart;
        while (cursor != null)
        {
            if (cursor.def == installDef)
            {
                return cursor;
            }

            cursor = cursor.parent;
        }

        return missingPart;
    }

    private bool TryAddReplacement(Pawn pawn, BodyPartRecord part, HediffDef replacement)
    {
        if (pawn.health.hediffSet.GetMissingPartFor(part) != null ||
            pawn.health.hediffSet.HasMissingPartFor(part) ||
            pawn.health.hediffSet.HasDirectlyAddedPartFor(part))
        {
            return false;
        }

        try
        {
            pawn.health.AddHediff(replacement, part, null, null);
            return pawn.health.hediffSet.HasDirectlyAddedPartFor(part);
        }
        catch
        {
            return false;
        }
    }

    private HediffDef FindReplacement(BodyPartDef part)
    {
        if (Config.replacements == null)
        {
            return null;
        }

        for (int i = 0; i < Config.replacements.Count; i++)
        {
            FleshReplacementRule rule = Config.replacements[i];
            if (rule?.part == part)
            {
                return rule.hediff;
            }
        }

        return null;
    }

    private bool CanUseFallbackForPart(BodyPartRecord part)
    {
        if (part?.def == null)
        {
            return false;
        }

        if (PartIsFingerOrToe(part))
        {
            return false;
        }

        return true;
    }

    private bool PartIsFingerOrToe(BodyPartRecord part)
    {
        string defName = part.def.defName ?? string.Empty;
        if (defName.IndexOf("Finger", StringComparison.OrdinalIgnoreCase) >= 0 ||
            defName.IndexOf("Toe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (part.groups == null)
        {
            return false;
        }

        for (int i = 0; i < part.groups.Count; i++)
        {
            BodyPartGroupDef group = part.groups[i];
            if (group == null)
            {
                continue;
            }

            string groupName = group.defName ?? string.Empty;
            if (groupName.Equals("Fingers", StringComparison.OrdinalIgnoreCase) ||
                groupName.Equals("Toes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
