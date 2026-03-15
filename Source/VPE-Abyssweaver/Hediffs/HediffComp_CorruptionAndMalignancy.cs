using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class HediffCompProperties_CorruptionSpread : HediffCompProperties
{
    public int severityIntervalTicks = 90;
    public float localSeverityGainPerPulse = 0.01f;
    public int spreadIntervalTicks = 360;
    public int spreadPartsPerPulse = 1;
    public int maxAffectedParts = 12;
    public float spreadInitialSeverity = 0.12f;
    public float spreadExistingSeverityGain = 0.02f;
    public string spreadHediffDefName = "VPEMYX_CorruptionLesion";

    public HediffCompProperties_CorruptionSpread()
    {
        compClass = typeof(HediffComp_CorruptionSpread);
    }
}

public class HediffComp_CorruptionSpread : HediffComp
{
    private int lastSpreadTick = -99999;
    private HediffCompProperties_CorruptionSpread Props => (HediffCompProperties_CorruptionSpread)props;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref lastSpreadTick, "lastSpreadTick", -99999);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(30, Props.severityIntervalTicks)))
        {
            return;
        }

        IncreaseSeverity(parent, Props.localSeverityGainPerPulse);

        if (!IsControllerInstance(pawn, parent))
        {
            return;
        }

        MigrateLegacyPartCorruptionIfNeeded(pawn);

        int now = Find.TickManager?.TicksGame ?? 0;
        if (now - lastSpreadTick < Math.Max(60, Props.spreadIntervalTicks))
        {
            return;
        }

        lastSpreadTick = now;
        SpreadCorruption(pawn);
    }

    private void SpreadCorruption(Pawn pawn)
    {
        HediffDef spreadDef = ResolveSpreadDef();
        if (spreadDef == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        int affectedParts = 0;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h != null && h.def == spreadDef && h.Part != null)
            {
                affectedParts++;
                IncreaseSeverity(h, Props.spreadExistingSeverityGain);
            }
        }

        if (affectedParts >= Math.Max(1, Props.maxAffectedParts))
        {
            return;
        }

        List<BodyPartRecord> candidates = BuildSpreadCandidates(pawn);
        int count = Math.Min(candidates.Count, Math.Max(1, Props.spreadPartsPerPulse));
        for (int i = 0; i < count; i++)
        {
            if (candidates.Count == 0)
            {
                break;
            }

            BodyPartRecord part = candidates.RandomElement();
            candidates.Remove(part);
            if (part == null)
            {
                continue;
            }

            if (IsPartOrAnyAncestorMissing(pawn, part))
            {
                continue;
            }

            Hediff existing = GetHediffOnPart(pawn, spreadDef, part);
            if (existing == null)
            {
                existing = TryAddHediffToPart(pawn, spreadDef, part);
                if (existing == null)
                {
                    continue;
                }
            }

            if (existing != null)
            {
                existing.Severity = Math.Max(existing.Severity, Math.Max(0.01f, Props.spreadInitialSeverity));
            }
        }
    }

    private List<BodyPartRecord> BuildSpreadCandidates(Pawn pawn)
    {
        List<BodyPartRecord> candidates = new List<BodyPartRecord>();
        HediffDef spreadDef = ResolveSpreadDef();
        if (spreadDef == null)
        {
            return candidates;
        }

        List<BodyPartRecord> allParts = pawn.RaceProps?.body?.AllParts;
        if (allParts == null)
        {
            return candidates;
        }

        BodyPartRecord origin = ResolveOriginPart(pawn);
        for (int i = 0; i < allParts.Count; i++)
        {
            BodyPartRecord part = allParts[i];
            if (part == null || part == origin || IsPartOrAnyAncestorMissing(pawn, part))
            {
                continue;
            }

            if (GetHediffOnPart(pawn, spreadDef, part) != null)
            {
                continue;
            }

            candidates.Add(part);
        }

        return candidates;
    }

    private static void IncreaseSeverity(Hediff hediff, float gain)
    {
        if (hediff == null || gain <= 0f)
        {
            return;
        }

        if (hediff.pawn == null || hediff.pawn.Dead || hediff.pawn.health?.hediffSet == null)
        {
            return;
        }

        try
        {
            float maxSeverity = hediff.def?.maxSeverity > 0f ? hediff.def.maxSeverity : 1f;
            hediff.Severity = Math.Min(maxSeverity, hediff.Severity + gain);
        }
        catch
        {
        }
    }

    private static bool IsControllerInstance(Pawn pawn, Hediff hediff)
    {
        if (pawn == null || hediff == null)
        {
            return false;
        }

        BodyPartRecord origin = ResolveOriginPart(pawn);
        if (origin == null)
        {
            return true;
        }

        return hediff.Part == origin;
    }

    private static BodyPartRecord ResolveOriginPart(Pawn pawn)
    {
        return pawn?.health?.hediffSet?.GetBrain() ?? pawn?.RaceProps?.body?.corePart;
    }

    private static Hediff GetHediffOnPart(Pawn pawn, HediffDef def, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
        {
            return null;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h != null && h.def == def && h.Part == part)
            {
                return h;
            }
        }

        return null;
    }

    private HediffDef ResolveSpreadDef()
    {
        if (!string.IsNullOrEmpty(Props.spreadHediffDefName))
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Props.spreadHediffDefName);
            if (def != null)
            {
                return def;
            }
        }

        return parent?.def;
    }

    private void MigrateLegacyPartCorruptionIfNeeded(Pawn pawn)
    {
        HediffDef spreadDef = ResolveSpreadDef();
        if (spreadDef == null || spreadDef == parent.def || pawn?.health?.hediffSet?.hediffs == null)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        List<Hediff> legacy = null;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h == null || h == parent || h.def != parent.def || h.Part == null)
            {
                continue;
            }

            legacy ??= new List<Hediff>();
            legacy.Add(h);
        }

        if (legacy == null || legacy.Count == 0)
        {
            return;
        }

        for (int i = 0; i < legacy.Count; i++)
        {
            Hediff old = legacy[i];
            if (old?.Part == null || IsPartOrAnyAncestorMissing(pawn, old.Part))
            {
                continue;
            }

            Hediff existing = GetHediffOnPart(pawn, spreadDef, old.Part);
            if (existing == null)
            {
                existing = TryAddHediffToPart(pawn, spreadDef, old.Part);
                if (existing == null)
                {
                    continue;
                }
            }

            if (existing != null)
            {
                existing.Severity = Math.Max(existing.Severity, old.Severity);
            }

            try
            {
                pawn.health.RemoveHediff(old);
            }
            catch
            {
            }
        }
    }

    private static bool IsPartOrAnyAncestorMissing(Pawn pawn, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet == null || part == null)
        {
            return true;
        }

        for (BodyPartRecord cursor = part; cursor != null; cursor = cursor.parent)
        {
            if (pawn.health.hediffSet.PartIsMissing(cursor))
            {
                return true;
            }
        }

        return false;
    }

    private static Hediff TryAddHediffToPart(Pawn pawn, HediffDef def, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet == null || def == null || !IsValidPartForHediff(pawn, part))
        {
            return null;
        }

        try
        {
            pawn.health.AddHediff(def, part);
        }
        catch
        {
            return null;
        }

        return GetHediffOnPart(pawn, def, part);
    }

    private static bool IsValidPartForHediff(Pawn pawn, BodyPartRecord part)
    {
        HediffSet set = pawn?.health?.hediffSet;
        if (set == null || part == null)
        {
            return false;
        }

        if (!set.HasBodyPart(part) || IsPartOrAnyAncestorMissing(pawn, part))
        {
            return false;
        }

        try
        {
            return set.GetPartHealth(part) > 0.001f;
        }
        catch
        {
            return false;
        }
    }
}

