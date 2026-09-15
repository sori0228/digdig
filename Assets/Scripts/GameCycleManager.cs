using UnityEngine;

public enum GamePhase { Tutorial, Digging, Cleared, GameOver, TreasureIntro, Shopping }

[System.Serializable]
public class TutorialPage
{
    public string title;
    [TextArea(3, 8)] public string body;
}

// Owns the dig cycle: generate a map, give the player just enough oil to make it interesting,
// and restart the cycle on either a clear (bottom reached) or a game over (oil depleted).
public class GameCycleManager : MonoBehaviour
{
    public static GamePhase CurrentPhase { get; private set; } = GamePhase.Digging;

    [Header("Refs")]
    public TerrainGenerator terrain;
    public DigController digController;
    public MerchantController merchant;
    public Transform player;

    [Header("Dig phase reset")]
    public Vector3 playerStartPosition = new Vector3(6f, 0.5f, 0f);

    [Header("Oil (starting amount is a fraction of the straight-line-down cost, so a straight dig can never reach the bottom)")]
    [Range(0.1f, 1.5f)] public float startOilMultiplier = 0.75f;

    [Header("Shop (merchant found in the shop biome room, priced in oil)")]
    public float shopTreasureRevealCost = 8f;

    [Header("Treasure reward (replaces the old relic system - just a big oil refill)")]
    public float treasureOilReward = 15f;

    bool treasureLocationRevealed;

    [Header("Tutorial (shown once at the start)")]
    public TutorialPage[] tutorialPages = new TutorialPage[]
    {
        new TutorialPage { title = "조작법", body = "WASD : 이동\n스페이스 : 점프\n1 / 2 / 3 : 도구 변경\n마우스 클릭 : 발굴" },
        new TutorialPage { title = "램프 기름", body = "모든 도구는 기름을 소모합니다.\n기름이 0이 되면 즉시 실패합니다.\n기름은 오직 땅속에서 발견하는 것으로만 채울 수 있습니다." },
        new TutorialPage { title = "목표", body = "맨 밑 지점까지 도달하면 스테이지 클리어입니다." },
    };

    int tutorialPage = 0;

    void Start()
    {
        if (terrain == null) terrain = FindFirstObjectByType<TerrainGenerator>();
        if (digController == null) digController = FindFirstObjectByType<DigController>();
        if (player == null && digController != null) player = digController.transform;
        if (merchant == null) merchant = FindFirstObjectByType<MerchantController>();

        if (digController != null) digController.OnOilDepleted += HandleOilDepleted;
        if (terrain != null)
        {
            terrain.OnTreasureFirstSeen += HandleTreasureFirstSeen;
            terrain.OnTreasureRevealed += HandleTreasureRevealed;
        }
        if (merchant != null) merchant.OnMerchantClicked += HandleMerchantClicked;

        // Re-roll and regenerate here too, not just in StartNewDigPhase() - otherwise every
        // Play session's first dig phase would reuse whatever seed happens to be serialized in
        // the Inspector (e.g. left over from editor testing), making the map/shop look "fixed".
        if (terrain != null)
        {
            terrain.seed = Random.Range(int.MinValue, int.MaxValue);
            terrain.Generate();
        }

        if (digController != null) digController.ResetOil(ComputeStartOil());

        treasureLocationRevealed = false;
        if (merchant != null) merchant.Reposition(terrain != null ? terrain.ShopMerchantCell : null);

        tutorialPage = 0;
        CurrentPhase = tutorialPages != null && tutorialPages.Length > 0 ? GamePhase.Tutorial : GamePhase.Digging;
    }

    float ComputeStartOil()
    {
        if (terrain == null) return 30f;
        Vector3Int col = terrain.WorldToCell(playerStartPosition);
        return terrain.GetStraightLineOilCost(col.x) * startOilMultiplier;
    }

    void HandleOilDepleted()
    {
        if (CurrentPhase == GamePhase.Digging) EnterGameOverPhase();
    }

