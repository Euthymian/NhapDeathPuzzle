using UnityEngine;
using UnityEngine.UI;

public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    public int rows = 8, cols = 8;
    public float cellSize = 110f;
    public RectTransform gridRoot;
    public GameObject cellPrefab;

    [Header("Colors")]
    public Color emptyColor = new Color(1f, 1f, 1f, 0.08f);
    public Color occupiedRed = Color.red; // current single color

    // State + visuals
    public int[,] board;        // 0 = empty, >0 = occupied (colorId)
    private Image[,] tiles;     // cached cell Images

    void Awake()
    {
        board = new int[rows, cols];
        tiles = new Image[rows, cols];
        BuildGrid();
    }

    void BuildGrid()
    {
        gridRoot.sizeDelta = new Vector2(cols * cellSize, rows * cellSize);

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var go = Instantiate(cellPrefab, gridRoot);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); // top-left
                float x = c * cellSize + cellSize * 0.5f;
                float y = -(r * cellSize + cellSize * 0.5f);
                rt.anchoredPosition = new Vector2(x, y);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize);

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = emptyColor;
                tiles[r, c] = img;
            }
    }

    public bool InBounds(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;
    public bool IsFree(int r, int c) => InBounds(r, c) && board[r, c] == 0;

    public void SetCell(int r, int c, int val)
    {
        if (!InBounds(r, c)) return;
        board[r, c] = val;
        UpdateCellVisual(r, c);
    }

    public void UpdateCellVisual(int r, int c)
    {
        if (!InBounds(r, c)) return;
        var img = tiles[r, c];
        if (!img) return;

        img.color = (board[r, c] == 0) ? emptyColor : occupiedRed;
    }

    public void RedrawAll()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                UpdateCellVisual(r, c);
    }
}
