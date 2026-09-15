using UnityEngine;

// 4단계(턴제로 개정): 램프 기름 = 턴제 자원. 실시간으로 줄지 않고, 파기에 성공할 때마다(1턴)
// GridWorld.OnBlockRemoved를 통해 한 번씩만 줄어든다. 비율에 따라 램프 반경은 매 프레임 보간한다.
// 0이 되면 화면이 검게 변하고 게임오버.
public class OilSystem : MonoBehaviour
{
    public GameConfig config;
    public GridWorld world;
    public LampSystem lampSystem;
    public PlayerController playerController;
    public DigSystem digSystem;
    public WallCling wallCling;
    public WebSystem webSystem;

    public float Oil { get; private set; }
    public float MaxOil { get; private set; }
    public bool IsGameOver { get; private set; }

    void Start()
    {
        MaxOil = config.startOil;
        Oil = MaxOil;
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (world != null) world.OnBlockRemoved += HandleBlockRemoved;
    }

    // 강화 페이즈에서 다음 판을 시작할 때 RunManager가 호출한다 (연료 최대량 강화 반영).
    public void ResetForNewRun(float newMaxOil)
    {
        MaxOil = Mathf.Max(1f, newMaxOil);
        Oil = MaxOil;
        IsGameOver = false;
    }

    void OnDestroy()
    {
        if (world != null) world.OnBlockRemoved -= HandleBlockRemoved;
    }

    // 파기 1회 = 1턴. 여기서만 기름이 줄어든다 (실시간 소모 없음).
    void HandleBlockRemoved(Vector3Int cell)
    {
        if (IsGameOver) return;

        Oil = Mathf.Max(0f, Oil - config.oilCostPerDig);
        if (Oil <= 0f) TriggerGameOver();
    }

    void Update()
    {
        if (IsGameOver) return;

        float pct = MaxOil > 0f ? Oil / MaxOil : 0f;
        if (lampSystem != null)
            lampSystem.radius = Mathf.Lerp(config.lampRadiusMin, config.lampRadiusMax, pct);
    }

    // 다른 시스템(연료 광물 등)이 기름을 회복시킬 때 사용.
    public void AddOil(float amount)
    {
        if (IsGameOver || amount <= 0f) return;
        Oil = Mathf.Min(MaxOil, Oil + amount);
    }

    // 웹 발판 설치 등 턴(파기)과 무관한 직접 비용에 사용.
    public void ConsumeOil(float amount)
    {
        if (IsGameOver || amount <= 0f) return;
        Oil = Mathf.Max(0f, Oil - amount);
        if (Oil <= 0f) TriggerGameOver();
    }

    void TriggerGameOver()
    {
        IsGameOver = true;
        if (playerController != null) playerController.enabled = false;
        if (digSystem != null) digSystem.enabled = false;
        if (wallCling != null) wallCling.enabled = false;
        if (webSystem != null) webSystem.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    // 게임오버 화면 전체(잃어버린 것 목록 포함)는 RunManager가 그린다.
    void OnGUI()
    {
        if (IsGameOver) return;
        DrawGauge();
    }

    void DrawGauge()
    {
        float pct = MaxOil > 0f ? Oil / MaxOil : 0f;

        var barRect = new Rect(20, 20, 300, 26);
        GUI.Box(barRect, "");

        var fillRect = new Rect(barRect.x + 2, barRect.y + 2, (barRect.width - 4) * pct, barRect.height - 4);
        Color barColor = pct <= 0.3f
            ? Color.Lerp(new Color(0.5f, 0f, 0f), Color.red, (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f)
            : new Color(1f, 0.8f, 0.3f);

        var prev = GUI.color;
        GUI.color = barColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = prev;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        GUI.Label(barRect, $"기름 {Mathf.CeilToInt(Oil)}", style);
    }
}
