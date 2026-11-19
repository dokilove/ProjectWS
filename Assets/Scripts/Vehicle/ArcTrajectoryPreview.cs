using UnityEngine;

/// <summary>
/// 차량 무기(VehicleWeaponSystem)가 곡사 발사체를 쏠 때, 현재 조준 대상에 대한 예상 포물선과 착탄 지점을 표시합니다.
/// 실제 발사체(Projectile)와 같은 BallisticUtility 계산을 사용하므로 미리보기와 실제 비행 경로가 일치합니다.
/// ProjectileData.showTrajectoryPreview가 켜져 있으면 VehicleWeaponSystem이 자동으로 추가합니다.
/// </summary>
[DefaultExecutionOrder(100)] // VehicleWeaponSystem.Update(자동 타겟 갱신) 이후 LateUpdate에서 그리기
public class ArcTrajectoryPreview : MonoBehaviour
{
    [Header("Arc Line")]
    [Tooltip("포물선을 나누는 구간 수 (많을수록 부드럽지만 레이캐스트가 늘어남)")]
    [Range(8, 64)]
    [SerializeField] private int segments = 30;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Color lineStartColor = new Color(1f, 0.85f, 0.4f, 0.15f);
    [SerializeField] private Color lineEndColor = new Color(1f, 0.6f, 0.2f, 0.8f);

    [Header("Landing Marker")]
    [Tooltip("착탄 지점 원 표시 여부 (폭발형이면 폭발 반경, 아니면 markerRadius 크기)")]
    [SerializeField] private bool showLandingMarker = true;
    [SerializeField] private float markerRadius = 0.6f;
    [SerializeField] private float markerWidth = 0.06f;
    [SerializeField] private Color markerColor = new Color(1f, 0.4f, 0.1f, 0.85f);
    [Range(12, 64)]
    [SerializeField] private int markerSegments = 32;

    [Header("Collision")]
    [Tooltip("궤적이 막히는지 검사할 레이어 (0이면 Ground | Obstacle | Default = Projectile 기본값과 동일)")]
    [SerializeField] private LayerMask collisionMask;

    private VehicleWeaponSystem weaponSystem;
    private Transform shooterRoot;
    private LineRenderer arcLine;
    private LineRenderer landingMarker;
    private Vector3[] arcPoints;
    private Vector3[] markerPoints;

    public void Init(VehicleWeaponSystem weapon, Transform shooter)
    {
        weaponSystem = weapon;
        shooterRoot = shooter;
    }

    private void Awake()
    {
        if (weaponSystem == null)
        {
            weaponSystem = GetComponent<VehicleWeaponSystem>();
        }

        if (collisionMask.value == 0)
        {
            int groundAndObstacle = LayerMask.GetMask("Ground", "Obstacle");
            if (groundAndObstacle == 0) groundAndObstacle = (1 << 12) | (1 << 13);
            collisionMask = groundAndObstacle | (1 << 0);
        }

        arcPoints = new Vector3[segments + 1];
        markerPoints = new Vector3[markerSegments];

        arcLine = CreateLine("TrajectoryPreview", lineWidth, false);
        arcLine.startColor = lineStartColor;
        arcLine.endColor = lineEndColor;

        landingMarker = CreateLine("LandingMarker", markerWidth, true);
        landingMarker.startColor = markerColor;
        landingMarker.endColor = markerColor;

        SetVisible(false);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private LineRenderer CreateLine(string objectName, float width, bool loop)
    {
        // 무기 오브젝트에 이미 MeshRenderer가 있을 수 있으므로 별도 자식 오브젝트에 LineRenderer 생성
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = loop ? 0 : 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        line.positionCount = 0;

        Material material = RuntimeMaterials.VertexColorUnlit;
        if (material != null) line.sharedMaterial = material;
        return line;
    }

    private void SetVisible(bool visible)
    {
        if (arcLine != null) arcLine.enabled = visible;
        if (landingMarker != null) landingMarker.enabled = visible && showLandingMarker;
    }

    private void LateUpdate()
    {
        ProjectileData data = GetArcingProjectileData();
        Transform target = weaponSystem != null ? weaponSystem.CurrentAutoTarget : null;

        if (data == null || target == null || !target.gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        // VehicleWeaponSystem.Fire와 동일한 발사 위치/방향 계산
        Transform firePoint = weaponSystem.FirePoint;
        Vector3 fireDirection = target.position - firePoint.position;
        fireDirection.y = 0f;
        fireDirection = fireDirection.sqrMagnitude > 0.001f ? fireDirection.normalized : firePoint.forward;
        Vector3 start = firePoint.position + fireDirection * 0.1f;

        Vector3 destination = BallisticUtility.GetDestination(start, fireDirection, data, target.position, fireDirection);
        BallisticUtility.Solution solution = BallisticUtility.Solve(start, destination, data, fireDirection);

        DrawArc(solution, data);
        SetVisible(true);
    }

    private ProjectileData GetArcingProjectileData()
    {
        if (weaponSystem == null) return null;
        WeaponData weaponData = weaponSystem.WeaponData;
        if (weaponData == null || weaponData.projectileData == null) return null;

        ProjectileData data = weaponData.projectileData;
        return (data.isArcing && data.showTrajectoryPreview) ? data : null;
    }

    private void DrawArc(BallisticUtility.Solution solution, ProjectileData data)
    {
        int count = 1;
        arcPoints[0] = solution.start;
        Vector3 impactPoint = solution.destination;

        // 구간마다 레이캐스트로 장애물/지면에 먼저 막히는지 검사 (Projectile도 비행 중 환경 충돌 시 폭발)
        for (int i = 1; i <= segments; i++)
        {
            float t = solution.flightTime * i / segments;
            Vector3 point = solution.Evaluate(t);
            Vector3 previous = arcPoints[count - 1];
            Vector3 delta = point - previous;
            float distance = delta.magnitude;

            if (distance > 0.0001f &&
                Physics.Raycast(previous, delta / distance, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore) &&
                !IsShooter(hit.collider))
            {
                arcPoints[count++] = hit.point;
                impactPoint = hit.point;
                break;
            }

            arcPoints[count++] = point;
            impactPoint = point;
        }

        arcLine.positionCount = count;
        arcLine.SetPositions(arcPoints);

        if (showLandingMarker)
        {
            float radius = data.isExplosive && data.explosionRadius > 0f ? data.explosionRadius : markerRadius;
            DrawMarker(impactPoint + Vector3.up * 0.05f, radius);
        }
    }

    private void DrawMarker(Vector3 center, float radius)
    {
        float step = Mathf.PI * 2f / markerSegments;
        for (int i = 0; i < markerSegments; i++)
        {
            float angle = step * i;
            markerPoints[i] = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }
        landingMarker.positionCount = markerSegments;
        landingMarker.SetPositions(markerPoints);
    }

    private bool IsShooter(Collider col)
    {
        if (shooterRoot == null) return false;
        return col.transform == shooterRoot || col.transform.IsChildOf(shooterRoot);
    }
}
