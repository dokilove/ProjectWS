using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 근접 공격 히트박스 판정 및 시각화 형태
/// </summary>
public enum MeleeHitboxShape
{
    Sector = 0, // 부채꼴 (베기, 회전베기, 광역기)
    Box = 1,    // 직사각형 박스 (찌르기, 돌진, 좁은 직선 판정)
    Circle = 2  // 전방 원형/구형 (내려찍기, 바닥 강타, 지면 폭발)
}

/// <summary>
/// 다단히트 공격의 개별 타격 정보 (임의 타이밍, 데미지, 대시, 형태/범위)
/// </summary>
[Serializable]
public class MeleeSubHit
{
    [Tooltip("모션 시작(0초) 후 이 타격이 터지는 시점 (초 단위, 예: 0.08, 0.22, 0.35)")]
    public float hitTime = 0.08f;

    [Tooltip("해당 타격의 데미지")]
    public float damage = 15f;

    [Tooltip("해당 타격 적중 시 대상에게 부여할 경직 시간 (초 단위)")]
    public float hitStunDuration = 0.15f;

    [Tooltip("해당 타격 순간의 추가 돌진/대시 힘 (0이면 없음, 음수면 백스텝)")]
    public float dashForce = 0f;

    [Tooltip("해당 타격 전용 비주얼 머티리얼 (선택)")]
    public Material overrideMaterial;

    [Header("공격 판정 형태 및 범위")]
    [Tooltip("해당 타격의 히트박스 형태")]
    public MeleeHitboxShape shape = MeleeHitboxShape.Sector;
    public float attackRadius = 5f;
    [Range(0f, 360f)] public float attackAngle = 90f;
    public float boxWidth = 2f;
    public float boxLength = 4f;
    public float forwardOffset = 0f;
}

[Serializable]
public class MeleeComboStep
{
    [Tooltip("콤보 단계 식별 이름 (예: 1타, 2타, 3타)")]
    public string stepName = "Combo Step";

    [Header("공격 판정 형태")]
    [Tooltip("공격 히트박스 형태 (부채꼴 / 직사각형 박스 / 전방 원형)")]
    public MeleeHitboxShape shape = MeleeHitboxShape.Sector;

    [Header("부채꼴(Sector) / 원형(Circle) 설정")]
    [Tooltip("공격 사거리 및 원형 반경")]
    public float attackRadius = 5f;

    [Tooltip("부채꼴 공격 각도 (도 단위, Sector 전용)")]
    [Range(0f, 360f)]
    public float attackAngle = 90f;

    [Header("직사각형(Box) 설정")]
    [Tooltip("박스 가로 너비 (Box 전용)")]
    public float boxWidth = 2f;

    [Tooltip("박스 전방 길이 (Box 전용)")]
    public float boxLength = 4f;

    [Header("오프셋 & 위력 (단발 기준)")]
    [Tooltip("캐릭터 전방으로 중심점 오프셋 거리 (Box / Circle 전용)")]
    public float forwardOffset = 0f;

    [Tooltip("기본 단발 데미지 (다단히트 미사용 시)")]
    public float damage = 20f;

    [Tooltip("단발 공격 적중 시 대상에게 부여할 경직 시간 (초 단위)")]
    public float hitStunDuration = 0.2f;

    [Tooltip("공격 시작 시 전방 대쉬 힘 (0이면 대쉬하지 않음)")]
    public float dashForce = 100f;

    [Tooltip("해당 단계 전용 비주얼 머티리얼 (선택)")]
    public Material overrideMaterial;

    [Header("다단히트 (Multi-Hit) 커스텀 설정")]
    [Tooltip("체크 시 아래 subHits 목록의 임의 타이밍과 데미지/대시를 순차적으로 실행합니다.")]
    public bool useMultiHit = false;

    [Tooltip("임의의 시점(초), 데미지, 대시력을 갖는 개별 타격 목록")]
    public List<MeleeSubHit> subHits = new List<MeleeSubHit>();

