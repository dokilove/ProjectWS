using UnityEngine;

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Data/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public float fireRate;
    public ProjectileData projectileData;
    public float lockOnRadius = 15f; // Default value, can be overridden in asset

    [Header("Vehicle Only")]
    public float attackAngle = 90f;  // Default value, can be overridden in asset

    [Header("Spread")]
    public float spreadAngle = 0f;
    public int projectilesPerShot = 1;

    [Header("Ammo")]
        public int magazineSize = 30;
    public float reloadTime = 1.5f;
    }
    