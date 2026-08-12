using Investigation;

namespace MurderVilla.InvestigationSystem
{
    /// <summary>
    /// The three questions the player must answer to close the case, and the
    /// single correct combination. Kept separate from CaseVerdictUI so the
    /// answer key isn't sitting in a MonoBehaviour that gets serialized into
    /// the scene.
    /// </summary>
    public enum LockMethod
    {
        None = 0,
        AutomaticLock,
        SecretPassage,
        LockedFromInside,
    }

    public static class CaseVerdict
    {
        public const SuspectId CorrectDrugger = SuspectId.Amy;
        public const SuspectId CorrectKiller = SuspectId.Coco;
        public const LockMethod CorrectLockMethod = LockMethod.AutomaticLock;

        public const int MinEvidenceRequired = 5;
        public const int MinSuspectsRequired = 5;

        public static bool IsCorrect(SuspectId drugger, SuspectId killer, LockMethod lockMethod)
        {
            return drugger == CorrectDrugger && killer == CorrectKiller &&
                lockMethod == CorrectLockMethod;
        }

        public static bool MeetsRequirements(int evidenceCount, int suspectsTalkedTo)
        {
            return evidenceCount >= MinEvidenceRequired &&
                suspectsTalkedTo >= MinSuspectsRequired;
        }
    }
}
