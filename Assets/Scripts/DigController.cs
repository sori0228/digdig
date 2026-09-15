using UnityEngine;
using UnityEngine.InputSystem;

public enum ToolType { Pickaxe, Drill, Bomb }

public class DigController : MonoBehaviour
{
    [Header("Refs")]
    public TerrainGenerator terrain;
    public Camera cam;

    [Header("Lamp Oil (single resource for everything - set by GameCycleManager at the start of each dig phase)")]
    public float oil = 30f;
    public float maxOil = 30f;

    [Header("Tool Oil Cost (fixed per use, regardless of tile hardness)")]
    public float pickaxeOilCost = 1f;
    public float drillOilCost = 2f;
    public float bombOilCost = 4f;

    [Header("Interaction")]
    public float interactRange = 3f;

    public event System.Action OnOilDepleted;
    bool oilDepletedFired;

    ToolType selectedTool = ToolType.Pickaxe;
    readonly Rect hudRect = new Rect(10, 10, 300, 150);
    readonly Rect oilBarRect = new Rect(10, 170, 300, 22);

    string message = "";
    float messageTimer = 0f;
    bool showHardness = false;

    void Start()
    {
        if (terrain == null) terrain = FindFirstObjectByType<TerrainGenerator>();
        if (cam == null) cam = Camera.main;
    }

    // Called by GameCycleManager at the start of every dig phase (new map, new straight-line cost).
    public void ResetOil(float amount)
    {
        maxOil = Mathf.Max(1f, amount);
        oil = maxOil;
        oilDepletedFired = false;
    }

    void ConsumeOil(float amount)
    {
        if (amount <= 0f) return;

        oil = Mathf.Max(0f, oil - amount);
        if (oil <= 0f && !oilDepletedFired)
        {
            oilDepletedFired = true;
            OnOilDepleted?.Invoke();
        }
    }