public class HediffCompProperties_CellularMalignancy : HediffCompProperties
{
    public int pulseIntervalTicks = 120;
    public int randomPartsPerPulse = 4;
    public float controllerSeverityGainPerPulse = 0.015f;
    public float tumorInitialSeverity = 0.18f;
    public float existingTumorSeverityGain = 0.05f;
    public string tumorHediffDefName = "Carcinoma";

    public HediffCompProperties_CellularMalignancy()
    {
        compClass = typeof(HediffComp_CellularMalignancy);
    }
}

public class HediffComp_CellularMalignancy : HediffComp
{
    private HediffCompProperties_CellularMalignancy Props => (HediffCompProperties_CellularMalignancy)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
        {
            return;
        }

        if (!pawn.IsHashIntervalTick(Math.Max(30, Props.pulseIntervalTicks)))
        {
            return;
        }

        HediffDef tumorDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.tumorHediffDefName);
        if (tumorDef == null)
        {
            return;
        }

        MigrateLegacyTumors(pawn, tumorDef);
        IncreaseSeverity(parent, Props.controllerSeverityGainPerPulse);
        WorsenExistingTumors(pawn, tumorDef);
        GrowNewTumors(pawn, tumorDef);
    }

    private void WorsenExistingTumors(Pawn pawn, HediffDef tumorDef)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h != null && h.def == tumorDef)
            {
                IncreaseSeverity(h, Props.existingTumorSeverityGain);
            }
        }
    }

    private void GrowNewTumors(Pawn pawn, HediffDef tumorDef)
    {
        List<BodyPartRecord> candidates = BuildTumorCandidates(pawn, tumorDef);
        int count = Math.Min(candidates.Count, Math.Max(1, Props.randomPartsPerPulse));
        for (int i = 0; i < count; i++)
        {
            if (candidates.Count == 0)
            {
                break;
            }

            BodyPartRecord part = candidates.RandomElement();
            candidates.Remove(part);
            if (part == null || IsPartOrAnyAncestorMissing(pawn, part))
            {
                continue;
            }

            Hediff tumor = GetHediffOnPart(pawn, tumorDef, part);
            if (tumor == null)
            {
                tumor = TryAddHediffToPart(pawn, tumorDef, part);
                if (tumor == null)
                {
                    continue;
                }
            }

            if (tumor != null)
            {
                tumor.Severity = Math.Max(tumor.Severity, Math.Max(0.01f, Props.tumorInitialSeverity));
            }
        }
    }

    private static List<BodyPartRecord> BuildTumorCandidates(Pawn pawn, HediffDef tumorDef)
    {
        List<BodyPartRecord> result = new List<BodyPartRecord>();
        List<BodyPartRecord> allParts = pawn.RaceProps?.body?.AllParts;
        if (allParts == null)
        {
            return result;
        }

        for (int i = 0; i < allParts.Count; i++)
        {
            BodyPartRecord part = allParts[i];
            if (part == null || IsPartOrAnyAncestorMissing(pawn, part))
            {
                continue;
            }

            if (PartIsFingerOrToe(part))
            {
                continue;
            }

            if (GetHediffOnPart(pawn, tumorDef, part) != null)
            {
                continue;
            }

            result.Add(part);
        }

        return result;
    }

    private static Hediff GetHediffOnPart(Pawn pawn, HediffDef def, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
        {
            return null;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h != null && h.def == def && h.Part == part)
            {
                return h;
            }
        }

        return null;
    }

    private static void IncreaseSeverity(Hediff hediff, float gain)
    {
        if (hediff == null || gain <= 0f)
        {
            return;
        }

        float maxSeverity = hediff.def?.maxSeverity > 0f ? hediff.def.maxSeverity : 1f;
        hediff.Severity = Math.Min(maxSeverity, hediff.Severity + gain);
    }

    private static void MigrateLegacyTumors(Pawn pawn, HediffDef tumorDef)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || tumorDef == null)
        {
            return;
        }

        HediffDef legacyDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_MalignantTumor");
        if (legacyDef == null || legacyDef == tumorDef)
        {
            return;
        }

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        List<Hediff> legacy = null;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];
            if (h != null && h.def == legacyDef && h.Part != null)
            {
                legacy ??= new List<Hediff>();
                legacy.Add(h);
            }
        }

        if (legacy == null || legacy.Count == 0)
        {
            return;
        }

        for (int i = 0; i < legacy.Count; i++)
        {
            Hediff old = legacy[i];
            if (old?.Part == null || IsPartOrAnyAncestorMissing(pawn, old.Part))
            {
                continue;
            }

            Hediff current = GetHediffOnPart(pawn, tumorDef, old.Part);
            if (current == null)
            {
                current = TryAddHediffToPart(pawn, tumorDef, old.Part);
                if (current == null)
                {
                    continue;
                }
            }

            if (current != null)
            {
                float maxSeverity = current.def?.maxSeverity > 0f ? current.def.maxSeverity : 1f;
                current.Severity = Math.Min(maxSeverity, Math.Max(current.Severity, old.Severity));
            }

            try
            {
                pawn.health.RemoveHediff(old);
            }
            catch
            {
            }
        }
    }

    private static bool IsPartOrAnyAncestorMissing(Pawn pawn, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet == null || part == null)
        {
            return true;
        }

        for (BodyPartRecord cursor = part; cursor != null; cursor = cursor.parent)
        {
            if (pawn.health.hediffSet.PartIsMissing(cursor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PartIsFingerOrToe(BodyPartRecord part)
    {
        if (part?.def == null)
        {
            return false;
        }

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
            string groupName = part.groups[i]?.defName ?? string.Empty;
            if (groupName.Equals("Fingers", StringComparison.OrdinalIgnoreCase) ||
                groupName.Equals("Toes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Hediff TryAddHediffToPart(Pawn pawn, HediffDef def, BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet == null || def == null || !IsValidPartForHediff(pawn, part))
        {
            return null;
        }

        try
        {
            pawn.health.AddHediff(def, part);
        }
        catch
        {
            return null;
        }

        return GetHediffOnPart(pawn, def, part);
    }

    private static bool IsValidPartForHediff(Pawn pawn, BodyPartRecord part)
    {
        HediffSet set = pawn?.health?.hediffSet;
        if (set == null || part == null)
        {
            return false;
        }

        if (!set.HasBodyPart(part) || IsPartOrAnyAncestorMissing(pawn, part))
        {
            return false;
        }

        try
        {
            return set.GetPartHealth(part) > 0.001f;
        }
        catch
        {
            return false;
        }
    }
}
