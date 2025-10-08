using UnityEngine;

public class TraySpawner : MonoBehaviour
{
    [Header("Wiring")]
    public Piece piecePrefab;
    public Transform trayRoot;
    public GridManager grid;
    public BoardLogic board;          // assign BoardLogic on GridRoot
    public GameOverUI gameOverUI;     // assign the panel
    public GameObject tilePrefab;
    public float cellSize = 110f;

    [Header("Shape pool")]
    public ShapeData[] pool;

    void Start() => Refill();

    public void Refill()
    {
        ClearTray();
        for (int i = 0; i < 3; i++) SpawnOne();

        // If the *new* set has no fits → game over
        if (!AnyPieceCanFitNow()) GameOver();
    }

    void SpawnOne()
    {
        var p = Instantiate(piecePrefab, trayRoot);
        p.tilePrefab = tilePrefab;
        p.cellSize = cellSize;
        p.data = pool[Random.Range(0, pool.Length)];
        p.OnConsumed += HandlePieceConsumed;
        p.BuildVisual();
    }

    void HandlePieceConsumed(Piece _)
    {
        // If tray is empty, refill (this also checks game over)
        if (TrayIsEmpty()) Refill();
        else
            // If pieces remain but none can fit → game over now
            if (!AnyPieceCanFitNow()) GameOver();
    }

    public void CheckGameOverNow()
    {
        if (!AnyPieceCanFitNow()) GameOver();
    }

    bool AnyPieceCanFitNow()
    {
        for (int i = 0; i < trayRoot.childCount; i++)
        {
            var p = trayRoot.GetChild(i).GetComponent<Piece>();
            if (p && p.gameObject.activeSelf && FitTest.CanFit(grid, p.data))
                return true;
        }
        return false;
    }

    bool TrayIsEmpty()
    {
        if (trayRoot.childCount == 0) return true;
        for (int i = 0; i < trayRoot.childCount; i++)
            if (trayRoot.GetChild(i).gameObject.activeSelf) return false;
        return true;
    }

    void ClearTray()
    {
        for (int i = trayRoot.childCount - 1; i >= 0; i--)
            Destroy(trayRoot.GetChild(i).gameObject);
    }

    void GameOver()
    {
        // disable remaining pieces’ interaction
        for (int i = 0; i < trayRoot.childCount; i++)
            trayRoot.GetChild(i).gameObject.SetActive(false);

        gameOverUI.Show(board.score, RestartGame);
    }

    void RestartGame()
    {
        // reset board state & visuals
        for (int r = 0; r < grid.rows; r++)
            for (int c = 0; c < grid.cols; c++)
                grid.SetCell(r, c, 0);

        board.score = 0;
        Refill();
    }
}
