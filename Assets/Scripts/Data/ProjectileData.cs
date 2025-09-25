// Assets/Scripts/Data/ProjectileData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "New Projectile Data", menuName = "Data/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    public float damage = 10f;
    public float speed = 20f;
    public float lifespan = 3f; // 이 시간이 지나면 발사체 자동 비활성화
    public GameObject hitEffectPrefab; // 피격 시 생성될 이펙트 (옵션)
}
