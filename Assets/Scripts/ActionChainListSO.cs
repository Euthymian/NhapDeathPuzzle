using UnityEngine;

[CreateAssetMenu(fileName = "ActionChains", menuName = "Levels/Action Chains (Simple Moves)")]
public class ActionChainListSO : ScriptableObject
{
    [System.Serializable]
    public class Step
    {
        [Header("Trigger (the animation playing now)")]
        public string triggerVisualId;    // e.g., "Main"
        public string triggerAnimation;   // e.g., "Jump"

        [Header("Next (played when trigger completes)")]
        public string nextVisualId;       // empty => no next
        public string nextAnimation;      // empty => no next

        [Header("Completion")]
        public bool isLastAnim = false;                 // renamed from isTerminal
        public bool disableOnComplete = false;          // renamed from disableTriggerVisualOnComplete

        // ---- SIMPLE MOVES: run at BEGIN of the trigger animation ----
        [System.Serializable]
        public class MoveSegment
        {
            [Tooltip("Which visual to move (by visualId)")]
            public string targetVisualId;

            [Tooltip("Anchor (by anchorId) to move to")]
            public string anchorId;

            [Tooltip("Seconds, linear tween")]
            [Min(0f)] public float duration = 0.35f;
        }

        [Header("Movement Sequence (runs sequentially at animation start)")]
        public MoveSegment[] moves = System.Array.Empty<MoveSegment>();
    }

    [System.Serializable]
    public class Chain
    {
        public string chainId;                         // e.g., "ServeDrink"
        public Step[] steps = System.Array.Empty<Step>();
    }

    [Header("Exactly one required chain to win")]
    public string requiredChainId;

    [Header("Chains")]
    public Chain[] chains = System.Array.Empty<Chain>();
}
