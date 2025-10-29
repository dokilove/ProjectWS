using UnityEngine;

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Data/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public float fireRate;
    public ProjectileData projectileData;
    public float lockOnRadius = 15f; // Default value, can be overridden in asset
    public float attackAngle = 90f;  // Default value, can be overridden in asset
}