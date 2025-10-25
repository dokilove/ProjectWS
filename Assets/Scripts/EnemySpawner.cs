
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 각 스포너가 생성할 적의 종류와 비율을 설정할 수 있도록 개선된 스포너

[System.Serializable] // Inspector 창에서 보이고 수정할 수 있도록 설정
public struct SpawnableEnemy
{
    public GameObject prefab; // 적 프리팹
    [Tooltip("이 값이 높을수록 더 자주 스폰됩니다.")]
    public float weight;      // 생성 가중치
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Wave Settings")]
    [SerializeField] private int enemiesPerWave = 10; // 한 웨이브에 스폰할 적의 수
    [SerializeField] private float waveCooldown = 15f; // 다음 웨이브까지의 대기 시간 (초)
    [SerializeField] private float spawnIntervalInWave = 0.5f; // 웨이브 내에서 각 적의 스폰 간격

    [Header("Enemy Types & Ratios")]
    [Tooltip("이 스포너가 생성할 적의 목록과 각 적의 생성 가중치를 설정합니다.")]
    [SerializeField] private List<SpawnableEnemy> spawnableEnemies; // 스폰할 적 목록 (프리팹 + 가중치)

    [Header("Pooling Settings")]
    [SerializeField] private int initialPoolSize = 10; // 각 적 종류별 초기 풀 크기

    private Dictionary<GameObject, List<GameObject>> enemyPool = new Dictionary<GameObject, List<GameObject>>();
    private float totalSpawnWeight;

    void Awake()
    {
        InitializePool();
        CalculateTotalWeight();
    }

    void Start()
    {
        StartCoroutine(SpawnEnemiesRoutine());
    }

    private void InitializePool()
    {
        // spawnableEnemies 리스트에 있는 모든 프리팹에 대해 풀을 생성합니다.
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

    // 가중치의 총합을 미리 계산해 둡니다.
    private void CalculateTotalWeight()
    {
        totalSpawnWeight = 0;
        foreach (var spawnable in spawnableEnemies)
        {
            totalSpawnWeight += spawnable.weight;
        }
    }

    private GameObject GetEnemyFromPool(GameObject prefab)
    {
        if (!enemyPool.ContainsKey(prefab)) return null;

        foreach (GameObject enemy in enemyPool[prefab])
        {
            if (!enemy.activeInHierarchy)
            {
                return enemy;
            }
        }
        
        GameObject newEnemy = Instantiate(prefab);
        enemyPool[prefab].Add(newEnemy);
        return newEnemy;
    }

    // 설정된 가중치에 따라 랜덤하게 적 프리팹을 선택하여 반환합니다.
    private GameObject GetRandomEnemyPrefab()
    {
        if (spawnableEnemies.Count == 0)
        {
            Debug.LogError("Spawnable Enemies list is empty!");
            return null;
        }

        Debug.Log($"--- Choosing Random Enemy ---");
        Debug.Log($"Total spawn weight: {totalSpawnWeight}");

        float randomValue = Random.Range(0, totalSpawnWeight);
        Debug.Log($"Initial random value: {randomValue}");

        foreach (var spawnable in spawnableEnemies)
        {
            if (spawnable.prefab == null) continue;
            Debug.Log($"Checking enemy '{spawnable.prefab.name}' with weight {spawnable.weight}. Current random value: {randomValue}");
            if (randomValue <= spawnable.weight)
            {
                Debug.Log($"--> Selected '{spawnable.prefab.name}'!");
                return spawnable.prefab;
            }
            else
            {
                randomValue -= spawnable.weight;
                Debug.Log($"Not selected. New random value: {randomValue}");
            }
        }

        Debug.LogWarning("Weighted random selection fell through. This should not happen. Returning last enemy.");
        return spawnableEnemies.Last().prefab;
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        while (true)
        {
            for (int i = 0; i < enemiesPerWave; i++)
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

                yield return new WaitForSeconds(spawnIntervalInWave);
            }

            yield return new WaitForSeconds(waveCooldown);
        }
    }
}
