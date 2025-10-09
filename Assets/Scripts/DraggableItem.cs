using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform)), RequireComponent(typeof(CanvasGroup))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Item Data")]
    public string itemName;
    public string animationToTrigger;

    [Header("Follow")]
    [SerializeField, Min(10f)] float followSpeedPxPerSec = 2000; // tune 1500–3000 for feel
    [SerializeField] bool useUnscaledTime = true;

    RectTransform rectTransform, parentRect;
    CanvasGroup canvasGroup;

    Vector2 originalPosition;
    Vector2 targetAnchored;
    Vector2 grabOffset;
    bool dragging, droppedOnZone;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentRect = rectTransform.parent as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();

        originalPosition = rectTransform.anchoredPosition;
        targetAnchored = originalPosition;
    }

    void Update()
    {
        if (!dragging) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float step = followSpeedPxPerSec * Mathf.Max(0f, dt);

        rectTransform.anchoredPosition = targetAnchored;
        //rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetAnchored, Mathf.Clamp01(step / Vector2.Distance(rectTransform.anchoredPosition, targetAnchored)));
        //Vector2.MoveTowards(rectTransform.anchoredPosition, targetAnchored, step);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        dragging = true;
        droppedOnZone = false;
        originalPosition = rectTransform.anchoredPosition;

        canvasGroup.blocksRaycasts = false; // let DropZones receive drops

        // Overlay canvas => camera is null
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, e.position, null, out var local);
        grabOffset = local - rectTransform.anchoredPosition;
        targetAnchored = local - grabOffset; // start chasing from first frame
    }

    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, e.position, null, out var local);
        targetAnchored = local - grabOffset; // update target; movement happens in Update()
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        canvasGroup.blocksRaycasts = true;

        if (!droppedOnZone)
        {
            // glide back using deltaTime
            StopAllCoroutines();
            StartCoroutine(SnapBack(originalPosition, 0.2f)); // quick 0.2s return
        }
        else
        {
            gameObject.SetActive(false);
            rectTransform.anchoredPosition = originalPosition; // prep for next time
        }
    }

    public void MarkAsDropped() => droppedOnZone = true;

    public void ResetItem()
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        rectTransform.anchoredPosition = originalPosition;
        targetAnchored = originalPosition;
        droppedOnZone = false;
        dragging = false;
        canvasGroup.blocksRaycasts = true;
    }

    System.Collections.IEnumerator SnapBack(Vector2 to, float duration)
    {
        Vector2 from = rectTransform.anchoredPosition;
        duration = Mathf.Max(0f, duration);
        float t = 0f;
        while (t < duration)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, u);
            yield return null;
        }
        rectTransform.anchoredPosition = to;
        targetAnchored = to;
    }
}
