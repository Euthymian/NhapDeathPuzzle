using UnityEngine;

[CreateAssetMenu(menuName = "BlockAdventure/Shape")]
public class ShapeData : ScriptableObject
{
    // Offsets relative to a reference cell (0,0)
    public Vector2Int[] cells;
    public int colorId = 1; // for color/bonus logic later
}
