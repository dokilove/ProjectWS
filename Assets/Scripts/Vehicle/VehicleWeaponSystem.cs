using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VehicleWeaponSystem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private float turretRotationSpeed = 10f;
    [SerializeField] private Transform firePoint;

    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    // --- Dependencies ---
    private Vehicle _vehicle;

    [Header("Auto Weapon Settings")]
    [SerializeField] private bool isAutoWeapon = false;
    [SerializeField] private LayerMask autoTargetLayer;

    // --- State ---
    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;
    private Coroutine reloadCoroutine;
    private Vector3 currentAimDirection;
    private Transform currentAutoTarget;
    private Collider[] autoTargetColliders = new Collider[20];

    // --- Projectile Pool ---
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Public Properties ---
    public Transform TurretTransform => turretTransform;
    public Transform FirePoint => firePoint != null ? firePoint : (turretTransform != null ? turretTransform : transform);
    public WeaponData WeaponData
    {
        get
        {
            if (weaponData != null) return weaponData;
            if (PlayerPawnManager.ActiveUnit != null && PlayerPawnManager.ActiveUnit.UnitWeaponSystem != null)
            {
                return PlayerPawnManager.ActiveUnit.UnitWeaponSystem.WeaponData;
            }
            return null;
        }
    }
    public int CurrentAmmo
    {
        get
        {
            if (weaponData == null && PlayerPawnManager.ActiveUnit != null && PlayerPawnManager.ActiveUnit.UnitWeaponSystem != null)
            {
                return PlayerPawnManager.ActiveUnit.UnitWeaponSystem.CurrentAmmo;
            }
            return currentAmmo;
        }
    }
    public bool IsReloading
    {
        get
        {
            if (weaponData == null && PlayerPawnManager.ActiveUnit != null && PlayerPawnManager.ActiveUnit.UnitWeaponSystem != null)
            {
                return PlayerPawnManager.ActiveUnit.UnitWeaponSystem.IsReloading;
            }
            return isReloading;
        }
    }
    public bool IsAutoWeapon { get => isAutoWeapon; set => isAutoWeapon = value; }
    public LayerMask AutoTargetLayer { get => autoTargetLayer; set => autoTargetLayer = value; }
    public Transform CurrentAutoTarget => currentAutoTarget;

    public void Init(Vehicle vehicle)
    {
        _vehicle = vehicle;
    }

    private void Start()
    {
        if (weaponData != null)
        {
            currentAmmo = weaponData.magazineSize;
            if (weaponData.projectileData != null && weaponData.projectileData.projectilePrefab != null)
            {
                for (int i = 0; i < poolSize; i++)
                {
                    GameObject proj = Instantiate(weaponData.projectileData.projectilePrefab);
                    proj.SetActive(false);
                    projectilePool.Add(proj);
                }
            }
        }
    }

    private void Update()
    {
        if (isAutoWeapon)
        {
            HandleAutoWeapon();
        }
    }

    private void HandleAutoWeapon()
    {
        UpdateAutoTarget();

        if (currentAutoTarget != null)
        {
            Transform vTransform = _vehicle != null ? _vehicle.transform : transform;
            SetAimAI(currentAutoTarget, vTransform);

            if (CanFire(currentAutoTarget))
            {
                Fire(currentAutoTarget);
            }
        }
    }

    private void UpdateAutoTarget()
    {
        if (weaponData == null) return;
        
        LayerMask targetMask = autoTargetLayer;
        if (targetMask.value == 0)
        {
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex >= 0)
            {
                targetMask = 1 << enemyLayerIndex;
            }
            else
            {
                targetMask = 1 << 10;
            }
        }

        int count = Physics.OverlapSphereNonAlloc(transform.position, weaponData.lockOnRadius, autoTargetColliders, targetMask);

        Transform bestTarget = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Transform target = autoTargetColliders[i].transform;

            if (target == transform || target.IsChildOf(transform) || (_vehicle != null && (target == _vehicle.transform || target.IsChildOf(_vehicle.transform)))) continue;

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = target;
            }
        }

        currentAutoTarget = bestTarget;
    }

    public void SetAim(Vector3 aimDirection, Transform vehicleTransform)
    {
        Transform tTransform = turretTransform != null ? turretTransform : transform;
        Transform vTransform = vehicleTransform != null ? vehicleTransform : transform;
        if (aimDirection.sqrMagnitude > 0.01f && tTransform != null && vTransform != null)
        {
            currentAimDirection = aimDirection;

            Vector3 localTargetDir = vTransform.InverseTransformDirection(currentAimDirection);
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            float maxAngle = WeaponData != null ? WeaponData.attackAngle : 90f;
            float clampedAngle = Mathf.Clamp(targetAngle, -maxAngle / 2f, maxAngle / 2f);

            Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
            tTransform.localRotation = Quaternion.Slerp(tTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);
        }
    }
    
    public void SetAimAI(Transform target, Transform vehicleTransform)
    {
        Transform tTransform = turretTransform != null ? turretTransform : transform;
        Transform vTransform = vehicleTransform != null ? vehicleTransform : transform;
        if (target != null && tTransform != null && vTransform != null)
        {
            Vector3 direction = target.position - tTransform.position;
            direction.y = 0;

            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 localTargetDir = vTransform.InverseTransformDirection(direction.normalized);
                float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
                float maxAngle = WeaponData != null ? WeaponData.attackAngle : 90f;
                float clampedAngle = Mathf.Clamp(targetAngle, -maxAngle / 2f, maxAngle / 2f);

                Quaternion desiredLocalRotation = Quaternion.Euler(0, clampedAngle, 0);
                tTransform.localRotation = Quaternion.Slerp(tTransform.localRotation, desiredLocalRotation, turretRotationSpeed * Time.deltaTime);
            }
        }
    }

    public void HandleFireInput()
    {
        if (CanFire())
        {
            Fire();
        }
        else if (CurrentAmmo <= 0 && !IsReloading)
        {
            HandleReloadInput();
        }
    }

    public void HandleFireHold(bool isHeld)
    {
        if (isHeld && CanFire())
        {
            Fire();
        }
        if (isHeld && CurrentAmmo <= 0 && !IsReloading)
        {
            HandleReloadInput();
        }
    }

    public void HandleReloadInput()
    {
        if (weaponData == null && PlayerPawnManager.ActiveUnit != null && PlayerPawnManager.ActiveUnit.UnitWeaponSystem != null)
        {
            PlayerPawnManager.ActiveUnit.UnitWeaponSystem.HandleReloadInput();
            return;
        }
        if (!isReloading && weaponData != null && currentAmmo < weaponData.magazineSize)
        {
            reloadCoroutine = StartCoroutine(Reload());
        }
    }

    public bool CanFire(Transform aiTarget = null)
    {
        WeaponData data = WeaponData;
        if (data == null || Time.time < nextFireTime || IsReloading || CurrentAmmo <= 0)
        {
            return false;
        }

        if (aiTarget != null || isAutoWeapon)
        {
            Transform target = aiTarget != null ? aiTarget : currentAutoTarget;
            if (target == null) return false;
            
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget > data.lockOnRadius) return false;

            Transform tTransform = turretTransform != null ? turretTransform : transform;
            Vector3 directionToTarget = (target.position - tTransform.position);
            directionToTarget.y = 0;
            float angleToTarget = Vector3.Angle(tTransform.forward, directionToTarget);
            return angleToTarget <= Mathf.Max(45f, data.attackAngle / 2f);
        }
        
        return true;
    }

    public void Fire(Transform aiTarget = null)
    {
        if (!CanFire(aiTarget)) return;

        WeaponData data = WeaponData;
        if (data == null) return;

        nextFireTime = Time.time + 1f / data.fireRate;

        if (weaponData == null && PlayerPawnManager.ActiveUnit != null && PlayerPawnManager.ActiveUnit.UnitWeaponSystem != null)
        {
            Transform fPoint = FirePoint;
            Vector3 fireDir = turretTransform != null ? turretTransform.forward : transform.forward;
            PlayerPawnManager.ActiveUnit.UnitWeaponSystem.FireFromVehicle(fPoint.position, fireDir);
            return;
        }

        currentAmmo--;
        
        Transform tTransform = turretTransform != null ? turretTransform : transform;
        Transform fPointLocal = firePoint != null ? firePoint : tTransform;

        Vector3 fireDirection = tTransform.forward;

        Transform target = aiTarget != null ? aiTarget : currentAutoTarget;
        if (target != null)
        {
            Vector3 directionToTarget = target.position - fPointLocal.position;
            directionToTarget.y = 0;
            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                fireDirection = directionToTarget.normalized;
            }
        }

        int sourceLayer = _vehicle != null ? _vehicle.gameObject.layer : gameObject.layer;

        for (int i = 0; i < data.projectilesPerShot; i++)
        {
            GameObject projectile = GetPooledProjectile();
            if (projectile != null)
            {
                Vector3 finalFireDirection = fireDirection;
                if (data.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-data.spreadAngle / 2, data.spreadAngle / 2);
                    finalFireDirection = Quaternion.AngleAxis(randomAngle, Vector3.up) * finalFireDirection;
                }

                projectile.transform.position = fPointLocal.position;
                projectile.transform.rotation = Quaternion.LookRotation(finalFireDirection);

                Projectile projScript = projectile.GetComponent<Projectile>();
                if (projScript != null)
                {
                    float projDamage = data.projectileData != null ? data.projectileData.damage : 10f;
                    projScript.Init(data.projectileData, finalFireDirection, sourceLayer, projDamage);
                }

                projectile.SetActive(true);
            }
        }
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(weaponData.reloadTime);
        currentAmmo = weaponData.magazineSize;
        isReloading = false;
        reloadCoroutine = null;
    }

    private GameObject GetPooledProjectile()
    {
        // Find an inactive projectile in the pool
        foreach (var proj in projectilePool)
        {
            if (proj != null && !proj.activeInHierarchy)
            {
                return proj;
            }
        }

        // If no inactive projectile is found, create a new one and add it to the pool.
        Debug.LogWarning($"[{gameObject.name}] Projectile pool exhausted! Instantiating a new one. Consider increasing the default pool size.", this);
        if (weaponData != null && weaponData.projectileData != null && weaponData.projectileData.projectilePrefab != null)
        {
            GameObject newProj = Instantiate(weaponData.projectileData.projectilePrefab);
            projectilePool.Add(newProj);
            return newProj;
        }

        return null;
    }
}