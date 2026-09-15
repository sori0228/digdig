using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// 3단계: 램프 빛 = 시야. Global Light 2D를 어둡게 깔고, 플레이어의 Point Light 2D 반경만큼만 밝게 보인다.
// 한 번이라도 빛이 닿았던 칸은 "탐사 기록"으로 남아 20% 밝기로 계속 보인다 (그렇지 않으면 판 길도 못 찾는다).
public class LampSystem : MonoBehaviour
{
    public GridWorld world;
    public Light2D lamp;
    public Tilemap fogTilemap;

    [Header("테스트용 - 4단계에서 기름 게이지가 이 값을 대신 제어한다")]
    [Range(0.5f, 12f)] public float radius = 5f;
    public float falloff = 0.4f;

    [Header("탐사 기록 밝기 (0=완전 암흑, 1=완전히 보임)")]
    [Range(0f, 1f)] public float memoryBrightness = 0.2f;

    readonly HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
    Tile unseenTile;
    Tile memoryTile;

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        BuildTiles();
    }

    void BuildTiles()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        unseenTile = ScriptableObject.CreateInstance<Tile>();
        unseenTile.sprite = sprite;
        unseenTile.color = Color.black;
        unseenTile.colliderType = Tile.ColliderType.None;

        memoryTile = ScriptableObject.CreateInstance<Tile>();
        memoryTile.sprite = sprite;
        memoryTile.color = new Color(0f, 0f, 0f, 1f - memoryBrightness);
        memoryTile.colliderType = Tile.ColliderType.None;
    }

    void Update()
    {
        if (lamp != null)
        {
            lamp.pointLightOuterRadius = radius;
            lamp.falloffIntensity = falloff;
        }

        UpdateFog();
    }

    void UpdateFog()
    {
        if (world == null || fogTilemap == null) return;

        Vector3Int playerCell = world.WorldToCell(transform.position);

        for (int x = 0; x < world.Width; x++)
        {
            for (int d = 1; d <= world.Depth; d++)
            {
                var cell = new Vector3Int(x, -d, 0);
                float dist = Vector2.Distance(new Vector2(cell.x + 0.5f, cell.y + 0.5f), new Vector2(playerCell.x + 0.5f, playerCell.y + 0.5f));

                if (dist <= radius)
                {
                    visited.Add(cell);
                    fogTilemap.SetTile(cell, null);
                }
                else if (visited.Contains(cell))
                {
                    fogTilemap.SetTile(cell, memoryTile);
                }
                else
                {
                    fogTilemap.SetTile(cell, unseenTile);
                }
            }
        }
    }
}
