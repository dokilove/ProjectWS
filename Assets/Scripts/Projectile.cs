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

    private static int _enemyAttackLayerMask = -1;

    private void Awake()
    {
        if (_enemyAttackLayerMask == -1)
        {
            _enemyAttackLayerMask = LayerMask.GetMask("EnemyAttackLayer");
            if (_enemyAttackLayerMask == 0)
            {
                _enemyAttackLayerMask = 1 << 15;
            }
        }
    }

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

        // 고속 이동 시 발사체 간 터널링(통과 현상) 방지를 위한 전방 충돌 검사
        float moveDistance = data.speed * Time.deltaTime;
        CheckProjectileCollision(moveDistance);

        if (!gameObject.activeInHierarchy) return;

        // 충돌 감지는 OnTriggerEnter에서도 처리하므로 여기서는 이동만 담당
        transform.Translate(moveDirection * moveDistance, Space.World);
    }

    private void CheckProjectileCollision(float moveDistance)
    {
        if (!gameObject.activeInHierarchy) return;

        // 플레이어/차량 발사체는 적 발사체 레이어를, 적 발사체는 플레이어/차량 발사체 레이어(0)를 탐색
        int targetMask = IsPlayerSide() ? _enemyAttackLayerMask : (1 << 0);

        if (Physics.SphereCast(transform.position, 0.3f, moveDirection, out RaycastHit hit, moveDistance, targetMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.gameObject == gameObject) return;

            Projectile otherProjectile = hit.collider.GetComponentInParent<Projectile>();
            if (otherProjectile != null && IsOpposingProjectile(otherProjectile))
            {
                CollideWithProjectile(otherProjectile);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy || !other.gameObject.activeInHierarchy)
        {
            return;
        }

        // 자기 자신(발사체)의 발사자 레이어와 충돌은 무시
        if (other.gameObject.layer == shooterLayer)
        {
            return;
        }

        // 1. 발사체끼리의 충돌 검사 (적대적 발사체와 충돌 시 상호 파괴)
        Projectile otherProjectile = other.GetComponentInParent<Projectile>();
        if (otherProjectile != null)
        {
            if (IsOpposingProjectile(otherProjectile))
            {
                CollideWithProjectile(otherProjectile);
            }
            return;
        }

        // 2. 저스트 회피 영역에 닿았는지 먼저 확인
        if (((1 << other.gameObject.layer) & justDodgeLayer) != 0)
        {
            OnGraze();
            return; // 저스트 회피 성공 시, 아래의 일반 피격 로직은 실행하지 않음
        }

        // 3. 일반 피격 대상인지 확인 (targetLayer 또는 playerLayers)
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
                // Unit is invincible (e.g., during a normal dodge), so do nothing.
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
                    float stunDuration = data != null ? data.hitStunDuration : 0.1f;
                    enemyHealth.TakeDamage(_damage, stunDuration);
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

    /// <summary>
    /// 플레이어의 근접 공격에 의해 발사체가 파괴/소멸될 때 호출됩니다.
    /// </summary>
    public void DestroyByMelee(string effectTag = null)
    {
        // 피격/소멸 이펙트 재생 (EffectPoolManager 우선, 미등록 시 hitEffectPrefab 생성)
        if (!string.IsNullOrEmpty(effectTag) && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(effectTag, transform.position, Quaternion.identity);
        }
        else if (data != null && data.hitEffectPrefab != null)
        {
            Instantiate(data.hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // 발사체 비활성화
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 이 발사체가 플레이어 또는 아군(차량 등) 측에서 발사된 것인지 여부
    /// </summary>
    public bool IsPlayerSide()
    {
        // shooterLayer가 Player(8), DodgingPlayer(9), Vehicle(11)이거나 targetLayer에 Enemy(10)가 포함된 경우
        return shooterLayer == 8 || shooterLayer == 9 || shooterLayer == 11 || 
               ((1 << 10) & targetLayer) != 0;
    }

    /// <summary>
    /// 상대 발사체가 적대적인 진영의 발사체인지 판별 (아군 발사체끼리는 충돌 무시)
    /// </summary>
    public bool IsOpposingProjectile(Projectile other)
    {
        if (other == null) return false;
        return this.IsPlayerSide() != other.IsPlayerSide();
    }

    /// <summary>
    /// 적대적 발사체와 정면 충돌하여 서로 상쇄/소멸할 때 호출
    /// </summary>
    public void CollideWithProjectile(Projectile other)
    {
        if (!gameObject.activeInHierarchy) return;

        Vector3 impactPoint = transform.position;
        if (other != null && other.gameObject.activeInHierarchy)
        {
            impactPoint = (transform.position + other.transform.position) * 0.5f;
            other.gameObject.SetActive(false);
        }

        SpawnCollisionEffect(impactPoint);
        gameObject.SetActive(false);
    }

    private void SpawnCollisionEffect(Vector3 position)
    {
        if (EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject("hit02", position, Quaternion.identity);
        }
        else if (data != null && data.hitEffectPrefab != null)
        {
            Instantiate(data.hitEffectPrefab, position, Quaternion.identity);
        }
    }
}
