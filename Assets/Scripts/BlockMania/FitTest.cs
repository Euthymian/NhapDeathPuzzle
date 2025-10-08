using UnityEngine;

public static class FitTest
{
    public static bool CanFit(GridManager grid, ShapeData data)
    {
        for (int r = 0; r < grid.rows; r++)
            for (int c = 0; c < grid.cols; c++)
            {
                bool ok = true;
                foreach (var cell in data.cells)
                {
                    int rr = r + cell.y, cc = c + cell.x;
                    if (!grid.InBounds(rr, cc) || !grid.IsFree(rr, cc)) { ok = false; break; }
                }
                if (ok) return true;
            }
        return false;
    }
}
