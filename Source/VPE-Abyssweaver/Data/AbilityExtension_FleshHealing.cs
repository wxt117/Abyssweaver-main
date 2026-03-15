using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

public class AbilityExtension_FleshHealing : DefModExtension
{
    public bool healInjuries = true;
    public bool healPermanentInjuries = true;
    public bool replaceMissingParts = true;
    public List<FleshReplacementRule> replacements;
    public HediffDef fallbackReplacement;
}

public class FleshReplacementRule
{
    public BodyPartDef part;
    public HediffDef hediff;
}
