using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;

    [Tooltip("Optional: tints the camera background per depth layer for a 'descending' feel.")]
    public TerrainGenerator terrain;
    public float colorLerpSpeed = 2f;

    Vector3 velocity;
    Camera cam;
    float shakeTimer;
    float shakeMagnitude;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (terrain == null) terrain = FindFirstObjectByType<TerrainGenerator>();
    }

    // 11단계: 가스 폭발 등에서 화면을 짧게 흔든다.
    public void Shake(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            transform.position += (Vector3)(Random.insideUnitCircle * shakeMagnitude);
        }

        if (cam != null && terrain != null)
        {
            int depth = Mathf.Max(1, Mathf.RoundToInt(-target.position.y));
            Color tint = terrain.GetBackgroundTint(depth);
            cam.backgroundColor = Color.Lerp(cam.backgroundColor, tint, Time.deltaTime * colorLerpSpeed);
        }
    }
}
