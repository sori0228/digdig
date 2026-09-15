using UnityEngine;

// 시작할 때 조작법 + 블록 색깔 설명을 페이지로 보여주고, 닫기 전까지 플레이어 조작을 멈춘다.
public class TutorialSystem : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public string title;
        [TextArea(4, 10)] public string body;
    }

    public Transform player;
    public PlayerController playerController;
    public DigSystem digSystem;
    public WallCling wallCling;
    public WebSystem webSystem;

    public Page[] pages = new Page[]
    {
        new Page
        {
            title = "조작법",
            body = "A / D : 좌우 이동\n스페이스 : 점프\n좌클릭(누르고 있기) : 인접한 칸 파기\n\n벽에 붙으면(벽 쪽으로 이동+공중) 매달립니다.\n매달린 상태에서 스페이스 : 반대쪽으로 튕겨나가기\n매달린 상태에서 우클릭(누르고 있기) : 웹 발판 조준 후 설치\n아래 + 스페이스 : 발판 아래로 통과\n\n웹 발판은 탄창 5발로 시작하며, 한 번 쏠 때마다 기름 10을 추가로 소모합니다.\n탄창은 땅속의 거미줄 실타래를 캐야만 충전됩니다."
        },
        new Page
        {
            title = "블록 색깔 구분",
            body = "갈색 = 흙 (빨리 파짐)\n회색 = 암반 (오래 걸림, 폭탄 없이도 파지긴 하지만 느림)\n노란빛 도는 모래색 = 모래 (파괴하면 위에 쌓여있던 모래가 연쇄로 쏟아짐)\n\n반짝이는 노란 점 = 광물 (램프 빛이 닿아야만 보임)"
        },
        new Page
        {
            title = "기름과 목표",
            body = "화면 위 기름 게이지가 곧 램프의 시야 반경입니다.\n칸을 하나 팔 때마다 기름이 1씩 줄고, 기름이 적을수록 시야가 좁아집니다.\n기름은 연료 광물(석탄·기름주머니)을 캐야만 회복됩니다.\n\n목표 깊이에 도달한 뒤 전리품을 들고 지상으로 돌아오면 클리어.\n기름이 0이 되면 그동안 모은 전리품을 전부 잃습니다."
        },
    };

    int pageIndex;
    bool active = true;

    void Start()
    {
        SetFrozen(true);
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

    void OnGUI()
    {
        if (!active || pages == null || pages.Length == 0) return;

        float w = 480f, h = 400f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        var page = pages[pageIndex];

        var titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        GUILayout.Label(page.title, titleStyle);

        var bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
        GUILayout.Label(page.body, bodyStyle, GUILayout.Height(290));

        var pageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label($"{pageIndex + 1} / {pages.Length}", pageStyle);

        GUILayout.BeginHorizontal();
        GUI.enabled = pageIndex > 0;
        if (GUILayout.Button("이전")) pageIndex--;
        GUI.enabled = true;

        if (pageIndex < pages.Length - 1)
        {
            if (GUILayout.Button("다음")) pageIndex++;
        }
        else
        {
            if (GUILayout.Button("시작하기"))
            {
                active = false;
                SetFrozen(false);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
