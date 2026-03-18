using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleInput : MonoBehaviour
{
    // --- Dependencies ---
    private Vehicle _vehicle;

    // --- Input State ---
    private InputSystem_Actions playerActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector2 mousePositionInput;
    private bool isFireHeld = false;
    private bool isNeutralTurning = false;

    public bool IsNeutralTurning => isNeutralTurning;

    private enum InputDeviceType { None, Gamepad, MouseKeyboard }
    private InputDeviceType lastUsedInputDevice = InputDeviceType.None;

    // --- Input Action Delegates ---
    private System.Action<InputAction.CallbackContext> onMovePerformed;
    private System.Action<InputAction.CallbackContext> onMoveCanceled;
    private System.Action<InputAction.CallbackContext> onLookPerformed;
    private System.Action<InputAction.CallbackContext> onLookCanceled;
    private System.Action<InputAction.CallbackContext> onMousePositionPerformed;
    private System.Action<InputAction.CallbackContext> onMousePositionCanceled;
    private System.Action<InputAction.CallbackContext> onFirePerformed;
    private System.Action<InputAction.CallbackContext> onFireHoldStarted;
    private System.Action<InputAction.CallbackContext> onFireHoldCanceled;
    private System.Action<InputAction.CallbackContext> onNeutralTurnStarted;
    private System.Action<InputAction.CallbackContext> onNeutralTurnCanceled;
    private System.Action<InputAction.CallbackContext> onReloadPerformed;

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
        playerActions = new InputSystem_Actions();

        onMovePerformed = ctx => { moveInput = ctx.ReadValue<Vector2>(); UpdateInputDevice(ctx.control.device); };
        onMoveCanceled = ctx => moveInput = Vector2.zero;
        onLookPerformed = ctx => { lookInput = ctx.ReadValue<Vector2>(); UpdateInputDevice(ctx.control.device); };
        onLookCanceled = ctx => lookInput = Vector2.zero;
        onMousePositionPerformed = ctx => { mousePositionInput = ctx.ReadValue<Vector2>(); UpdateInputDevice(ctx.control.device); };
        onMousePositionCanceled = ctx => mousePositionInput = Vector2.zero;

        onFirePerformed = ctx => _vehicle.VehicleWeaponSystem.HandleFireInput();
        onFireHoldStarted = ctx => isFireHeld = true;
        onFireHoldCanceled = ctx => isFireHeld = false;
        onReloadPerformed = ctx => _vehicle.VehicleWeaponSystem.HandleReloadInput();

        onNeutralTurnStarted = ctx => isNeutralTurning = true;
        onNeutralTurnCanceled = ctx => isNeutralTurning = false;
    }

    private void UpdateInputDevice(InputControl device)
    {
        if (device is Gamepad) lastUsedInputDevice = InputDeviceType.Gamepad;
        else if (device is Mouse || device is Keyboard) lastUsedInputDevice = InputDeviceType.MouseKeyboard;
    }

    public void EnableInput()
    {
        playerActions.Vehicle.Enable();
        playerActions.Vehicle.Move.performed += onMovePerformed;
        playerActions.Vehicle.Move.canceled += onMoveCanceled;
        playerActions.Vehicle.Look.performed += onLookPerformed;
        playerActions.Vehicle.Look.canceled += onLookCanceled;
        playerActions.Vehicle.MousePosition.performed += onMousePositionPerformed;
        playerActions.Vehicle.MousePosition.canceled += onMousePositionCanceled;
        playerActions.Vehicle.Fire.performed += onFirePerformed;
        playerActions.Vehicle.Fire_Hold.started += onFireHoldStarted;
        playerActions.Vehicle.Fire_Hold.canceled += onFireHoldCanceled;
        playerActions.Vehicle.NeutralTurn.started += onNeutralTurnStarted;
        playerActions.Vehicle.NeutralTurn.canceled += onNeutralTurnCanceled;
        playerActions.Vehicle.Reload.performed += onReloadPerformed;
    }

    public void DisableInput()
    {
        if (playerActions == null) return;
        playerActions.Vehicle.Disable();
        playerActions.Vehicle.Move.performed -= onMovePerformed;
        playerActions.Vehicle.Move.canceled -= onMoveCanceled;
        playerActions.Vehicle.Look.performed -= onLookPerformed;
        playerActions.Vehicle.Look.canceled -= onLookCanceled;
        playerActions.Vehicle.MousePosition.performed -= onMousePositionPerformed;
        playerActions.Vehicle.MousePosition.canceled -= onMousePositionCanceled;
        playerActions.Vehicle.Fire.performed -= onFirePerformed;
        playerActions.Vehicle.Fire_Hold.started -= onFireHoldStarted;
        playerActions.Vehicle.Fire_Hold.canceled -= onFireHoldCanceled;
        playerActions.Vehicle.NeutralTurn.started -= onNeutralTurnStarted;
        playerActions.Vehicle.NeutralTurn.canceled -= onNeutralTurnCanceled;
        playerActions.Vehicle.Reload.performed -= onReloadPerformed;

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isFireHeld = false;
        isNeutralTurning = false;
    }

    private void Update()
    {
        if (!_vehicle.IsControlledByPlayer) return;

        _vehicle.VehicleMove.SetPlayerInput(moveInput, isNeutralTurning);
        _vehicle.VehicleWeaponSystem.HandleFireHold(isFireHeld);
        HandlePlayerAiming();
    }

    private void HandlePlayerAiming()
    {
        Vector3 calculatedAimDirection = Vector3.zero;

        if (lastUsedInputDevice == InputDeviceType.Gamepad)
        {
            if (lookInput.sqrMagnitude > 0.01f)
            {
                Vector3 camForward = Camera.main.transform.forward;
                Vector3 camRight = Camera.main.transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();
                calculatedAimDirection = (camForward * lookInput.y + camRight * lookInput.x).normalized;
            }
        }
        else if (lastUsedInputDevice == InputDeviceType.MouseKeyboard)
        {
            Ray ray = Camera.main.ScreenPointToRay(mousePositionInput);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                Vector3 directionToMouse = hit.point - transform.position;
                directionToMouse.y = 0;
                if (directionToMouse.sqrMagnitude > 0.01f)
                {
                    calculatedAimDirection = directionToMouse.normalized;
                }
            }
        }

        if (_vehicle.VehicleWeaponSystem != null)
        {
            _vehicle.VehicleWeaponSystem.SetAim(calculatedAimDirection, transform);
        }
        
        lookInput = Vector2.zero;
    }
}