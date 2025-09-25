// Assets/Scripts/Projectile.cs
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileData data;
    [SerializeField] private LayerMask enemyLayer; // Inspector에서 Enemy 레이어를 지정해줘야 합니다.

    private Vector3 moveDirection;
    private float currentLifespan;

    // 오브젝트 풀에서 활성화될 때 호출될 함수
    public void Initialize(Vector3 direction)
    {
        moveDirection = direction.normalized;
        currentLifespan = data.lifespan;
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
        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDistance, enemyLayer))
        {
            // 적과 충돌한 경우
            EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(data.damage);
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
