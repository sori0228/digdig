using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Map Size (1 unit = 1 meter)")]
    public int width = 12;
    public int depth = 40;

    [Header("Base Blocks (hardness = hits required with pickaxe/drill; bomb always 1-shots)")]
    public Color dirtColor = new Color(0.55f, 0.35f, 0.20f);
    public float dirtHardness = 1f;
    public Color stoneColor = new Color(0.45f, 0.45f, 0.45f);
    public float stoneHardness = 3f;

    [System.Serializable]
    public class LayerDef
    {
        public string name = "Layer";
        public int startDepth = 1;
        public int endDepth = 10;
        [Range(0f, 1f)] public float rockChance = 0f;
        [Tooltip("Multiplies the global sand chunk chance while scanning starting cells in this layer.")]
        public float sandChunkMultiplier = 1f;
        public Color backgroundTint = new Color(0.15f, 0.15f, 0.18f);
    }

    [Header("Layers (fixed depth bands - randomization only affects contents within each)")]
    public LayerDef[] layers = new LayerDef[]
    {
        new LayerDef { name = "표토",   startDepth = 1,  endDepth = 10, rockChance = 0.05f, sandChunkMultiplier = 0.5f, backgroundTint = new Color(0.25f, 0.18f, 0.12f) },
        new LayerDef { name = "암반대", startDepth = 11, endDepth = 20, rockChance = 0.45f, sandChunkMultiplier = 0.5f, backgroundTint = new Color(0.18f, 0.18f, 0.20f) },
        new LayerDef { name = "수맥대", startDepth = 21, endDepth = 30, rockChance = 0.20f, sandChunkMultiplier = 2.5f, backgroundTint = new Color(0.16f, 0.20f, 0.26f) },
        new LayerDef { name = "심부",   startDepth = 31, endDepth = 40, rockChance = 0.65f, sandChunkMultiplier = 0.3f, backgroundTint = new Color(0.10f, 0.10f, 0.14f) },
    };

    [Header("Biomes (chunked, non-overlapping regions that override the base dirt/rock layer)")]
    public Color sandColor = new Color(0.93f, 0.82f, 0.55f);
    public float sandHardness = 1f;
    [Tooltip("Sand biome layer's max vertical thickness, in tiles.")]
    public int sandMaxThickness = 3;
    [Tooltip("Base chance, per scanned starting cell, that a new sand chunk begins there (scaled per-layer by LayerDef.sandChunkMultiplier).")]
    [Range(0f, 1f)] public float sandChunkChance = 0.01f;
    public int sandChunkMinWidth = 3;
    public int sandChunkMaxWidth = 6;
    [Tooltip("Oil drained per second when the player is crushed by/between sand blocks.")]
    public float sandCrushDamagePerSecond = 5f;

    [Header("Biomes - Shop (a single walled room, not a scattered chunk)")]
    [Tooltip("Fixed vertical thickness of the shop room, in tiles.")]
    public int shopHeight = 4;
    public int shopMinWidth = 4;
    public int shopMaxWidth = 7;
    [Tooltip("Prototype: fixed depth the room's top row always starts at.")]
    public int shopDepthStart = 13;
    public int shopPlacementAttempts = 200;
    public Color shopWallColor = new Color(0.20f, 0.18f, 0.22f);
    [Tooltip("Border walls are this hard - not literally undiggable, but impractical without the entrance.")]
    public float shopWallHardness = 20f;

    // World cell of the shop's hollow interior center (for positioning the merchant), or null if
    // no valid spot was found this generation (extremely small maps only, in practice always set).
    public Vector3Int? ShopMerchantCell { get; private set; }

    public enum BiomeType { None, Sand, Shop }

    [Header("Randomization")]
    public int seed = 0;

    [Header("Debug")]
    public bool showHardnessGizmos = true;

    [Header("Fog of War")]
    [Tooltip("A tilemap rendered above Ground. Cells not yet revealed are covered by an opaque fog tile.")]
    public Tilemap fogTilemap;
    public Color fogColor = new Color(0.06f, 0.06f, 0.08f, 1f);
    [Tooltip("Surface rows (from the top) that are always revealed, regardless of digging.")]
    public int alwaysRevealedSurfaceRows = 2;

    public readonly HashSet<Vector3Int> Revealed = new HashSet<Vector3Int>();
    Tile fogTile;

    [Header("Treasure (prototype: guaranteed near the surface)")]
    public int treasureSize = 3;
    [Tooltip("The treasure's topmost row is guaranteed to be within this many tiles of the surface.")]
    public int treasureMaxDepthForGuarantee = 3;
    [Tooltip("A second tilemap rendered behind Ground. Digging away a Ground tile over the treasure lets this peek through, even before the whole 3x3 is cleared.")]
    public Tilemap treasureTilemap;
    public Color treasureColor = new Color(1f, 0.2f, 0.8f, 1f);

    // World-space center of the current treasure's 3x3, once placed. Used by the shop's
    // "reveal treasure location" buff.
    public Vector3? TreasureWorldCenter { get; private set; }

    // Fired once, the moment the first tile of the treasure's area is dug away (a peek-through).
    public event System.Action OnTreasureFirstSeen;

    // Fired once, the moment the last tile of the treasure's area is dug away.
    public event System.Action OnTreasureRevealed;

    Tilemap tilemap;
    Sprite squareSprite;
    readonly Dictionary<Color, Tile> tileCache = new Dictionary<Color, Tile>();

    public readonly Dictionary<Vector3Int, float> HardnessAt = new Dictionary<Vector3Int, float>();
    public readonly Dictionary<Vector3Int, int> DamageAt = new Dictionary<Vector3Int, int>();
    public readonly Dictionary<Vector3Int, BiomeType> BiomeAt = new Dictionary<Vector3Int, BiomeType>();

    readonly HashSet<Vector3Int> treasureCells = new HashSet<Vector3Int>();
    readonly HashSet<Vector3Int> treasureRemaining = new HashSet<Vector3Int>();
    bool treasureFirstSeenTriggered;
    bool treasureTriggered;

    void OnEnable()
    {
        Generate();
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

    Tile GetOrMakeTile(Color color)
    {
        if (tileCache.TryGetValue(color, out var cached)) return cached;

        Tile t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = GetSquareSprite();
        t.color = color;
        t.colliderType = Tile.ColliderType.Grid;
        tileCache[color] = t;
        return t;
    }

    public LayerDef GetLayer(int depthValue)
    {
        foreach (var l in layers)
            if (depthValue >= l.startDepth && depthValue <= l.endDepth) return l;
        return layers.Length > 0 ? layers[layers.Length - 1] : null;
    }

    public Color GetBackgroundTint(int depthValue)
    {
        var l = GetLayer(depthValue);
        return l != null ? l.backgroundTint : Color.black;
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        tilemap = GetComponent<Tilemap>();
        if (tilemap == null) return;

        tilemap.ClearAllTiles();
        HardnessAt.Clear();
        DamageAt.Clear();
        BiomeAt.Clear();
        tileCache.Clear();

        Random.State prevState = Random.state;
        Random.InitState(seed);

        // Cell index d runs 1..depth; index 0 is unused (kept for readability with 1-based depth).
        var colorGrid = new Color[width, depth + 1];
        var hardnessGrid = new float[width, depth + 1];
        var biomeGrid = new BiomeType[width, depth + 1];
        var emptyGrid = new bool[width, depth + 1];
        var claimed = new bool[width, depth + 1];

        for (int x = 0; x < width; x++)
        {
            for (int d = 1; d <= depth; d++)
            {
                var layer = GetLayer(d);
                bool isRock = layer != null && Random.value < layer.rockChance;
                colorGrid[x, d] = isRock ? stoneColor : dirtColor;
                hardnessGrid[x, d] = isRock ? stoneHardness : dirtHardness;
            }
        }

        // Biome pass: chunked regions that override the base dirt/rock layer just set above.
        // The shop is placed first (a single reserved rectangle, claimed so nothing else can eat
        // into it) so a later sand chunk can never overlap it.
        GenerateShopBiome(biomeGrid, colorGrid, hardnessGrid, emptyGrid, claimed);
        GenerateSandBiome(biomeGrid, colorGrid, hardnessGrid);

        // Write the finished grid to the tilemap.
        for (int x = 0; x < width; x++)
        {
            for (int d = 1; d <= depth; d++)
            {
                var pos = new Vector3Int(x, -d, 0);

                if (emptyGrid[x, d])
                {
                    // Hollow shop interior: no tile at all, nothing to dig.
                    tilemap.SetTile(pos, null);
                    BiomeAt[pos] = biomeGrid[x, d];
                    continue;
                }

                tilemap.SetTile(pos, GetOrMakeTile(colorGrid[x, d]));
                HardnessAt[pos] = hardnessGrid[x, d];
                if (biomeGrid[x, d] != BiomeType.None) BiomeAt[pos] = biomeGrid[x, d];
            }
        }

        InitFog(emptyGrid);
        PlaceTreasure();

        Random.state = prevState;
    }

    // Resets the fog: only the top surface rows and hollow cells (e.g. the shop interior, which
    // acts like a small pre-existing cave) start revealed. Everything else is covered by an
    // opaque fog tile on a separate tilemap rendered above Ground, until Dig() or sand settling
    // reveals it.
    void InitFog(bool[,] emptyGrid)
    {
        Revealed.Clear();

        int topRows = Mathf.Max(0, alwaysRevealedSurfaceRows);
        for (int x = 0; x < width; x++)
            for (int d = 1; d <= Mathf.Min(topRows, depth); d++)
                Revealed.Add(new Vector3Int(x, -d, 0));

        for (int x = 0; x < width; x++)
            for (int d = 1; d <= depth; d++)
                if (emptyGrid[x, d]) RevealAround(new Vector3Int(x, -d, 0), paintFog: false);

        if (fogTilemap == null) return;

        fogTilemap.ClearAllTiles();
        for (int x = 0; x < width; x++)
        {
            for (int d = 1; d <= depth; d++)
            {
                var pos = new Vector3Int(x, -d, 0);
                if (!Revealed.Contains(pos)) fogTilemap.SetTile(pos, GetFogTile());
            }
        }
    }

    Tile GetFogTile()
    {
        if (fogTile != null) return fogTile;
        fogTile = ScriptableObject.CreateInstance<Tile>();
        fogTile.sprite = GetSquareSprite();
        fogTile.color = fogColor;
        fogTile.colliderType = Tile.ColliderType.None;
        return fogTile;
    }

    // Marks cell and its 8 neighbors as revealed. When paintFog is true, also clears the fog
    // tile there immediately (used after the initial generation pass, e.g. from Dig()).
    void RevealAround(Vector3Int cell, bool paintFog)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var c = cell + new Vector3Int(dx, dy, 0);
                int cd = -c.y;
                if (c.x < 0 || c.x >= width || cd < 1 || cd > depth) continue;

                if (Revealed.Add(c) && paintFog && fogTilemap != null)
                    fogTilemap.SetTile(c, null);
            }
        }
    }

    // Scatters rectangular sand chunks (width sandChunkMinWidth..sandChunkMaxWidth,
    // thickness 1..sandMaxThickness) across the map. Cells already claimed by another
    // biome chunk are skipped one-by-one, so biomes never overlap. The scan chance is
    // scaled per-layer so e.g. the water-vein layer reads as "모래 많음".
    void GenerateSandBiome(BiomeType[,] biomeGrid, Color[,] colorGrid, float[,] hardnessGrid)
    {
        for (int x = 0; x < width; x++)
        {
            for (int d = 1; d <= depth; d++)
            {
                if (biomeGrid[x, d] != BiomeType.None) continue;

                var layer = GetLayer(d);
                float chance = sandChunkChance * (layer != null ? layer.sandChunkMultiplier : 1f);
                if (Random.value >= chance) continue;

                int chunkWidth = Random.Range(sandChunkMinWidth, sandChunkMaxWidth + 1);
                int thickness = Random.Range(1, sandMaxThickness + 1);

                for (int cx = x; cx < x + chunkWidth && cx < width; cx++)
                {
                    for (int cd = d; cd < d + thickness && cd <= depth; cd++)
                    {
                        if (biomeGrid[cx, cd] != BiomeType.None) continue;
                        biomeGrid[cx, cd] = BiomeType.Sand;
                        colorGrid[cx, cd] = sandColor;
                        hardnessGrid[cx, cd] = sandHardness;
                    }
                }
            }
        }
    }

    // Places exactly one shop room: a shopHeight-tall x [shopMinWidth,shopMaxWidth]-wide rectangle,
    // walled on its border (hardness shopWallHardness) except a single non-corner entrance cell
    // left as untouched base terrain. The interior (non-border) cells are left fully hollow (no
    // tile at all). Tries random placements until one doesn't overlap another biome's claimed
    // cells - unlike the sand chunk scatter, this is all-or-nothing so the room's walls are
    // never partially eaten by another biome.
    void GenerateShopBiome(BiomeType[,] biomeGrid, Color[,] colorGrid, float[,] hardnessGrid, bool[,] emptyGrid, bool[,] claimed)
    {
        ShopMerchantCell = null;

        int h = shopHeight;
        if (h > depth) return;

        for (int attempt = 0; attempt < shopPlacementAttempts; attempt++)
        {
            int w = Random.Range(shopMinWidth, shopMaxWidth + 1);
            if (w > width) continue;

            int x0 = Random.Range(0, width - w + 1);
            int d0 = shopDepthStart; // prototype: always this exact depth band
            if (d0 + h - 1 > depth) continue;

            bool clear = true;
            for (int cx = x0; cx < x0 + w && clear; cx++)
                for (int cd = d0; cd < d0 + h; cd++)
                    if (biomeGrid[cx, cd] != BiomeType.None) { clear = false; break; }

            if (!clear) continue;

            // Entrance: one non-corner cell on a random side of the border. Left/right are only
            // valid if there's map left to stand on beyond that wall - otherwise the entrance
            // would sit flush against the map boundary (MapBounds' invisible wall) with no way
            // to approach it from outside and no way through the room's other walls either,
            // making it permanently unreachable.
            var validSides = new List<int> { 0, 1 }; // top, bottom always reachable
            if (x0 > 0) validSides.Add(2); // left
            if (x0 + w < width) validSides.Add(3); // right
            int side = validSides[Random.Range(0, validSides.Count)];
            int ex, ed;
            if (side == 0) { ed = d0; ex = Random.Range(x0 + 1, x0 + w - 1); }
            else if (side == 1) { ed = d0 + h - 1; ex = Random.Range(x0 + 1, x0 + w - 1); }
            else if (side == 2) { ex = x0; ed = Random.Range(d0 + 1, d0 + h - 1); }
            else { ex = x0 + w - 1; ed = Random.Range(d0 + 1, d0 + h - 1); }

            for (int cx = x0; cx < x0 + w; cx++)
            {
                for (int cd = d0; cd < d0 + h; cd++)
                {
                    if (cx == ex && cd == ed) continue; // entrance: leave base terrain untouched

                    bool isBorder = cx == x0 || cx == x0 + w - 1 || cd == d0 || cd == d0 + h - 1;
                    biomeGrid[cx, cd] = BiomeType.Shop;
                    claimed[cx, cd] = true; // keep other biomes out of the whole room, walls included

                    if (isBorder)
                    {
                        colorGrid[cx, cd] = shopWallColor;
                        hardnessGrid[cx, cd] = shopWallHardness;
                    }
                    else
                    {
                        emptyGrid[cx, cd] = true;
                    }
                }
            }

            ShopMerchantCell = new Vector3Int(x0 + w / 2, -(d0 + h / 2), 0);
            return;
        }
    }

    void PlaceTreasure()
    {
        treasureCells.Clear();
        treasureRemaining.Clear();
        treasureFirstSeenTriggered = false;
        treasureTriggered = false;
        TreasureWorldCenter = null;

        if (treasureTilemap != null) treasureTilemap.ClearAllTiles();

        int maxTop = Mathf.Max(1, treasureMaxDepthForGuarantee - treasureSize + 1);

        // Retry a few times to avoid landing on a hollow shop interior (no Ground tile to dig,
        // which would permanently block the reveal events from ever firing for that cell).
        int topDepth = 1;
        int x0 = 0;
        bool placed = false;

        for (int attempt = 0; attempt < 30 && !placed; attempt++)
        {
            topDepth = Random.Range(1, maxTop + 1);
            x0 = Random.Range(0, Mathf.Max(1, width - treasureSize + 1));

            placed = true;
            for (int i = 0; i < treasureSize && placed; i++)
                for (int j = 0; j < treasureSize; j++)
                    if (!tilemap.HasTile(new Vector3Int(x0 + i, -(topDepth + j), 0))) { placed = false; break; }
        }

        Tile treasureTile = treasureTilemap != null ? GetOrMakeTile(treasureColor) : null;

        for (int i = 0; i < treasureSize; i++)
        {
            for (int j = 0; j < treasureSize; j++)
            {
                var cell = new Vector3Int(x0 + i, -(topDepth + j), 0);
                treasureCells.Add(cell);
                treasureRemaining.Add(cell);

                if (treasureTilemap != null) treasureTilemap.SetTile(cell, treasureTile);
            }
        }

        TreasureWorldCenter = new Vector3(x0 + treasureSize / 2f, -(topDepth + treasureSize / 2f), 0f);
    }

    public Vector3Int WorldToCell(Vector3 world)
    {
        return tilemap.WorldToCell(world);
    }

    public bool HasTile(Vector3Int cell)
    {
        return tilemap != null && tilemap.HasTile(cell);
    }

    public float GetHardness(Vector3Int cell)
    {
        return HardnessAt.TryGetValue(cell, out float h) ? h : 0f;
    }

    public int GetDamage(Vector3Int cell)
    {
        return DamageAt.TryGetValue(cell, out int d) ? d : 0;
    }

    public bool IsSand(Vector3Int cell)
    {
        return BiomeAt.TryGetValue(cell, out var b) && b == BiomeType.Sand;
    }

    // Hits needed to break a tile: bomb always 1-shots regardless of hardness; pickaxe/drill
    // need as many hits as the tile's hardness value (dirt/sand=1, rock=3, shop wall=very high).
    public int GetRequiredHits(Vector3Int cell, ToolType tool)
    {
        if (tool == ToolType.Bomb) return 1;
        return Mathf.Max(1, Mathf.RoundToInt(GetHardness(cell)));
    }

    // Registers one hit on the tile with the given tool. Returns false only if the cell was
    // empty to begin with. broke tells the caller whether this hit was the one that destroyed it.
    public bool Dig(Vector3Int cell, ToolType tool, out bool broke)
    {
        broke = false;

        if (tilemap == null || !tilemap.HasTile(cell)) return false;

        int requiredHits = GetRequiredHits(cell, tool);
        int hits = GetDamage(cell) + 1;

        if (hits < requiredHits)
        {
            DamageAt[cell] = hits;
            return true;
        }

        HardnessAt.Remove(cell);
        DamageAt.Remove(cell);
        tilemap.SetTile(cell, null);
        broke = true;

        RevealAround(cell, paintFog: true);

        if (treasureRemaining.Remove(cell))
        {
            if (!treasureFirstSeenTriggered)
            {
                treasureFirstSeenTriggered = true;
                OnTreasureFirstSeen?.Invoke();
            }

            if (!treasureTriggered && treasureRemaining.Count == 0)
            {
                treasureTriggered = true;
                OnTreasureRevealed?.Invoke();
            }
        }

        SettleSandAbove(cell);

        return true;
    }

    // Straight-line oil cost: the total pickaxe hits needed to clear column x from the surface
    // all the way to the bottom (empty/hollow cells, e.g. a shop interior, are free). Used to set
    // each dig phase's starting oil to a fraction of this, per the design doc.
    public float GetStraightLineOilCost(int x)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        float total = 0f;
        for (int d = 1; d <= depth; d++)
        {
            var cell = new Vector3Int(x, -d, 0);
            if (!tilemap.HasTile(cell)) continue;
            total += GetHardness(cell);
        }
        return Mathf.Max(1f, total);
    }

    // After a cell is emptied, any sand tile directly above it falls straight down to fill
    // the gap; if that uncovers more sand further up, it keeps falling (chain reaction).
    // Only sand falls this way - dirt/rock tiles stay put.
    void SettleSandAbove(Vector3Int emptyCell)
    {
        Vector3Int current = emptyCell;
        while (true)
        {
            Vector3Int above = current + Vector3Int.up;
            if (!tilemap.HasTile(above) || !IsSand(above)) return;

            MoveTile(above, current);
            current = above;
            RevealAround(current, paintFog: true);
        }
    }

    void MoveTile(Vector3Int from, Vector3Int to)
    {
        TileBase tile = tilemap.GetTile(from);
        tilemap.SetTile(to, tile);
        tilemap.SetTile(from, null);

        if (HardnessAt.TryGetValue(from, out float h)) { HardnessAt[to] = h; HardnessAt.Remove(from); }
        else HardnessAt.Remove(to);

        if (DamageAt.TryGetValue(from, out int dmg)) { DamageAt[to] = dmg; DamageAt.Remove(from); }
        else DamageAt.Remove(to);

        BiomeAt[to] = BiomeType.Sand;
        BiomeAt.Remove(from);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showHardnessGizmos) return;
        var sceneCam = UnityEditor.SceneView.lastActiveSceneView != null ? UnityEditor.SceneView.lastActiveSceneView.camera : null;
        if (sceneCam == null) return;

        var planes = GeometryUtility.CalculateFrustumPlanes(sceneCam);
        var style = new GUIStyle { fontSize = 10, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;

        foreach (var kv in HardnessAt)
        {
            Vector3 worldCenter = new Vector3(kv.Key.x + 0.5f, kv.Key.y + 0.5f, 0f);
            var bounds = new Bounds(worldCenter, Vector3.one * 0.9f);
            if (!GeometryUtility.TestPlanesAABB(planes, bounds)) continue;

            UnityEditor.Handles.Label(worldCenter, kv.Value.ToString("0.#"), style);
        }
    }
#endif
}
