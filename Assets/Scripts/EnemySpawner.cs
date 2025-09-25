
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Wave Settings")]
    [SerializeField] private int enemiesPerWave = 10; // 한 웨이브에 스폰할 적의 수
    [SerializeField] private float waveCooldown = 15f; // 다음 웨이브까지의 대기 시간 (초)
    [SerializeField] private float spawnIntervalInWave = 0.5f; // 웨이브 내에서 각 적의 스폰 간격

    [Header("General Settings")]
    [SerializeField] private List<GameObject> enemyPrefabs; // 스폰할 적 프리팹 목록
    [SerializeField] private int initialPoolSize = 20; // 각 적 종류별 초기 풀 크기

    private Dictionary<GameObject, List<GameObject>> enemyPool = new Dictionary<GameObject, List<GameObject>>();

    void Awake()
    {
        InitializePool();
    }

    void Start()
    {
        StartCoroutine(SpawnEnemiesRoutine());
    }

    private void InitializePool()
    {
        foreach (GameObject prefab in enemyPrefabs)
        {
            List<GameObject> pool = new List<GameObject>();
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject enemy = Instantiate(prefab);
                enemy.SetActive(false);
                pool.Add(enemy);
            }
            enemyPool[prefab] = pool;
        }
    }

    private GameObject GetEnemyFromPool(GameObject prefab)
    {
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

    private IEnumerator SpawnEnemiesRoutine()
    {
        while (true)
        {
            // 한 웨이브 생성
            for (int i = 0; i < enemiesPerWave; i++)
            {
                if (enemyPrefabs.Count == 0) continue;
                GameObject enemyPrefabToSpawn = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
                GameObject enemy = GetEnemyFromPool(enemyPrefabToSpawn);

                enemy.transform.position = transform.position;
                enemy.SetActive(true);

                // 웨이브 내의 짧은 스폰 간격
                yield return new WaitForSeconds(spawnIntervalInWave);
            }

            // 웨이브 간의 긴 쿨다운
            yield return new WaitForSeconds(waveCooldown);
        }
    }
}
