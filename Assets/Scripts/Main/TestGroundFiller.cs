using UnityEngine;
using UnityEngine.Tilemaps;

// 1단계용 임시 고정 지형. 2단계에서 GridWorld로 교체된다.
[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class TestGroundFiller : MonoBehaviour
{
    public int width = 20;
    public int depth = 10;
    public Color color = new Color(0.55f, 0.35f, 0.20f);

    Tilemap tilemap;
    Tile tile;

    void OnEnable()
    {
        Fill();
    }

    [ContextMenu("Fill")]
    public void Fill()
    {
        tilemap = GetComponent<Tilemap>();
        if (tilemap == null) return;

        tilemap.ClearAllTiles();

        if (tile == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = Tile.ColliderType.Grid;
        }

        for (int x = 0; x < width; x++)
            for (int d = 1; d <= depth; d++)
                tilemap.SetTile(new Vector3Int(x, -d, 0), tile);
    }
}
