// Assets/Scripts/Projectile.cs
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileData data;
    [SerializeField] private LayerMask targetLayer; // Inspector에서 Target 레이어를 지정해줘야 합니다.
    [SerializeField] private LayerMask playerLayers; // New: Layers that belong to the player (Unit or Vehicle)
    [SerializeField] private LayerMask justDodgeLayer; // Inspector에서 JustDodgeTrigger 레이어를 지정

    private Vector3 moveDirection;
    private float currentLifespan;
    private int shooterLayer; // To ignore collision with the shooter
    private float _damage; // [NEW] 실제 데미지 값을 저장할 필드

    // 오브젝트 풀에서 활성화될 때 호출될 함수
    public void Init(ProjectileData projectileData, Vector3 direction, int shooterGameObjectLayer, float overrideDamage) // [MODIFIED] overrideDamage 인자 추가
    {
        data = projectileData; // Assign the data
        moveDirection = direction.normalized;
        currentLifespan = data.lifespan;
        shooterLayer = shooterGameObjectLayer;
        _damage = overrideDamage; // [NEW] 전달받은 데미지로 설정
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

        // 충돌 감지는 OnTriggerEnter에서 처리하므로 여기서는 이동만 담당
        float moveDistance = data.speed * Time.deltaTime;
        transform.Translate(moveDirection * moveDistance, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 자기 자신(발사체)의 레이어와 충돌은 무시
        if (other.gameObject.layer == shooterLayer)
        {
            return;
        }

        // 저스트 회피 영역에 닿았는지 먼저 확인
        if (((1 << other.gameObject.layer) & justDodgeLayer) != 0)
        {
            OnGraze();
            return; // 저스트 회피 성공 시, 아래의 일반 피격 로직은 실행하지 않음
        }

        // 일반 피격 대상인지 확인 (targetLayer 또는 playerLayers)
        if (!(((1 << other.gameObject.layer) & targetLayer) != 0 || ((1 << other.gameObject.layer) & playerLayers) != 0))
        {
            // 대상이 아니면 아무것도 하지 않고 통과 (또는 벽과 같은 환경에 부딪혔을 때의 로직 추가 가능)
            // 예: if (other.gameObject.CompareTag("Environment")) { gameObject.SetActive(false); }
            return;
        }

        // --- Target Hit Logic ---
        Unit unit = other.GetComponentInParent<Unit>();
        if (unit != null)
        {
            if (unit.IsInvincible)
            {
                Debug.Log("Hit an invincible unit! No damage will be dealt."); // 디버그 로그 추가
                if (!string.IsNullOrEmpty(unit.guardEffectPoolTag) && EffectPoolManager.Instance != null)
                {
                    EffectPoolManager.Instance.GetPooledObject(unit.guardEffectPoolTag, other.ClosestPoint(transform.position), Quaternion.identity);
                }
                gameObject.SetActive(false);
                return;
            }
            else
            {
                unit.TakeDamage(_damage);
            }
        }
        else
        {
            Vehicle vehicle = other.GetComponentInParent<Vehicle>();
            if (vehicle != null)
            {
                vehicle.TakeDamage(_damage);
            }
            else
            {
                EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(_damage);
                }
            }
        }

        // 피격 이펙트 생성
        if (data.hitEffectPrefab != null)
        {
            // 충돌 지점에 이펙트 생성
            Instantiate(data.hitEffectPrefab, other.ClosestPoint(transform.position), Quaternion.LookRotation(other.transform.position - transform.position));
        }

        // 발사체 비활성화
        gameObject.SetActive(false);
    }


    // [NEW] 저스트 회피 트리거에 스쳤을 때 호출될 함수
    public void OnGraze()
    {
        Debug.Log("Projectile grazed! Deactivating.");
        // 여기에 발사체가 튕겨나가거나 특수 효과를 내는 로직을 추가할 수 있습니다.
        gameObject.SetActive(false); // 오브젝트 풀링 사용 시 비활성화
    }
}
