using HarmonyLib;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

[HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
public static class Patch_VerbShoot_TryCastShot_DomainOfSilence
{
    public static bool Prefix(Verb_Shoot __instance, ref bool __result)
    {
        if (!DomainOfSilenceSuppressionUtility.ShouldSuppressTechShot(__instance))
        {
            return true;
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class Patch_VerbLaunchProjectile_TryCastShot_DomainOfSilence
{
    public static bool Prefix(Verb_LaunchProjectile __instance, ref bool __result)
    {
        if (!DomainOfSilenceSuppressionUtility.ShouldSuppressTechShot(__instance))
        {
            return true;
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Projectile_Explosive), "Impact")]
public static class Patch_ProjectileExplosive_Impact_DomainOfSilence
{
    public static bool Prefix(Projectile_Explosive __instance)
    {
        Map map = __instance?.Map;
        if (map == null || !DomainOfSilenceDomainUtility.IsCellInsideAnyDomain(map, __instance.Position))
        {
            return true;
        }

        FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail("ElectricalSpark") ??
                         DefDatabase<FleckDef>.GetNamedSilentFail("DustPuffThick");
        if (fleck != null)
        {
            FleckMaker.Static(__instance.Position, map, fleck, 1.3f);
        }

        __instance.Destroy(DestroyMode.Vanish);
        return false;
    }
}

internal static class DomainOfSilenceSuppressionUtility
{
    public static bool ShouldSuppressTechShot(Verb verb)
    {
        if (verb?.Caster == null || !IsTechRangedVerb(verb))
        {
            return false;
        }

        Thing caster = verb.Caster;
        if (!DomainOfSilenceDomainUtility.IsThingInsideAnyDomain(caster))
        {
            return false;
        }

        if (caster is Thing thing && thing.IsHashIntervalTick(15))
        {
            FleckDef fleck = DefDatabase<FleckDef>.GetNamedSilentFail("ElectricalSpark");
            if (fleck != null && thing.Map != null)
            {
                FleckMaker.Static(thing.Position, thing.Map, fleck, 0.8f);
            }
        }

        return true;
    }

    private static bool IsTechRangedVerb(Verb verb)
    {
        if (verb?.verbProps == null || verb.verbProps.IsMeleeAttack || verb.verbProps.range <= 1.42f)
        {
            return false;
        }

        Thing caster = verb.Caster;
        if (caster is Building_Turret)
        {
            return true;
        }

        if (caster is Pawn pawn && pawn.RaceProps?.IsMechanoid == true)
        {
            return true;
        }

        ThingDef weaponDef = verb.EquipmentSource?.def;
        if (weaponDef == null && caster is Pawn shooter)
        {
            weaponDef = shooter.equipment?.Primary?.def;
        }

        if (weaponDef?.IsRangedWeapon == true && weaponDef.techLevel >= TechLevel.Industrial)
        {
            return true;
        }

        return false;
    }
}
