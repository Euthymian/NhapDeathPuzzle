using System;
using Spine.Unity;
using UnityEngine;

[RequireComponent(typeof(SkeletonGraphic), typeof(RectTransform))]
public class Visual : MonoBehaviour
{
    [Header("ID")]
    public string visualId; // unique per visual in the level

    [SerializeField] private string targetAnimationName;
    [SerializeField] private string defaultAnimationName;

    public RectTransform Rect { get; private set; }
    private SkeletonGraphic skeletonGraphic;

    // Events
    public class AnimationStartEventArgs : EventArgs
    {
        public string animationName;
        public Visual visual;
    }
    public class AnimationCompleteEventArgs : EventArgs
    {
        public string animationName;
        public Visual visual;
    }

    public event EventHandler<AnimationStartEventArgs> OnAnimationStartEvent;
    public event EventHandler<AnimationCompleteEventArgs> OnAnimationCompleteEvent;

    private void Awake()
    {
        skeletonGraphic = GetComponent<SkeletonGraphic>();
        Rect = GetComponent<RectTransform>();

        // Subscribe to Spine events
        skeletonGraphic.AnimationState.Start += HandleStart;
        skeletonGraphic.AnimationState.Complete += HandleComplete;
    }

    public void PlayAnim(string animationName, bool loop)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        skeletonGraphic.timeScale = 1f; // reset speed
        skeletonGraphic.AnimationState.SetAnimation(0, animationName, loop);
    }

    private void HandleStart(Spine.TrackEntry entry)
    {
        var name = entry?.Animation?.Name ?? "";
        OnAnimationStartEvent?.Invoke(this, new AnimationStartEventArgs
        {
            animationName = name,
            visual = this
        });
    }

    private void HandleComplete(Spine.TrackEntry entry)
    {
        var name = entry?.Animation?.Name ?? "";

        OnAnimationCompleteEvent?.Invoke(this, new AnimationCompleteEventArgs
        {
            animationName = name,
            visual = this
        });
    }

    public void ResetState()
    {
        if(skeletonGraphic != null) 
            skeletonGraphic.AnimationState.SetAnimation(0, defaultAnimationName, true);
    }

    // NEW: listen for LevelManager’s wrong-chain event and reset to default
    public void HandleWrongChainReset(object sender, EventArgs e)
    {
        ResetState();
    }

    private void OnDestroy()
    {
        if (skeletonGraphic != null)
        {
            skeletonGraphic.AnimationState.Start -= HandleStart;
            skeletonGraphic.AnimationState.Complete -= HandleComplete;
        }

        // Defensive: detach from LevelManager's event if still present
        var lm = FindObjectOfType<LevelManager>();
        if (lm != null)
            lm.OnWrongChainComplete -= HandleWrongChainReset;
    }
}
