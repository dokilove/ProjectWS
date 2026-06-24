using UnityEngine;
using UnityEngine.InputSystem;

public class UnitInput : MonoBehaviour
{
    // --- Dependencies ---
    private Unit _unit; // Reference to the coordinator

    // --- Input State ---
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 mousePositionInput;
    private bool isFireHeld = false;
    private Vector3 aimDirection;

    private enum InputDeviceType { None, Gamepad, MouseKeyboard }
    private InputDeviceType lastUsedInputDevice = InputDeviceType.None;

    // --- Input Action Delegates ---
    private System.Action<InputAction.CallbackContext> onMovePerformed;
    private System.Action<InputAction.CallbackContext> onMoveCanceled;
    private System.Action<InputAction.CallbackContext> onLookPerformed;
    private System.Action<InputAction.CallbackContext> onLookCanceled;
    private System.Action<InputAction.CallbackContext> onMousePositionPerformed;
    private System.Action<InputAction.CallbackContext> onMousePositionCanceled;
    private System.Action<InputAction.CallbackContext> onEvadePerformed;
    private System.Action<InputAction.CallbackContext> onInteractPerformed;
    private System.Action<InputAction.CallbackContext> onFirePerformed;
    private System.Action<InputAction.CallbackContext> onFireHoldStarted;
    private System.Action<InputAction.CallbackContext> onFireHoldCanceled;
    private System.Action<InputAction.CallbackContext> onReloadPerformed;
    private System.Action<InputAction.CallbackContext> onMeleeChargeStarted;
    private System.Action<InputAction.CallbackContext> onMeleeChargeCanceled;

    /// <summary>
    /// Initializes the Input component with a reference to the central Unit coordinator.
    /// </summary>
    public void Init(Unit unit)
    {
        _unit = unit;
        playerActions = new InputSystem_Actions();
        aimDirection = transform.forward; // Initialize aim direction

        // Initialize delegates
        onMovePerformed = ctx => { moveInput = ctx.ReadValue<Vector2>(); UpdateInputDevice(ctx.control.device); };
        onMoveCanceled = ctx => moveInput = Vector2.zero;
        onLookPerformed = ctx => { lookInput = ctx.ReadValue<Vector2>(); UpdateInputDevice(ctx.control.device); };
        onLookCanceled = ctx => lookInput = Vector2.zero;
        onMousePositionPerformed = ctx => { mousePositionInput = ctx.ReadValue<Vector2>(); UpdateInputDevice(ctx.control.device); };
        onMousePositionCanceled = ctx => mousePositionInput = Vector2.zero;
        
        onEvadePerformed = ctx => _unit.UnitMove.PerformEvade(moveInput);
        onInteractPerformed = ctx => OnInteract(ctx); // Placeholder for now

        onFirePerformed = ctx => _unit.UnitWeaponSystem.HandleFireInput();
        onFireHoldStarted = ctx => isFireHeld = true;
        onFireHoldCanceled = ctx => isFireHeld = false;
        onReloadPerformed = ctx => _unit.UnitWeaponSystem.HandleReloadInput();

        onMeleeChargeStarted = ctx => _unit.UnitMeleeSystem.HandleMeleeChargeInput();
        onMeleeChargeCanceled = ctx => _unit.UnitMeleeSystem.HandleMeleeChargeReleaseInput();
    }

    private void UpdateInputDevice(InputControl device)
    {
        if (device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad;
        else if (device is Mouse || device is Keyboard) lastUsedInputDevice = InputDeviceType.MouseKeyboard;
    }

    public void EnableInput()
    {
        playerActions.Player.Enable();
        playerActions.Player.Move.performed += onMovePerformed;
        playerActions.Player.Move.canceled += onMoveCanceled;
        playerActions.Player.Look.performed += onLookPerformed;
        playerActions.Player.Look.canceled += onLookCanceled;
        playerActions.Player.MousePosition.performed += onMousePositionPerformed;
        playerActions.Player.MousePosition.canceled += onMousePositionCanceled;
        playerActions.Player.Evade.performed += onEvadePerformed;
        playerActions.Player.Interact.performed += onInteractPerformed;
        playerActions.Player.Fire.performed += onFirePerformed;
        playerActions.Player.Fire_Hold.started += onFireHoldStarted;
        playerActions.Player.Fire_Hold.canceled += onFireHoldCanceled;
        playerActions.Player.Reload.performed += onReloadPerformed;
        playerActions.Player.MeleeAttack_Hold.started += onMeleeChargeStarted;
        playerActions.Player.MeleeAttack_Hold.canceled += onMeleeChargeCanceled;
    }

    public void DisableInput()
    {
        if (playerActions == null) return;
        playerActions.Player.Disable();
        playerActions.Player.Move.performed -= onMovePerformed;
        playerActions.Player.Move.canceled -= onMoveCanceled;
        playerActions.Player.Look.performed -= onLookPerformed;
        playerActions.Player.Look.canceled -= onLookCanceled;
        playerActions.Player.MousePosition.performed -= onMousePositionPerformed;
        playerActions.Player.MousePosition.canceled -= onMousePositionCanceled;
        playerActions.Player.Evade.performed -= onEvadePerformed;
        playerActions.Player.Interact.performed -= onInteractPerformed;
        playerActions.Player.Fire.performed -= onFirePerformed;
        playerActions.Player.Fire_Hold.started -= onFireHoldStarted;
        playerActions.Player.Fire_Hold.canceled -= onFireHoldCanceled;
        playerActions.Player.Reload.performed -= onReloadPerformed;
        playerActions.Player.MeleeAttack_Hold.started -= onMeleeChargeStarted;
        playerActions.Player.MeleeAttack_Hold.canceled -= onMeleeChargeCanceled;

        // Reset state
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isFireHeld = false;
    }

    private void Update()
    {
        if (!_unit.IsControlledByPlayer) return;

        HandleAiming();
        _unit.UnitWeaponSystem.HandleFireHold(isFireHeld);
    }

    private void FixedUpdate()
    {
        if (!_unit.IsControlledByPlayer) return;

        _unit.UnitMove.HandleMovement(moveInput, aimDirection);
        // The animator component will also need this data in its own FixedUpdate
        _unit.UnitAnimator.SetMoveParameters(moveInput, aimDirection);
    }

    private void HandleAiming()
    {
        if (lastUsedInputDevice == InputDeviceType.Gamepad)
        {
            if (lookInput.sqrMagnitude > 0.1f * 0.1f)
            {
                Vector3 camForward = Camera.main.transform.forward;
                Vector3 camRight = Camera.main.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();
                Vector3 lookDirection = (camForward * lookInput.y + camRight * lookInput.x).normalized;
                if (lookDirection != Vector3.zero) aimDirection = lookDirection;
            }
        }
        else if (lastUsedInputDevice == InputDeviceType.MouseKeyboard)
        {
            Ray ray = Camera.main.ScreenPointToRay(mousePositionInput);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                Vector3 directionToMouse = hit.point - transform.position;
                directionToMouse.y = 0;
                if (directionToMouse.sqrMagnitude > 0.01f) aimDirection = directionToMouse.normalized;
            }
        }

        // Pass aim direction to other components that need it
        _unit.UnitWeaponSystem.SetAim(aimDirection);
        // Melee system might also need it if attacks are directional
        _unit.UnitMeleeSystem.SetAim(aimDirection);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // This can be expanded to find the closest interactable object
        Debug.Log("Unit Interact button pressed!");
    }
}