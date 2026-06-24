using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewMeleeData", menuName = "Data/Melee Data")]
public class MeleeData : ScriptableObject
{
    [Header("콤보 공격 설정")]
    [Header("▼▼ 아래 3개 리스트의 개수는 항상 일치시켜야 합니다! ▼▼")]
    [Tooltip("콤보 각 단계별 공격력입니다. 리스트의 크기가 최대 콤보 횟수가 됩니다.")]
    public List<float> comboDamages;

    [Tooltip("콤보 각 단계별 공격 반경(거리)입니다.")]
    public List<float> comboAttackRadii;

    [Tooltip("콤보 각 단계별 공격 각도입니다.")]
    public List<float> comboAttackAngles;

    [Header("차지 공격 설정")]
    [Tooltip("차지 공격의 공격력입니다.")]
    public float chargeAttackDamage;

    [Tooltip("차지 공격의 반경(거리)입니다.")]
    public float chargeAttackRadius;

    [Tooltip("차지 공격의 각도입니다.")]
    public float chargeAttackAngle;

    [Header("대쉬 설정")]
    [Tooltip("콤보 각 단계별 대쉬 힘입니다. 0이면 대쉬하지 않습니다.")]
    public List<float> comboDashForces;
    [Tooltip("차지 공격의 대쉬 힘입니다. 0이면 대쉬하지 않습니다.")]
    public float chargeAttackDashForce;

    [Header("타이밍 설정")]
    [Tooltip("공격 간 최소 간격(쿨다운)입니다. 너무 빠른 연속 입력을 방지합니다.")]
    public float attackCooldown = 0.2f;
    [Header("▼▼ 콤보 관련 리스트들과 개수를 일치시켜야 합니다! ▼▼")]
    [Tooltip("콤보 각 단계 이후 다음 콤보로 넘어가기 위한 대기 시간입니다. 이 시간을 초과하면 콤보가 초기화됩니다.")]
    public List<float> comboResetTimes;

    [Tooltip("차지 공격으로 인정되기 위해 버튼을 누르고 있어야 하는 최소 시간입니다.")]
    public float chargeTimeThreshold;

    [Header("락온 반경 설정")]
    [Tooltip("근접 모드에서 사용될 락온 반경입니다.")]
    public float meleeLockOnRadius = 5f;
}
