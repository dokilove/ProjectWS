// Assets/Scripts/Projectile.cs
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileData data;
    [SerializeField] private LayerMask targetLayer; // Inspector에서 Target 레이어를 지정해줘야 합니다.
    [SerializeField] private LayerMask playerLayers; // New: Layers that belong to the player (Unit or Vehicle)

    private Vector3 moveDirection;
    private float currentLifespan;
    private int shooterLayer; // To ignore collision with the shooter

    // 오브젝트 풀에서 활성화될 때 호출될 함수
    public void Init(ProjectileData projectileData, Vector3 direction, int shooterGameObjectLayer)
    {
        data = projectileData; // Assign the data
        moveDirection = direction.normalized;
        currentLifespan = data.lifespan;
        shooterLayer = shooterGameObjectLayer;

        // Optionally ignore collision with the shooter's layer
        // Physics.IgnoreLayerCollision(gameObject.layer, shooterLayer, true);
        // This might be better handled in Projectile.cs's collision logic or Unity's Physics settings
    }

    private void Update()
    {
        // 수명이 다하면 비활성화
        currentLifespan -= Time.deltaTime;
        if (currentLifespan <= 0)
        {
            gameObject.SetActive(false);
            return;
        }

        float moveDistance = data.speed * Time.deltaTime;

        // 이동하기 전에 해당 경로에 적이 있는지 Raycast로 확인
        // Raycast should ignore the shooter's layer
        LayerMask raycastLayerMask = targetLayer | playerLayers;
        // If shooterLayer is valid, remove it from the raycastLayerMask
        // This is a bit tricky with LayerMask, often easier to manage with Physics.IgnoreLayerCollision
        // For now, we'll rely on the targetLayer to define what it hits.
        // If the shooter is also in the targetLayer, we'd need more sophisticated logic.

        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDistance, raycastLayerMask))
        {
            // Check if the hit object is the shooter itself
            if (hit.collider.gameObject.layer == shooterLayer)
            {
                // Ignore collision with self and continue moving
                transform.Translate(moveDirection * moveDistance, Space.World);
                return;
            }

            // Target hit logic
            // Assuming targets have a health component or similar
            // For now, let's just log and disable
            Debug.Log($"Projectile hit {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

            // Example: Damage logic (needs to be adapted to your health system)
            // If the target has a Unit or Vehicle component, deal damage
            Unit unit = hit.collider.GetComponent<Unit>();
            if (unit != null)
            {
                if (unit.IsInvincible)
                {
                    // Play guard effect if unit is invincible
                    if (!string.IsNullOrEmpty(unit.guardEffectPoolTag) && EffectPoolManager.Instance != null)
                    {
                        EffectPoolManager.Instance.GetPooledObject(unit.guardEffectPoolTag, hit.point, Quaternion.identity);
                    }
                    gameObject.SetActive(false); // Deactivate projectile
                    return; // Do not proceed with damage
                }
                else
                {
                    unit.TakeDamage(data.damage);
                }
            }
            else
            {
                Vehicle vehicle = hit.collider.GetComponent<Vehicle>();
                if (vehicle != null)
                {
                    vehicle.TakeDamage(data.damage);
                }
                else
                {
                    // Fallback for other damageable objects, e.g., EnemyHealth
                    EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.TakeDamage(data.damage);
                    }
                }
            }

            // 피격 이펙트 생성
            if (data.hitEffectPrefab != null)
            {
                Instantiate(data.hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }

            // 발사체 비활성화
            gameObject.SetActive(false);
        }
        else
        {
            // 충돌하지 않았으면 그냥 이동
            transform.Translate(moveDirection * moveDistance, Space.World);
        }
    }
}
