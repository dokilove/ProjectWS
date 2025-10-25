using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (enemyData != null)
        {
            agent.speed = enemyData.moveSpeed;
        }
    }

    void Update()
    {
        // Add checks here to ensure the agent is active, enabled, and on NavMesh
        if (PlayerPawnManager.ActivePlayerTransform != null && agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.SetDestination(PlayerPawnManager.ActivePlayerTransform.position);
        }
    }
}