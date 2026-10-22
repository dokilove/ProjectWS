using UnityEngine;

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Data/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("사격 기본 설정 (Firing)")]
    [Tooltip("연사 속도 (초당 발사 횟수, 예: 2 = 1초에 2발 사격, 10 = 1초에 10발 사격)")]
    public float fireRate = 5f;

    [Tooltip("발사 시 생성할 탄환의 데이터 에셋 (ProjectileData)")]
    public ProjectileData projectileData;

    [Tooltip("목표 자동 탐색 및 락온 최대 반경 (미터 단위)")]
    public float lockOnRadius = 15f;

    [Header("사격 각도 및 터렛 설정 (Angle & Turret)")]
    [Tooltip("차량 터렛 자동 조준 사격 가능 각도 (도 단위, 예: 90 = 전방 기준 좌우 45도, 360 = 전방향)")]
    [Range(0f, 360f)]
    public float attackAngle = 90f;

    [Tooltip("플레이어가 차량 탑승 시 적용할 사격 각도 제한 (도 단위)")]
    [Range(0f, 360f)]
    public float vehicleMountedAttackAngle = 90f;

    [Header("탄 퍼짐 및 발사수 (Spread & Shot Count)")]
    [Tooltip("탄 퍼짐 각도 (0 = 정밀 직선 사격, 클수록 샷건처럼 넓게 퍼짐)")]
    public float spreadAngle = 0f;

    [Tooltip("1회 사격 시 동시에 발사되는 탄환 수 (기본: 1, 샷건/산탄총: 5~8 등)")]
    public int projectilesPerShot = 1;

    [Header("탄약 및 재장전 (Ammo & Reload)")]
    [Tooltip("탄창 용량 (재장전하기 전까지 발사할 수 있는 최대 탄환 수)")]
    public int magazineSize = 30;

    [Tooltip("재장전에 걸리는 시간 (초 단위)")]
    public float reloadTime = 1.5f;
}