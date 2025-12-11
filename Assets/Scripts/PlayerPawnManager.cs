using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPawnManager : MonoBehaviour
{
    [Header("Pawn References")]
    [SerializeField] private UnitController unitPawn; // Assign the player character (Unit) here
    [SerializeField] private VehicleController vehiclePawn; // Assign the vehicle here

    [Header("Camera")]
    [SerializeField] private ManualFollowCamera followCamera; // Assign your Main Camera with ManualFollowCamera script here

    [Header("Vehicle Interaction")]
    [SerializeField] private float vehicleDetectionRadius = 3f; // 탈것 탐지 반경
    [SerializeField] private LayerMask vehicleLayer; // 탈것 레이어 (Unity Editor에서 설정 필요)
    [SerializeField] private LayerMask groundLayer; // 바닥 레이어 (Unity Editor에서 설정 필요)

    private UnitController currentUnit; // Reference to the currently possessed Unit
    private VehicleController currentVehicle; // Reference to the currently possessed Vehicle

    private InputSystem_Actions playerActions;
    private InputSystem_Actions.PlayerActions playerOnFootActions; // For Unit control
    private InputSystem_Actions.VehicleActions playerInVehicleActions; // For Vehicle control

    public static Transform ActivePlayerTransform { get; private set; }

    private void Awake()
    {
        playerActions = new InputSystem_Actions();
        playerOnFootActions = playerActions.Player;
        playerInVehicleActions = playerActions.Vehicle;

        // Subscribe to Interact action (always active for the PlayerPawnManager)
        playerOnFootActions.Interact.performed += OnInteract;
        playerOnFootActions.Interact_Hold.performed += OnInteractHold;
        // The Interact_Hold action will be subscribed/unsubscribed dynamically for vehicle

        // Initially disable all pawns
        // Pawns will be disabled/enabled by PossessUnit/PossessVehicle in Start()
        // No need to call DisableControl here, as it might be too early.
    }

    private void Start()
    {
        // Ensure both pawns start in a known, disabled state before placing them.
        // This prevents physics from interfering with the initial placement.
        unitPawn?.DisableControl();
        vehiclePawn?.DisableControl();

        // Adjust initial positions to be on the ground for both player and vehicle.
        // This loop ensures the exact same logic is applied to both.
        Transform[] pawnsToPlace = { unitPawn?.transform, vehiclePawn?.transform };

        foreach (Transform pawnTransform in pawnsToPlace)
        {
            if (pawnTransform != null)
            {
                pawnTransform.position = FindGroundPosition(pawnTransform.position, pawnTransform);
            }
        }

        // Start by possessing the Unit, which will re-enable it.
        PossessUnit(unitPawn);
    }

    private void OnEnable()
    {
        // Only enable the relevant action map based on current possession
        if (currentUnit != null && currentUnit.IsControlledByPlayer)
        {
            playerOnFootActions.Enable();
        }
        else if (currentVehicle != null && currentVehicle.IsControlledByPlayer)
        {
            playerInVehicleActions.Enable();
        }
    }

    private void OnDisable()
    {
        playerOnFootActions.Disable();
        playerInVehicleActions.Disable();
        playerOnFootActions.Interact.performed -= OnInteract;
        playerOnFootActions.Interact_Hold.performed -= OnInteractHold;
        // Unsubscribe Interact_Hold if it was subscribed for vehicle
        playerInVehicleActions.Interact_Hold.performed -= OnInteractHold;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // If currently controlling Unit, try to enter vehicle
        if (currentUnit != null && currentUnit.IsControlledByPlayer)
        {
            TryEnterVehicle();
        }
        // If currently controlling Vehicle, Interact does nothing (Interact_Hold handles leaving)
    }

    private void OnInteractHold(InputAction.CallbackContext context)
    {
        // If currently controlling Vehicle, try to exit
        if (currentVehicle != null && currentVehicle.IsControlledByPlayer)
        {
            ExitVehicle();
        }
    }

    

    private void PossessUnit(UnitController unitToPossess)
    {
        if (unitToPossess == null)
        {
            Debug.LogError("PlayerPawnManager: Cannot possess null Unit!");
            return;
        }

        // Unpossess current if any
        if (currentUnit != null && currentUnit.IsControlledByPlayer)
        {
            currentUnit.DisableControl();
        }
        if (currentVehicle != null && currentVehicle.IsControlledByPlayer)
        {
            currentVehicle.DisableControl();
            playerInVehicleActions.Interact_Hold.performed -= OnInteractHold; // Unsubscribe Interact_Hold
            playerInVehicleActions.Disable(); // Disable Vehicle actions
        }

        currentUnit = unitToPossess;
        currentVehicle = null; // Ensure no vehicle is possessed

        currentUnit.EnableControl(); // Enable Unit control
        playerOnFootActions.Enable(); // Enable Player actions

        ActivePlayerTransform = currentUnit.transform; // Set active transform

        if (followCamera != null)
        {
            followCamera.SetTarget(currentUnit.transform);
        }
        Debug.Log($"PlayerPawnManager: Possessed Unit: {currentUnit.name}");
    }

    private void PossessVehicle(VehicleController vehicleToPossess)
    {
        if (vehicleToPossess == null)
        {
            Debug.LogError("PlayerPawnManager: Cannot possess null Vehicle!");
            return;
        }

        // Unpossess current if any
        if (currentUnit != null && currentUnit.IsControlledByPlayer)
        {
            currentUnit.DisableControl();
            playerOnFootActions.Disable(); // Disable Player actions
            playerOnFootActions.Interact_Hold.performed -= OnInteractHold; // Unsubscribe Interact_Hold
        }
        if (currentVehicle != null && currentVehicle.IsControlledByPlayer)
        {
            currentVehicle.DisableControl();
            playerInVehicleActions.Interact_Hold.performed -= OnInteractHold; // Unsubscribe Interact_Hold
        }

        currentVehicle = vehicleToPossess;
        currentUnit = null; // Ensure no unit is possessed

        // Position vehicle on the ground
        currentVehicle.transform.position = FindGroundPosition(currentVehicle.transform.position, currentVehicle.transform);

        currentVehicle.EnableControl(); // Enable Vehicle control
        playerInVehicleActions.Enable(); // Enable Vehicle actions
        playerInVehicleActions.Interact_Hold.performed += OnInteractHold; // Subscribe to Interact_Hold

        ActivePlayerTransform = currentVehicle.transform; // Set active transform

        if (followCamera != null)
        {
            followCamera.SetTarget(currentVehicle.transform);
        }
        Debug.Log($"PlayerPawnManager: Possessed Vehicle: {currentVehicle.name}");
    }

    private void TryEnterVehicle()
    {
        Collider[] hitColliders = Physics.OverlapSphere(currentUnit.transform.position, vehicleDetectionRadius, vehicleLayer);
        VehicleController nearestVehicle = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.TryGetComponent<VehicleController>(out VehicleController vehicle))
            {
                float distance = Vector3.Distance(currentUnit.transform.position, vehicle.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestVehicle = vehicle;
                }
            }
        }

        if (nearestVehicle != null)
        {
            // Position unit near vehicle before possessing
            currentUnit.transform.position = nearestVehicle.transform.position + nearestVehicle.transform.forward * 2f; // Exit in front of vehicle
            PossessVehicle(nearestVehicle);
        }
    }

    private void ExitVehicle()
    {
        if (currentVehicle != null)
        {
            // Calculate desired exit position
            Vector3 desiredExitPosition = currentVehicle.transform.position + currentVehicle.transform.forward * 2f;
            // Find ground position for the unit
            unitPawn.transform.position = FindGroundPosition(desiredExitPosition, unitPawn.transform);
            PossessUnit(unitPawn);
        }
    }

    private Vector3 FindGroundPosition(Vector3 desiredPosition, Transform objectToPlace)
    {
        // Define a raycast origin slightly above the desired position
        float raycastOriginOffset = 10f; // Start 10 units above
        Vector3 rayOrigin = desiredPosition + Vector3.up * raycastOriginOffset;

        // Define the maximum distance for the raycast
        float raycastDistance = raycastOriginOffset + 20f; // 10 units down from origin + 20 more units

        Debug.Log($"[GroundCheck] Starting ground check for '{objectToPlace.name}' at position: {desiredPosition}. Raycasting from: {rayOrigin}");

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer)) // Use groundLayer
        {
            Debug.Log($"[GroundCheck] Success! Ground detected at: {hit.point} on object: {hit.collider.name}");

            var col = objectToPlace.GetComponent<Collider>();
            if (col != null)
            {
                float offset = 0f;
                // Check for specific collider types to get the most accurate offset from the pivot to the bottom.
                if (col is SphereCollider sphere)
                {
                    offset = sphere.radius - sphere.center.y;
                    Debug.Log($"[GroundCheck] Detected SphereCollider. Offset = radius({sphere.radius}) - center.y({sphere.center.y}) = {offset}");
                }
                else if (col is CapsuleCollider capsule)
                {
                    offset = (capsule.height / 2f) - capsule.center.y;
                    Debug.Log($"[GroundCheck] Detected CapsuleCollider. Offset = (height/2)({capsule.height / 2f}) - center.y({capsule.center.y}) = {offset}");
                }
                else if (col is BoxCollider box)
                {
                    offset = (box.size.y / 2f) - box.center.y;
                    Debug.Log($"[GroundCheck] Detected BoxCollider. Offset = (size.y/2)({box.size.y / 2f}) - center.y({box.center.y}) = {offset}");
                }
                else // Fallback for any other collider type
                {
                    // This is less accurate but better than nothing.
                    offset = col.bounds.extents.y;
                    Debug.Log($"[GroundCheck] Detected generic {col.GetType()}. Using world bounds.extents.y for offset: {offset}");
                }

                Vector3 finalPosition = hit.point + Vector3.up * offset;
                Debug.Log($"[GroundCheck] Calculated final position for '{objectToPlace.name}': {finalPosition}");
                return finalPosition;
            }
            else
            {
                Debug.LogWarning($"[GroundCheck] {objectToPlace.name} has no collider! Cannot apply height offset.");
                Debug.Log($"[GroundCheck] Calculated final position for '{objectToPlace.name}': {hit.point}");
                return hit.point; // Return original hit point if no collider
            }
        }
        else
        {
            Debug.LogWarning($"[GroundCheck] Failed! FindGroundPosition did not find ground for '{objectToPlace.name}'. Returning original position.");
            Debug.Log($"[GroundCheck] No position change for '{objectToPlace.name}'.");
            return desiredPosition;
        }
    }
}
