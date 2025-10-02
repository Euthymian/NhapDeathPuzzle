using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public event EventHandler OnLevelComplete;

    [Header("Config (SO)")]
    [SerializeField] private ActionChainListSO actionChainListSO;

    [Header("Scene refs")]
    [SerializeField] private List<Visual> visualList = new();
    [SerializeField] private List<DraggableItem> itemsToReset = new();

    // map visualId -> instance
    private Dictionary<string, Visual> visualById;

    // flattened list of steps while remembering which chain they belong to
    private struct StepRef
    {
        public string chainId;
        public ActionChainListSO.Step step;
    }

    private List<StepRef> allSteps;
    private string requiredChainId;

    private void Start()
    {
        foreach (var v in visualList)
            v.OnAnimationCompleteEvent += OnVisualComplete;

        visualById = new Dictionary<string, Visual>(StringComparer.Ordinal);
        foreach (var v in visualList)
            if (!string.IsNullOrEmpty(v.visualId))
                visualById[v.visualId] = v;

        // Flatten chains -> steps and read required chain id
        allSteps = new List<StepRef>();
        requiredChainId = actionChainListSO ? actionChainListSO.requiredChainId : null;

        if (actionChainListSO && actionChainListSO.chains != null)
        {
            foreach (var chain in actionChainListSO.chains)
            {
                if (chain == null || chain.steps == null) continue;

                foreach (var s in chain.steps)
                {
                    if (s == null) continue;
                    allSteps.Add(new StepRef
                    {
                        chainId = chain.chainId,
                        step = s
                    });
                }
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var v in visualList)
            if (v) v.OnAnimationCompleteEvent -= OnVisualComplete;
    }

    private void OnVisualComplete(object sender, Visual.AnimationCompleteEventArgs e)
    {
        // Find the first step that matches (visualId, animationName)
        StepRef? chosen = null;

        for (int i = 0; i < allSteps.Count; i++)
        {
            var sr = allSteps[i];
            var s = sr.step;
            if (s.triggerVisualId == e.visual.visualId &&
                s.triggerAnimation == e.animationName)
            {
                chosen = sr;
                break; // first match wins (no branching)
            }
        }

        if (!chosen.HasValue)
        {
            // No matching step; typically do nothing. If you want strict flow, you could ResetLevelState();
            return;
        }

        var stepRef = chosen.Value;
        var step = stepRef.step;

        // Kick next if provided
        if (!string.IsNullOrEmpty(step.nextVisualId) &&
            visualById.TryGetValue(step.nextVisualId, out var nextV) &&
            !string.IsNullOrEmpty(step.nextAnimation))
        {
            nextV.PlayAnim(step.nextAnimation, false);
        }

        // Optional: disable the visual that just completed
        if (step.disableOnComplete && e.visual != null)
        {
            e.visual.transform.parent.gameObject.SetActive(false);
        }

        // Terminal? If it's the required chain, level complete
        if (step.isLastAnim && !string.IsNullOrEmpty(requiredChainId))
        {
            if (string.Equals(stepRef.chainId, requiredChainId, StringComparison.Ordinal))
            {
                OnLevelComplete?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ResetLevelState();
            }
        }
    }

    public void ResetLevelState()
    {
        foreach (var it in itemsToReset)
            if (it) it.ResetItem();
    }
}
