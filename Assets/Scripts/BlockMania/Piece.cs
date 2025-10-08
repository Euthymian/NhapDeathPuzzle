using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Piece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Shape + Visuals")]
    public ShapeData data;                 // set by spawner
    public GameObject tilePrefab;          // UI Image prefab for one cell
    public RectTransform visualRoot;       // where tiles spawn
    public float cellSize = 110f;

    [HideInInspector] public System.Action<Piece> OnConsumed; // spawner hook

    // runtime refs
    private RectTransform rect;
    private RectTransform originalParent;
    private RectTransform dragLayer;       // full-screen RectTransform under Canvas
    private Canvas canvas;
    private GridManager grid;

    // drag state
    private Vector2 dragOffsetLocal;       // pointer -> piece offset in dragLayer space
    private Vector2 lastPointerScreenPos;

    void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        grid = FindObjectOfType<GridManager>();

        NormalizeRect(rect);
        if (visualRoot) NormalizeRect(visualRoot);

        var layerGO = GameObject.Find("DragLayer");
        dragLayer = layerGO ? layerGO.GetComponent<RectTransform>()
                            : canvas.GetComponent<RectTransform>();

        // ensure root has a raycastable Graphic for events
        var img = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        if (img.color.a < 0.02f) img.color = new Color(1f, 1f, 1f, 0.01f);
        img.raycastTarget = true;

        //BuildVisual();
    }

    static void NormalizeRect(RectTransform r)
    {
        var world = r.position;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.position = world;
    }

    // Resize hitbox to shape bounds, center visuals, keep tiles non-raycast
    public void BuildVisual()
    {
        foreach (Transform child in visualRoot) Destroy(child.gameObject);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in data.cells)
        {
            var go = Instantiate(tilePrefab, visualRoot);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = new Vector2(cell.x * cellSize, -cell.y * cellSize);

            var img = go.GetComponent<Image>();
            if (img) img.raycastTarget = false;

            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        // center visuals
        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        visualRoot.anchoredPosition = new Vector2(-cx * cellSize, +cy * cellSize);

        // resize root rect so entire piece is clickable
        int wCells = (maxX - minX + 1);
        int hCells = (maxY - minY + 1);
        rect.sizeDelta = new Vector2(wCells * cellSize, hCells * cellSize);
    }

    // Set a new pivot but keep the piece visually in the same place
    static void SetPivotKeepingPosition(RectTransform rt, Vector2 newPivot)
    {
        if (!rt) return;
        Vector2 size = rt.rect.size;
        Vector2 deltaPivot = newPivot - rt.pivot;
        Vector2 delta = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        rt.pivot = newPivot;
        rt.anchoredPosition += delta;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        originalParent = (RectTransform)rect.parent;

        // keep world pos on reparent to avoid jump
        rect.SetParent(dragLayer, worldPositionStays: true);

        var cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        // pivot follows where you grabbed
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, cam, out var localInRect);
        Vector2 size = rect.rect.size;
        float px = Mathf.Clamp01((localInRect.x + size.x * 0.5f) / size.x);
        float py = Mathf.Clamp01((localInRect.y + size.y * 0.5f) / size.y);
        SetPivotKeepingPosition(rect, new Vector2(px, py));

        // compute pointer offset in dragLayer space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, cam, out var pointerLocal);
        dragOffsetLocal = rect.anchoredPosition - pointerLocal;

        lastPointerScreenPos = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        lastPointerScreenPos = e.position;
        var cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, cam, out var pointerLocal);
        rect.anchoredPosition = pointerLocal + dragOffsetLocal;

        // preview: alpha = 1 if fully placeable by current visuals
        SetPreviewAlpha(TryGetShownCells(out _));
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (TryGetShownCells(out var cells))
        {
            PlaceShownCells(cells);
            OnConsumed?.Invoke(this);
            gameObject.SetActive(false);
        }
        else
        {
            rect.SetParent(originalParent, worldPositionStays: false);
            rect.anchoredPosition = Vector2.zero;
            SetPreviewAlpha(true);
        }
    }

    // fade only alpha for invalid preview
    void SetPreviewAlpha(bool ok)
    {
        foreach (Transform t in visualRoot)
        {
            var img = t.GetComponent<Image>();
            if (!img) continue;
            var c = img.color;
            c.a = ok ? 1f : 0.5f;
            img.color = c;
        }
    }

    // Use where the tiles actually are on screen to compute target cells
    bool TryGetShownCells(out List<Vector2Int> cells)
    {
        cells = new List<Vector2Int>();
        var cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
        RectTransform gridRT = grid.gridRoot;

        var seen = new HashSet<Vector2Int>();

        for (int i = 0; i < visualRoot.childCount; i++)
        {
            var tileRT = (RectTransform)visualRoot.GetChild(i);

            // world → screen
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, tileRT.position);
            // screen → grid local
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRT, screen, cam, out var gridLocal);

            // top-left metric independent of pivot
            float xTL = gridLocal.x + gridRT.rect.width * gridRT.pivot.x;
            float yTL = -(gridLocal.y - gridRT.rect.height * (1f - gridRT.pivot.y));

            // index by tile center (round to nearest)
            float fx = (xTL / cellSize) - 0.5f;
            float fy = (yTL / cellSize) - 0.5f;
            int col = Mathf.RoundToInt(fx);
            int row = Mathf.RoundToInt(fy);

            var rc = new Vector2Int(row, col);
            if (!grid.InBounds(row, col)) return false;     // out of board
            if (!seen.Add(rc)) continue;                    // dedupe overlaps
            if (!grid.IsFree(row, col)) return false;       // occupied
            cells.Add(rc);
        }
        return true;
    }

    void PlaceShownCells(List<Vector2Int> cells)
    {
        var seen = new HashSet<Vector2Int>();
        foreach (var rc in cells)
        {
            if (!seen.Add(rc)) continue;
            grid.SetCell(rc.x, rc.y, data.colorId);
        }
        var logic = FindObjectOfType<BoardLogic>();
        if (logic != null) logic.ResolveClears();
    }
}
