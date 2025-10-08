using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public event EventHandler OnLevelComplete;

    // NEW: fired when a chain finishes but is NOT the required chain
    public event EventHandler OnWrongChainComplete;

    [Header("Config (SO)")]
    [SerializeField] private ActionChainListSO actionChainListSO;

    [Header("Scene refs")]
    [SerializeField] private List<Visual> visualList = new();
    [SerializeField] private List<DraggableItem> itemsToReset = new(); // optional

    // -------- Anchors (positions to snap to) --------
    [Serializable]
    public class AnchorDef { public string anchorId; public RectTransform rect; }
    [Header("Move Anchors")]
    [SerializeField] private List<AnchorDef> anchors = new();

    private Dictionary<string, RectTransform> anchorById;
    private Dictionary<string, Visual> visualById;

    private struct StepRef { public string chainId; public ActionChainListSO.Step step; }
    private List<StepRef> allSteps;
    private string requiredChainId;

    private ParticleSystem ps;
    private Camera renderPSCam;

    private void Start()
    {
        Camera[] cameras = Camera.allCameras;
        foreach (Camera e in cameras)
        {
            if (e.tag == "MainCamera") continue;
            else renderPSCam = e;
        }

        ps = GetComponentInChildren<ParticleSystem>();

        if (ps)
        {
            ps.Stop();
            ps.transform.parent = renderPSCam.transform;
            ps.transform.localPosition = new Vector3(0, 0, 5);
        }

        foreach (var v in visualList)
        {
            v.OnAnimationStartEvent += OnVisualStart;        // moves at begin of animation
            v.OnAnimationCompleteEvent += OnVisualComplete;  // chain progression

            // NEW: visuals self-reset when a non-required chain ends
            OnWrongChainComplete += v.HandleWrongChainReset;
        }

        visualById = new Dictionary<string, Visual>(StringComparer.Ordinal);
        foreach (var v in visualList)
            if (!string.IsNullOrEmpty(v.visualId)) visualById[v.visualId] = v;

        anchorById = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        foreach (var a in anchors)
            if (a != null && !string.IsNullOrEmpty(a.anchorId) && a.rect) anchorById[a.anchorId] = a.rect;

        allSteps = new List<StepRef>();
        requiredChainId = actionChainListSO ? actionChainListSO.requiredChainId : null;

        if (actionChainListSO?.chains != null)
        {
            foreach (var chain in actionChainListSO.chains)
            {
                if (chain?.steps == null) continue;
                foreach (var s in chain.steps)
                {
                    if (s == null) continue;
                    allSteps.Add(new StepRef { chainId = chain.chainId, step = s });
                }
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var v in visualList)
        {
            v.OnAnimationStartEvent -= OnVisualStart;
            v.OnAnimationCompleteEvent -= OnVisualComplete;

            // NEW
            OnWrongChainComplete -= v.HandleWrongChainReset;
        }
    }

    // ---- MOVES: run at BEGIN of trigger animation ----
    private void OnVisualStart(object sender, Visual.AnimationStartEventArgs e)
    {
        StepRef? chosen = FindStep(e.visual.visualId, e.animationName);
        if (!chosen.HasValue) return;

        StartCoroutine(RunMovesSequential(chosen.Value.step));
    }

    // ---- CHAIN: progress on COMPLETE of trigger animation ----
    private void OnVisualComplete(object sender, Visual.AnimationCompleteEventArgs e)
    {
        StepRef? chosen = FindStep(e.visual.visualId, e.animationName);
        if (!chosen.HasValue) return;

        var sr = chosen.Value;
        var step = sr.step;

        // Play NEXT if provided
        if (!string.IsNullOrEmpty(step.nextVisualId) && !string.IsNullOrEmpty(step.nextAnimation) &&
            visualById.TryGetValue(step.nextVisualId, out var nextV) && nextV != null)
        {
            nextV.PlayAnim(step.nextAnimation, false);
        }

        // Disable trigger visual if requested (after its animation finished)
        if (step.disableOnComplete)
        {
            if (e.visual.transform.parent) e.visual.transform.parent.gameObject.SetActive(false);
            else e.visual.gameObject.SetActive(false);
        }

        // Last anim? finish level if this is the required chain
        if (step.isLastAnim)
        {
            if (string.Equals(sr.chainId, requiredChainId, StringComparison.Ordinal))
            {
                if (ps) ps.Play();
                OnLevelComplete?.Invoke(this, EventArgs.Empty);
            }
            // NEW: if last anim but NOT the required chain → broadcast reset event
            else
            {
                ResetItems();
                OnWrongChainComplete?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private StepRef? FindStep(string visualId, string animName)
    {
        for (int i = 0; i < allSteps.Count; i++)
        {
            var sr = allSteps[i];
            var s = sr.step;
            if (s.triggerVisualId == visualId && s.triggerAnimation == animName)
                return sr;
        }
        return null;
    }

    // -------- movement (linear) --------
    private IEnumerator RunMovesSequential(ActionChainListSO.Step step)
    {
        if (step.moves == null || step.moves.Length == 0) yield break;

        for (int i = 0; i < step.moves.Length; i++)
        {
            var m = step.moves[i];
            if (m == null) continue;

            // resolve visual
            if (!visualById.TryGetValue(m.targetVisualId, out var vis) || vis?.Rect == null)
                continue;

            // resolve anchor
            if (string.IsNullOrEmpty(m.anchorId) || !anchorById.TryGetValue(m.anchorId, out var anchor) || !anchor)
                continue;

            // convert anchor world position -> parent local (anchored) space
            var parent = vis.Rect.parent as RectTransform;
            if (!parent) continue;

            Vector3 world = anchor.position;
            Vector3 local = parent.InverseTransformPoint(world);
            Vector2 targetAP = new Vector2(local.x, local.y);

            // linear tween (no ease)
            float duration = Mathf.Max(0f, m.duration);
            Vector2 from = vis.Rect.anchoredPosition;

            if (duration <= 0f)
            {
                vis.Rect.anchoredPosition = targetAP;
                continue;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                vis.Rect.anchoredPosition = Vector2.LerpUnclamped(from, targetAP, u);
                yield return null;
            }
            vis.Rect.anchoredPosition = targetAP;
        }
    }

    // Optional manual reset (kept for buttons/debug etc.)
    public void ResetItems()
    {
        foreach (var it in itemsToReset)
        {
            it.ResetItem();

        }
    }
}
