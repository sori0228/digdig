using UnityEngine;

public enum OreCategory { Fuel, Loot, Ammo }

[CreateAssetMenu(fileName = "OreType", menuName = "땅파기/OreType")]
public class OreType : ScriptableObject
{
    public string oreName;
    public Sprite sprite;
    public OreCategory category;
    [Tooltip("연료면 즉시 회복되는 기름, 전리품이면 귀환 시 가치, 탄창이면 충전되는 웹 발판 개수.")]
    public float value;
}
