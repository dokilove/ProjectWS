using UnityEngine;

public enum EnemySize
{
    S, // 소형: 차량에 깔려 즉사 (차량 감속/반동 없음)
    M, // 중형: 기본 충돌/넉백/데미지 (기본 감속/반동)
    L  // 대형: 넉백 저항 (차량에 큰 감속 및 큰 반동 피해)
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Data/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Classification (분류)")]
    [Tooltip("적 크기 분류 (S: 소형 - 차량에 깔려 즉사, M: 중형 - 일반 박치기, L: 대형 - 넉백 저항 및 큰 반동)")]
    public EnemySize size = EnemySize.M;

    [Header("Stats (기본 스펙)")]
    [Tooltip("적의 최대 체력")]
    public float maxHealth = 100f;

    [Tooltip("적의 이동 속도 (NavMeshAgent 이동 속도)")]
    public float moveSpeed = 3.5f;

    [Header("Hit Stun Settings (피격 경직 설정)")]
    [Tooltip("경직 허용 여부 (체크 해제 시 슈퍼아머 상태가 되어 경직에 걸리지 않음)")]
    public bool canBeStunned = true;

    [Tooltip("경직 저항 계수 (1 = 100% 경직 시간 적용, 0.5 = 경직 시간 50% 단축, 0 = 경직 없음)")]
    [Range(0f, 1f)]
    public float stunResistance = 1.0f;
}