    void HandleTreasureFirstSeen()
    {
        if (CurrentPhase != GamePhase.Digging) return;

        CurrentPhase = GamePhase.TreasureIntro;

        var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void HandleTreasureRevealed()
    {
        if (digController != null) digController.AddOil(treasureOilReward);
    }

    void HandleMerchantClicked()
    {
        if (CurrentPhase != GamePhase.Digging) return;

        CurrentPhase = GamePhase.Shopping;

        var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        if (CurrentPhase != GamePhase.Digging) return;

        if (player != null && terrain != null && player.position.y <= -terrain.depth + 1f)
        {
            EnterClearedPhase();
        }
    }

    void EnterClearedPhase()
    {
        CurrentPhase = GamePhase.Cleared;

        var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void EnterGameOverPhase()
    {
        CurrentPhase = GamePhase.GameOver;

        var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void StartNewDigPhase()
    {
        terrain.seed = Random.Range(int.MinValue, int.MaxValue);
        terrain.Generate();

        treasureLocationRevealed = false;
        if (merchant != null) merchant.Reposition(terrain.ShopMerchantCell);

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = playerStartPosition;
            }
            else
            {
                player.position = playerStartPosition;
            }
        }

        if (digController != null) digController.ResetOil(ComputeStartOil());

        CurrentPhase = GamePhase.Digging;
    }

    void OnGUI()
    {
        if (CurrentPhase == GamePhase.Tutorial)
        {
            DrawTutorial();
            return;
        }

        if (CurrentPhase == GamePhase.Cleared)
        {
            DrawClearedScreen();
            return;
        }

        if (CurrentPhase == GamePhase.GameOver)
        {
            DrawGameOverScreen();
            return;
        }

        if (CurrentPhase == GamePhase.TreasureIntro)
        {
            DrawTreasureIntro();
            return;
        }

        if (CurrentPhase == GamePhase.Shopping)
        {
            DrawShopScreen();
            return;
        }

        if (CurrentPhase == GamePhase.Digging)
        {
            DrawTreasureLocationMarker();
        }
    }

    void DrawTutorial()
    {
        float w = 420f, h = 280f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        var page = tutorialPages[tutorialPage];

        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        GUILayout.Label(page.title, titleStyle);

        var bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
        GUILayout.Label(page.body, bodyStyle, GUILayout.Height(140));

        var pageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label($"{tutorialPage + 1} / {tutorialPages.Length}", pageStyle);

        GUILayout.BeginHorizontal();
        GUI.enabled = tutorialPage > 0;
        if (GUILayout.Button("이전")) tutorialPage--;
        GUI.enabled = true;

        if (tutorialPage < tutorialPages.Length - 1)
        {
            if (GUILayout.Button("다음")) tutorialPage++;
        }
        else
        {
            if (GUILayout.Button("시작하기")) CurrentPhase = GamePhase.Digging;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    void DrawClearedScreen()
    {
        float w = 400f, h = 200f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);
        GUILayout.FlexibleSpace();

        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
        GUILayout.Label("스테이지 클리어!", titleStyle);

        GUILayout.Space(10);
        if (GUILayout.Button("다음 사이클 시작", GUILayout.Height(30))) StartNewDigPhase();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    void DrawGameOverScreen()
    {
        float w = 400f, h = 200f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);
        GUILayout.FlexibleSpace();

        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
        GUI.color = new Color(1f, 0.4f, 0.4f);
        GUILayout.Label("게임 오버", titleStyle);
        GUI.color = Color.white;

        var subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        GUILayout.Label("기름이 바닥났습니다", subStyle);

        GUILayout.Space(10);
        if (GUILayout.Button("다시 시작", GUILayout.Height(30))) StartNewDigPhase();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    void DrawTreasureIntro()
    {
        float w = 420f, h = 220f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);
        GUILayout.FlexibleSpace();

        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold };
        GUI.color = new Color(1f, 0.85f, 0.3f);
        GUILayout.Label("무언가 묻혀 있다!", titleStyle);
        GUI.color = Color.white;

        var bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, wordWrap = true };
        GUILayout.Label($"주변 3x3 영역에 보물이 묻혀 있는 것 같다.\n전부 파내면 기름을 크게 회복한다! (+{treasureOilReward:0.#})", bodyStyle, GUILayout.Height(70));

        GUILayout.Space(10);
        if (GUILayout.Button("확인", GUILayout.Height(30))) CurrentPhase = GamePhase.Digging;

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    void DrawShopScreen()
    {
        float w = 420f, h = 220f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        GUILayout.Label("상인", titleStyle);
        GUILayout.Label($"보유 기름: {(digController != null ? Mathf.CeilToInt(digController.oil) : 0)}");

        GUILayout.Space(10);

        bool revealAvailable = !treasureLocationRevealed && digController != null && digController.oil >= shopTreasureRevealCost;
        GUI.enabled = revealAvailable;
        string revealLabel = treasureLocationRevealed
            ? "보물 위치 표시 - 이미 활성화됨"
            : $"보물 위치 표시 ({shopTreasureRevealCost:0.#}기름)";
        if (GUILayout.Button(revealLabel, GUILayout.Height(36)))
        {
            digController.oil -= shopTreasureRevealCost;
            treasureLocationRevealed = true;
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        if (GUILayout.Button("닫기", GUILayout.Height(30))) CurrentPhase = GamePhase.Digging;

        GUILayout.EndArea();
    }

    void DrawTreasureLocationMarker()
    {
        if (!treasureLocationRevealed || terrain == null || !terrain.TreasureWorldCenter.HasValue || player == null) return;

        Vector3 delta = terrain.TreasureWorldCenter.Value - player.position;

        string text = "보물 위치: ";
        bool any = false;
        if (delta.x > 0.5f) { text += $"오른쪽 {delta.x:0}m"; any = true; }
        else if (delta.x < -0.5f) { text += $"왼쪽 {-delta.x:0}m"; any = true; }
        if (delta.y > 0.5f) { text += (any ? ", " : "") + $"위 {delta.y:0}m"; any = true; }
        else if (delta.y < -0.5f) { text += (any ? ", " : "") + $"아래 {-delta.y:0}m"; any = true; }
        if (!any) text += "바로 근처!";

        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(1f, 0.3f, 0.85f);
        GUI.Label(new Rect(10, Screen.height - 30, 400, 24), text, style);
    }
}
