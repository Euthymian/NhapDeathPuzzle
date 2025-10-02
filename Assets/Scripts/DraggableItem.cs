using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Item Data")]
    public string itemName;               // e.g., "Coc", "Keo"
    public string animationToTrigger;     // e.g., "Cut"

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas canvas;

    //private Transform originalParent;
    private Vector2 originalPosition;
    private bool droppedOnZone;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        //originalParent = transform.parent;
        originalPosition = rectTransform.anchoredPosition;
        droppedOnZone = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        canvasGroup.blocksRaycasts = false;
        droppedOnZone = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (!droppedOnZone)
        {
            //Di chuyen tu tu ve vi tri ban dau, tam thoi kh di chuyen
            rectTransform.anchoredPosition = originalPosition;
        }
        else
        {
            gameObject.SetActive(false);
            rectTransform.anchoredPosition = originalPosition;
        }
    }

    public void MarkAsDropped()
    {
        droppedOnZone = true;
    }

    public void ResetItem()
    {
        gameObject.SetActive(true);
        //transform.SetParent(originalParent);
        rectTransform.anchoredPosition = originalPosition;
        droppedOnZone = false;
    }
}
