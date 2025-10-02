using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    [Header("IDs")]
    public string zoneId;

    [Header("Rules")]
    [SerializeField] private List<string> acceptedItemNames = new();

    [Header("Target Visual")]
    [SerializeField] private Visual visual;  

    //public event EventHandler<ItemDroppedEventArgs> OnItemDropped;
    //public class ItemDroppedEventArgs : EventArgs
    //{
    //    public DraggableItem droppedItem;
    //    public DropZone dropZone;
    //}

    public bool Accepts(DraggableItem item)
        => item != null && acceptedItemNames.Contains(item.itemName);

    public void OnDrop(PointerEventData eventData)
    {
        var draggable = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<DraggableItem>() : null;
        if (draggable == null) return;

        if (Accepts(draggable))
        {
            draggable.MarkAsDropped();

            if (visual && !string.IsNullOrEmpty(draggable.animationToTrigger))
                visual.PlayAnim(draggable.animationToTrigger, false);

            //OnItemDropped?.Invoke(this, new ItemDroppedEventArgs
            //{
            //    droppedItem = draggable,
            //    dropZone = this
            //});
        }
    }
}