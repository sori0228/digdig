using UnityEngine;
using UnityEngine.InputSystem;

// 1단계: 실시간 2D 플랫포머 조작. A/D 이동, Space 점프. 중력은 Rigidbody2D 기본 물리를 사용한다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 6f;

    [Header("점프 (지형 1칸 = 1유닛 기준, 2~3칸 높이를 목표로 튜닝)")]
    public float jumpHeight = 2.5f;
    public float groundCheckDistance = 0.15f;
    public LayerMask groundMask = ~0;

    public WallCling wallCling;

    Rigidbody2D rb;
    CapsuleCollider2D col;
    bool grounded;
    bool jumpQueued;

    void Awake()
    {
        // 레이캐스트 시작점이 자기 자신의 콜라이더 경계에 걸쳐 있으면 그 콜라이더 자신을
        // 맞은 것으로 잡아버려서(자기 자신이 "바닥"이 됨) grounded가 항상 true가 되는
        // 버그가 있었다 - 이걸 꺼서 자기 자신은 절대 히트하지 않게 한다.
        Physics2D.queriesStartInColliders = false;

        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        rb.freezeRotation = true;
        if (wallCling == null) wallCling = GetComponent<WallCling>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpQueued = true;
    }

    void FixedUpdate()
    {
        // 벽에 매달려 있는 동안은 WallCling이 물리를 전담한다.
        if (wallCling != null && wallCling.IsClinging)
        {
            jumpQueued = false;
            return;
        }

        grounded = CheckGrounded();

        float x = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        }

        Vector2 v = rb.linearVelocity;
        v.x = x * moveSpeed;

        if (jumpQueued && grounded)
        {
            float g = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            v.y = Mathf.Sqrt(2f * g * jumpHeight);
        }
        jumpQueued = false;

        rb.linearVelocity = v;
    }

    bool CheckGrounded()
    {
        Vector2 origin = (Vector2)transform.position + col.offset - new Vector2(0f, col.size.y * 0.5f);
        var hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundMask);
        return hit.collider != null;
    }
}
