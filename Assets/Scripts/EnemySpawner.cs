
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
    [Header("Totem Data (스폰 토템 데이터)")]
    [Tooltip("스폰할 적 목록, 시간대별 웨이브 설정, 토템 체력 등을 담은 ScriptableObject")]
    [SerializeField] private SpawnTotemData totemData;

    [Header("Dependencies")]
    [SerializeField] private DayNightCycle dayNightCycle;

    // --- State ---
    private float currentHealth;
    private Dictionary<GameObject, List<GameObject>> enemyPool = new Dictionary<GameObject, List<GameObject>>();
    private float totalSpawnWeight;
    private PhaseSpawnSettings defaultSettings; // Fallback settings

    // --- Public Properties ---
    public SpawnTotemData TotemData => totemData;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => totemData != null ? totemData.maxHealth : 300f;
    public bool IsDead => currentHealth <= 0f;
    public EnemySize Size => totemData != null ? totemData.size : EnemySize.L;

    private PhaseSpawnSettings[] ActivePhaseSettings =>
        totemData != null ? totemData.phaseSettings : null;

    private List<SpawnableEnemy> ActiveSpawnableEnemies =>
        totemData != null ? totemData.spawnableEnemies : null;

    private int ActiveInitialPoolSize =>
        totemData != null ? totemData.initialPoolSize : 10;

    void Awake()
    {
        // 공격 판정(근접, 발사체, 차량 박치기)이 감지할 수 있도록 Default 레이어인 경우 자동으로 Enemy 레이어로 설정
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            if (gameObject.layer == 0)
            {
                gameObject.layer = enemyLayer;
            }
            foreach (Collider col in GetComponentsInChildren<Collider>(true))
            {
                if (col.gameObject.layer == 0)
                {
                    col.gameObject.layer = enemyLayer;
                }
            }
        }

        if (totemData == null)
        {
            Debug.LogWarning($"[EnemySpawner] '{gameObject.name}'에 SpawnTotemData가 할당되지 않았습니다!", this);
        }

        currentHealth = MaxHealth;
        InitializePool();
        CalculateTotalWeight();
        // Create a default setting entry as a fallback
        defaultSettings = new PhaseSpawnSettings { enemiesPerWave = 5, waveCooldown = 20f, spawnIntervalInWave = 1f };
    }

    private void OnEnable()
    {
        currentHealth = MaxHealth;
    }

    void Start()
    {
        if (totemData == null)
        {
            enabled = false;
            return;
        }

        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
            if (dayNightCycle == null)
            {
                Debug.LogError("DayNightCycle reference is not set in the EnemySpawner!", this);
                enabled = false;
                return;
            }
        }
        StartCoroutine(SpawnEnemiesRoutine());
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        Debug.Log($"[SpawnTotem] {gameObject.name} took {damage} damage. Current HP: {currentHealth}/{MaxHealth}");

        string hitTag = totemData != null ? totemData.hitEffectPoolTag : "hit01";
        if (!string.IsNullOrEmpty(hitTag) && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(hitTag, transform.position, Quaternion.identity);
        }

        if (DamageIndicatorManager.Instance != null)
        {
            DamageIndicatorManager.Instance.ShowDamage(transform.position, Mathf.RoundToInt(damage));
        }

        if (currentHealth <= 0f)
        {
            DestroyTotem();
        }
    }

    private void DestroyTotem()
    {
        Debug.Log($"[SpawnTotem] {gameObject.name} has been destroyed!");

        string destroyTag = totemData != null && !string.IsNullOrEmpty(totemData.destroyEffectPoolTag)
            ? totemData.destroyEffectPoolTag
            : (totemData != null ? totemData.hitEffectPoolTag : "hit01");

        if (!string.IsNullOrEmpty(destroyTag) && EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.GetPooledObject(destroyTag, transform.position, Quaternion.identity);
        }

        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private void InitializePool()
    {
        var enemies = ActiveSpawnableEnemies;
        if (enemies == null) return;

        int poolCount = ActiveInitialPoolSize;
        foreach (SpawnableEnemy spawnable in enemies)
        {
            if (spawnable.prefab == null) continue;
            if (enemyPool.ContainsKey(spawnable.prefab)) continue;

            List<GameObject> pool = new List<GameObject>();
            for (int i = 0; i < poolCount; i++)
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
        var enemies = ActiveSpawnableEnemies;
        totalSpawnWeight = enemies != null ? enemies.Sum(e => e.weight) : 0f;
    }

    private GameObject GetEnemyFromPool(GameObject prefab)
    {
        if (!enemyPool.ContainsKey(prefab)) return null;
        GameObject enemy = enemyPool[prefab].FirstOrDefault(e => e != null && !e.activeInHierarchy);
        if (enemy == null)
        {
            enemy = Instantiate(prefab);
            enemyPool[prefab].Add(enemy);
        }
        return enemy;
    }

    private GameObject GetRandomEnemyPrefab()
    {
        var enemies = ActiveSpawnableEnemies;
        if (enemies == null || enemies.Count == 0) return null;
        float randomValue = Random.Range(0, totalSpawnWeight);
        foreach (var spawnable in enemies)
        {
            if (randomValue <= spawnable.weight) return spawnable.prefab;
            randomValue -= spawnable.weight;
        }
        return enemies.Last().prefab;
    }

    private PhaseSpawnSettings GetCurrentPhaseSettings()
    {
        var settingsArray = ActivePhaseSettings;
        if (dayNightCycle != null && settingsArray != null)
        {
            TimePhase currentPhase = dayNightCycle.CurrentPhase;
            foreach (var setting in settingsArray)
            {
                if (setting.phase == currentPhase)
                {
                    return setting;
                }
            }
        }
        if (dayNightCycle != null)
        {
            Debug.LogWarning($"No spawn settings found for phase {dayNightCycle.CurrentPhase}. Using default fallback settings.");
        }
        return defaultSettings;
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        while (!IsDead)
        {
            // Get the settings for the current phase at the beginning of each wave
            PhaseSpawnSettings currentSettings = GetCurrentPhaseSettings();

            for (int i = 0; i < currentSettings.enemiesPerWave; i++)
            {
                if (IsDead) yield break;

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