    public void AddOil(float amount)
    {
        if (amount <= 0f) return;
        oil = Mathf.Min(maxOil, oil + amount);
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) selectedTool = ToolType.Pickaxe;
            if (Keyboard.current.digit2Key.wasPressedThisFrame) selectedTool = ToolType.Drill;
            if (Keyboard.current.digit3Key.wasPressedThisFrame) selectedTool = ToolType.Bomb;
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f) message = "";
        }

        if (GameCycleManager.CurrentPhase == GamePhase.Digging && IsCrushedBySand())
            ConsumeOil(terrain.sandCrushDamagePerSecond * Time.deltaTime);

        if (GameCycleManager.CurrentPhase != GamePhase.Digging) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverHud())
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            worldPos.z = 0f;
            TryDig(worldPos);
        }
    }

    // Crushed if sand occupies the player's own cell (buried by a fall), or if sand
    // blocks both opposite sides of the cell the player is standing in (pinned in a gap).
    bool IsCrushedBySand()
    {
        if (terrain == null) return false;

        Vector3Int cell = terrain.WorldToCell(transform.position);
        return terrain.IsSand(cell)
            || (terrain.IsSand(cell + Vector3Int.left) && terrain.IsSand(cell + Vector3Int.right))
            || (terrain.IsSand(cell + Vector3Int.up) && terrain.IsSand(cell + Vector3Int.down));
    }

    bool IsPointerOverHud()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 guiPoint = new Vector2(mousePos.x, Screen.height - mousePos.y);
        return hudRect.Contains(guiPoint) || oilBarRect.Contains(guiPoint);
    }

    float CostFor(ToolType t)
    {
        switch (t)
        {
            case ToolType.Pickaxe: return pickaxeOilCost;
            case ToolType.Drill: return drillOilCost;
            case ToolType.Bomb: return bombOilCost;
        }
        return 0f;
    }

    void ShowMessage(string msg)
    {
        message = msg;
        messageTimer = 1.2f;
    }

    void TryDig(Vector3 worldPos)
    {
        if (terrain == null) return;

        float cost = CostFor(selectedTool);
        if (oil < cost)
        {
            ShowMessage("기름이 부족합니다");
            return;
        }

        if (Vector2.Distance(transform.position, worldPos) > interactRange)
        {
            ShowMessage("사거리 밖입니다 (플레이어 주변만 가능)");
            return;
        }

        Vector3Int clickedCell = terrain.WorldToCell(worldPos);
        if (!terrain.HasTile(clickedCell))
            return; // empty space (already dug, or open sky): no interaction at all

        ConsumeOil(cost);

        switch (selectedTool)
        {
            case ToolType.Pickaxe:
                DigCellWithFeedback(clickedCell);
                break;
            case ToolType.Drill:
                DigDrill(clickedCell);
                break;
            case ToolType.Bomb:
                DigArea(clickedCell, 1);
                break;
        }
    }

    // Pickaxe hits a single target, so it's worth telling the player how much progress they made.
    void DigCellWithFeedback(Vector3Int cell)
    {
        if (!terrain.Dig(cell, ToolType.Pickaxe, out bool broke)) return;

        if (!broke)
        {
            int hits = terrain.GetDamage(cell);
            int needed = terrain.GetRequiredHits(cell, ToolType.Pickaxe);
            ShowMessage($"단단합니다! 진행 중 ({hits}/{needed})");
        }
    }

    void DigCell(Vector3Int cell, ToolType tool)
    {
        terrain.Dig(cell, tool, out _);
    }

    void DigArea(Vector3Int center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                DigCell(center + new Vector3Int(dx, dy, 0), ToolType.Bomb);
    }

    void DigDrill(Vector3Int clickedCell)
    {
        Vector3Int playerCell = terrain.WorldToCell(transform.position);
        Vector3Int delta = clickedCell - playerCell;

        Vector3Int dir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? new Vector3Int(delta.x >= 0 ? 1 : -1, 0, 0)
            : new Vector3Int(0, delta.y >= 0 ? 1 : -1, 0);

        Vector3Int cell = playerCell;
        for (int i = 0; i < 3; i++)
        {
            cell += dir;
            DigCell(cell, ToolType.Drill);
        }
    }

    void OnGUI()
    {
        if (GameCycleManager.CurrentPhase != GamePhase.Digging) return;

        DrawOilBar();

        GUI.Box(hudRect, "");
        GUILayout.BeginArea(hudRect);
        GUILayout.Label($"선택된 도구: {ToolLabel(selectedTool)} (1/2/3 키)");

        GUILayout.BeginHorizontal();
        GUI.enabled = oil >= pickaxeOilCost;
        if (GUILayout.Button($"곡괭이 ({pickaxeOilCost:0.#}기름)")) selectedTool = ToolType.Pickaxe;
        GUI.enabled = oil >= drillOilCost;
        if (GUILayout.Button($"드릴 ({drillOilCost:0.#}기름)")) selectedTool = ToolType.Drill;
        GUI.enabled = oil >= bombOilCost;
        if (GUILayout.Button($"폭탄 ({bombOilCost:0.#}기름)")) selectedTool = ToolType.Bomb;
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (GUILayout.Button(showHardness ? "경도 보기 끄기" : "경도 보기 켜기")) showHardness = !showHardness;

        if (!string.IsNullOrEmpty(message))
        {
            GUI.color = Color.yellow;
            GUILayout.Label(message);
            GUI.color = Color.white;
        }

        GUILayout.EndArea();

        DrawHardnessOverlay();
    }

    void DrawOilBar()
    {
        GUI.Box(oilBarRect, "");

        float pct = maxOil > 0f ? Mathf.Clamp01(oil / maxOil) : 0f;
        var fillRect = new Rect(oilBarRect.x + 2, oilBarRect.y + 2, (oilBarRect.width - 4) * pct, oilBarRect.height - 4);

        var prevColor = GUI.color;
        GUI.color = Color.Lerp(new Color(0.8f, 0.2f, 0.15f), new Color(1f, 0.75f, 0.2f), pct);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        GUI.Label(oilBarRect, $"기름 {Mathf.CeilToInt(oil)} / {Mathf.CeilToInt(maxOil)}", style);
    }

    void DrawHardnessOverlay()
    {
        if (!showHardness || terrain == null || cam == null || !cam.orthographic) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 camPos = cam.transform.position;
        float minX = camPos.x - halfW, maxX = camPos.x + halfW;
        float minY = camPos.y - halfH, maxY = camPos.y + halfH;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        style.normal.textColor = Color.white;

        foreach (var kv in terrain.HardnessAt)
        {
            float wx = kv.Key.x + 0.5f;
            float wy = kv.Key.y + 0.5f;
            if (wx < minX || wx > maxX || wy < minY || wy > maxY) continue;

            Vector3 screen = cam.WorldToScreenPoint(new Vector3(wx, wy, 0f));
            float guiX = screen.x;
            float guiY = Screen.height - screen.y;
            GUI.Label(new Rect(guiX - 12, guiY - 8, 24, 16), kv.Value.ToString("0.#"), style);
        }
    }

    string ToolLabel(ToolType t)
    {
        switch (t)
        {
            case ToolType.Pickaxe: return "곡괭이 (1칸)";
            case ToolType.Drill: return "드릴 (직선 3칸)";
            case ToolType.Bomb: return "폭탄 (3x3)";
        }
        return "";
    }
}
