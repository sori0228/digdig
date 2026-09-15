using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

// 2단계(지형 데이터) + 9단계(광맥 줄기) + 10단계(층별 성질) + 11단계(가스 주머니).
// 실제 지형/광물/가스 데이터는 이 2차원 배열들이 갖고, Tilemap은 화면 표시 전용이다.
[RequireComponent(typeof(Tilemap))]
public class GridWorld : MonoBehaviour
{
    public GameConfig config;

    [Header("블록 종류")]
    public BlockType dirt;
    public BlockType sand;
    public BlockType rock;

    [System.Serializable]
    public class LayerDef
    {
        public string name = "층";
        public int minDepth = 1;
        public int maxDepth = 10;
        [Range(0f, 1f)] public float rockChance = 0.1f;
        [Range(0f, 1f)] public float sandChance = 0.02f;
        public bool gasEnabled = false;
        public Color backgroundTint = new Color(0.15f, 0.15f, 0.18f);
    }

    [Header("10단계: 층별 성질 (10m 단위 4개 층)")]
    public LayerDef[] layers = new LayerDef[]
    {
        new LayerDef { name = "1층 표토",   minDepth = 1,  maxDepth = 10, rockChance = 0.05f, sandChance = 0.02f, gasEnabled = false, backgroundTint = new Color(0.25f, 0.18f, 0.12f) },
        new LayerDef { name = "2층 암반대", minDepth = 11, maxDepth = 20, rockChance = 0.45f, sandChance = 0.05f, gasEnabled = true,  backgroundTint = new Color(0.18f, 0.18f, 0.20f) },
        new LayerDef { name = "3층 수맥대", minDepth = 21, maxDepth = 30, rockChance = 0.20f, sandChance = 0.35f, gasEnabled = false, backgroundTint = new Color(0.16f, 0.20f, 0.26f) },
        new LayerDef { name = "4층 심부",   minDepth = 31, maxDepth = 40, rockChance = 0.65f, sandChance = 0.10f, gasEnabled = true,  backgroundTint = new Color(0.08f, 0.08f, 0.11f) },
    };

    [Header("9단계: 광맥 줄기 (광물별 개수/길이)")]
    public OreType coalOre;
    public OreType ironOre;
    public OreType oilPocketOre;
    public OreType gemOre;
    public OreType bigGemOre;
    public int veinsPerOre = 6;
    public int veinMinLength = 3;
    public int veinMaxLength = 6;

    [Header("웹 발판 탄창 광물 (깊이 무관하게 전역 배치)")]
    public OreType webThreadOre;
    public int webThreadVeins = 4;

    [Header("11단계: 가스 주머니")]
    [Range(0f, 1f)] public float gasChance = 0.02f;
    public float gasDamage = 20f;
    public int gasBlastRadius = 1;

    public int seed = 0;

    // 광물을 캤을 때(칸이 완전히 파괴됐을 때) 발화. 연료/전리품 처리는 구독자가 맡는다.
    public event System.Action<Vector3Int, OreType> OnOreCollected;

    // 플레이어가 성공적으로 한 칸을 파낼 때마다(연쇄낙하 제외) 한 번 발화 - "1턴"의 정의.
    public event System.Action<Vector3Int> OnBlockRemoved;

    // 가스 주머니를 캤을 때 발화 (HazardSystem이 기름 피해 + 3x3 파괴를 처리).
    public event System.Action<Vector3Int> OnGasTriggered;

    Tilemap tilemap;
    BlockType[,] blocks; // [x, depth] depth: 1..config.mapDepth (0 unused)
    OreType[,] ores;
    bool[,] gas;
    readonly Dictionary<BlockType, Tile> tileCache = new Dictionary<BlockType, Tile>();
    Sprite squareSprite;

    public int Width => config.mapWidth;
    public int Depth => config.mapDepth;

    void Awake()
    {
        tilemap = GetComponent<Tilemap>();
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (config == null || tilemap == null) return;

        tilemap.ClearAllTiles();
        blocks = new BlockType[config.mapWidth, config.mapDepth + 1];
        ores = new OreType[config.mapWidth, config.mapDepth + 1];
        gas = new bool[config.mapWidth, config.mapDepth + 1];

        var prevState = Random.state;
        Random.InitState(seed);

        for (int x = 0; x < config.mapWidth; x++)
        {
            for (int d = 1; d <= config.mapDepth; d++)
            {
                var layer = GetLayer(d);
                float roll = Random.value;
                if (layer != null && roll < layer.sandChance) blocks[x, d] = sand;
                else if (layer != null && roll < layer.sandChance + layer.rockChance) blocks[x, d] = rock;
                else blocks[x, d] = dirt;
            }
        }

        for (int i = 0; i < layers.Length; i++)
        {
            var l = layers[i];
            switch (i)
            {
                case 0: GenerateOreVeins(coalOre, l.minDepth, l.maxDepth, veinsPerOre); break;
                case 1:
                    GenerateOreVeins(ironOre, l.minDepth, l.maxDepth, veinsPerOre);
                    GenerateOreVeins(oilPocketOre, l.minDepth, l.maxDepth, Mathf.Max(1, veinsPerOre / 2));
                    break;
                case 2: GenerateOreVeins(gemOre, l.minDepth, l.maxDepth, veinsPerOre); break;
                case 3: GenerateOreVeins(bigGemOre, l.minDepth, l.maxDepth, Mathf.Max(1, veinsPerOre / 2)); break;
            }

            if (l.gasEnabled) GenerateGas(l.minDepth, l.maxDepth);
        }

        GenerateOreVeins(webThreadOre, 1, config.mapDepth, webThreadVeins);

        for (int x = 0; x < config.mapWidth; x++)
            for (int d = 1; d <= config.mapDepth; d++)
                tilemap.SetTile(new Vector3Int(x, -d, 0), GetTile(blocks[x, d]));

        Random.state = prevState;
    }

