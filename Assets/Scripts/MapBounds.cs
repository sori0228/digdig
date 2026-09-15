using UnityEngine;

// Creates invisible walls at the left/right edges of the map (so the player can't
// walk off the sides) and a floor at the bottom (so reaching the deepest point stops
// the fall instead of dropping into the void). Sized from TerrainGenerator's
// width/depth, so it stays correct even if those are changed in the Inspector.
public class MapBounds : MonoBehaviour
{
    public TerrainGenerator terrain;
    public float wallThickness = 0.5f;
    public float verticalMargin = 20f;

    BoxCollider2D leftWall;
    BoxCollider2D rightWall;
    BoxCollider2D bottomWall;

    void Awake()
    {
        leftWall = CreateWall("LeftWall");
        rightWall = CreateWall("RightWall");
        bottomWall = CreateWall("BottomWall");
    }

    void Start()
    {
        if (terrain == null) terrain = FindFirstObjectByType<TerrainGenerator>();
        Apply();
    }

    BoxCollider2D CreateWall(string wallName)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(transform);
        return go.AddComponent<BoxCollider2D>();
    }

    public void Apply()
    {
        if (terrain == null || leftWall == null || rightWall == null || bottomWall == null) return;

        float height = terrain.depth + verticalMargin * 2f;
        float centerY = -terrain.depth * 0.5f;

        leftWall.transform.position = new Vector3(-wallThickness * 0.5f, centerY, 0f);
        leftWall.size = new Vector2(wallThickness, height);

        rightWall.transform.position = new Vector3(terrain.width + wallThickness * 0.5f, centerY, 0f);
        rightWall.size = new Vector2(wallThickness, height);

        bottomWall.transform.position = new Vector3(terrain.width * 0.5f, -terrain.depth - wallThickness * 0.5f, 0f);
        bottomWall.size = new Vector2(terrain.width + wallThickness * 2f, wallThickness);
    }
}
