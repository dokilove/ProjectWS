using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

public class VehicleAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private VehicleAIBehaviour aiBehaviour;
    [SerializeField] private LayerMask enemyLayer; // This should be player/target layer

    // --- Dependencies ---
    private Vehicle _vehicle;
    private NavMeshAgent agent;

    // --- State ---
    private Transform currentTarget;
    private List<Transform> potentialTargets = new List<Transform>();
    private Collider[] aiTargetColliders = new Collider[20];
    private float lastFollowStopTime = -Mathf.Infinity; // Added for cooldown

    // --- Public Properties ---
    public Transform CurrentTarget => currentTarget;

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
        // The agent is managed by VehicleMove, but we can get a reference to it
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        // When this component is enabled, take control from the player
        if (_vehicle != null && !_vehicle.IsControlledByPlayer)
        {
            agent.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
            agent.enabled = false;
        }
    }

    void Update()
    {
        if (_vehicle == null || _vehicle.IsControlledByPlayer || aiBehaviour == null)
        {
            enabled = false;
            return;
        }

        HandleAIControl();
    }

    private void HandleAIControl()
    {
        if (PlayerPawnManager.ActivePlayerTransform == null || aiBehaviour == null)
        {
            if (agent != null && agent.enabled) agent.enabled = false;
            return;
        }

        // Ensure the agent is enabled if AI is running
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            agent.speed = aiBehaviour.followSpeed;
            agent.stoppingDistance = aiBehaviour.followStopDistance;
        }

        float distanceToTarget = Vector3.Distance(transform.position, PlayerPawnManager.ActivePlayerTransform.position);

        // --- Movement ---
        if (distanceToTarget > aiBehaviour.followStopDistance)
        {
            // Only resume following if cooldown has passed
            if (Time.time >= lastFollowStopTime + aiBehaviour.followCoolTime)
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);
                }
            }
            else
            {
                // Still in cooldown, so ensure agent is stopped
                if (agent != null && agent.isOnNavMesh && agent.hasPath)
                {
                    agent.ResetPath();
                }
            }
        }
        else // Player is within stop distance
        {
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
            {
                agent.ResetPath();
            }
            lastFollowStopTime = Time.time; // Update last stop time
        }

        // --- Targeting & Weapons ---
        var weaponSystem = _vehicle.AutoWeaponSystem != null ? _vehicle.AutoWeaponSystem : null;
        if (weaponSystem != null && weaponSystem.IsAutoWeapon)
        {
            UpdateAndSelectTargetAI(weaponSystem.WeaponData);
            weaponSystem.SetAimAI(currentTarget, transform);

            if (weaponSystem.CanFire(currentTarget))
            {
                weaponSystem.Fire(currentTarget);
            }
            else if (weaponSystem.CurrentAmmo <= 0 && !weaponSystem.IsReloading)
            {
                weaponSystem.HandleReloadInput();
            }
        }
    }

    private void UpdateAndSelectTargetAI(WeaponData weaponData)
    {
        if (weaponData == null)
        {
            currentTarget = null;
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, weaponData.lockOnRadius, aiTargetColliders, enemyLayer);

        potentialTargets.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Transform target = aiTargetColliders[i].transform;
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget <= weaponData.attackAngle / 2)
            {
                potentialTargets.Add(target);
            }
        }

        if (potentialTargets.Count == 0)
        {
            currentTarget = null;
            return;
        }

        currentTarget = potentialTargets.OrderBy(t => Vector3.Distance(transform.position, t.position)).FirstOrDefault();
    }
}