using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for Coroutines

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private ManualFollowCamera followCamera; // Assign your Main Camera with ManualFollowCamera script here

    [Header("Evade")]
    [SerializeField] private float evadeForce = 10f;
    [SerializeField] private float dodgeDuration = 0.5f; // How long the dodge lasts
    [SerializeField] private int dodgingPlayerLayer; // Assign the 'DodgingPlayer' layer ID in the Inspector
    [SerializeField] private float pushRadius = 2f; // Radius to detect enemies for pushing
    [SerializeField] private float pushForce = 0.05f; // Total distance to push enemies
    [SerializeField] private float pushSmoothTime = 0.2f; // How long the push effect lasts

    [Header("Attacks - Auto Fire & Targeting")]
    [SerializeField] private Transform turretTransform; // 회전할 포탑 Transform
    [SerializeField] private float turretRotationSpeed = 10f; // 포탑 회전 속도
    [SerializeField] private GameObject projectilePrefab; // 발사체 프리팹
    [SerializeField] private Transform firePoint; // 발사 위치
    [SerializeField] private float fireRate = 2f; // 초당 발사 횟수
    [SerializeField] private float detectionRadius = 15f; // 적 탐지 반경
    [SerializeField] private LayerMask enemyLayer; // 적 레이어

    private float nextFireTime = 0f;
    private System.Collections.Generic.List<GameObject> projectilePool = new System.Collections.Generic.List<GameObject>();
    private int poolSize = 20;

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private int originalLayer; // To store the player's original layer

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions();
        originalLayer = gameObject.layer; // Store the original layer
    }

    private void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (projectilePrefab == null) continue;
            GameObject proj = Instantiate(projectilePrefab);
            proj.SetActive(false);
            projectilePool.Add(proj);
        }
    }

    private void OnEnable()
    {
        playerActions.Player.Enable();
        playerActions.Player.Evade.performed += OnEvade;
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
        playerActions.Player.Evade.performed -= OnEvade;
    }

    private void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();

        // 타겟 탐지 및 포탑 회전
        Transform target = FindNearestEnemy();
        RotateTurret(target);

        // 자동 발사
        if (target != null && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Fire();
        }
    }

    private void FixedUpdate()
    {
        Vector3 moveVector = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.linearVelocity = moveVector * moveSpeed;

        if (moveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private Transform FindNearestEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        Transform nearestEnemy = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestEnemy = enemy.transform;
            }
        }
        return nearestEnemy;
    }

    private void RotateTurret(Transform target)
    {
        if (turretTransform == null) return;

        Quaternion targetRotation;
        if (target != null)
        {
            // 타겟이 있으면 타겟 방향으로
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0; // 포탑이 위아래로 기울지 않도록
            targetRotation = Quaternion.LookRotation(direction);
        }
        else
        {
            // 타겟이 없으면 플레이어의 정면 방향으로
            targetRotation = transform.rotation;
        }

        // 부드럽게 회전
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, turretRotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        GameObject projectile = GetPooledProjectile();
        if (projectile != null)
        {
            projectile.transform.position = firePoint.position;
            projectile.transform.rotation = firePoint.rotation; // 포탑의 방향을 그대로 따름

            projectile.SetActive(true);
            projectile.GetComponent<Projectile>().Initialize(turretTransform.forward); // 포탑의 정면으로 발사
        }
    }

    private GameObject GetPooledProjectile()
    {
        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (!projectilePool[i].activeInHierarchy)
            {
                return projectilePool[i];
            }
        }
        return null;
    }

    private void OnEvade(InputAction.CallbackContext context)
    {
        if (gameObject.layer == dodgingPlayerLayer) return;

        Vector3 evadeDirection = (moveInput.sqrMagnitude > 0.1f) 
            ? new Vector3(moveInput.x, 0, moveInput.y).normalized 
            : -transform.forward;
            
        rb.AddForce(evadeDirection * evadeForce, ForceMode.Impulse);
        gameObject.layer = dodgingPlayerLayer;
        Invoke(nameof(ResetPlayerLayer), dodgeDuration);

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pushRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                if (hitCollider.TryGetComponent<NavMeshAgent>(out var enemyAgent))
                {
                    Vector3 pushDir = (hitCollider.transform.position - transform.position).normalized;
                    pushDir.y = 0;
                    StartCoroutine(SmoothPushEnemy(enemyAgent, pushDir.normalized * pushForce, pushSmoothTime));
                }
            }
        }

        if (followCamera != null)
        {
            followCamera.TriggerEvadeDamping();
        }
    }

    private IEnumerator SmoothPushEnemy(NavMeshAgent agentToPush, Vector3 pushVector, float duration)
    {
        // 안전 코드: Agent가 유효하고 NavMesh 위에 있을 때만 실행
        if (agentToPush == null || !agentToPush.isActiveAndEnabled || !agentToPush.isOnNavMesh)
        {
            yield break; // 코루틴 중단
        }

        Vector3 startPosition = agentToPush.transform.position;
        Vector3 targetPosition = startPosition + pushVector;
        float elapsedTime = 0f;
        agentToPush.isStopped = true;

        while (elapsedTime < duration)
        {
            // 안전 코드: 루프 중에도 Agent가 비활성화되면 중단
            if (!agentToPush.isActiveAndEnabled)
            {
                yield break;
            }
            Vector3 currentTargetPosition = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            agentToPush.Move(currentTargetPosition - agentToPush.transform.position);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // isStopped를 다시 false로 설정하기 전에도 유효한지 최종 확인
        if (agentToPush.isActiveAndEnabled)
        {
            agentToPush.isStopped = false;
        }
    }

    private void ResetPlayerLayer()
    {
        gameObject.layer = originalLayer;
    }
}
