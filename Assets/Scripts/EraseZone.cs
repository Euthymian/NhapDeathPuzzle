using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform)), RequireComponent(typeof(RawImage))]
public class EraseZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Trigger (first animation)")]
    [SerializeField] private Visual targetVisual;
    [SerializeField] private string animationToTrigger = "EraseStart";
    [Range(0.05f, 1f)] public float triggerPercent = 0.9f;

    [Header("Brush (texture pixels)")]
    [Min(2)] public int brushRadius = 80;

    [Header("Mask build (eligible pixels)")]
    [Range(0f, 1f)] public float alphaThreshold = 0.5f;

    RawImage raw;
    RectTransform rect;
    Texture2D paintTex;
    Color32[] pixelBuffer, originalPixels;
    int texW, texH;

    // Masks as arrays (much faster than Dictionary)
    int[] eligible;      // 1 if this pixel can be erased (alpha >= threshold)
    int[] paintedFlags;  // 0->1 atomically when first erased
    int totalEligible;   // denominator
    int erasedCount;     // incremented atomically by worker

    // Worker thread infra
    struct BrushOp { public int x, y, r; }
    readonly ConcurrentQueue<BrushOp> queue = new ConcurrentQueue<BrushOp>();
    Thread worker;
    volatile bool run;
    volatile bool bufferDirty;    // set by worker when it changed pixelBuffer

    // Drag state
    bool dragging;
    Vector2 lastLocal;
    Camera uiCam = null; // overlay

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        raw = GetComponent<RawImage>();
        if (!raw.raycastTarget) raw.raycastTarget = true;

        var src = raw.texture as Texture2D;
        paintTex = DuplicateReadable(src);
        if (!paintTex) { Debug.LogError("EraseZone: Texture must be readable."); enabled = false; return; }
        raw.texture = paintTex;

        texW = paintTex.width; texH = paintTex.height;
        originalPixels = paintTex.GetPixels32();
        pixelBuffer = (Color32[])originalPixels.Clone();

        // Build masks
        BuildMasks(originalPixels, alphaThreshold);

        // Initial upload
        paintTex.SetPixels32(pixelBuffer);
        paintTex.Apply(false, false);

        // Start worker thread
        run = true;
        worker = new Thread(WorkerLoop) { IsBackground = true, Name = "EraseZoneWorker" };
        worker.Start();
    }

    void OnDestroy()
    {
        run = false;
        if (worker != null && worker.IsAlive) worker.Join();
    }

    void LateUpdate()
    {
        // Upload at most once per frame if worker changed buffer
        if (bufferDirty)
        {
            bufferDirty = false;
            paintTex.SetPixels32(pixelBuffer);
            paintTex.Apply(false, false);
        }
    }

    // --- Pointer ---
    public void OnPointerDown(PointerEventData e)
    {
        dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, uiCam, out lastLocal);
        if (!dragging) return;
        EnqueueBrushAtLocal(lastLocal);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, uiCam, out var lp)) return;

        float seg = Vector2.Distance(lastLocal, lp);
        int steps = Mathf.Max(1, Mathf.CeilToInt(seg / (brushRadius * 0.5f)));
        for (int i = 1; i <= steps; i++)
            EnqueueBrushAtLocal(Vector2.Lerp(lastLocal, lp, i / (float)steps));

        lastLocal = lp;

        float progress = totalEligible > 0 ? (float)Volatile.Read(ref erasedCount) / totalEligible : 0f;
        if (progress >= triggerPercent && targetVisual)
            targetVisual.PlayAnim(animationToTrigger, false);
    }

    public void OnPointerUp(PointerEventData e)
    {
        dragging = false;
        float progress = totalEligible > 0 ? (float)Volatile.Read(ref erasedCount) / totalEligible : 0f;
        if (progress >= triggerPercent && targetVisual)
            targetVisual.PlayAnim(animationToTrigger, false);
    }

    // --- Reset (fast, no stall) ---
    public void ResetZone()
    {
        // Clear worker queue
        while (queue.TryDequeue(out _)) { }

        // Reset CPU buffer
        System.Array.Copy(originalPixels, pixelBuffer, pixelBuffer.Length);

        // Reset masks/counters
        System.Array.Clear(paintedFlags, 0, paintedFlags.Length);
        Interlocked.Exchange(ref erasedCount, 0);

        bufferDirty = true; // trigger one upload in LateUpdate
    }

    // ================== Worker & helpers ==================

    void WorkerLoop()
    {
        while (run)
        {
            if (!queue.TryDequeue(out var op))
            {
                Thread.Sleep(0); // yield
                continue;
            }

            int r2 = op.r * op.r;

            int minY = Mathf.Max(0, op.y - op.r);
            int maxY = Mathf.Min(texH - 1, op.y + op.r);
            int minX = Mathf.Max(0, op.x - op.r);
            int maxX = Mathf.Min(texW - 1, op.x + op.r);

            int newlyErased = 0;

            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - op.y;
                int row = y * texW;

                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - op.x;
                    if (dx * dx + dy * dy > r2) continue;

                    int idx = row + x;

                    if (eligible[idx] == 0) continue; // not part of erasable area

                    // Set alpha to 0 in buffer
                    var c = pixelBuffer[idx];
                    if (c.a != 0) { c.a = 0; pixelBuffer[idx] = c; }

                    // First-time mark: atomically flip 0->1
                    if (Interlocked.Exchange(ref paintedFlags[idx], 1) == 0)
                        newlyErased++;
                }
            }

            if (newlyErased > 0)
            {
                Interlocked.Add(ref erasedCount, newlyErased);
                bufferDirty = true; // tell main thread to upload this frame
            }
        }
    }

    void EnqueueBrushAtLocal(Vector2 localPos)
    {
        // local -> normalized
        Rect rr = rect.rect;
        float nx = (localPos.x - rr.x) / rr.width;
        float ny = (localPos.y - rr.y) / rr.height;

        // uvRect aware
        var uv = raw.uvRect;
        float u = uv.x + nx * uv.width;
        float v = uv.y + ny * uv.height;

        int cx = Mathf.RoundToInt(u * texW);
        int cy = Mathf.RoundToInt(v * texH);
        if (cx < 0 || cx >= texW || cy < 0 || cy >= texH) return;

        queue.Enqueue(new BrushOp { x = cx, y = cy, r = brushRadius });
    }

    void BuildMasks(Color32[] src, float thr01)
    {
        int n = src.Length;
        eligible = new int[n];
        paintedFlags = new int[n];
        totalEligible = 0;
        byte thr = (byte)Mathf.Clamp(Mathf.RoundToInt(thr01 * 255f), 0, 255);

        for (int i = 0; i < n; i++)
        {
            if (src[i].a >= thr)
            {
                eligible[i] = 1;
                totalEligible++;
            }
        }
        erasedCount = 0;
    }

    Texture2D DuplicateReadable(Texture2D src)
    {
        if (!src) return null;
        try
        {
            var px = src.GetPixels32();
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            copy.SetPixels32(px); copy.Apply(false, false);
            return copy;
        }
        catch { return null; }
    }
}
