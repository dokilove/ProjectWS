using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class VehicleMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 8f;

    // --- Dependencies ---
    private Vehicle _vehicle;
    private Rigidbody rb;
    private NavMeshAgent agent;

    // --- Input State (from VehicleInput) ---
    private Vector2 playerMoveInput;
    private bool isNeutralTurning;

    // --- Physics State ---
    private float currentSpeed = 0f;
    private bool isReverseMode = false;

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();

        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    public void EnableControl()
    {
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        agent.enabled = false; // AI is disabled when player takes control
    }

    public void DisableControl()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        agent.enabled = false;
        playerMoveInput = Vector2.zero;
        isNeutralTurning = false;
    }

    private void Update()
    {
        // For AI, continuously sync the agent's internal position with the Rigidbody's actual position
        if (!_vehicle.IsControlledByPlayer && agent.enabled)
        {
            agent.nextPosition = transform.position;
        }
    }

    private void FixedUpdate()
    {
        HandleVehiclePhysics();
    }

    // --- Public Setters for Input ---
    public void SetPlayerInput(Vector2 moveInput, bool neutralTurn)
    {
        this.playerMoveInput = moveInput;
        this.isNeutralTurning = neutralTurn;
    }

    private void HandleVehiclePhysics()
    {
        Vector3 targetDirection = Vector3.zero;
        float inputMagnitude = 0f;

        if (_vehicle.IsControlledByPlayer)
        {
            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
            Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);

            Vector3 moveForward = -cameraForwardFlat;
            Vector3 moveVector = (moveForward * playerMoveInput.y + cameraRightFlat * playerMoveInput.x);

            targetDirection = moveVector.normalized;
            inputMagnitude = Mathf.Clamp01(moveVector.magnitude);

            if (isNeutralTurning)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * 2f * Time.fixedDeltaTime);
                rb.linearVelocity = transform.forward * currentSpeed;

                if (targetDirection.sqrMagnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDirection);
                    rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
                }
                
                rb.angularVelocity = Vector3.zero;
                return;
            }
        }
        else // AI Controlled
        {
            if (agent.enabled && agent.hasPath)
            {
                targetDirection = agent.desiredVelocity.normalized;
                inputMagnitude = 1f;
            }
        }

        float finalSpeedTarget = moveSpeed * inputMagnitude;
        Vector3 finalSteerDirection = targetDirection;

        if (_vehicle.IsControlledByPlayer && targetDirection.sqrMagnitude > 0.01f)
        {
            float dot = Vector3.Dot(transform.forward, targetDirection.normalized);
            if (!isReverseMode && dot < -0.6f) isReverseMode = true;
            else if (isReverseMode && dot > -0.2f) isReverseMode = false;

            if (isReverseMode)
            {
                finalSpeedTarget = -moveSpeed * inputMagnitude;
                finalSteerDirection = -targetDirection;
            }
        }

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(finalSteerDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            currentSpeed = Mathf.Lerp(currentSpeed, finalSpeedTarget, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = transform.forward * currentSpeed;
        rb.angularVelocity = Vector3.zero;
    }
}