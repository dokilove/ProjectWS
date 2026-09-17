using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Data/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Stats")]
    public float maxHealth = 100f;
    public float moveSpeed = 3.5f;

    [Header("Hit Stun Settings (피격 경직 설정)")]
    [Tooltip("경직 허용 여부 (체크 해제 시 슈퍼아머 상태가 되어 경직에 걸리지 않음)")]
    public bool canBeStunned = true;

    [Tooltip("경직 저항 계수 (1 = 100% 경직 시간 적용, 0.5 = 경직 시간 50% 단축, 0 = 경직 없음)")]
    [Range(0f, 1f)]
    public float stunResistance = 1.0f;
}
