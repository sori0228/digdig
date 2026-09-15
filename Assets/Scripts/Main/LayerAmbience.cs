using UnityEngine;

// 10단계: 층별 배경색 전환 + 층 경계를 지날 때 이름을 잠깐 표시.
public class LayerAmbience : MonoBehaviour
{
    public GridWorld world;
    public Transform player;
    public Camera cam;
    public float colorLerpSpeed = 2f;
    public float toastDuration = 2f;

    int lastLayerIndex = -1;
    string toastText;
    float toastTimer;

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (world == null || player == null || cam == null) return;

        int depth = Mathf.Max(1, Mathf.RoundToInt(-player.position.y));
        var layer = world.GetLayer(depth);
        if (layer == null) return;

        cam.backgroundColor = Color.Lerp(cam.backgroundColor, layer.backgroundTint, Time.deltaTime * colorLerpSpeed);

        int idx = world.GetLayerIndex(depth);
        if (idx != lastLayerIndex)
        {
            if (lastLayerIndex != -1)
            {
                toastText = layer.name;
                toastTimer = toastDuration;
            }
            lastLayerIndex = idx;
        }

        if (toastTimer > 0f) toastTimer -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (toastTimer <= 0f || string.IsNullOrEmpty(toastText)) return;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01(toastTimer));
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 40), toastText, style);
    }
}
