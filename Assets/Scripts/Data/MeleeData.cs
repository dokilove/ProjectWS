using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewMeleeData", menuName = "Data/Melee Data")]
public class MeleeData : ScriptableObject
{
    [Header("콤보 공격 설정")]
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

    [Header("타이밍 설정")]
    [Tooltip("콤보 각 단계 이후 다음 콤보로 넘어가기 위한 대기 시간입니다. 이 시간을 초과하면 콤보가 초기화됩니다.")]
    public List<float> comboResetTimes;

    [Tooltip("차지 공격으로 인정되기 위해 버튼을 누르고 있어야 하는 최소 시간입니다.")]
    public float chargeTimeThreshold;
}
