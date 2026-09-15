using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 2단계: 좌클릭을 누르고 있으면 마우스 방향의 인접 칸(상하좌우+대각선, 1칸 거리)을 판다.
// 블록마다 BlockType.digTime만큼 걸리고, 진행 중에는 원형 게이지를 표시한다.
// 마우스를 떼거나 다른 칸으로 옮기면 진행도가 초기화된다.
public class DigSystem : MonoBehaviour
{
    public GridWorld world;
    public Camera cam;

    Vector3Int activeCell;
    bool hasActiveCell;
    float progress;

    Canvas gaugeCanvas;
    Image gaugeFill;
    RectTransform gaugeRoot;

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (cam == null) cam = Camera.main;
        BuildGaugeUI();
    }

    void BuildGaugeUI()
    {
        var canvasGO = new GameObject("DigGaugeCanvas");
        gaugeCanvas = canvasGO.AddComponent<Canvas>();
        gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var bgGO = new GameObject("GaugeBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.sprite = MakeCircleSprite();
        bgImage.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRect = bgImage.rectTransform;
        bgRect.sizeDelta = new Vector2(48, 48);
        gaugeRoot = bgRect;

        var fillGO = new GameObject("GaugeFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        gaugeFill = fillGO.AddComponent<Image>();
        gaugeFill.sprite = MakeCircleSprite();
        gaugeFill.color = new Color(1f, 0.85f, 0.3f, 0.95f);
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Radial360;
        gaugeFill.fillOrigin = (int)Image.Origin360.Top;
        gaugeFill.fillAmount = 0f;
        var fillRect = gaugeFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        gaugeCanvas.gameObject.SetActive(false);
    }

    Sprite MakeCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size);
        var center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                tex.SetPixel(x, y, dist <= size / 2f ? Color.white : new Color(1, 1, 1, 0));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void Update()
    {
        if (world == null || cam == null || Mouse.current == null) return;

        if (!Mouse.current.leftButton.isPressed)
        {
            ResetProgress();
            return;
        }

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        mouseWorld.z = 0f;

        if (!TryPickAdjacentCell(mouseWorld, out Vector3Int targetCell))
        {
            ResetProgress();
            return;
        }

        var block = world.GetBlock(targetCell);
        if (block == null)
        {
            ResetProgress();
            return;
        }

        if (!hasActiveCell || targetCell != activeCell)
        {
            activeCell = targetCell;
            hasActiveCell = true;
            progress = 0f;
        }

        progress += Time.deltaTime;
        ShowGauge(targetCell, progress / Mathf.Max(0.01f, block.digTime));

        if (progress >= block.digTime)
        {
            world.RemoveBlock(targetCell);
            ResetProgress();
        }
    }

    bool TryPickAdjacentCell(Vector3 mouseWorld, out Vector3Int result)
    {
        Vector3Int playerCell = world.WorldToCell(transform.position);
        Vector2 dir = mouseWorld - transform.position;

        if (dir.sqrMagnitude < 0.0001f)
        {
            result = default;
            return false;
        }

        dir.Normalize();

        Vector3Int best = default;
        float bestDot = -2f;
        bool found = false;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                var candidate = new Vector2(dx, dy).normalized;
                float dot = Vector2.Dot(candidate, dir);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = playerCell + new Vector3Int(dx, dy, 0);
                    found = true;
                }
            }
        }

        result = best;
        return found;
    }

    void ShowGauge(Vector3Int cell, float pct)
    {
        gaugeCanvas.gameObject.SetActive(true);
        gaugeFill.fillAmount = Mathf.Clamp01(pct);

        Vector3 worldCenter = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        Vector3 screenPos = cam.WorldToScreenPoint(worldCenter);
        gaugeRoot.position = screenPos;
    }

    void ResetProgress()
    {
        hasActiveCell = false;
        progress = 0f;
        if (gaugeCanvas != null) gaugeCanvas.gameObject.SetActive(false);
    }
}
