using System.Collections.Generic;
using UnityEngine;

// 8단계: 회수 시스템(인벤토리)과 승패 판정. 전리품(철/보석/큰보석) 6칸, 목표 깊이,
// 지상 귀환 클리어, 기름 고갈 게임오버(인벤토리 전부 상실) 화면까지 이 클래스가 관리한다.
// 클리어든 게임오버든 판이 끝나면 강화 페이즈로 이어진다 - 클리어라면 전리품 가치가 재화로 쌓이고,
// 게임오버라면 재화 없이(이미 다 잃었으니) 그 재화로 연료 최대량/웹 탄창을 영구 강화한 뒤
// "발굴 시작"을 누르면 강화가 반영된 새 판이 시작된다.
public class RunManager : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeOption
    {
        public string label;
        public int level;
        public int maxLevel = 5;
        public float amountPerLevel;
        public int baseCost = 20;
        public int costIncrement = 15;
        public int CurrentCost => baseCost + level * costIncrement;
    }

    public GameConfig config;
    public GridWorld world;
    public OilSystem oilSystem;
    public Transform player;
    public Vector3 playerStartPosition = new Vector3(8f, 2f, 0f);
    public PlayerController playerController;
    public DigSystem digSystem;
    public WallCling wallCling;
    public WebSystem webSystem;

    [Header("강화 페이즈")]
    public int currency;
    public UpgradeOption fuelUpgrade = new UpgradeOption { label = "연료 최대량", amountPerLevel = 15f, baseCost = 20, costIncrement = 15 };
    public UpgradeOption ammoUpgrade = new UpgradeOption { label = "웹 탄창", amountPerLevel = 1f, maxLevel = 5, baseCost = 25, costIncrement = 20 };

    readonly List<OreType> inventory = new List<OreType>();
    readonly List<OreType> lostItems = new List<OreType>();

    OreType pendingItem;
    bool choosingDiscard;
    bool deathHandled;
    int lastRunEarnings;

    bool targetReached;
    float maxDepthReached;
    public bool IsCleared { get; private set; }

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (world != null) world.OnOreCollected += HandleOreCollected;
    }

    void OnDestroy()
    {
        if (world != null) world.OnOreCollected -= HandleOreCollected;
    }

    void HandleOreCollected(Vector3Int cell, OreType ore)
    {
        if (ore.category != OreCategory.Loot) return; // 연료는 OreSystem이 처리한다.
        if (choosingDiscard || (oilSystem != null && oilSystem.IsGameOver)) return;

        if (inventory.Count < config.inventorySlots)
        {
            inventory.Add(ore);
            return;
        }

        pendingItem = ore;
        choosingDiscard = true;
        SetFrozen(true);
    }

    void DiscardExisting(int index)
    {
        inventory.RemoveAt(index);
        inventory.Add(pendingItem);
        ResolveDiscard();
    }

    void DiscardNew()
    {
        ResolveDiscard();
    }

    void ResolveDiscard()
    {
        pendingItem = null;
        choosingDiscard = false;
        SetFrozen(false);
    }

    void SetFrozen(bool frozen)
    {
        if (playerController != null) playerController.enabled = !frozen;
        if (digSystem != null) digSystem.enabled = !frozen;
        if (wallCling != null) wallCling.enabled = !frozen;
        if (webSystem != null) webSystem.enabled = !frozen;

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                if (frozen) rb.linearVelocity = Vector2.zero;
                rb.simulated = !frozen;
            }
        }
    }

    void Update()
    {
        if (oilSystem != null && oilSystem.IsGameOver)
        {
            if (!deathHandled)
            {
                deathHandled = true;
                lostItems.Clear();
                lostItems.AddRange(inventory);
                inventory.Clear();
            }
            return;
        }

        if (IsCleared || choosingDiscard || player == null) return;

        float depth = Mathf.Max(0f, -player.position.y);
        if (depth > maxDepthReached) maxDepthReached = depth;

        if (!targetReached && maxDepthReached >= config.targetDepth)
            targetReached = true;
    }

    // 지상 거점(ReturnPoint)에서 상호작용하면 호출된다 - 그 자리에서 바로 이번 판을 마무리한다.
    public void TryReturn()
    {
        if (IsCleared || choosingDiscard || (oilSystem != null && oilSystem.IsGameOver)) return;
        TriggerCleared();
    }

    void TriggerCleared()
    {
        IsCleared = true;

        lastRunEarnings = 0;
        foreach (var item in inventory) lastRunEarnings += Mathf.RoundToInt(item.value);
        currency += lastRunEarnings;

        SetFrozen(true);
    }

    void TryBuyUpgrade(UpgradeOption u)
    {
        if (u.level >= u.maxLevel || currency < u.CurrentCost) return;
        currency -= u.CurrentCost;
        u.level++;
    }

    // 강화 페이즈에서 "발굴 시작"을 누르면 호출된다 - 강화가 반영된 새 지형/기름/탄창으로 리셋한다.
    void StartNewDigPhase()
    {
        world.seed = Random.Range(int.MinValue, int.MaxValue);
        world.Generate();

        inventory.Clear();
        lostItems.Clear();
        pendingItem = null;
        choosingDiscard = false;
        deathHandled = false;
        targetReached = false;
        maxDepthReached = 0f;
        IsCleared = false;

        if (oilSystem != null) oilSystem.ResetForNewRun(config.startOil + fuelUpgrade.level * fuelUpgrade.amountPerLevel);
        if (webSystem != null) webSystem.ResetForNewRun(config.webMaxAmmo + Mathf.RoundToInt(ammoUpgrade.level * ammoUpgrade.amountPerLevel));

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

        SetFrozen(false);
    }

    void OnGUI()
    {
        bool gameOver = oilSystem != null && oilSystem.IsGameOver;

        if (gameOver || IsCleared)
        {
            DrawUpgradePhaseScreen(gameOver);
            return;
        }

        DrawDepthHud();
        DrawInventoryHud();

        if (choosingDiscard) DrawDiscardChoice();
        else if (targetReached) DrawTargetReachedToast();
    }

    void DrawDepthHud()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = Color.white;
        float depth = player != null ? Mathf.Max(0f, -player.position.y) : 0f;
        GUI.Label(new Rect(20, 90, 260, 24), $"깊이 {depth:0}m / 목표 {config.targetDepth:0}m", style);
    }

    void DrawInventoryHud()
    {
        float slotSize = 40f;
        float startX = 20f;
        float y = Screen.height - slotSize - 20f;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, wordWrap = true };
        style.normal.textColor = Color.white;

        for (int i = 0; i < config.inventorySlots; i++)
        {
            var rect = new Rect(startX + i * (slotSize + 6f), y, slotSize, slotSize);
            GUI.Box(rect, "");
            if (i < inventory.Count)
                GUI.Label(rect, inventory[i].oreName, style);
        }
    }

    void DrawDiscardChoice()
    {
        float w = 420f, h = 280f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        GUILayout.Label("인벤토리가 가득 찼다 - 무엇을 버릴까?", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true });
        GUILayout.Space(8);

        for (int i = 0; i < inventory.Count; i++)
        {
            var item = inventory[i];
            if (GUILayout.Button($"버리기: {item.oreName} (가치 {item.value:0})", GUILayout.Height(28)))
                DiscardExisting(i);
        }

        GUILayout.Space(8);
        if (pendingItem != null && GUILayout.Button($"새로 캔 {pendingItem.oreName}을(를) 포기하기", GUILayout.Height(28)))
            DiscardNew();

        GUILayout.EndArea();
    }

    void DrawTargetReachedToast()
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(0.6f, 1f, 0.6f);
        GUI.Label(new Rect(0, 20, Screen.width, 30), "목표 깊이 도달! 이제부터는 자유 - 지상으로 귀환하면 클리어", style);
    }

    // 클리어 결산(또는 게임오버 결산) + 강화 페이즈를 한 화면에서 처리한다.
    // 죽었을 때는 재화를 얻지 못하지만(이미 다 잃었으니), 강화 페이즈 자체는 똑같이 갈 수 있다 -
    // 이전 판들에서 모아둔 재화로 강화하고 다시 시작할 수 있게.
    void DrawUpgradePhaseScreen(bool isGameOver)
    {
        float w = 460f, h = 480f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        string title = isGameOver ? "게임 오버 - 강화 페이즈" : "클리어! - 강화 페이즈";
        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        if (isGameOver) titleStyle.normal.textColor = new Color(1f, 0.6f, 0.6f);
        GUILayout.Label(title, titleStyle);
        GUILayout.Space(6);

        if (isGameOver)
        {
            GUILayout.Label("기름이 바닥나서 들고 있던 전리품을 전부 잃었다.");
            if (lostItems.Count > 0)
                foreach (var item in lostItems) GUILayout.Label($"- {item.oreName} ({item.value:0}) [분실]");
            else
                GUILayout.Label("(잃어버린 전리품 없음)");
        }
        else
        {
            foreach (var item in inventory)
                GUILayout.Label($"- {item.oreName} ({item.value:0})");
            if (inventory.Count == 0) GUILayout.Label("(들고 온 전리품 없음)");

            GUILayout.Space(4);
            GUILayout.Label($"이번 판 획득 재화: {lastRunEarnings}");
        }

        GUILayout.Space(4);
        GUILayout.Label($"보유 재화: {currency}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

        GUILayout.Space(10);
        GUILayout.Label("강화", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
        DrawUpgradeRow(fuelUpgrade);
        DrawUpgradeRow(ammoUpgrade);

        GUILayout.Space(12);
        if (GUILayout.Button("발굴 시작", GUILayout.Height(34)))
            StartNewDigPhase();

        GUILayout.EndArea();
    }

    void DrawUpgradeRow(UpgradeOption u)
    {
        bool maxed = u.level >= u.maxLevel;
        bool affordable = currency >= u.CurrentCost;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"{u.label} Lv.{u.level}/{u.maxLevel} (+{u.amountPerLevel:0}/레벨)", GUILayout.Width(280));

        GUI.enabled = !maxed && affordable;
        if (GUILayout.Button(maxed ? "MAX" : $"강화 ({u.CurrentCost})", GUILayout.Width(110)))
            TryBuyUpgrade(u);
        GUI.enabled = true;

        GUILayout.EndHorizontal();
    }

}
