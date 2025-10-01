using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f; // Vehicle might be faster
    [SerializeField] private float rotationSpeed = 15f;

    private Rigidbody rb;
    private InputSystem_Actions playerActions; // Will use player's input actions
    private Vector2 moveInput;

    public bool IsControlledByPlayer { get; private set; } = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions(); // Initialize here, but enable/disable externally
    }

    public void EnableControl()
    {
        IsControlledByPlayer = true;
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
        if (!IsControlledByPlayer) return;
        moveInput = playerActions.Vehicle.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (!IsControlledByPlayer) return;

        rb.angularVelocity = Vector3.zero; // Prevent unwanted rotation from collisions

        Vector3 moveVector = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.linearVelocity = moveVector * moveSpeed;

        if (moveVector != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVector);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}