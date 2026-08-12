using System.Collections.Generic;
using UnityEngine;

namespace Investigation
{
    /// <summary>
    /// Tracks which suspects the player has spoken to this playthrough.
    /// Mirrors EvidenceLog: a persistent singleton the verdict screen reads
    /// its "talked to 5 suspects" requirement from.
    /// </summary>
    public class SuspectInterviewLog : MonoBehaviour
    {
        public static SuspectInterviewLog Instance { get; private set; }

        private readonly HashSet<string> _talkedTo = new();

        public int Count => _talkedTo.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool HasTalkedTo(string characterName) => _talkedTo.Contains(characterName);

        public void RecordConversation(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return;
            _talkedTo.Add(characterName);
        }
    }
}
