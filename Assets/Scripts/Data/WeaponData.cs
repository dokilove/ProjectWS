using UnityEngine;

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Data/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public float fireRate;
    public ProjectileData projectileData;
    public float lockOnRadius = 15f; // Default value, can be overridden in asset

    [Header("Vehicle Only")]
    public float attackAngle = 90f;  // Default value for vehicle turret auto-aim
    public float vehicleMountedAttackAngle = 90f; // 플레이어가 차량 탑승 시 적용할 사격 각도 제한

    [Header("Spread")]
    public float spreadAngle = 0f;
    public int projectilesPerShot = 1;

    [Header("Ammo")]
        public int magazineSize = 30;
    public float reloadTime = 1.5f;
    }
    