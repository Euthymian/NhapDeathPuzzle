using System;
using Spine.Unity;
using UnityEngine;

[RequireComponent(typeof(SkeletonGraphic))]
public class Visual : MonoBehaviour
{
    [Header("ID")]
    public string visualId;               

    [Header("Idle")]
    [SerializeField] private bool playIdleOnComplete = true;
    [SerializeField] private string idleAnimationName = "idle";

    private SkeletonGraphic skeletonGraphic;

    public event EventHandler<AnimationCompleteEventArgs> OnAnimationCompleteEvent;
    public class AnimationCompleteEventArgs : EventArgs
    {
        public string animationName;
        public Visual visual;
    }

    private void Awake()
    {
        skeletonGraphic = GetComponent<SkeletonGraphic>();
        skeletonGraphic.AnimationState.Complete += HandleComplete;
    }

    public void PlayAnim(string animationName, bool loop)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        skeletonGraphic.AnimationState.SetAnimation(0, animationName, loop);
    }

    private void HandleComplete(Spine.TrackEntry entry)
    {
        var name = entry?.Animation?.Name;

        if (playIdleOnComplete && !string.IsNullOrEmpty(idleAnimationName) && name != idleAnimationName)
            skeletonGraphic.AnimationState.SetAnimation(0, idleAnimationName, true);

        OnAnimationCompleteEvent?.Invoke(this, new AnimationCompleteEventArgs
        {
            animationName = name,
            visual = this
        });
    }

    private void OnDestroy()
    {
        if (skeletonGraphic != null)
            skeletonGraphic.AnimationState.Complete -= HandleComplete;
    }
}

