using UnityEngine;

[CreateAssetMenu(fileName = "BlockType", menuName = "땅파기/BlockType")]
public class BlockType : ScriptableObject
{
    public string blockName;
    public Sprite sprite;
    [Tooltip("이 블록 하나를 파는 데 걸리는 시간(초).")]
    public float digTime = 0.3f;
    [Tooltip("파괴됐을 때 위 칸이 이 종류면 연쇄로 한 칸씩 떨어지는가 (모래).")]
    public bool chainFall = false;
}
