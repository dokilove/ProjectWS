using UnityEngine;

[CreateAssetMenu(fileName = "New ProjectileData", menuName = "Data/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    public int damage;
    public float speed;
    public float lifespan;
    public GameObject hitEffectPrefab;
    public GameObject projectilePrefab;

    [Header("Hit Stun (피격 경직)")]
    [Tooltip("적중 시 대상에게 부여할 경직 시간 (초 단위, 기본값: 0.1초)")]
    public float hitStunDuration = 0.1f;
}