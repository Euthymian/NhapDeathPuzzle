using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Put this on the SAME GameObject as the RawImage you want to erase (Screen Space – Overlay).
/// When enough of the eligible pixels are erased, it triggers the first animation on targetVisual.
/// LevelManager continues the chain via Visual events.
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(RawImage))]
public class EraseZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Trigger (first animation)")]
    [SerializeField] private Visual targetVisual;
    [SerializeField] private string animationToTrigger = "EraseStart";
    [Range(0.05f, 1f)] public float triggerPercent = 0.9f;   // match your Dishes 90% default

    [Header("Brush (texture pixels)")]
    [Min(2)] public int brushRadius = 32;

    [Header("Mask build (eligible pixels)")]
    [Range(0f, 1f)] public float alphaThreshold = 0.5f;

    // Internals (same pattern as Dishes)
    private RawImage targetImage;
    private RectTransform rect;
    private Texture2D paintTex;
    private Dictionary<Vector2Int, bool> paintCache = new(); // eligible pixels & painted flag
    private int texWidth, texHeight, totalEligible;
    private Color32[] pixelBuffer;
    private Color32[] originalPixels;

    private bool drawing;
    private Vector2 lastLocal;  // last pointer position in local rect space
    private int erasedCount;    // how many eligible pixels have been painted (first time)
    private Camera uiCam = null; // Overlay => null

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        targetImage = GetComponent<RawImage>();
        if (!targetImage.raycastTarget) targetImage.raycastTarget = true; // must receive pointer events

        // Make writable copy of the RawImage texture
        var src = targetImage.texture as Texture2D;
        if(src != null)
        {
            Debug.Log(src.name);
        }
        paintTex = DuplicateReadable(src);
        if (paintTex == null)
        {
            Debug.LogError("[EraseZone] RawImage.texture must be a readable Texture2D.");
            enabled = false;
            return;
        }
        targetImage.texture = paintTex;

        texWidth = paintTex.width;
        texHeight = paintTex.height;

        originalPixels = paintTex.GetPixels32();
        pixelBuffer = (Color32[])originalPixels.Clone();

        // Build eligible mask (alpha >= threshold)
        BuildPaintCacheFromAlpha(originalPixels, alphaThreshold);

        // Ensure texture matches buffer
        paintTex.SetPixels32(pixelBuffer);
        paintTex.Apply(false, false);

        erasedCount = 0;
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
        erasedCount = 0;
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

        float progress = totalEligible > 0 ? (float)erasedCount / totalEligible : 0f;
        if (progress >= triggerPercent && targetVisual)
        {
            targetVisual.PlayAnim(animationToTrigger, false);
        }
    }

    // ===== Core erase (same logic as Dishes) =====
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

        return DrawBrush(texX, texY, brushRadius);
    }

    int DrawBrush(int centerX, int centerY, int radius)
    {
        int added = 0;
        int sqrR = radius * radius;

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
                if (paintCache.TryGetValue(key, out bool alreadyErased))
                {
                    int idx = y * texWidth + x;

                    // Erase in buffer
                    var c = pixelBuffer[idx];
                    if (c.a != 0) { c.a = 0; pixelBuffer[idx] = c; }

                    // Count first-time erase
                    if (!alreadyErased)
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

        erasedCount += addAmount;

        // Push buffer to texture (you can throttle if needed)
        paintTex.SetPixels32(pixelBuffer);
        paintTex.Apply(false, false);
    }

    void BuildPaintCacheFromAlpha(Color32[] src, float threshold)
    {
        paintCache.Clear();
        totalEligible = 0;

        byte thr = (byte)Mathf.Clamp(Mathf.RoundToInt(threshold * 255f), 0, 255);

        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                int idx = y * texWidth + x;
                if (src[idx].a >= thr)
                {
                    paintCache[new Vector2Int(x, y)] = false; // eligible & not erased yet
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
