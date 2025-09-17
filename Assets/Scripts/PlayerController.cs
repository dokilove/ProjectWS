using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float evadeForce = 10f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private ManualFollowCamera followCamera; // Assign your Main Camera with ManualFollowCamera script here

    private Rigidbody rb;
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerActions = new InputSystem_Actions();
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

        // Trigger camera damping
        if (followCamera != null)
        {
            followCamera.TriggerEvadeDamping();
        }
    }
}
