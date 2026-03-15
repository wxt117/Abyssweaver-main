using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace VPE_MyExtension;

[HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
public static class Patch_Pawn_PreApplyDamage_AbyssalTransfer
{
    private static readonly HashSet<int> ActiveTransfers = new HashSet<int>();

    public static void Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
    {
        if (absorbed || __instance == null || __instance.Dead || dinfo.Amount <= 0f)
        {
            return;
        }

        if (!TryGetLinkComp(__instance, out HediffComp_AbyssalDamageTransferLink linkComp))
        {
            return;
        }

        if (!linkComp.TryGetLinkedAbyssal(out Pawn abyssal) ||
            abyssal == null ||
            abyssal == __instance ||
            abyssal.Dead ||
            !abyssal.Spawned ||
            abyssal.Map != __instance.Map)
        {
            return;
        }

        int ownerId = __instance.thingIDNumber;
        if (!ActiveTransfers.Add(ownerId))
        {
            return;
        }

        try
        {
            DamageInfo redirected = new DamageInfo(
                dinfo.Def,
                dinfo.Amount,
                dinfo.ArmorPenetrationInt,
                dinfo.Angle,
                dinfo.Instigator,
                dinfo.HitPart,
                dinfo.Weapon);
            abyssal.TakeDamage(redirected);
            absorbed = true;
            dinfo.SetAmount(0f);
        }
        finally
        {
            ActiveTransfers.Remove(ownerId);
        }
    }

    private static bool TryGetLinkComp(Pawn pawn, out HediffComp_AbyssalDamageTransferLink comp)
    {
        comp = null;
        if (pawn?.health?.hediffSet == null)
        {
            return false;
        }

        HediffDef linkDef = MyExtensionDefOf.VPEMYX_AbyssalDamageTransferLink ??
                            DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_AbyssalDamageTransferLink");
        if (linkDef == null)
        {
            return false;
        }

        Hediff link = pawn.health.hediffSet.GetFirstHediffOfDef(linkDef);
        comp = link?.TryGetComp<HediffComp_AbyssalDamageTransferLink>();
        return comp != null;
    }
}
