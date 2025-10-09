using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Put this on the SAME GameObject as the RawImage you want to draw on (Screen Space – Overlay).
/// It "paints" into a runtime Texture2D. When painted coverage >= triggerPercent,
/// it plays the first animation on targetVisual; LevelManager continues the chain via Visual events.
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(RawImage))]
public class DrawZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Trigger (first animation)")]
    [SerializeField] private Visual targetVisual;
    [SerializeField] private string animationToTrigger = "DrawStart";
    [Range(0.05f, 1f)] public float triggerPercent = 0.9f; // e.g., 90% like your Dishes

    [Header("Object to instantiate")]
    [SerializeField] private GameObject objectToInstantiate;
    [SerializeField] private RectTransform posToInstantiate;

    [Header("Brush (texture pixels)")]
    [Min(2)] public int brushRadius = 32;
    public Color brushColor = Color.white;

    [Header("Where is drawing allowed?")]
    [Tooltip("If true, pixels with alpha < alphaThreshold are eligible (i.e., transparent areas). If false, alpha >= threshold (opaque areas).")]
    public bool paintWhereTransparent = true;
    [Range(0f, 1f)] public float alphaThreshold = 0.5f;

    // Internals (mirrors your Dishes/EraseZone pattern)
    private RawImage targetImage;
    private RectTransform rect;
    private Texture2D paintTex;                  // runtime writable copy
    private Dictionary<Vector2Int, bool> paintCache = new(); // eligible pixels & painted flag
    private int texWidth, texHeight, totalEligible;
    private Color32[] pixelBuffer;               // mutable CPU buffer
    private Color32[] originalPixels;            // for reset

    private bool drawing;
    private Vector2 lastLocal;                   // last pointer pos in local rect space
    private int paintedCount;                    // how many eligible pixels have been painted first-time
    private Camera uiCam = null;                 // Overlay => null

    private Texture2D originalTexture;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        targetImage = GetComponent<RawImage>();
        if (!targetImage.raycastTarget) targetImage.raycastTarget = true; // must receive pointer events

        // Make writable copy of the RawImage texture
        var src = targetImage.texture as Texture2D;
        originalTexture = paintTex = DuplicateReadable(src);
        if (paintTex == null)
        {
            Debug.LogError("[DrawZone] RawImage.texture must be a readable Texture2D.");
            enabled = false;
            return;
        }
        targetImage.texture = paintTex;

        texWidth = paintTex.width;
        texHeight = paintTex.height;

        originalPixels = paintTex.GetPixels32();
        pixelBuffer = (Color32[])originalPixels.Clone();

        // Build eligible mask based on alpha and mode
        BuildPaintCacheForDraw(originalPixels, alphaThreshold, paintWhereTransparent);

        // Ensure texture matches buffer
        paintTex.SetPixels32(pixelBuffer);
        paintTex.Apply(false, false);

        paintedCount = 0;
    }

    // ===== Public reset (call from LevelManager on wrong chain) =====
    public void ResetZone()
    {
        if (paintTex == null || originalPixels == null) return;

        System.Array.Copy(originalPixels, pixelBuffer, originalPixels.Length);
        paintTex.SetPixels32(pixelBuffer);
        paintTex.Apply(false, false);

        // reset flags & progress
        var keys = new List<Vector2Int>(paintCache.Keys);
        for (int i = 0; i < keys.Count; i++) paintCache[keys[i]] = false;
        paintedCount = 0;
    }

    // ===== Pointer =====
    public void OnPointerDown(PointerEventData e)
    {
        drawing = RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, uiCam, out lastLocal);
        if (drawing)
        {
            int add = DrawBrushAtLocal(lastLocal);
            AfterBrush(add);
        }
    }

    public void OnDrag(PointerEventData e)
    {
        if (!drawing) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, uiCam, out var lp)) return;

        // Interpolate to avoid gaps between frames
        float seg = Vector2.Distance(lastLocal, lp);
        int steps = Mathf.Max(1, Mathf.CeilToInt(seg / (brushRadius * 0.5f)));
        int added = 0;
        for (int i = 1; i <= steps; i++)
            added += DrawBrushAtLocal(Vector2.Lerp(lastLocal, lp, i / (float)steps));

        lastLocal = lp;
        AfterBrush(added);
    }

    public void OnPointerUp(PointerEventData e)
    {
        drawing = false;

        float progress = totalEligible > 0 ? (float)paintedCount / totalEligible : 0f;
        if (progress >= triggerPercent && targetVisual)
        {
            GameObject tmp = Instantiate(objectToInstantiate, posToInstantiate.position, Quaternion.identity, targetVisual.transform);
            tmp.transform.localScale = new Vector3(6,6,1);
            targetVisual.PlayAnim(animationToTrigger, false);
        }
        else
        {
            Debug.Log("Reset draw");
        }
    }

    // ===== Core draw (inverse of erase) =====
    // Map local UI point -> texture pixels
    int DrawBrushAtLocal(Vector2 localPos)
    {
        Rect rectPx = rect.rect;

        // local (rect space) -> normalized (0..1)
        float normX = (localPos.x - rectPx.x) / rectPx.width;
        float normY = (localPos.y - rectPx.y) / rectPx.height;

        // account for uvRect in RawImage (if changed)
        var uv = targetImage.uvRect; // default (0,0,1,1)
        float u = uv.x + normX * uv.width;
        float v = uv.y + normY * uv.height;

        int texX = Mathf.RoundToInt(u * texWidth);
        int texY = Mathf.RoundToInt(v * texHeight);

        if (texX < 0 || texX >= texWidth || texY < 0 || texY >= texHeight) return 0;

        return DrawBrush(texX, texY, brushRadius, brushColor);
    }

    // Circle brush in texture space; returns how many NEW eligible pixels were painted
    int DrawBrush(int centerX, int centerY, int radius, Color color)
    {
        int added = 0;
        int sqrR = radius * radius;
        Color32 col32 = color;

        int minY = Mathf.Max(0, centerY - radius);
        int maxY = Mathf.Min(texHeight - 1, centerY + radius);
        int minX = Mathf.Max(0, centerX - radius);
        int maxX = Mathf.Min(texWidth - 1, centerX + radius);

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy > sqrR) continue;

                var key = new Vector2Int(x, y);
                if (paintCache.TryGetValue(key, out bool alreadyPainted))
                {
                    int idx = y * texWidth + x;

                    // Paint in buffer (set color & ensure alpha = 255 so it's visible)
                    var c = pixelBuffer[idx];
                    c.r = col32.r; c.g = col32.g; c.b = col32.b; c.a = 255;
                    pixelBuffer[idx] = c;

                    // Count first-time paint
                    if (!alreadyPainted)
                    {
                        paintCache[key] = true;
                        added++;
                    }
                }
            }
        return added;
    }

    void AfterBrush(int addAmount)
    {
        if (addAmount <= 0) return;

        paintedCount += addAmount;

        // Push buffer to texture (you can throttle if needed)
        paintTex.SetPixels32(pixelBuffer);
        paintTex.Apply(false, false);
    }

    void BuildPaintCacheForDraw(Color32[] src, float threshold, bool whereTransparent)
    {
        paintCache.Clear();
        totalEligible = 0;

        byte thr = (byte)Mathf.Clamp(Mathf.RoundToInt(threshold * 255f), 0, 255);

        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                int idx = y * texWidth + x;
                byte a = src[idx].a;

                bool eligible = whereTransparent ? (a < thr) : (a >= thr);
                if (eligible)
                {
                    paintCache[new Vector2Int(x, y)] = false; // eligible & not painted yet
                    totalEligible++;
                }
            }
    }

    Texture2D DuplicateReadable(Texture2D src)
    {
        if (!src) return null;
        try
        {
            var px = src.GetPixels32();
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            copy.SetPixels32(px);
            copy.Apply(false, false);
            return copy;
        }
        catch
        {
            return null; // not readable
        }
    }
}
