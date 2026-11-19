using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnitWeaponSystem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject pistolModel; // Reference to the pistol GameObject

    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    // --- Dependencies ---
    private Unit _unit;

    // --- State ---
    private Vector3 aimDirection;
    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;

    // --- Projectile Pool ---
    private List<GameObject> projectilePool = new List<GameObject>();
    private int poolSize = 20;

    // --- Public Properties ---
    public Transform FirePoint => firePoint;
    public Transform TurretTransform => turretTransform;
    public int CurrentAmmo => currentAmmo;
    public WeaponData WeaponData => weaponData;

    public void Init(Unit unit)
    {
        _unit = unit;
    }

    private void Start()
    {
        if (weaponData == null)
        {
            Debug.LogWarning("WeaponData is not assigned in the inspector!", this);
            return;
        }

        currentAmmo = weaponData.magazineSize;
        aimDirection = transform.forward;

        // Initialize projectile pool
        if (weaponData.projectileData != null && weaponData.projectileData.projectilePrefab != null)
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject proj = Instantiate(weaponData.projectileData.projectilePrefab);
                proj.SetActive(false);
                projectilePool.Add(proj);
            }
        }
        
        // Initial state: Melee mode, so hide pistol
        if (pistolModel != null)
        {
            pistolModel.SetActive(false);
        }
    }

    // --- Attack Mode Callbacks ---
    public void OnEnterRangedMode()
    {
        if (pistolModel != null)
        {
            pistolModel.SetActive(true);
        }
        _unit.UnitAnimator.SetRangedAttackState(true); // Enable shooting animation
    }

    public void OnEnterMeleeMode()
    {
        if (pistolModel != null)
        {
            pistolModel.SetActive(false);
        }
        _unit.UnitAnimator.SetRangedAttackState(false); // Disable shooting animation
    }

    /// <summary>
    /// Sets the aiming direction for the turret. Called by UnitInput.
    /// </summary>
    public void SetAim(Vector3 newAimDirection, Vector3? newAimPoint = null)
    {
        aimDirection = newAimDirection;
        hasAimPoint = newAimPoint.HasValue;
        if (hasAimPoint)
        {
            aimPoint = newAimPoint.Value;
        }

        if (turretTransform != null && aimDirection.sqrMagnitude > 0.01f)
        {
            // Turret only rotates on Y axis, so we project the aim direction onto the horizontal plane.
            Vector3 horizontalAim = newAimDirection;
            horizontalAim.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(horizontalAim.normalized);
            turretTransform.rotation = Quaternion.Slerp(turretTransform.rotation, targetRotation, 20f * Time.deltaTime);
        }
    }

    private bool hasAimPoint;
    private Vector3 aimPoint;

    /// <summary>
    /// 실제 발사 방향 (수평). 조준선 표시와 발사가 같은 값을 사용합니다.
    /// - 마우스 조준: 발사 위치(FirePoint)에서 조준 지점을 향하는 방향
    ///   (조준 방향은 캐릭터 중심 기준인데 총구는 옆으로 떨어져 있어 평행하게 어긋나던 문제 보정)
    /// - 게임패드 등 조준 지점이 없을 때: 조준 방향 그대로
    /// 포탑 회전(Slerp) 지연과 무관하게 항상 현재 조준을 따릅니다.
    /// </summary>
    public Vector3 GetFireDirection()
    {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;

        if (hasAimPoint)
        {
            Vector3 fromMuzzle = aimPoint - origin;
            fromMuzzle.y = 0f;
            Vector3 fromBody = aimPoint - transform.position;
            fromBody.y = 0f;

            // 커서가 캐릭터에 너무 가까우면(총구 오프셋 안쪽) 방향이 뒤집힐 수 있으므로 조준 방향을 사용
            if (fromBody.sqrMagnitude > 1f && fromMuzzle.sqrMagnitude > 0.01f && Vector3.Dot(fromMuzzle, fromBody) > 0f)
            {
                return fromMuzzle.normalized;
            }
        }

        Vector3 direction = aimDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            return direction.normalized;
        }

        Vector3 forward = turretTransform != null ? turretTransform.forward : transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    /// <summary>
    /// Handles a single fire input event (e.g., button press).
    /// </summary>
    public void HandleFireInput()
    {
        if (CanFire())
        {
            Fire();
        }
        else if (currentAmmo <= 0 && !isReloading)
        {
            StartReload();
        }
    }

    /// <summary>
    /// Handles continuous fire input (e.g., button held down).
    /// </summary>
    public void HandleFireHold(bool isHeld)
    {
        if (isHeld && CanFire())
        {
            Fire();
        }
        if (isHeld && currentAmmo <= 0 && !isReloading)
        {
            StartReload();
        }
    }

    private float reloadFinishTime = 0f;

    /// <summary>
    /// 재장전 중인지 여부 (읽기 전용: 상태를 변경하지 않음).
    /// 재장전 완료 처리는 Update/각 입력 처리 시작 시 UpdateReloadState()에서 수행합니다.
    /// </summary>
    public bool IsReloading => isReloading;

    private void Update()
    {
        UpdateReloadState();
    }

    /// <summary>
    /// 재장전 시간이 지났으면 탄약을 채우고 재장전 상태를 해제합니다.
    /// Unit이 비활성(차량 탑승) 상태면 Update가 돌지 않으므로, 외부에서 호출되는 사격/재장전 진입점에서도 호출합니다.
    /// </summary>
    private void UpdateReloadState()
    {
        if (isReloading && Time.time >= reloadFinishTime)
        {
            currentAmmo = weaponData != null ? weaponData.magazineSize : 30;
            isReloading = false;
        }
    }

    /// <summary>
    /// Handles the reload input event.
    /// </summary>
    public void HandleReloadInput()
    {
        UpdateReloadState();
        if (!isReloading && weaponData != null && currentAmmo < weaponData.magazineSize)
        {
            StartReload();
        }
    }

    public void StartReload()
    {
        if (isReloading || weaponData == null) return;
        isReloading = true;
        reloadFinishTime = Time.time + weaponData.reloadTime;
        if (_unit != null && _unit.UnitAnimator != null && _unit.gameObject.activeInHierarchy)
        {
            _unit.UnitAnimator.TriggerReload();
        }
    }

    private bool CanFire()
    {
        if (weaponData == null) return false;
        UpdateReloadState();
        return Time.time >= nextFireTime && !isReloading && currentAmmo > 0;
    }

    public void FireFromVehicle(Vector3 position, Vector3 direction)
    {
        if (!CanFire())
        {
            if (currentAmmo <= 0 && !isReloading)
            {
                StartReload();
            }
            return;
        }

        nextFireTime = Time.time + 1f / weaponData.fireRate;
        currentAmmo--;

        if (_unit != null && _unit.UnitAnimator != null && _unit.gameObject.activeInHierarchy)
        {
            _unit.UnitAnimator.TriggerAttack();
        }

        bool isBuffed = _unit != null ? _unit.IsNextAttackBuffed : false;
        float damage = weaponData.projectileData.damage;
        float finalDamage = isBuffed && _unit != null ? _unit.GetBuffedDamage(damage) : damage;

        int ownerLayer = _unit != null ? _unit.gameObject.layer : LayerMask.NameToLayer("Player");
        if (ownerLayer < 0) ownerLayer = 0;

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectileGO = GetPooledProjectile();
            if (projectileGO != null)
            {
                projectileGO.transform.position = position;

                Vector3 finalFireDirection = direction;
                if (weaponData.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-weaponData.spreadAngle / 2, weaponData.spreadAngle / 2);
                    finalFireDirection = Quaternion.AngleAxis(randomAngle, Vector3.up) * finalFireDirection;
                }
                
                projectileGO.transform.rotation = Quaternion.LookRotation(finalFireDirection);
                projectileGO.SetActive(true);

                Projectile projectile = projectileGO.GetComponent<Projectile>();
                if (projectile != null)
                {
                    Transform shooterRoot = _unit != null ? _unit.transform : transform.root;
                    projectile.Init(weaponData.projectileData, finalFireDirection, ownerLayer, finalDamage, null, shooterRoot);
                }
            }
        }
    }

    private void Fire()
    {
        if (!CanFire()) return;

        nextFireTime = Time.time + 1f / weaponData.fireRate;
        currentAmmo--;
        
        _unit.UnitAnimator.TriggerAttack();

        bool isBuffed = _unit.IsNextAttackBuffed;
        float damage = weaponData.projectileData.damage;
        float finalDamage = isBuffed ? _unit.GetBuffedDamage(damage) : damage;

        // 포탑의 현재 forward(회전 지연 포함) 대신, 조준선과 동일한 실제 조준 방향으로 발사
        Vector3 baseFireDirection = GetFireDirection();

        for (int i = 0; i < weaponData.projectilesPerShot; i++)
        {
            GameObject projectileGO = GetPooledProjectile();
            if (projectileGO != null)
            {
                projectileGO.transform.position = firePoint.position;

                Vector3 fireDirection = baseFireDirection;
                if (weaponData.spreadAngle > 0)
                {
                    float randomAngle = Random.Range(-weaponData.spreadAngle / 2, weaponData.spreadAngle / 2);
                    fireDirection = Quaternion.AngleAxis(randomAngle, Vector3.up) * fireDirection;
                }
                
                projectileGO.transform.rotation = Quaternion.LookRotation(fireDirection);
                projectileGO.SetActive(true);

                Projectile projectile = projectileGO.GetComponent<Projectile>();
                if (projectile != null)
                {
                    Transform shooterRoot = _unit != null ? _unit.transform : transform.root;
                    projectile.Init(weaponData.projectileData, fireDirection, _unit.gameObject.layer, finalDamage, null, shooterRoot);
                }
            }
        }
    }

    private GameObject GetPooledProjectile()
    {
        if (projectilePool == null) return null;
        
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
