using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSpawnTotemData", menuName = "Data/Spawn Totem Data")]
public class SpawnTotemData : ScriptableObject
{
    [Header("Totem Stats (스폰 토템 체력 및 판정)")]
    [Tooltip("스폰 토템의 최대 체력 (0 이하가 되면 파괴되어 스폰 중단)")]
    public float maxHealth = 300f;

    [Tooltip("차량 박치기 등 충돌 판정 시 적용될 크기 분류 (기본값 L: 대형 판정으로 넉백되지 않고 차량에 큰 반동/감속 부여)")]
    public EnemySize size = EnemySize.L;

    [Tooltip("피격 시 재생할 이펙트 풀 태그 (EffectPoolManager)")]
    public string hitEffectPoolTag = "hit01";

    [Tooltip("파괴 시 재생할 이펙트 풀 태그 (비어있으면 hitEffectPoolTag 사용)")]
    public string destroyEffectPoolTag = "hit01";

    [Header("Wave Settings per Phase (시간대별 웨이브 설정)")]
    [Tooltip("각 시간 단계(Morning, Noon, Evening, Night, DeepNight)별 스폰 설정을 정의합니다.")]
    public PhaseSpawnSettings[] phaseSettings;

    [Header("Enemy Types & Ratios (스폰할 적 종류 및 가중치)")]
    [Tooltip("이 스폰 토템이 생성할 적 프리팹 목록과 각 적의 스폰 가중치를 설정합니다.")]
    public List<SpawnableEnemy> spawnableEnemies = new List<SpawnableEnemy>();

    [Header("Pooling Settings (오브젝트 풀 설정)")]
    [Tooltip("적 종류별 초기 풀 생성 개수")]
    public int initialPoolSize = 10;
}
