using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 5단계: 광물 획득 처리(연료는 즉시 기름 회복) + 램프 빛 안에 있는 미채굴 광물을 반짝이게 표시.
public class OreSystem : MonoBehaviour
{
    public GridWorld world;
    public LampSystem lampSystem;
    public OilSystem oilSystem;
    public WebSystem webSystem;
    public Tilemap oreTilemap;

    class Floater
    {
        public Vector3 worldPos;
        public string text;
        public float timer;
    }

    class VisibleOreLabel
    {
        public Vector3 worldPos;
        public string name;
    }

    Tile oreTile;
    readonly List<Floater> floaters = new List<Floater>();
    readonly List<VisibleOreLabel> visibleLabels = new List<VisibleOreLabel>();

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (lampSystem == null) lampSystem = GetComponent<LampSystem>();
        if (oilSystem == null) oilSystem = GetComponent<OilSystem>();
        if (webSystem == null) webSystem = GetComponent<WebSystem>();

        if (world != null) world.OnOreCollected += HandleOreCollected;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        oreTile = ScriptableObject.CreateInstance<Tile>();
        oreTile.sprite = sprite;
        oreTile.colliderType = Tile.ColliderType.None;
    }

    void OnDestroy()
    {
        if (world != null) world.OnOreCollected -= HandleOreCollected;
    }

    void HandleOreCollected(Vector3Int cell, OreType ore)
    {
        string text;
        switch (ore.category)
        {
            case OreCategory.Fuel:
                if (oilSystem != null) oilSystem.AddOil(ore.value);
                text = $"+{ore.value:0}";
                break;
            case OreCategory.Ammo:
                if (webSystem != null) webSystem.AddAmmo(Mathf.RoundToInt(ore.value));
                text = $"탄창 +{ore.value:0}";
                break;
            default:
                text = $"{ore.oreName} 획득";
                break;
        }

        floaters.Add(new Floater
        {
            worldPos = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
            text = text,
            timer = 1f
        });
    }

    void Update()
    {
        UpdateShimmer();

        for (int i = floaters.Count - 1; i >= 0; i--)
        {
            floaters[i].timer -= Time.deltaTime;
            floaters[i].worldPos += Vector3.up * Time.deltaTime * 0.6f;
            if (floaters[i].timer <= 0f) floaters.RemoveAt(i);
        }
    }

    void UpdateShimmer()
    {
        if (world == null || oreTilemap == null) return;

        float radius = lampSystem != null ? lampSystem.radius : 0f;
        Vector3Int playerCell = world.WorldToCell(transform.position);

        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
        oreTile.color = new Color(1f, 0.95f, 0.4f, pulse);

        visibleLabels.Clear();

        for (int x = 0; x < world.Width; x++)
        {
            for (int d = 1; d <= world.Depth; d++)
            {
                var cell = new Vector3Int(x, -d, 0);
                var ore = world.GetOre(cell);
                if (ore == null)
                {
                    oreTilemap.SetTile(cell, null);
                    continue;
                }

                float dist = Vector2.Distance(new Vector2(cell.x + 0.5f, cell.y + 0.5f), new Vector2(playerCell.x + 0.5f, playerCell.y + 0.5f));
                bool visible = dist <= radius;
                oreTilemap.SetTile(cell, visible ? oreTile : null);

                if (visible)
                {
                    visibleLabels.Add(new VisibleOreLabel
                    {
                        worldPos = new Vector3(cell.x + 0.5f, cell.y + 1.1f, 0f),
                        name = ore.oreName
                    });
                }
            }
        }
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // 임시: 광물 스프라이트가 없어서 종류 구분이 안 되니, 보이는 광물 위에 이름을 적어준다.
        var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        labelStyle.normal.textColor = Color.white;

        foreach (var l in visibleLabels)
        {
            Vector3 screen = cam.WorldToScreenPoint(l.worldPos);
            GUI.Label(new Rect(screen.x - 40, Screen.height - screen.y - 10, 80, 18), l.name, labelStyle);
        }

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(1f, 0.9f, 0.4f);

        foreach (var f in floaters)
        {
            Vector3 screen = cam.WorldToScreenPoint(f.worldPos);
            GUI.Label(new Rect(screen.x - 40, Screen.height - screen.y - 10, 80, 24), f.text, style);
        }
    }
}
