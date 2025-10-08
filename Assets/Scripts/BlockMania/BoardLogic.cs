using UnityEngine;
using System.Collections.Generic;

public class BoardLogic : MonoBehaviour
{
    public GridManager grid;
    public int score;

    // Call after a successful placement
    public int ResolveClears()
    {
        var rowsToClear = new List<int>();
        var colsToClear = new List<int>();

        // full rows
        for (int r = 0; r < grid.rows; r++)
        {
            bool full = true;
            for (int c = 0; c < grid.cols; c++)
                if (grid.board[r, c] == 0) { full = false; break; }
            if (full) rowsToClear.Add(r);
        }

        // full cols
        for (int c = 0; c < grid.cols; c++)
        {
            bool full = true;
            for (int r = 0; r < grid.rows; r++)
                if (grid.board[r, c] == 0) { full = false; break; }
            if (full) colsToClear.Add(c);
        }

        int clearedGroups = 0;

        // clear rows
        foreach (var r in rowsToClear)
        {
            for (int c = 0; c < grid.cols; c++)
            {
                grid.board[r, c] = 0;
                grid.UpdateCellVisual(r, c);
            }
            clearedGroups++;
        }

        // clear cols
        foreach (var c in colsToClear)
        {
            for (int r = 0; r < grid.rows; r++)
            {
                grid.board[r, c] = 0;
                grid.UpdateCellVisual(r, c);
            }
            clearedGroups++;
        }

        // simple scoring: 10 per cleared line/column + small combo
        if (clearedGroups > 0)
            score += 10 * clearedGroups + (clearedGroups - 1) * 5;

        return clearedGroups;
    }
}
