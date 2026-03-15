using RimWorld;
using Verse;

namespace VPE_MyExtension;

[DefOf]
public static class MyExtensionDefOf
{
    public static ThoughtDef VPEMYX_PsychicScreamThought;
    public static ThoughtDef VPEMYX_DelusionBackflowThought;
    public static ThoughtDef VPEMYX_CognitiveHazardThought;
    public static ThoughtDef VPEMYX_GlobalLobotomyThought;
    public static ThoughtDef VPEMYX_WailingAuraThought;
    public static ThoughtDef VPEMYX_LegionDreadThought;

    public static HediffDef VPEMYX_VoidReturnTimer;
    public static HediffDef VPEMYX_DelusionBackflowBuff;
    public static HediffDef VPEMYX_DistortedMetabolism;
    public static HediffDef VPEMYX_EmbraceGloom;
    public static HediffDef VPEMYX_FleshFrenzyBuff;
    public static HediffDef VPEMYX_CorpseThreadsCounter;
    public static HediffDef VPEMYX_MindDrained;
    public static HediffDef VPEMYX_MindSiphonSensitivity;
    public static HediffDef VPEMYX_CognitiveHazardAura;
    public static HediffDef VPEMYX_CognitiveHazardVictim;
    public static HediffDef VPEMYX_VoidSight;
    public static HediffDef VPEMYX_FleshMoldingTier1;
    public static HediffDef VPEMYX_FleshMoldingTier2;
    public static HediffDef VPEMYX_FleshMoldingTier3;
    public static HediffDef VPEMYX_AbyssalAtrocityCore;
    public static HediffDef VPEMYX_Corruption;
    public static HediffDef VPEMYX_CellularMalignancy;
    public static HediffDef VPEMYX_MalignantTumor;
    public static HediffDef VPEMYX_AbyssalDamageTransferLink;
    public static HediffDef VPEMYX_WailingAuraVictim;
    public static HediffDef VPEMYX_LegionMomentum;
    public static HediffDef VPEMYX_LegionDreadField;
    public static HediffDef VPEMYX_LegionRiftBurn;
    public static HediffDef VPEMYX_LegionScreamShock;
    public static HediffDef VPEMYX_VoidAscensionCocoon;
    public static HediffDef VPEMYX_VoidAvatar;
    public static HediffDef VPEMYX_DomainOfSilenceChannel;
    public static HediffDef VPEMYX_BrainWithering;
    public static HediffDef VPEMYX_DarkBeaconChannel;

    static MyExtensionDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(MyExtensionDefOf));
    }
}
