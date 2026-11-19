using UnityEngine;

/// <summary>
/// 곡사 발사체의 탄도 계산 공용 유틸리티입니다.
/// Projectile(실제 비행)과 ArcTrajectoryPreview(예상 궤적)가 같은 계산을 쓰도록 분리했습니다.
/// </summary>
public static class BallisticUtility
{
    public const float MinFlightTime = 0.25f;

    public struct Solution
    {
        public Vector3 start;
        public Vector3 destination;
        public Vector3 horizontalVelocity;
        public float verticalVelocity;
        public float gravity;
        public float flightTime;
        public Vector3 moveDirection; // 수평 진행 방향

        /// <summary>발사 후 t초 시점의 위치 (등가속도 해석해)</summary>
        public Vector3 Evaluate(float t)
        {
            return start + horizontalVelocity * t + Vector3.up * (verticalVelocity * t + 0.5f * gravity * t * t);
        }
    }

    public static float GetGravity(ProjectileData data)
    {
        float multiplier = (data != null && data.gravityMultiplier > 0f) ? data.gravityMultiplier : 1f;
        return Physics.gravity.y * multiplier;
    }

    /// <summary>
    /// 목표 지점을 결정합니다. 목표가 없거나 너무 가까우면(허공 사격) 사거리 지점을 가상 목표로 사용합니다.
    /// </summary>
    public static Vector3 GetDestination(Vector3 start, Vector3 direction, ProjectileData data, Vector3? targetPosition, Vector3 fallbackForward)
    {
        if (targetPosition.HasValue && (targetPosition.Value - start).sqrMagnitude > 0.25f)
        {
            return targetPosition.Value;
        }

        Vector3 defaultDir = new Vector3(direction.x, 0f, direction.z).normalized;
        if (defaultDir.sqrMagnitude < 0.001f)
        {
            defaultDir = fallbackForward;
            defaultDir.y = 0f;
            defaultDir.Normalize();
        }

        float speed = data != null ? data.speed : 10f;
        float defaultDist = Mathf.Max(6f, speed * 0.7f);
        return start + defaultDir * defaultDist;
    }

    /// <summary>
    /// 시작점에서 목표점에 도달하는 탄도(수평 속도, 초기 수직 속도, 비행 시간)를 역계산합니다.
    /// 비행 시간 T = 수평 거리 / 속도 (최소 MinFlightTime)
    /// </summary>
    public static Solution Solve(Vector3 start, Vector3 destination, ProjectileData data, Vector3 fallbackForward)
    {
        Vector3 displacement = destination - start;
        Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
        float horizontalDistance = horizontalDisplacement.magnitude;
        float heightDifference = displacement.y;

        float speed = Mathf.Max(1f, data != null ? data.speed : 10f);
        float flightTime = Mathf.Max(MinFlightTime, horizontalDistance / speed);
        float gravity = GetGravity(data);

        Solution solution;
        solution.start = start;
        solution.destination = destination;
        solution.gravity = gravity;
        solution.flightTime = flightTime;

        // 1. 수평 속도: V_xz = D_xz / T
        solution.horizontalVelocity = horizontalDistance > 0.001f ? (horizontalDisplacement / flightTime) : (fallbackForward * speed);
        solution.moveDirection = horizontalDistance > 0.001f ? horizontalDisplacement.normalized : fallbackForward;

        // 2. 수직 초기 속도: V_y = (H - 0.5 * g * T^2) / T
        solution.verticalVelocity = (heightDifference - 0.5f * gravity * (flightTime * flightTime)) / flightTime;
        return solution;
    }
}
