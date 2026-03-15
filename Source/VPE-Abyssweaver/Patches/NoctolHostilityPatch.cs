using HarmonyLib;
using RimWorld;
using Verse;

namespace VPE_MyExtension;

[HarmonyPatch(typeof(GenHostility), "HostileTo", new[] { typeof(Thing), typeof(Thing) })]
public static class Patch_GenHostility_HostileTo_NoctolVoidSight
{
    public static void Postfix(Thing a, Thing b, ref bool __result)
    {
        if (!__result || a == null || b == null)
        {
            return;
        }

        if (a is Pawn pawnA && b is Pawn pawnB)
        {
            if (IsNoctolVersusVoidSight(pawnA, pawnB) || IsNoctolVersusVoidSight(pawnB, pawnA))
            {
                __result = false;
            }
        }
    }

    private static bool IsNoctolVersusVoidSight(Pawn maybeNoctol, Pawn maybeVoidSight)
    {
        if (maybeNoctol?.kindDef?.defName != "Noctol")
        {
            return false;
        }

        HediffDef voidSight = MyExtensionDefOf.VPEMYX_VoidSight ??
                              DefDatabase<HediffDef>.GetNamedSilentFail("VPEMYX_VoidSight");
        return voidSight != null && maybeVoidSight?.health?.hediffSet?.HasHediff(voidSight) == true;
    }
}
