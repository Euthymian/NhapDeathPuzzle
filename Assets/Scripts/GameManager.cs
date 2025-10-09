using JetBrains.Annotations;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private LevelManager levelManager;
    [SerializeField] private Canvas canvas;

    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject settingPanel;

    [SerializeField] private GameObject winPanel;
    [SerializeField] private Button nextButton;

    [SerializeField] private RawImage finishedImage;

    [Header("UI Level Capture (entire scene is UI)")]
    [Tooltip("Canvas that contains the entire level UI (currently Screen Space - Overlay).")]
    private Canvas levelCanvas;
    [Tooltip("Top RectTransform under the canvas that encloses the whole level (big square root).")]
    private RectTransform sceneRoot;
    [Tooltip("Square output size.")]
    [SerializeField] private int rtSize = 2048;
    [Tooltip("Extra margin around the bounds (0.05 = 5%).")]
    [SerializeField, Range(0f, 0.5f)] private float paddingPercent = 0.05f;
    [Tooltip("Pixels per 1 world unit while temporarily in World Space.")]
    [SerializeField] private float pixelsPerWorldUnit = 100f;


    [SerializeField] private int currentLevel;

    private void Awake()
    {
        Init(currentLevel);
        levelCanvas = canvas;
        nextButton.gameObject.SetActive(false);
        nextButton.onClick.AddListener(() =>
        {
            currentLevel++;
            Destroy(levelManager.gameObject);
            Init(currentLevel);
        });

        settingButton.onClick.AddListener(() =>
        {
            settingPanel.SetActive(true);
        });
    }

    private void Init(int level)
    {
        if (finishedImage != null && finishedImage.texture is RenderTexture rt)
        {
            finishedImage.texture = null;
            if (rt.IsCreated()) rt.Release();
            Destroy(rt);
        }

        winPanel.SetActive(false);
        nextButton.gameObject.SetActive(false);

        GameObject levelGameObject = Resources.Load<GameObject>($"Level{level}");
        Transform levelTransform = Instantiate(levelGameObject, canvas.transform).transform;
        levelTransform.localPosition = Vector3.zero;
        levelTransform.SetAsFirstSibling();

        sceneRoot = levelTransform.GetComponent<RectTransform>();

        levelManager = FindObjectOfType<LevelManager>();
        levelManager.OnLevelComplete += HandleLevelComplete;
    }

    private void OnDestroy()
    {
        // 1) Unsubscribe from LevelManager event safely
        if (levelManager != null)
            levelManager.OnLevelComplete -= HandleLevelComplete;

        // 2) Remove button listeners to avoid dangling delegates
        if (nextButton != null)
            nextButton.onClick.RemoveAllListeners();
        if (settingButton != null)
            settingButton.onClick.RemoveAllListeners();

        // 3) Release the captured RenderTexture if we created one
        if (finishedImage != null && finishedImage.texture is RenderTexture rt)
        {
            finishedImage.texture = null;
            if (rt.IsCreated()) rt.Release();
            Destroy(rt);
        }

        // 4) Stop any pending coroutines started by this Mono
        StopAllCoroutines();
    }


    private void HandleLevelComplete(object _, System.EventArgs __)
    {
        StartCoroutine(CaptureFullUI_Co());

    }

    private IEnumerator CaptureFullUI_Co()
    {
        if (!levelCanvas || !sceneRoot || !finishedImage)
            yield break;

        // --- Save original canvas state ---
        var origMode = levelCanvas.renderMode;
        var origWorldCam = levelCanvas.worldCamera;
        var origPlaneDist = levelCanvas.planeDistance;
        var origPos = levelCanvas.transform.position;
        var origRot = levelCanvas.transform.rotation;
        var origScale = levelCanvas.transform.localScale;

        settingButton.gameObject.SetActive(false);

        Canvas.ForceUpdateCanvases();

        // --- Compute bounds of the entire level (in canvas local space) ---
        var localBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(levelCanvas.transform, sceneRoot);
        var pad = Mathf.Max(0f, paddingPercent);
        var paddedSize = localBounds.extents * (1f + pad * 2f);
        var paddedCenter = localBounds.center;


        // --- Switch to WORLD SPACE temporarily ---
        levelCanvas.renderMode = RenderMode.WorldSpace;
        levelCanvas.transform.position = Vector3.zero;
        levelCanvas.transform.rotation = Quaternion.identity;
        levelCanvas.transform.localScale = Vector3.one / Mathf.Max(1f, pixelsPerWorldUnit);
        Canvas.ForceUpdateCanvases();

        // Convert canvas-local (pixels) to world using our scale
        var worldCenter = canvas.transform.TransformPoint(new Vector3(paddedCenter.x, paddedCenter.y, 0f) / pixelsPerWorldUnit);
        var worldHalfW = (paddedSize.x * 0.5f) / pixelsPerWorldUnit;
        var worldHalfH = (paddedSize.y * 0.5f) / pixelsPerWorldUnit;


        // --- Create TEMP ortho camera + RT (no scene changes kept) ---
        var camGO = new GameObject("~TempUICaptureCam");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // transparent
        cam.nearClipPlane = -100f;
        cam.farClipPlane = 100f;
        cam.orthographicSize = Mathf.Max(worldHalfH, worldHalfW);
        camGO.transform.position = new Vector3(worldCenter.x, worldCenter.y, -10f);
        camGO.transform.rotation = Quaternion.identity;

        var rt = new RenderTexture(rtSize, rtSize, 24, RenderTextureFormat.ARGB32) { name = "~UIWholeRT" };
        rt.Create();
        cam.targetTexture = rt;

        // --- URP/Built-in safe one-frame render ---
        cam.enabled = true;
        yield return new WaitForEndOfFrame();
        cam.enabled = false;

        cam.targetTexture = null;
        Destroy(camGO);

        // --- Push to RawImage (on your Overlay win panel) ---
        finishedImage.texture = rt;
        finishedImage.color = Color.white;
        finishedImage.uvRect = new Rect(0, 0, 1, 1);
        finishedImage.gameObject.SetActive(true);

        // --- Restore canvas exactly as it was ---
        levelCanvas.renderMode = origMode;
        levelCanvas.worldCamera = origWorldCam;
        levelCanvas.planeDistance = origPlaneDist;
        levelCanvas.transform.position = origPos;
        levelCanvas.transform.rotation = origRot;
        levelCanvas.transform.localScale = origScale;

        //Canvas.ForceUpdateCanvases(); // optional

        winPanel.SetActive(true);
        nextButton.gameObject.SetActive(true);

        settingButton.gameObject.SetActive(true);

        finishedImage.GetComponent<Animator>().Play("CaptureDeath");
    }
}
