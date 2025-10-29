using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class VehicleController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f; // Vehicle might be faster
    [SerializeField] private float rotationSpeed = 15f;

    [Header("AI Follow")]
    [SerializeField] private float followSpeed = 3f;
    [SerializeField] private float followStopDistance = 5f;

    [Header("Attacks")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 10f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float lockOnRadius = 20f; // Vehicle has longer range
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WeaponData weaponData;

    private float nextFireTime = 0f;
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Targeting Fields ---
    private Transform currentTarget;
    private List<Transform> potentialTargets = new List<Transform>();

    private Rigidbody rb;
    private NavMeshAgent agent;
    private InputSystem_Actions playerActions; // Will use player's input actions
    private Vector2 moveInput;

    public bool IsControlledByPlayer { get; private set; } = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        playerActions = new InputSystem_Actions(); // Initialize here, but enable/disable externally
    }

    private void Start()
    {
        if (weaponData == null || weaponData.projectilePrefab == null) return;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject proj = Instantiate(weaponData.projectilePrefab);
            proj.SetActive(false);
            projectilePool.Add(proj);
        }
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
        agent.enabled = false;
        rb.isKinematic = false;
        // Set constraints for player control: allow movement and Y-axis rotation
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        playerActions.Vehicle.Enable();
    }

    public void DisableControl()
    {
        IsControlledByPlayer = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll; // Freeze everything when not controlled

        agent.enabled = true;
        agent.speed = followSpeed;
        agent.stoppingDistance = followStopDistance;

        // Only disable if playerActions has been initialized
        if (playerActions != null)
        {
            playerActions.Vehicle.Disable();
        }
        rb.linearVelocity = Vector3.zero; // Stop vehicle movement when player exits
        moveInput = Vector2.zero; // Reset input
    }

            private void Update()
            {
                if (IsControlledByPlayer)
                {
                    moveInput = playerActions.Vehicle.Move.ReadValue<Vector2>();
                }
                else
                {
                    // AI Follow & Attack Logic
                    if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        if (PlayerPawnManager.ActivePlayerTransform != null)
                        {
                            agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);
                        }
                    }
        
                    UpdateAndSelectTarget();
                    RotateTurret(currentTarget);
        
                    if (currentTarget != null && Time.time >= nextFireTime)
                    {
                        if (weaponData != null)
                        {
                            nextFireTime = Time.time + 1f / weaponData.fireRate;
                            Fire();
                        }
                    }
                }
            }    
        private void UpdateAndSelectTarget()
        {
            potentialTargets = Physics.OverlapSphere(transform.position, lockOnRadius, enemyLayer)
                                    .Select(col => col.transform)
                                    .ToList();
    
            if (potentialTargets.Count == 0)
            {
                currentTarget = null;
                return;
            }
    
            // Find the closest one and set it as the current target.
            Transform closestTarget = null;
            float minDistance = float.MaxValue;
            foreach (var target in potentialTargets)
            {
                float distance = Vector3.Distance(transform.position, target.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = target;
                }
            }
            currentTarget = closestTarget;
        }
    
        private void FixedUpdate()    {
        if (!IsControlledByPlayer) return;

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

    private void RotateTurret(Transform target)
    {
        if (turretTransform == null) return;

        Quaternion targetRotation;
        if (target != null)
        {
            Vector3 direction = target.position - turretTransform.position;
            direction.y = 0;
            targetRotation = Quaternion.LookRotation(direction);
        }
        else
        {
            // If no target, maybe point forward relative to the vehicle's body
            targetRotation = transform.rotation;
        }
        turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, turretRotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        if (currentTarget == null) return;

        GameObject projectile = GetPooledProjectile();
        if (projectile != null)
        {
            Vector3 directionToTarget = (currentTarget.position - firePoint.position).normalized;
            projectile.transform.position = firePoint.position;
            projectile.transform.rotation = Quaternion.LookRotation(directionToTarget);
            projectile.SetActive(true);
            projectile.GetComponent<Projectile>().Initialize(projectile.transform.forward);
        }
    }

    private GameObject GetPooledProjectile()
    { 
        if (projectilePool.Count == 0) return null;

        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (!projectilePool[i].activeInHierarchy)
            {
                return projectilePool[i];
            }
        }
        return null;
    }
}