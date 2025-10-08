using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for List

[RequireComponent(typeof(Rigidbody))]
public class UnitController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;
    // Removed followCamera reference as it will be managed by the main PlayerController

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
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    private Rigidbody rb;
    private InputSystem_Actions playerActions; // Will use player's input actions
    private Vector2 moveInput;
    private int originalLayer; // To store the player's original layer

    public bool IsControlledByPlayer { get; private set; } = false; // New field to indicate if this Unit is currently controlled

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions(); // Initialize here, but enable/disable externally
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

    // Enable/Disable methods for external control
    public void EnableControl()
    {
        IsControlledByPlayer = true;
        this.enabled = true; // Enable this script
        rb.isKinematic = false; // Ensure physics are active
        // Set constraints for player control: allow movement and Y-axis rotation
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        gameObject.SetActive(true); // Ensure GameObject is active
        playerActions.Player.Enable(); // Enable Player action map
        playerActions.Player.Evade.performed += OnEvade;
        playerActions.Player.Interact.performed += OnInteract;
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        this.enabled = false; // Disable this script
        rb.isKinematic = true; // Disable physics
        rb.constraints = RigidbodyConstraints.FreezeAll; // Freeze everything when not controlled
        gameObject.SetActive(false); // Disable GameObject

        // Only disable and unsubscribe if playerActions has been initialized
        if (playerActions != null)
        {
            playerActions.Player.Disable(); // Disable Player action map
            playerActions.Player.Evade.performed -= OnEvade;
            playerActions.Player.Interact.performed -= OnInteract;
        }
        rb.linearVelocity = Vector3.zero; // Stop movement
        moveInput = Vector2.zero; // Reset input
    }

    private void Update()
    {
        if (!IsControlledByPlayer) return; // Only process input if controlled

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
        if (!IsControlledByPlayer) return; // Only process physics if controlled

        rb.angularVelocity = Vector3.zero; // Prevent unwanted rotation from collisions

        Vector3 moveForward;
        Vector3 moveRight;

        // Check if camera is looking nearly straight up or down
        if (Mathf.Abs(Vector3.Dot(Camera.main.transform.forward, Vector3.up)) > 0.99f)
        {
            // Gimbal lock case: Use world axes for movement
            moveForward = Vector3.forward;
            moveRight = Vector3.right;
        }
        else
        {
            // Standard case: Use camera-relative axes
            moveForward = Camera.main.transform.forward;
            moveRight = Camera.main.transform.right;

            moveForward.y = 0;
            moveRight.y = 0;
            moveForward.Normalize();
            moveRight.Normalize();
        }

        // Calculate movement direction
        Vector3 moveVector = (moveForward * moveInput.y + moveRight * moveInput.x);

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
        Debug.Log("Unit Fire action triggered!"); // NEW LINE
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
        Debug.Log("Unit Evade button pressed!"); // NEW LINE
        if (gameObject.layer == dodgingPlayerLayer) return;

        Vector3 evadeDirection;
        if (moveInput.sqrMagnitude > 0.1f)
        {
            // Re-calculate camera-relative direction to ensure dodge is correct
            Transform camTransform = Camera.main.transform;
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();
            evadeDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            // If standing still, dodge backwards relative to character's orientation
            evadeDirection = -transform.forward;
        }

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
    }

    // OnInteract and OnExitVehicle will be handled by the main PlayerController
    private void OnInteract(InputAction.CallbackContext context)
    {
        Debug.Log("Unit Interact button pressed!"); // NEW LINE
        // This will be handled by the PlayerPawnManager
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