    void GenerateOreVeins(OreType ore, int minDepth, int maxDepth, int count)
    {
        if (ore == null) return;

        for (int v = 0; v < count; v++)
        {
            int sx = Random.Range(0, config.mapWidth);
            int sd = Random.Range(minDepth, maxDepth + 1);
            if (blocks[sx, sd] == null || blocks[sx, sd] == sand) continue;

            Vector2Int dir = RandomCardinalDir();
            int length = Random.Range(veinMinLength, veinMaxLength + 1);
            int x = sx, d = sd;

            for (int i = 0; i < length; i++)
            {
                if (x < 0 || x >= config.mapWidth || d < minDepth || d > maxDepth) break;

                if (blocks[x, d] != null && blocks[x, d] != sand && ores[x, d] == null)
                    ores[x, d] = ore;

                if (Random.value < 0.35f) dir = RandomCardinalDir();
                x += dir.x;
                d += dir.y;
            }
        }
    }

    void GenerateGas(int minDepth, int maxDepth)
    {
        for (int x = 0; x < config.mapWidth; x++)
        {
            for (int d = minDepth; d <= maxDepth; d++)
            {
                if (blocks[x, d] == null || ores[x, d] != null) continue;
                if (Random.value < gasChance) gas[x, d] = true;
            }
        }
    }

    Vector2Int RandomCardinalDir()
    {
        switch (Random.Range(0, 4))
        {
            case 0: return new Vector2Int(1, 0);
            case 1: return new Vector2Int(-1, 0);
            case 2: return new Vector2Int(0, 1);
            default: return new Vector2Int(0, -1);
        }
    }

    public LayerDef GetLayer(int depth)
    {
        foreach (var l in layers)
            if (depth >= l.minDepth && depth <= l.maxDepth) return l;
        return layers.Length > 0 ? layers[layers.Length - 1] : null;
    }

    public int GetLayerIndex(int depth)
    {
        for (int i = 0; i < layers.Length; i++)
            if (depth >= layers[i].minDepth && depth <= layers[i].maxDepth) return i;
        return layers.Length - 1;
    }

    public bool InBounds(Vector3Int cell)
    {
        int d = -cell.y;
        return cell.x >= 0 && cell.x < config.mapWidth && d >= 1 && d <= config.mapDepth;
    }

    public BlockType GetBlock(Vector3Int cell)
    {
        if (!InBounds(cell)) return null;
        return blocks[cell.x, -cell.y];
    }

    public bool HasBlock(Vector3Int cell) => GetBlock(cell) != null;

    public OreType GetOre(Vector3Int cell)
    {
        if (!InBounds(cell)) return null;
        return ores[cell.x, -cell.y];
    }

    public Vector3Int WorldToCell(Vector3 world) => tilemap.WorldToCell(world);

    // 칸을 제거하고, 위 칸이 연쇄낙하 블록(모래)이면 한 칸씩 떨어뜨린다.
    public void RemoveBlock(Vector3Int cell)
    {
        if (!InBounds(cell)) return;

        bool hadGas = gas[cell.x, -cell.y];
        gas[cell.x, -cell.y] = false;

        var ore = ores[cell.x, -cell.y];
        ores[cell.x, -cell.y] = null;

        blocks[cell.x, -cell.y] = null;
        tilemap.SetTile(cell, null);

        if (ore != null) OnOreCollected?.Invoke(cell, ore);
        OnBlockRemoved?.Invoke(cell);

        var current = cell;
        while (true)
        {
            var above = current + Vector3Int.up;
            var block = GetBlock(above);
            if (block == null || !block.chainFall) break;

            blocks[current.x, -current.y] = block;
            blocks[above.x, -above.y] = null;
            tilemap.SetTile(current, GetTile(block));
            tilemap.SetTile(above, null);
            current = above;
        }

        if (hadGas) OnGasTriggered?.Invoke(cell);
    }

    // 폭발 등으로 인한 "조용한" 파괴 - 턴/기름을 소모하지 않고, 광물도 얻지 못한다.
    public void DestroyArea(Vector3Int center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var c = center + new Vector3Int(dx, dy, 0);
                if (!InBounds(c) || blocks[c.x, -c.y] == null) continue;

                ores[c.x, -c.y] = null;
                gas[c.x, -c.y] = false;
                blocks[c.x, -c.y] = null;
                tilemap.SetTile(c, null);
            }
        }
    }

    Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

    Tile GetTile(BlockType b)
    {
        if (b == null) return null;
        if (tileCache.TryGetValue(b, out var cached)) return cached;

        var t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = b.sprite != null ? b.sprite : GetSquareSprite();
        t.color = b.sprite != null ? Color.white : FallbackColor(b);
        t.colliderType = Tile.ColliderType.Grid;
        tileCache[b] = t;
        return t;
    }

    // 스프라이트가 아직 없는 블록을 구분하기 위한 임시 색상.
    Color FallbackColor(BlockType b)
    {
        if (b == dirt) return new Color(0.55f, 0.35f, 0.20f);
        if (b == sand) return new Color(0.93f, 0.82f, 0.55f);
        if (b == rock) return new Color(0.45f, 0.45f, 0.45f);
        return Color.magenta;
    }
}
