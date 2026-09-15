using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float maxFallSpeed = 20f;
    public float jumpHeight = 1.2f;

    [Tooltip("Gravity scale used only while a jump is airborne. Much lower than the normal fall gravity, so the jump lingers near its peak instead of snapping through a quick parabola.")]
    public float jumpGravityScale = 0.5f;

    Rigidbody2D rb;
    bool grounded;
    bool jumpQueued;
    bool jumping;
    float normalGravityScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        normalGravityScale = rb.gravityScale;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpQueued = true;
    }

    void FixedUpdate()
    {
        if (GameCycleManager.CurrentPhase != GamePhase.Digging || Keyboard.current == null)
        {
            rb.linearVelocity = Vector2.zero;
            jumpQueued = false;
            return;
        }

        float x = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;

        Vector2 v = rb.linearVelocity;
        v.x = x * moveSpeed;

        if (jumpQueued && grounded)
        {
            jumping = true;
            rb.gravityScale = jumpGravityScale;
            float g = Mathf.Abs(Physics2D.gravity.y * jumpGravityScale);
            v.y = Mathf.Sqrt(2f * g * jumpHeight);
        }
        jumpQueued = false;

        // Once the jump arc comes back down and touches ground again, restore normal fall gravity.
        if (jumping && grounded && v.y <= 0f)
        {
            jumping = false;
            rb.gravityScale = normalGravityScale;
        }

        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
        rb.linearVelocity = v;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                grounded = true;
                return;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        grounded = false;
    }
}
