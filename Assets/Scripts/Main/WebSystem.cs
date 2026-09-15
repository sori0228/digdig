using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 7단계(재설계): 웹 발판. 벽에 매달린 상태에서만 우클릭으로 조준 -> 발판 설치.
// 설치마다 기름을 고정으로 소모하고, 탄창(발사 가능 횟수)도 하나 깎인다.
// 탄창은 5발로 시작하고 땅속 아이템(Ammo 광물)을 캐서만 충전된다. 설치된 발판 개수 제한은 없다.
public class WebSystem : MonoBehaviour
{
    public GameConfig config;
    public GridWorld world;
    public WallCling wallCling;
    public OilSystem oilSystem;
    public Camera cam;
    public Collider2D playerCollider;

    public int Ammo { get; private set; }
    public int MaxAmmo { get; private set; }

    class WebPlatform
    {
        public GameObject go;
        public EdgeCollider2D collider;
    }

    readonly List<WebPlatform> webs = new List<WebPlatform>();
    LineRenderer aimLine;
    bool aiming;
    Vector3Int aimPlacementCell;

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (wallCling == null) wallCling = GetComponent<WallCling>();
        if (oilSystem == null) oilSystem = GetComponent<OilSystem>();
        if (cam == null) cam = Camera.main;
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        MaxAmmo = config.webMaxAmmo;
        Ammo = MaxAmmo;

        var lineGO = new GameObject("WebAimLine");
        aimLine = lineGO.AddComponent<LineRenderer>();
        aimLine.positionCount = 2;
        aimLine.startWidth = aimLine.endWidth = 0.06f;
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor = aimLine.endColor = new Color(1f, 1f, 1f, 0.7f);
        aimLine.sortingOrder = 20;
        aimLine.enabled = false;
    }

    // 땅속 탄창 광물을 캤을 때 OreSystem이 호출한다.
    public void AddAmmo(int amount)
    {
        if (amount <= 0) return;
        Ammo = Mathf.Min(MaxAmmo, Ammo + amount);
    }

    // 강화 페이즈에서 다음 판을 시작할 때 RunManager가 호출한다 (탄창 강화 반영).
    public void ResetForNewRun(int newMaxAmmo)
    {
        MaxAmmo = Mathf.Max(0, newMaxAmmo);
        Ammo = MaxAmmo;
    }

    void Update()
    {
        bool canAim = wallCling != null && wallCling.IsClinging && Ammo > 0;

        if (canAim && Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            aiming = true;
            UpdateAim();
        }
        else if (aiming)
        {
            aiming = false;
            aimLine.enabled = false;
            PlaceWeb(aimPlacementCell);
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame &&
            (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed))
        {
            DropThroughNearbyWebs();
        }
    }

    void UpdateAim()
    {
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        mouseWorld.z = 0f;

        Vector2 dir = (Vector2)mouseWorld - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        Vector3Int lastFreeCell = world.WorldToCell(transform.position);

        for (int i = 1; i <= config.webMaxLength; i++)
        {
            Vector2 samplePos = (Vector2)transform.position + dir * i;
            Vector3Int cell = world.WorldToCell(samplePos);
            if (world.HasBlock(cell)) break;
            lastFreeCell = cell;
        }

        aimPlacementCell = lastFreeCell;

        Vector3 endWorld = new Vector3(lastFreeCell.x + 0.5f, lastFreeCell.y + 0.5f, 0f);
        aimLine.enabled = true;
        aimLine.SetPosition(0, transform.position);
        aimLine.SetPosition(1, endWorld);
    }

    // 조준해서 우클릭을 떼면 그 자리에 항상 설치된다 (기름 고정 비용 + 탄창 1발 소모).
    void PlaceWeb(Vector3Int cell)
    {
        if (Ammo <= 0 || oilSystem == null) return;

        Ammo--;
        oilSystem.ConsumeOil(config.webOilCost);

        var go = new GameObject("WebPlatform");
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        var edge = go.AddComponent<EdgeCollider2D>();
        edge.points = new Vector2[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) };
        edge.usedByEffector = true;

        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeLineSprite();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(1f, 0.08f);
        sr.color = new Color(0.9f, 0.9f, 1f, 0.9f);
        sr.sortingOrder = 4;

        webs.Add(new WebPlatform { go = go, collider = edge });
    }

    void DropThroughNearbyWebs()
    {
        if (playerCollider == null) return;

        foreach (var w in webs)
        {
            if (w.collider != null && w.collider.IsTouching(playerCollider))
                StartCoroutine(TemporarilyIgnore(w.collider));
        }
    }

    IEnumerator TemporarilyIgnore(Collider2D platformCollider)
    {
        Physics2D.IgnoreCollision(playerCollider, platformCollider, true);
        yield return new WaitForSeconds(0.3f);
        if (platformCollider != null) Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
    }

    Sprite MakeLineSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = Ammo > 0 ? Color.white : new Color(1f, 0.5f, 0.5f);
        GUI.Label(new Rect(20, 60, 200, 24), $"탄창 {Ammo} / {MaxAmmo}", style);
    }
}
