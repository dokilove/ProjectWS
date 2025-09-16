using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

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
    }

    private void OnDisable()
    {
        playerActions.Player.Disable();
    }

    private void Update()
    {
        moveInput = playerActions.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Create a 3D movement vector from the 2D input.
        // We use the x input for x-axis movement and the y input for z-axis movement.
        Vector3 moveVector = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.linearVelocity = moveVector * moveSpeed;
    }
}
