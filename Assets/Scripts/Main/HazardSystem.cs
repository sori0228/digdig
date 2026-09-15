using UnityEngine;

// 11단계: 가스 주머니. 파면 기름을 크게 잃고 주변 3x3이 파괴된다 (손해지만 길은 뚫린다).
public class HazardSystem : MonoBehaviour
{
    public GridWorld world;
    public OilSystem oilSystem;
    public CameraFollow cameraFollow;

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<GridWorld>();
        if (oilSystem == null) oilSystem = GetComponent<OilSystem>();
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();

        if (world != null) world.OnGasTriggered += HandleGasTriggered;
    }

    void OnDestroy()
    {
        if (world != null) world.OnGasTriggered -= HandleGasTriggered;
    }

    void HandleGasTriggered(Vector3Int cell)
    {
        if (oilSystem != null) oilSystem.ConsumeOil(world.gasDamage);
        world.DestroyArea(cell, world.gasBlastRadius);
        if (cameraFollow != null) cameraFollow.Shake(0.3f, 0.15f);
    }
}
