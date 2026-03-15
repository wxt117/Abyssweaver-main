using System.Collections.Generic;
using Verse;

namespace VPE_MyExtension;

public class MyExtensionGameComponent : GameComponent
{
    private Dictionary<int, int> revivedCorpsesByPawn;
    private HashSet<int> fleshFrenzyUnlocked;
    private Dictionary<int, int> abyssalOwnerByPawn;

    public MyExtensionGameComponent(Game game)
    {
    }

    public bool RegisterRevivedCorpses(Pawn caster, int amount, int unlockThreshold, out int totalRevived)
    {
        totalRevived = 0;
        if (caster == null || amount <= 0)
        {
            return false;
        }

        revivedCorpsesByPawn ??= new Dictionary<int, int>();
        fleshFrenzyUnlocked ??= new HashSet<int>();

        int key = caster.thingIDNumber;
        revivedCorpsesByPawn.TryGetValue(key, out int current);
        current += amount;
        revivedCorpsesByPawn[key] = current;
        totalRevived = current;

        if (current < unlockThreshold || fleshFrenzyUnlocked.Contains(key))
        {
            return false;
        }

        fleshFrenzyUnlocked.Add(key);
        return true;
    }

    public bool IsFleshFrenzyUnlocked(Pawn caster)
    {
        if (caster == null || fleshFrenzyUnlocked == null)
        {
            return false;
        }

        return fleshFrenzyUnlocked.Contains(caster.thingIDNumber);
    }

    public int GetRevivedCorpses(Pawn caster)
    {
        if (caster == null || revivedCorpsesByPawn == null)
        {
            return 0;
        }

        return revivedCorpsesByPawn.TryGetValue(caster.thingIDNumber, out int value) ? value : 0;
    }

    public void RegisterAbyssalOwner(Pawn abyssal, Pawn owner)
    {
        if (abyssal == null || owner == null)
        {
            return;
        }

        abyssalOwnerByPawn ??= new Dictionary<int, int>();
        abyssalOwnerByPawn[abyssal.thingIDNumber] = owner.thingIDNumber;
    }

    public bool TryGetAbyssalOwnerId(Pawn abyssal, out int ownerPawnId)
    {
        ownerPawnId = -1;
        if (abyssal == null || abyssalOwnerByPawn == null)
        {
            return false;
        }

        return abyssalOwnerByPawn.TryGetValue(abyssal.thingIDNumber, out ownerPawnId);
    }

    public void RemoveAbyssalOwner(Pawn abyssal)
    {
        if (abyssal == null || abyssalOwnerByPawn == null)
        {
            return;
        }

        abyssalOwnerByPawn.Remove(abyssal.thingIDNumber);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        List<int> revivedKeys = null;
        List<int> revivedValues = null;
        Scribe_Collections.Look(ref revivedCorpsesByPawn, "revivedCorpsesByPawn", LookMode.Value, LookMode.Value, ref revivedKeys, ref revivedValues);

        Scribe_Collections.Look(ref fleshFrenzyUnlocked, "fleshFrenzyUnlocked", LookMode.Value);

        List<int> abyssalKeys = null;
        List<int> abyssalOwnerValues = null;
        Scribe_Collections.Look(ref abyssalOwnerByPawn, "abyssalOwnerByPawn", LookMode.Value, LookMode.Value, ref abyssalKeys, ref abyssalOwnerValues);
    }
}
