using UnityEngine;

[CreateAssetMenu(fileName = "New VehicleAI", menuName = "Data/Vehicle AI Behaviour")]
public class VehicleAIBehaviour : ScriptableObject
{
    [Header("차량 AI 추적 설정 (Follow Settings)")]
    [Tooltip("AI 차량이 목표를 추적할 때의 이동 속도")]
    public float followSpeed = 3f;

    [Tooltip("목표 대상과의 유지 거리 (이 거리 이내로 접근하면 정지)")]
    public float followStopDistance = 5f;

    [Tooltip("추적 경로 재계산 및 행동 쿨타임 (초 단위)")]
    public float followCoolTime = 2f;
}
