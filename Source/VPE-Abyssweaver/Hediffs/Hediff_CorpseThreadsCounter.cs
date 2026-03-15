using Verse;

namespace VPE_MyExtension;

public class Hediff_CorpseThreadsCounter : Hediff
{
    private int revivedCount;
    private int unlockThreshold = 40;

    public override string LabelBase
    {
        get
        {
            string baseLabel = base.LabelBase;
            return $"{baseLabel} ({revivedCount}/{unlockThreshold})";
        }
    }

    public void SetCounter(int count, int threshold)
    {
        revivedCount = count < 0 ? 0 : count;
        unlockThreshold = threshold < 1 ? 1 : threshold;
        Severity = revivedCount;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref revivedCount, "revivedCount", 0);
        Scribe_Values.Look(ref unlockThreshold, "unlockThreshold", 40);
    }
}
