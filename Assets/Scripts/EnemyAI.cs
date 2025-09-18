using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent

public class EnemyAI : MonoBehaviour
{
    public Transform playerTransform; // Assign the player's Transform in the Inspector
    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Find the player if not assigned in the Inspector
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("Player object not found! Please tag your player as 'Player' or assign it in the Inspector.");
            }
        }
    }

    void Update()
    {
        // Add checks here to ensure the agent is active, enabled, and on NavMesh
        if (playerTransform != null && agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.SetDestination(playerTransform.position);
        }
    }
}