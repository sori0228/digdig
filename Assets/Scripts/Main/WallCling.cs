using UnityEngine;
using UnityEngine.InputSystem;

// 6단계: 벽 매달리기. 벽에 붙어 있는 동안 중력 0, 위치 고정. Space로 반대쪽으로 점프해서 뗀다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class WallCling : MonoBehaviour
{
    public float rayDistance = 0.15f;
    public LayerMask wallMask = ~0;
    public SpriteRenderer spriteRenderer;
    public Color clingColor = new Color(0.4f, 0.9f, 1f);
    public float jumpAwaySpeedX = 5f;
    public float jumpAwaySpeedY = 8f;

    Rigidbody2D rb;
    CapsuleCollider2D col;
    Color normalColor;
    float normalGravityScale;
    bool jumpAwayQueued;
    float moveInput;

    public bool IsClinging { get; private set; }
    public int WallDirection { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        normalGravityScale = rb.gravityScale;
        if (spriteRenderer != null) normalColor = spriteRenderer.color;
    }

    void Update()
    {
        moveInput = 0f;
        if (Keyboard.current == null) return;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput += 1f;

        if (IsClinging && Keyboard.current.spaceKey.wasPressedThisFrame) jumpAwayQueued = true;
    }

    void FixedUpdate()
    {
        int touch = DetectWall();

        if (!IsClinging)
        {
            bool pressingTowardWall = (touch < 0 && moveInput < 0f) || (touch > 0 && moveInput > 0f);
            if (touch != 0 && pressingTowardWall && rb.linearVelocity.y <= 0.05f)
                StartCling(touch);
            return;
        }

        if (jumpAwayQueued)
        {
            jumpAwayQueued = false;
            JumpAway();
            return;
        }

        bool releasing = touch != WallDirection
            || (touch < 0 && moveInput > 0f)
            || (touch > 0 && moveInput < 0f);

        if (releasing)
        {
            StopCling();
            return;
        }

        rb.linearVelocity = Vector2.zero;
    }

    // 좌우 콜라이더 경계에서 바깥으로 짧게 쏴서 벽 접촉을 감지한다 (자기 자신 콜라이더 오검출 방지).
    int DetectWall()
    {
        float halfWidth = col.size.x * 0.5f;
        Vector2 origin = (Vector2)transform.position + col.offset;

        var hitRight = Physics2D.Raycast(origin + new Vector2(halfWidth, 0f), Vector2.right, rayDistance, wallMask);
        if (hitRight.collider != null) return 1;

        var hitLeft = Physics2D.Raycast(origin - new Vector2(halfWidth, 0f), Vector2.left, rayDistance, wallMask);
        if (hitLeft.collider != null) return -1;

        return 0;
    }

    void StartCling(int dir)
    {
        IsClinging = true;
        WallDirection = dir;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        if (spriteRenderer != null) spriteRenderer.color = clingColor;
    }

    void StopCling()
    {
        IsClinging = false;
        WallDirection = 0;
        rb.gravityScale = normalGravityScale;
        if (spriteRenderer != null) spriteRenderer.color = normalColor;
    }

    void JumpAway()
    {
        int dir = -WallDirection;
        StopCling();
        rb.linearVelocity = new Vector2(dir * jumpAwaySpeedX, jumpAwaySpeedY);
    }
}
