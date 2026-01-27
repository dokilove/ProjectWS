using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
public class PlayerPawnManager : MonoBehaviour
{
    [Header("Pawn Prefabs")]
    [SerializeField] private GameObject unitPawnPrefab; // Assign the player character (Unit) prefab here
    [SerializeField] private GameObject vehiclePawnPrefab; // Assign the vehicle prefab here

    [Header("Spawning")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform vehicleSpawnPoint;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera playerCam; // Assign your Main Camera with ManualFollowCamera script here
    [SerializeField] private CinemachineCamera vehicleCam;

    [Header("Vehicle Interaction")]
    [SerializeField] private float vehicleDetectionRadius = 3f; // 탈것 탐지 반경
    [SerializeField] private LayerMask vehicleLayer; // 탈것 레이어 (Unity Editor에서 설정 필요)
    [SerializeField] private LayerMask groundLayer; // 바닥 레이어 (Unity Editor에서 설정 필요)

    // --- Instantiated References ---
    private UnitController unitPawn; 
    private VehicleController vehiclePawn;

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
    }

    private void Start()
    {
        // --- 0. Validate Dependencies ---
        if (playerCam == null)
        {
            Debug.LogError("PlayerPawnManager: 'playerCam' is not assigned in the Inspector! Please assign your main camera.");            
        }
        if (vehicleCam == null)
        {
            Debug.LogError("PlayerPawnManager: 'vehicleCam' is not assigned in the Inspector! Please assign your main camera.");
        }

        // --- 1. Spawn Pawns from Prefabs ---
        SpawnPawns();

        if (unitPawn == null)
        {
            Debug.LogError("PlayerPawnManager: Unit Pawn failed to spawn. Aborting Start().");
            return;
        }

        // --- 2. Initialize Pawns ---
        // Ensure both pawns start in a known, disabled state before placing them.
        unitPawn.DisableControl();
        vehiclePawn?.DisableControl();

        // Adjust initial positions to be on the ground.
        unitPawn.transform.position = FindGroundPosition(unitPawn.transform.position, unitPawn.transform);
        if (vehiclePawn != null)
        {
            vehiclePawn.transform.position = FindGroundPosition(vehiclePawn.transform.position, vehiclePawn.transform);
        }

        // --- 3. Start by possessing the Unit ---
        PossessUnit(unitPawn);
    }

    private void SpawnPawns()
    {
        // Determine spawn positions
        Vector3 unitSpawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
        Quaternion unitSpawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : transform.rotation;

        // Spawn Unit
        if (unitPawnPrefab != null)
        {
            GameObject unitGO = Instantiate(unitPawnPrefab, unitSpawnPos, unitSpawnRot);
            unitPawn = unitGO.GetComponent<UnitController>();
            if (unitPawn == null)
            {
                Debug.LogError($"PlayerPawnManager: The prefab '{unitPawnPrefab.name}' does not have a UnitController component.");
            }
        }
        else
        {
            Debug.LogError("PlayerPawnManager: Unit Pawn Prefab is not assigned!");
            return;
        }

        // Spawn Vehicle (optional)
        if (vehiclePawnPrefab != null)
        {
            Vector3 vehicleSpawnPos = vehicleSpawnPoint != null ? vehicleSpawnPoint.position : transform.position + Vector3.right * 5f; // Default offset
            Quaternion vehicleSpawnRot = vehicleSpawnPoint != null ? vehicleSpawnPoint.rotation : transform.rotation;

            GameObject vehicleGO = Instantiate(vehiclePawnPrefab, vehicleSpawnPos, vehicleSpawnRot);
            vehiclePawn = vehicleGO.GetComponent<VehicleController>();
            if (vehiclePawn == null)
            {
                Debug.LogError($"PlayerPawnManager: The prefab '{vehiclePawnPrefab.name}' does not have a VehicleController component.");
            }
        }
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
            // 차량에서 내릴 때 플레이어 카메라 위치를 차량 카메라 위치로 옮김
            playerCam.ForceCameraPosition(vehicleCam.transform.position, vehicleCam.transform.rotation);
        }

        currentUnit = unitToPossess;
        currentVehicle = null; // Ensure no vehicle is possessed

        currentUnit.EnableControl(); // Enable Unit control
        playerOnFootActions.Enable(); // Enable Player actions

        ActivePlayerTransform = currentUnit.transform; // Set active transform

        if (playerCam != null)
        {
            playerCam.Target.TrackingTarget = currentUnit.transform;
        }
        playerCam.Priority = 10;
        vehicleCam.Priority = 5;
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

        if (vehicleCam != null)
        {
            vehicleCam.Target.TrackingTarget = currentVehicle.transform;
        }

        playerCam.Priority = 5;
        vehicleCam.Priority = 10;
        Debug.Log($"PlayerPawnManager: Possessed Vehicle: {currentVehicle.name}");
    }

    [Header("Exit Collision")]
    [SerializeField] private LayerMask obstacleLayer; // Walls and other obstacles
    [SerializeField] private float playerSpawnCheckRadius = 0.5f; // Should match player's collider radius

    private Vector3 FindSafeExitPosition()
    {
        Transform vehicleTransform = currentVehicle.transform;

        // Define potential exit points relative to the vehicle (left, right, back)
        Vector3[] exitOffsets = new Vector3[]
        {
            -vehicleTransform.right * 2.5f, // Left side
            vehicleTransform.right * 2.5f,  // Right side
            -vehicleTransform.forward * 3f // Back side
        };

        foreach (var offset in exitOffsets)
        {
            // We check a bit above the ground to avoid hitting the floor
            Vector3 checkPosition = vehicleTransform.position + offset + Vector3.up * (playerSpawnCheckRadius + 0.1f);
            
            // Check if the area is clear of obstacles
            if (!Physics.CheckSphere(checkPosition, playerSpawnCheckRadius, obstacleLayer))
            {
                Debug.Log($"[ExitCheck] Found safe exit position near {checkPosition}");
                return vehicleTransform.position + offset; // Return the position at the vehicle's level
            }
        }

        // Fallback: If all else fails, spawn on top of the vehicle
        Debug.LogWarning("[ExitCheck] No safe exit position found. Spawning on top of vehicle as a fallback.");
        return vehicleTransform.position + Vector3.up * 2f;
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
            // Find a safe position that isn't inside a wall
            Vector3 safeExitPosition = FindSafeExitPosition();
            
            // Place the player at the safe position, adjusted to the ground
            unitPawn.transform.position = FindGroundPosition(safeExitPosition, unitPawn.transform);

            // Snap the vehicle to its correct ground position before disabling it
            currentVehicle.transform.position = FindGroundPosition(currentVehicle.transform.position, currentVehicle.transform);

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
