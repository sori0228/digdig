using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "땅파기/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("맵")]
    public int mapWidth = 16;
    public int mapDepth = 40;

    [Header("기름 (턴제 - 파기 1회 = 1턴)")]
    public float startOil = 90f;
    public float oilCostPerDig = 1f;

    [Header("램프 빛 반경 (기름 비율로 선형 보간)")]
    public float lampRadiusMax = 7f;
    public float lampRadiusMin = 1.5f;

    [Header("낙하")]
    public float fallStunThreshold = 5f;
    public float fallStunDuration = 1f;
    public float fallStunOilLoss = 5f;

    [Header("웹 발판 (사용마다 고정 기름 비용 + 탄창 소모)")]
    public float webOilCost = 10f;
    public int webMaxAmmo = 5;
    public int webMaxLength = 6;

    [Header("인벤토리")]
    public int inventorySlots = 6;

    [Header("스테이지")]
    public float targetDepth = 20f;
}
