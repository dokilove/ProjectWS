// Assets/Scripts/Projectile.cs
using UnityEngine;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    [SerializeField] protected ProjectileData data;
    [SerializeField] protected LayerMask targetLayer;       // Inspector에서 Target 레이어를 지정해줘야 합니다.
    [SerializeField] protected LayerMask playerLayers;      // Layers that belong to the player (Unit or Vehicle)
    [SerializeField] protected LayerMask justDodgeLayer;    // Inspector에서 JustDodgeTrigger 레이어를 지정
    [SerializeField] protected LayerMask environmentLayers; // 지면 및 장애물 레이어 (Ground, Obstacle)

    protected Vector3 moveDirection;
    protected Vector3 horizontalVelocity; // 탄도학 역계산 수평 속도 벡터
    protected Vector3 currentVelocity;    // 곡사 시 실시간 속도 벡터
    protected float verticalVelocity;     // 곡사용 수직 속도
    protected float currentLifespan;
    protected int shooterLayer;           // To ignore collision with the shooter
    protected Transform shooterTransform; // 발사자 루트/부모 트랜스폼 (자기 자신 및 자식 콜라이더 무시용)
    protected float _damage;              // 실제 데미지 값을 저장할 필드

    protected Collider _collider;         // 발사체 본체 콜라이더 (곡사 시 비활성화)
    private static int _enemyAttackLayerMask = -1;
    private static int _defaultEnvLayerMask = -1;
    private static int _groundLayerMask = -1;
    private static int _obstacleLayerMask = -1;

    protected virtual void Awake()
    {
        _collider = GetComponent<Collider>();

        if (_groundLayerMask == -1)
        {
            _groundLayerMask = LayerMask.GetMask("Ground");
            if (_groundLayerMask == 0)
            {
                _groundLayerMask = 1 << 12;
            }
        }

        if (_obstacleLayerMask == -1)
        {
            _obstacleLayerMask = LayerMask.GetMask("Obstacle");
            if (_obstacleLayerMask == 0)
            {
                _obstacleLayerMask = 1 << 13;
            }
        }

        if (_enemyAttackLayerMask == -1)
        {
            _enemyAttackLayerMask = LayerMask.GetMask("EnemyAttackLayer");
            if (_enemyAttackLayerMask == 0)
            {
                _enemyAttackLayerMask = 1 << 15;
            }
        }

        if (_defaultEnvLayerMask == -1)
        {
            _defaultEnvLayerMask = _groundLayerMask | _obstacleLayerMask;
            // Default 레이어(0)의 벽/오브젝트도 환경 충돌로 포함
            _defaultEnvLayerMask |= (1 << 0);
        }

        if (environmentLayers == 0)
        {
            environmentLayers = _defaultEnvLayerMask;
        }
    }

    // 오브젝트 풀에서 활성화될 때 호출될 함수 (targetPosition 및 shooter 지원)
    public virtual void Init(ProjectileData projectileData, Vector3 direction, int shooterGameObjectLayer, float overrideDamage, Vector3? targetPosition = null, Transform shooter = null)
    {
        data = projectileData;
        shooterLayer = shooterGameObjectLayer;
        shooterTransform = shooter;
        _damage = overrideDamage;
        currentLifespan = data != null ? data.lifespan : 5f;

        // 발사체 본체 콜라이더와 발사자(차량/플레이어 등)의 모든 자식 콜라이더 간의 물리 충돌 무시 등록
        if (_collider != null && shooterTransform != null)
        {
            Collider[] shooterColliders = shooterTransform.GetComponentsInChildren<Collider>();
            for (int i = 0; i < shooterColliders.Length; i++)
            {
                if (shooterColliders[i] != null && shooterColliders[i] != _collider)
                {
                    Physics.IgnoreCollision(_collider, shooterColliders[i], true);
                }
            }
        }

        if (environmentLayers == 0)
        {
            environmentLayers = _defaultEnvLayerMask;
        }

        // 곡사 궤적 초기화 (목표 지점 탄도학 역계산)
        if (data != null && data.isArcing)
        {
            // [곡사 특화] 비행 중 공중의 적/장애물 콜라이더에 스쳐서 조기 폭발하지 않도록 자체 충돌박스 비활성화
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            Vector3 destination;
            if (targetPosition.HasValue && (targetPosition.Value - transform.position).sqrMagnitude > 0.25f)
            {
                destination = targetPosition.Value;
            }
            else
            {
                // 지정된 타겟 좌표가 없을 경우(허공 사격): 사거리 지점을 가상 목표로 설정
                Vector3 defaultDir = new Vector3(direction.x, 0f, direction.z).normalized;
                if (defaultDir.sqrMagnitude < 0.001f)
                {
                    defaultDir = transform.forward;
                    defaultDir.y = 0f;
                    defaultDir.Normalize();
                }
                float defaultDist = Mathf.Max(6f, data.speed * 0.7f);
                destination = transform.position + defaultDir * defaultDist;
            }

            Vector3 displacement = destination - transform.position;
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
            float horizontalDistance = horizontalDisplacement.magnitude;
            float heightDifference = displacement.y;

            // 비행 시간 T = 수평 거리 / 속도 (최소 0.25초)
            float speed = Mathf.Max(1f, data.speed);
            float flightTime = Mathf.Max(0.25f, horizontalDistance / speed);

            // 중력 가속도
            float gravity = Physics.gravity.y * (data.gravityMultiplier > 0 ? data.gravityMultiplier : 1f);

            // 탄도학 역계산:
            // 1. 수평 속도: V_xz = D_xz / T
            horizontalVelocity = horizontalDistance > 0.001f ? (horizontalDisplacement / flightTime) : (transform.forward * speed);
            moveDirection = horizontalDistance > 0.001f ? horizontalDisplacement.normalized : transform.forward;

            // 2. 수직 초기 속도: V_y = (H - 0.5 * g * T^2) / T
            verticalVelocity = (heightDifference - 0.5f * gravity * (flightTime * flightTime)) / flightTime;

            currentVelocity = horizontalVelocity + Vector3.up * verticalVelocity;

            if (currentVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(currentVelocity);
            }
        }
        else
        {
            // 일반 직사 탄환은 콜라이더 활성화
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            moveDirection = direction.normalized;
            horizontalVelocity = moveDirection * (data != null ? data.speed : 10f);
            currentVelocity = horizontalVelocity;
        }
    }

    protected virtual void Update()
    {
        // 수명이 다하면 비활성화
        currentLifespan -= Time.deltaTime;
        if (currentLifespan <= 0)
        {
            OnLifespanExpired();
            return;
        }

        Move(Time.deltaTime);
    }

    protected virtual void OnLifespanExpired()
    {
        // 폭발 탄환이 지면에 닿지 못하고 수명이 다했을 때도 폭발 처리 가능 (선택적)
        if (data != null && data.isExplosive)
        {
            Explode(transform.position);
            return;
        }

        gameObject.SetActive(false);
    }

    protected virtual void Move(float deltaTime)
    {
        if (data != null && data.isArcing)
        {
            // 중력 가속도 적용 (Physics.gravity.y * gravityMultiplier)
            float gravity = Physics.gravity.y * (data.gravityMultiplier > 0 ? data.gravityMultiplier : 1f);
            verticalVelocity += gravity * deltaTime;

            // 역계산된 수평 속도와 중력 가속도가 적용된 수직 속도로 실시간 이동 벡터 갱신
            currentVelocity = horizontalVelocity + Vector3.up * verticalVelocity;

            float moveDistance = currentVelocity.magnitude * deltaTime;
            Vector3 flightDir = currentVelocity.sqrMagnitude > 0.001f ? currentVelocity.normalized : moveDirection;

            // [곡사 특화] 공중의 적/발사체 충돌은 건너뛰고, 지면(Ground) 및 장애물(Obstacle) 착탄만 감지
            float checkDist = moveDistance + 0.15f;
            if (Physics.Raycast(transform.position, flightDir, out RaycastHit envHit, checkDist, environmentLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsShooter(envHit.collider))
                {
                    Explode(envHit.point);
                    return;
                }
            }

            // 하강 중 지면 관통 방지를 위한 직하방 레이캐스트 보강
            if (verticalVelocity < 0f)
            {
                float downCheckDist = Mathf.Abs(verticalVelocity * deltaTime) + 0.2f;
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit downHit, downCheckDist, environmentLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!IsShooter(downHit.collider))
                    {
                        Explode(downHit.point);
                        return;
                    }
                }
            }

            transform.position += currentVelocity * deltaTime;

            // 낙하 진행 방향으로 탄두 회전 정렬
            if (currentVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(currentVelocity);
            }
        }
        else
        {
            // 고속 이동 시 발사체 간 터널링(통과 현상) 방지를 위한 전방 충돌 검사
            float moveDistance = (data != null ? data.speed : 10f) * deltaTime;
            CheckProjectileCollision(moveDistance, moveDirection);

            if (!gameObject.activeInHierarchy) return;

            // 전방 타겟 및 환경 충돌 검사 (고속 이동 시 차량/플레이어 터널링 방지)
            // 직사 탄환은 지면(Ground) 위를 비행하므로 지면 레이어를 레이캐스트 검사 마스크에서 제외
            int straightEnvMask = environmentLayers & ~_groundLayerMask;
            int hitMask = targetLayer | playerLayers | straightEnvMask;
            if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsShooter(hit.collider))
                {
                    HandleImpact(hit.collider, hit.point);
                    return;
                }
            }

            transform.Translate(moveDirection * moveDistance, Space.World);
        }
    }

    /// <summary>
    /// 대상 콜라이더가 발사자 본인(차량/플레이어 등 최상위 루트 및 모든 자식 콜라이더)이거나 발사자 레이어인지 판별합니다.
    /// </summary>
    protected virtual bool IsShooter(Collider col)
    {
        if (col == null) return false;

        if (shooterTransform != null)
        {
            if (col.transform == shooterTransform || col.transform.IsChildOf(shooterTransform) || col.transform.root == shooterTransform.root)
            {
                return true;
            }
        }

        if (col.gameObject.layer == shooterLayer)
        {
            return true;
        }

        return false;
    }

    protected virtual void CheckProjectileCollision(float moveDistance, Vector3 checkDirection)
    {
        if (!gameObject.activeInHierarchy) return;

        // 플레이어/차량 발사체는 적 발사체 레이어를, 적 발사체는 플레이어/차량 발사체 레이어를 탐색
        int targetMask = IsPlayerSide() ? _enemyAttackLayerMask : (1 << 0);

        if (Physics.SphereCast(transform.position, 0.3f, checkDirection, out RaycastHit hit, moveDistance, targetMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.gameObject == gameObject) return;

            Projectile otherProjectile = hit.collider.GetComponentInParent<Projectile>();
            if (otherProjectile != null && IsOpposingProjectile(otherProjectile))
            {
                CollideWithProjectile(otherProjectile);
            }
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy || !other.gameObject.activeInHierarchy)
        {
            return;
        }

        // 자기 자신(발사자 본체 및 모든 자식 콜라이더)과의 충돌은 무시
        if (IsShooter(other))
        {
            return;
        }

        // 직사 탄환은 높이 100m의 Y축 콜라이더로 XZ 평면을 비행하므로 바닥 지면(Ground)과의 트리거 충돌은 완전히 무시
        bool isArcing = data != null && data.isArcing;
        if (!isArcing && ((1 << other.gameObject.layer) & _groundLayerMask) != 0)
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

        // 3. 충돌 지점 계산
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // 4. 일반 피격 대상 또는 환경 충돌 처리
        HandleImpact(other, hitPoint);
    }

    /// <summary>
    /// 충돌 발생 시 단일 타격, 범위 폭발, 또는 벽/환경 착탄 소멸을 처리합니다.
    /// </summary>
    protected virtual void HandleImpact(Collider other, Vector3 hitPoint)
    {
        if (!gameObject.activeInHierarchy || IsShooter(other)) return;

        // 직사 탄환은 지면(Ground) 충돌로 소멸되지 않도록 2중 방어
        bool isArcing = data != null && data.isArcing;
        if (!isArcing && ((1 << other.gameObject.layer) & _groundLayerMask) != 0)
        {
            return;
        }

        bool isTarget = ((1 << other.gameObject.layer) & targetLayer) != 0 || ((1 << other.gameObject.layer) & playerLayers) != 0;

        // 1. 폭발형 탄환인 경우: 타겟이든 벽/바닥(환경)이든 부딪히면 무조건 즉시 폭발
        if (data != null && data.isExplosive)
        {
            Explode(hitPoint);
            return;
        }

        // 2. 일반 단일 타격 대상인 경우: 데미지 적용 후 피격 이펙트 및 소멸
        if (isTarget)
        {
            ApplyDamageToTarget(other, hitPoint);
            SpawnHitEffect(hitPoint, Quaternion.LookRotation(other.transform.position - transform.position));
            gameObject.SetActive(false);
            return;
        }

        // 3. 타겟이 아닌 물체(벽, 바닥, 장애물 등 환경)에 부딪힌 경우:
        //    피격 이펙트 생성 후 즉시 소멸 (벽에 박힌 채 남아있는 현상 방지)
        Vector3 hitNormal = (transform.position - hitPoint).normalized;
        if (hitNormal.sqrMagnitude < 0.001f)
        {
            hitNormal = -moveDirection;
        }
        SpawnHitEffect(hitPoint, Quaternion.LookRotation(hitNormal));
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 폭발 중심점을 기준으로 반경 내 모든 적대적 대상에게 피해와 넉백, 경직을 전달합니다.
    /// </summary>
    public virtual void Explode(Vector3 explosionCenter)
    {
        if (!gameObject.activeInHierarchy) return;

        // 1. 폭발 이펙트 생성
        GameObject effectPrefab = (data != null && data.explosionEffectPrefab != null)
            ? data.explosionEffectPrefab
            : (data != null ? data.hitEffectPrefab : null);

        if (effectPrefab != null)
        {
            Instantiate(effectPrefab, explosionCenter, Quaternion.identity);
        }

        // 2. 반경 내 피해 대상 판정
        float radius = data != null ? data.explosionRadius : 3.5f;
        int damageMask = targetLayer | playerLayers;
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, radius, damageMask, QueryTriggerInteraction.Collide);

        HashSet<int> processedRoots = new HashSet<int>();

        foreach (Collider col in hitColliders)
        {
            if (col == null || !col.gameObject.activeInHierarchy) continue;
            if (IsShooter(col)) continue;

            // 콜라이더가 여러 개 붙어있는 단일 대상의 중복 피격 방지
            Transform rootTransform = col.transform.root;
            int rootId = rootTransform != null ? rootTransform.gameObject.GetInstanceID() : col.gameObject.GetInstanceID();
            if (processedRoots.Contains(rootId)) continue;
            processedRoots.Add(rootId);

            Vector3 targetHitPoint = col.ClosestPoint(explosionCenter);
            ApplyDamageToTarget(col, targetHitPoint, isExplosion: true, explosionCenter: explosionCenter);
        }

        // 발사체 비활성화
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 개별 대상에 데미지, 경직, 넉백을 전달합니다.
    /// </summary>
    protected virtual void ApplyDamageToTarget(Collider other, Vector3 hitPoint, bool isExplosion = false, Vector3 explosionCenter = default)
    {
        Unit unit = other.GetComponentInParent<Unit>();
        if (unit != null)
        {
            if (unit.IsInvincible)
            {
                return;
            }
            unit.TakeDamage(_damage);
            return;
        }

        Vehicle vehicle = other.GetComponentInParent<Vehicle>();
        if (vehicle != null)
        {
            vehicle.TakeDamage(_damage);
            return;
        }

        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            float stunDuration = data != null ? data.hitStunDuration : 0.1f;
            enemyHealth.TakeDamage(_damage, stunDuration);

            // 폭발 시 넉백 적용
            if (isExplosion && data != null && data.explosionKnockback > 0f)
            {
                EnemyAI enemyAI = other.GetComponentInParent<EnemyAI>();
                if (enemyAI != null)
                {
                    Vector3 knockbackDir = other.transform.position - explosionCenter;
                    knockbackDir.y = 0f;
                    if (knockbackDir.sqrMagnitude < 0.001f)
                    {
                        knockbackDir = moveDirection;
                    }
                    enemyAI.ApplyKnockback(knockbackDir.normalized, data.explosionKnockback);
                }
            }
            return;
        }

        EnemySpawner spawner = other.GetComponentInParent<EnemySpawner>();
        if (spawner != null)
        {
            spawner.TakeDamage(_damage);
        }
    }

    protected virtual void SpawnHitEffect(Vector3 position, Quaternion rotation)
    {
        if (data != null && data.hitEffectPrefab != null)
        {
            Instantiate(data.hitEffectPrefab, position, rotation);
        }
    }

    // [NEW] 저스트 회피 트리거에 스쳤을 때 호출될 함수
    public virtual void OnGraze()
    {
        Debug.Log("Projectile grazed! Deactivating.");
        gameObject.SetActive(false); // 오브젝트 풀링 사용 시 비활성화
    }

    /// <summary>
    /// 플레이어의 근접 공격에 의해 발사체가 파괴/소멸될 때 호출됩니다.
    /// </summary>
    public virtual void DestroyByMelee(string effectTag = null)
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
    public virtual void CollideWithProjectile(Projectile other)
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

    protected virtual void SpawnCollisionEffect(Vector3 position)
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
