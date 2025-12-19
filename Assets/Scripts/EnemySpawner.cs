
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct SpawnableEnemy
{
    public GameObject prefab;
    [Tooltip("이 값이 높을수록 더 자주 스폰됩니다.")]
    public float weight;
}

[System.Serializable]
public struct PhaseSpawnSettings
{
    public TimePhase phase;
    [Tooltip("이 시간대에 한 웨이브에 스폰할 적의 수")]
    public int enemiesPerWave;
    [Tooltip("이 시간대에 다음 웨이브까지의 대기 시간 (초)")]
    public float waveCooldown;
    [Tooltip("이 시간대에 웨이브 내에서 각 적의 스폰 간격")]
    public float spawnIntervalInWave;
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Wave Settings per Phase")]
    [Tooltip("각 시간 단계별 스폰 설정을 정의합니다.")]
    [SerializeField] private PhaseSpawnSettings[] phaseSettings;

    [Header("Enemy Types & Ratios")]
    [Tooltip("이 스포너가 생성할 적의 목록과 각 적의 생성 가중치를 설정합니다.")]
    [SerializeField] private List<SpawnableEnemy> spawnableEnemies;

    [Header("Pooling Settings")]
    [SerializeField] private int initialPoolSize = 10;

    private Dictionary<GameObject, List<GameObject>> enemyPool = new Dictionary<GameObject, List<GameObject>>();
    private float totalSpawnWeight;
    private PhaseSpawnSettings defaultSettings; // Fallback settings

    void Awake()
    {
        InitializePool();
        CalculateTotalWeight();
        // Create a default setting entry as a fallback
        defaultSettings = new PhaseSpawnSettings { enemiesPerWave = 5, waveCooldown = 20f, spawnIntervalInWave = 1f };
    }

    void Start()
    {
        if (dayNightCycle == null)
        {
            Debug.LogError("DayNightCycle reference is not set in the EnemySpawner!", this);
            // Disable the spawner if the dependency is missing
            enabled = false;
            return;
        }
        StartCoroutine(SpawnEnemiesRoutine());
    }

    private void InitializePool()
    {
        foreach (SpawnableEnemy spawnable in spawnableEnemies)
        {
            if (spawnable.prefab == null) continue;
            List<GameObject> pool = new List<GameObject>();
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject enemy = Instantiate(spawnable.prefab);
                enemy.SetActive(false);
                pool.Add(enemy);
            }
            enemyPool[spawnable.prefab] = pool;
        }
    }

    private void CalculateTotalWeight()
    {
        totalSpawnWeight = spawnableEnemies.Sum(e => e.weight);
    }

    private GameObject GetEnemyFromPool(GameObject prefab)
    {
        if (!enemyPool.ContainsKey(prefab)) return null;
        GameObject enemy = enemyPool[prefab].FirstOrDefault(e => !e.activeInHierarchy);
        if (enemy == null)
        {
            enemy = Instantiate(prefab);
            enemyPool[prefab].Add(enemy);
        }
        return enemy;
    }

    private GameObject GetRandomEnemyPrefab()
    {
        if (spawnableEnemies.Count == 0) return null;
        float randomValue = Random.Range(0, totalSpawnWeight);
        foreach (var spawnable in spawnableEnemies)
        {
            if (randomValue <= spawnable.weight) return spawnable.prefab;
            randomValue -= spawnable.weight;
        }
        return spawnableEnemies.Last().prefab;
    }

    private PhaseSpawnSettings GetCurrentPhaseSettings()
    {
        if (dayNightCycle != null)
        {
            TimePhase currentPhase = dayNightCycle.CurrentPhase;
            foreach (var setting in phaseSettings)
            {
                if (setting.phase == currentPhase)
                {
                    return setting;
                }
            }
        }
        Debug.LogWarning($"No spawn settings found for phase {dayNightCycle.CurrentPhase}. Using default fallback settings.");
        return defaultSettings;
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        while (true)
        {
            // Get the settings for the current phase at the beginning of each wave
            PhaseSpawnSettings currentSettings = GetCurrentPhaseSettings();

            for (int i = 0; i < currentSettings.enemiesPerWave; i++)
            {
                while (EnemyManager.Instance != null &&
                       EnemyManager.Instance.GetActiveEnemyCount() >= EnemyManager.Instance.MaxActiveEnemies)
                {
                    yield return new WaitForSeconds(1f);
                }

                GameObject enemyPrefabToSpawn = GetRandomEnemyPrefab();
                if (enemyPrefabToSpawn == null) continue;

                GameObject enemy = GetEnemyFromPool(enemyPrefabToSpawn);
                if (enemy == null) continue;

                enemy.transform.position = transform.position;
                enemy.SetActive(true);

                yield return new WaitForSeconds(currentSettings.spawnIntervalInWave);
            }

            yield return new WaitForSeconds(currentSettings.waveCooldown);
        }
    }
}
