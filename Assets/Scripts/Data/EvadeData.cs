using UnityEngine;

[CreateAssetMenu(fileName = "NewEvadeData", menuName = "Data/Evade Data")]
public class EvadeData : ScriptableObject
{
    [Header("Evade")]
    [Tooltip("회피 시 가해지는 순간적인 힘의 크기 (이동 거리)")]
    public float evadeForce = 10f;
    [Tooltip("회피 중 무적 및 이동 상태가 지속되는 시간 (초)")]
    public float dodgeDuration = 0.5f;
    [Tooltip("회피 중에 적용될 플레이어의 레이어 (주로 적의 공격을 무시하는 레이어)")]
    public int dodgingPlayerLayer;
    [Tooltip("회피 시 주변의 적을 밀어낼 반경")]
    public float pushRadius = 2f;
    [Tooltip("주변의 적을 밀어내는 힘의 크기")]
    public float pushForce = 0.05f;
    [Tooltip("적을 밀어내는 효과가 지속되는 시간")]
    public float pushSmoothTime = 0.2f;

    [Space(10)]
    [Header("Charges")]
    [Tooltip("최대 회피 충전 횟수")]
    public int maxEvadeCharges = 3;
    [Tooltip("회피 충전 1개당 재충전되는 데 걸리는 시간 (초)")]
    public float evadeChargeRegenTime = 2.0f;

    [Space(10)]
    [Header("Visuals")]
    [Tooltip("회피 잔상(Trail)이 지속되는 추가 시간")]
    public float trailOffset = 0.1f;
}
