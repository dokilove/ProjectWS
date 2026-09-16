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

    private void Awake()
    {
        lookInput = Vector2.zero; // Ensure lookInput is zero at start
    }

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
    private System.Action<InputAction.CallbackContext> onSwitchAttackModeStarted;
    private System.Action<InputAction.CallbackContext> onSwitchAttackModeCanceled;
    private System.Action<InputAction.CallbackContext> onMeleeAttackPerformed; // For forced melee switch

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

        // Attack Mode specific delegates
        onFirePerformed = ctx => { if (_unit.CurrentAttackMode == AttackMode.Ranged) _unit.UnitWeaponSystem.HandleFireInput(); };
        onFireHoldStarted = ctx => { if (_unit.CurrentAttackMode == AttackMode.Ranged) isFireHeld = true; };
        onFireHoldCanceled = ctx => { if (_unit.CurrentAttackMode == AttackMode.Ranged) isFireHeld = false; };
        onReloadPerformed = ctx => { _unit.UnitWeaponSystem.HandleReloadInput(); };

        onMeleeChargeStarted = ctx => { 
            if (_unit.CurrentAttackMode == AttackMode.Melee) 
            {
                HandleAiming();
                _unit.UnitMeleeSystem.HandleMeleeChargeInput(); 
            }
        };
        onMeleeChargeCanceled = ctx => { 
            if (_unit.CurrentAttackMode == AttackMode.Melee) 
            {
                HandleAiming();
                _unit.UnitMeleeSystem.HandleMeleeChargeReleaseInput(); 
            }
        };

        // Mode switching delegates
        onSwitchAttackModeStarted = ctx => { if (lastUsedInputDevice == InputDeviceType.MouseKeyboard) _unit.SetAttackMode(AttackMode.Ranged); };
        onSwitchAttackModeCanceled = ctx => { if (lastUsedInputDevice == InputDeviceType.MouseKeyboard) _unit.SetAttackMode(AttackMode.Melee); };
        onMeleeAttackPerformed = ctx => {
            // Only allow forced melee switch if Ranged mode input is NOT actively held.
            if (!_unit.UnitInput.IsRangedModeInputActive)
            {
                _unit.SetAttackMode(AttackMode.Melee);
                HandleAiming();
                _unit.UnitMeleeSystem.HandleMeleeComboInput();
            }
            else
            {
                Debug.Log("MeleeAttack ignored because Ranged mode input is actively held.");
            }
        };
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
        playerActions.Player.SwitchAttackMode.started += onSwitchAttackModeStarted;
        playerActions.Player.SwitchAttackMode.canceled += onSwitchAttackModeCanceled;
        playerActions.Player.MeleeAttack.performed += onMeleeAttackPerformed;
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
        playerActions.Player.SwitchAttackMode.started -= onSwitchAttackModeStarted;
        playerActions.Player.SwitchAttackMode.canceled -= onSwitchAttackModeCanceled;
        playerActions.Player.MeleeAttack.performed -= onMeleeAttackPerformed;

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

        // Gamepad Ranged Mode Switching
        if (lastUsedInputDevice == InputDeviceType.Gamepad)
        {
            if (lookInput.sqrMagnitude > 0.1f * 0.1f) // Deadzone check
            {
                _unit.SetAttackMode(AttackMode.Ranged);
            }
            else
            {
                _unit.SetAttackMode(AttackMode.Melee);
            }
        }
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
        // In Melee mode, auto-aim is persistent if a target is found.
        // In Ranged mode, aiming is always player-controlled.

        if (_unit.CurrentAttackMode == AttackMode.Melee)
        {
            // 1순위: 이동 입력이 있을 때는 이동 방향으로 조준
            if (moveInput.sqrMagnitude > 0.01f)
            {
                if (Camera.main != null)
                {
                    Vector3 cameraRight = Camera.main.transform.right;
                    Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
                    Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);
                    Vector3 moveForward = -cameraForwardFlat;
                    Vector3 moveRight = cameraRightFlat;

                    Vector3 moveDirection = (moveForward * moveInput.y + moveRight * moveInput.x).normalized;
                    if (moveDirection != Vector3.zero)
                    {
                        aimDirection = moveDirection;
                    }
                }
                else
                {
                    Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
                    if (moveDirection != Vector3.zero)
                    {
                        aimDirection = moveDirection;
                    }
                }

                _unit.UnitMove.ResetAutoAim();
            }
            // 2순위: 이동 입력이 없고, Combo 2 이상이거나 차지 중일 때만 적 자동 추적
            else
            {
                bool canAutoAim = _unit.UnitMeleeSystem != null &&
                    (_unit.UnitMeleeSystem.ComboCounter > 0 || _unit.UnitMeleeSystem.IsMeleeChargePrimed);

                if (canAutoAim && _unit.UnitMeleeSystem.AutoAimTargetPosition != Vector3.zero)
                {
                    Vector3 dirToEnemy = _unit.UnitMeleeSystem.AutoAimTargetPosition - transform.position;
                    dirToEnemy.y = 0;
                    if (dirToEnemy.sqrMagnitude > 0.001f)
                    {
                        aimDirection = dirToEnemy.normalized;
                        _unit.UnitMove.SetIsAutoAiming(true);
                    }
                    else
                    {
                        Vector3 fwd = transform.forward;
                        fwd.y = 0;
                        if (fwd.sqrMagnitude > 0.001f)
                        {
                            aimDirection = fwd.normalized;
                        }
                        _unit.UnitMove.ResetAutoAim();
                    }
                }
                // 3순위: Combo 1 대기 상태이거나 적이 없을 때 전방 유지
                else
                {
                    Vector3 fwd = transform.forward;
                    fwd.y = 0;
                    if (fwd.sqrMagnitude > 0.001f)
                    {
                        aimDirection = fwd.normalized;
                    }

                    _unit.UnitMove.ResetAutoAim();
                }
            }
        }
        else // Ranged Mode
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

    /// <summary>
    /// 현재 이동 입력 벡터
    /// </summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>
    /// Checks if the input for Ranged Mode (SwitchAttackMode or Look stick) is currently active.
    /// </summary>
    public bool IsRangedModeInputActive
    {
        get
        {
            if (lastUsedInputDevice == InputDeviceType.MouseKeyboard)
            {
                return playerActions.Player.SwitchAttackMode.IsPressed();
            }
            else if (lastUsedInputDevice == InputDeviceType.Gamepad)
            {
                return playerActions.Player.Look.ReadValue<Vector2>().sqrMagnitude > 0.1f * 0.1f;
            }
            return false;
        }
    }

    /// <summary>
    /// Resets any active hold states for fire and melee charge.
    /// Called by Unit when the attack mode changes.
    /// </summary>
    public void ResetHoldStates()
    {
        isFireHeld = false;
        _unit.UnitMeleeSystem.CancelCharge();
    }
}