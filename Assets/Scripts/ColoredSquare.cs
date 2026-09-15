using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ColoredSquare : MonoBehaviour
{
    public Color color = Color.white;

    [Tooltip("정사각형의 월드 유닛 한 변 길이. transform.localScale은 건드리지 않는다 " +
        "(레이캐스트 계산이 col.size를 직접 읽는 스크립트들과 어긋나지 않도록).")]
    public float worldSize = 1f;

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        float pixelsPerUnit = 1f / Mathf.Max(0.0001f, worldSize);
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sr.color = color;
    }
}
