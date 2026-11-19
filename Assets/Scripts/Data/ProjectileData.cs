using UnityEngine;

[CreateAssetMenu(fileName = "New ProjectileData", menuName = "Data/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    [Header("기본 공격력 및 속도 (Base Stats)")]
    [Tooltip("발사체 적중 시 기본 데미지")]
    public int damage = 20;

    [Tooltip("발사체 비행 속도 (m/s)")]
    public float speed = 30f;

    [Tooltip("발사체 최대 수명 (초 단위, 이 시간이 지나면 자동 소멸/풀 반환)")]
    public float lifespan = 3f;

    [Header("프리팹 및 이펙트 (Prefabs & Visuals)")]
    [Tooltip("실제 월드에 스폰/풀링할 발사체 본체 프리팹 (Projectile 컴포넌트 포함)")]
    public GameObject projectilePrefab;

    [Tooltip("단일 피격 또는 착탄 시 생성할 기본 피격 이펙트 프리팹")]
    public GameObject hitEffectPrefab;

    [Header("Hit Stun (피격 경직)")]
    [Tooltip("적중 시 대상에게 부여할 경직 시간 (초 단위, 기본값: 0.1초)")]
    public float hitStunDuration = 0.1f;

    [Header("Arcing Trajectory (곡사 궤적)")]
    [Tooltip("곡사(포물선) 비행 적용 여부")]
    public bool isArcing = false;
    [Tooltip("상향 발사 각도 (도 단위, 예: 25~45도)")]
    [Range(0f, 85f)]
    public float launchAngle = 30f;
    [Tooltip("중력 가속도 배수 (1.0 = 9.81m/s^2)")]
    public float gravityMultiplier = 2.5f;

    [Header("Flight Trail (비행 꼬리)")]
    [Tooltip("비행 중 발사체 뒤에 꼬리(TrailRenderer)를 표시할지 여부")]
    public bool showTrail = false;
    [Tooltip("꼬리가 남아 있는 시간 (초)")]
    public float trailTime = 0.35f;
    [Tooltip("꼬리 시작(발사체 쪽) 두께")]
    public float trailStartWidth = 0.25f;
    [Tooltip("꼬리 끝 두께")]
    public float trailEndWidth = 0f;
    [Tooltip("꼬리 시작(발사체 쪽) 색상")]
    public Color trailStartColor = new Color(1f, 0.85f, 0.4f, 0.9f);
    [Tooltip("꼬리 끝 색상 (알파 0이면 자연스럽게 사라짐)")]
    public Color trailEndColor = new Color(1f, 0.4f, 0.1f, 0f);
    [Tooltip("꼬리 머티리얼 (미할당 시 정점 색상을 쓰는 기본 언릿 머티리얼 사용)")]
    public Material trailMaterial;

    [Header("Trajectory Preview (예상 궤적)")]
    [Tooltip("이 발사체를 쏘는 차량 무기에 예상 궤적(포물선 + 착탄 지점)을 표시할지 여부 (곡사 발사체만 해당)")]
    public bool showTrajectoryPreview = false;

    [Header("Explosion / AoE (범위 폭발)")]
    [Tooltip("착탄 시 범위 폭발(스플래시) 적용 여부")]
    public bool isExplosive = false;
    [Tooltip("폭발 피해 반경 (미터)")]
    public float explosionRadius = 3.5f;
    [Tooltip("폭발 중심 기준 넉백 거리")]
    public float explosionKnockback = 2.0f;
    [Tooltip("폭발 전용 이펙트 프리팹 (미할당 시 hitEffectPrefab 사용)")]
    public GameObject explosionEffectPrefab;
}