    [Header("타이밍 설정 (초 단위)")]
    [Tooltip("단발 공격 시 버튼 입력 후 실제 데미지/이펙트가 터지는 시점 (선딜레이)")]
    public float hitDelay = 0.08f;

    [Tooltip("다음 콤보 입력이나 회피로 캔슬 가능한 시점 (선입력/캔슬 윈도우 시작)")]
    public float cancelWindowTime = 0.2f;

    [Tooltip("이 공격 모션의 전체 동작 시간 (다음 행동 제한)")]
    public float totalDuration = 0.35f;

    [Tooltip("다음 콤보 입력을 기다리는 제한 시간 (초과 시 1타로 리셋)")]
    public float comboResetTime = 0.7f;
}

[Serializable]
public class MeleeChargeAttackData
{
    [Header("공격 판정 형태")]
    [Tooltip("차지 공격 히트박스 형태 (기본: 부채꼴 260도 광역 회전베기)")]
    public MeleeHitboxShape shape = MeleeHitboxShape.Sector;

    [Header("부채꼴(Sector) / 원형(Circle) 설정")]
    [Tooltip("차지 공격의 사거리 및 원형 반경")]
    public float attackRadius = 8f;

    [Tooltip("차지 공격의 각도 (Sector 전용)")]
    [Range(0f, 360f)]
    public float attackAngle = 260f;

    [Header("직사각형(Box) 설정")]
    [Tooltip("박스 가로 너비 (Box 전용)")]
    public float boxWidth = 3f;

    [Tooltip("박스 전방 길이 (Box 전용)")]
    public float boxLength = 6f;

    [Header("오프셋 & 위력 (단발 기준)")]
    [Tooltip("캐릭터 전방으로 중심점 오프셋 거리 (Box / Circle 전용)")]
    public float forwardOffset = 0f;

    [Tooltip("차지 공격의 기본 데미지")]
    public float damage = 120f;

    [Tooltip("차지 공격 적중 시 대상에게 부여할 경직 시간 (초 단위)")]
    public float hitStunDuration = 0.6f;

    [Tooltip("차지 공격의 대쉬 힘 (음수면 백스텝)")]
    public float dashForce = -100f;

    [Tooltip("차지 공격 전용 비주얼 머티리얼 (선택)")]
    public Material overrideMaterial;

    [Header("다단히트 (Multi-Hit) 커스텀 설정")]
    [Tooltip("체크 시 아래 subHits 목록의 임의 타이밍과 데미지/대시를 순차적으로 실행합니다.")]
    public bool useMultiHit = false;

    [Tooltip("차지 공격 중 발생하는 개별 타격 목록 (예: 회전 4연타)")]
    public List<MeleeSubHit> subHits = new List<MeleeSubHit>();

    [Header("타이밍 설정 (초 단위)")]
    [Tooltip("차지 공격으로 인정되기 위해 버튼을 누르고 있어야 하는 최소 시간")]
    public float chargeTimeThreshold = 1.0f;

    [Tooltip("단발 공격 시 릴리즈 후 실제 데미지/이펙트가 터지는 시점 (선딜레이)")]
    public float hitDelay = 0.1f;

    [Tooltip("차지 공격의 전체 동작 시간")]
    public float totalDuration = 0.5f;
}

[CreateAssetMenu(fileName = "NewMeleeData", menuName = "Data/Melee Data")]
public class MeleeData : ScriptableObject
{
    [Header("콤보 공격 단계 목록 (순서대로 1타, 2타, 3타...)")]
    public List<MeleeComboStep> comboSteps = new List<MeleeComboStep>();

    [Header("차지 공격 설정")]
    public MeleeChargeAttackData chargeAttack = new MeleeChargeAttackData();

    [Header("락온 반경 설정")]
    [Tooltip("근접 모드에서 사용될 락온 반경")]
    public float meleeLockOnRadius = 6f;
}
