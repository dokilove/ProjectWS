using UnityEngine;

[RequireComponent(typeof(Animator))]
public class UnitAnimator : MonoBehaviour
{
    private Animator _animator;
    private Rigidbody _rigidbody; // Needed for accurate velocity

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        // We get the rigidbody from the parent, as this script is on the same level
        _rigidbody = GetComponentInParent<Rigidbody>();
    }

    // --- Attack Mode Callbacks ---
    public void OnEnterRangedMode()
    {
        SetRangedAttackState(true); // Enable shooting animation
        // TODO: Set animator parameters for ranged movement (e.g., strafing blend tree)
        Debug.Log("UnitAnimator: Entering Ranged Mode");
    }

    public void OnEnterMeleeMode()
    {
        SetRangedAttackState(false); // Disable shooting animation
        // TODO: Set animator parameters for melee movement (e.g., normal blend tree)
        Debug.Log("UnitAnimator: Entering Melee Mode");
    }

    public void SetRangedAttackState(bool isRanged)
    {
        if (_animator != null)
        {
            _animator.SetBool("IsRangedAttacking", isRanged);
        }
    }

    /// <summary>
    /// Sets the movement parameters (Run_x, Run_y) on the Animator.
    /// Should be called from FixedUpdate to align with physics.
    /// </summary>
    public void SetMoveParameters(Vector2 moveInput, Vector3 aimDirection)
    {
        if (_animator == null || _rigidbody == null) return;

        // Use the actual velocity for accuracy, not the raw input
        Vector3 currentVelocity = _rigidbody.linearVelocity;
        Vector3 currentMoveDirection = currentVelocity.normalized;

        // Get the aiming direction as the reference "forward"
        Vector3 aimForward = aimDirection;
        aimForward.y = 0;
        aimForward.Normalize();

        // Get the perpendicular right vector from the aim direction
        Vector3 aimRight = Vector3.Cross(Vector3.up, aimForward);

        // Project the move direction onto the aim-relative axes
        float runX = Vector3.Dot(currentMoveDirection, aimRight);
        float runY = Vector3.Dot(currentMoveDirection, aimForward);

        // If there's input but no velocity (e.g., stuck on a wall),
        // use the input directly to show intent.
        if (moveInput.sqrMagnitude > 0.1f && currentVelocity.sqrMagnitude < 0.1f)
        {
            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraRightFlat = new Vector3(cameraRight.x, 0, cameraRight.z).normalized;
            Vector3 cameraForwardFlat = Vector3.Cross(Vector3.up, cameraRightFlat);
            Vector3 moveForward = -cameraForwardFlat;
            Vector3 moveRight = cameraRightFlat;
            Vector3 moveIntentDirection = (moveForward * moveInput.y + moveRight * moveInput.x).normalized;

            runX = Vector3.Dot(moveIntentDirection, aimRight);
            runY = Vector3.Dot(moveIntentDirection, aimForward);
        }

        _animator.SetFloat("Run_x", runX);
        _animator.SetFloat("Run_y", runY);
    }

    public void TriggerAttack()
    {
        if (_animator != null) _animator.SetTrigger("Attack");
    }

    public void TriggerReload()
    {
        if (_animator != null) _animator.SetTrigger("Reload");
    }

    public void TriggerEvade()
    {
        if (_animator != null) _animator.SetTrigger("Evade");
    }

    public void TriggerMelee(int comboCounter)
    {
        if (_animator != null) _animator.SetTrigger("Melee" + comboCounter);
    }

    public void TriggerChargeMelee()
    {
        if (_animator != null) _animator.SetTrigger("MeleeChargeAttack");
    }

    public void ForceIdle()
    {
        if (_animator != null) _animator.SetTrigger("ForceIdle");
    }
}