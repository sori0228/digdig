using UnityEngine;
using UnityEngine.InputSystem;

// 지상에 놓인 귀환 거점. 플레이어가 가까이 와서 상호작용(E)하면 그 자리에서 이번 판을 마무리한다.
public class ReturnPoint : MonoBehaviour
{
    public RunManager runManager;
    public Transform player;
    public float interactRange = 1.5f;

    bool playerNear;

    void Start()
    {
        if (runManager == null) runManager = FindFirstObjectByType<RunManager>();
    }

    void Update()
    {
        if (player == null) return;

        playerNear = Vector2.Distance(transform.position, player.position) <= interactRange;

        if (playerNear && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            runManager?.TryReturn();
    }

    void OnGUI()
    {
        if (!playerNear || runManager == null || runManager.IsCleared) return;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(0.6f, 1f, 0.7f);
        GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30), "E : 귀환하여 마무리하기", style);
    }
}
