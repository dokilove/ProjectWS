using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for Coroutines

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float evadeForce = 10f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private ManualFollowCamera followCamera; // Assign your Main Camera with ManualFollowCamera script here

    // Dodge specific variables
    [SerializeField] private float dodgeDuration = 0.5f; // How long the dodge lasts
    [SerializeField] private int dodgingPlayerLayer; // Assign the 'DodgingPlayer' layer ID in the Inspector
    [SerializeField] private float pushRadius = 2f; // Radius to detect enemies for pushing
    [SerializeField] private float pushForce = 0.05f; // Total distance to push enemies
    [SerializeField] private float pushSmoothTime = 0.2f; // How long the push effect lasts

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
    }

    private void FixedUpdate()
    {
        // Create a 3D movement vector from the 2D input.
        Vector3 moveVector = new Vector3(moveInput.x, 0f, moveInput.y);

        // Apply movement
        rb.linearVelocity = moveVector * moveSpeed;

        // Apply rotation
        if (moveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnEvade(InputAction.CallbackContext context)
    {
        // Prevent dodging if already dodging
        if (gameObject.layer == dodgingPlayerLayer) return;

        Vector3 evadeDirection;
        if (moveInput.sqrMagnitude > 0.1f)
        {
            evadeDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        }
        else
        {
            evadeDirection = -transform.forward;
        }
        rb.AddForce(evadeDirection * evadeForce, ForceMode.Impulse);

        // Change player layer to ignore collisions
        gameObject.layer = dodgingPlayerLayer;
        Invoke("ResetPlayerLayer", dodgeDuration); // Reset layer after dodge duration

        // Push nearby enemies
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pushRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                NavMeshAgent enemyAgent = hitCollider.GetComponent<NavMeshAgent>();
                if (enemyAgent != null) // Ensure it's a NavMeshAgent enemy
                {
                    Vector3 pushDir = (hitCollider.transform.position - transform.position).normalized;
                    pushDir.y = 0; // Flatten the push direction to XZ plane
                    pushDir.Normalize(); // Re-normalize after flattening

                    // Start coroutine to smoothly push the enemy
                    StartCoroutine(SmoothPushEnemy(enemyAgent, pushDir * pushForce, pushSmoothTime));
                }
            }
        }

        // Trigger camera damping
        if (followCamera != null)
        {
            followCamera.TriggerEvadeDamping();
        }
    }

    private IEnumerator SmoothPushEnemy(NavMeshAgent agentToPush, Vector3 pushVector, float duration)
    {
        Vector3 startPosition = agentToPush.transform.position;
        // The target position is just for calculating the total displacement
        Vector3 targetPosition = startPosition + pushVector;
        float elapsedTime = 0f;

        // Stop the agent from actively seeking a path during the push
        agentToPush.isStopped = true;

        while (elapsedTime < duration)
        {
            // Calculate the desired displacement for this frame
            Vector3 currentTargetPosition = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            Vector3 displacement = currentTargetPosition - agentToPush.transform.position;

            // Use NavMeshAgent.Move() to apply the displacement, respecting NavMesh and obstacles
            agentToPush.Move(displacement);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Ensure it reaches the target position as much as possible within NavMesh constraints
        // And re-enable its pathfinding
        agentToPush.isStopped = false;
    }

    private void ResetPlayerLayer()
    {
        gameObject.layer = originalLayer;
    }
}
