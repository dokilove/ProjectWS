using UnityEngine;

[CreateAssetMenu(fileName = "New ProjectileData", menuName = "Data/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    public int damage;
    public float speed;
    public float lifespan;
    public GameObject hitEffectPrefab;
    public GameObject projectilePrefab;
}