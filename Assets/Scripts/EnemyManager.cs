using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [SerializeField]
    private int maxActiveEnemies = 50; // Global limit for all spawners

    private HashSet<GameObject> activeEnemies = new HashSet<GameObject>();

    public int MaxActiveEnemies => maxActiveEnemies;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterEnemy(GameObject enemy)
    {
        activeEnemies.Add(enemy);
    }

    public void DeregisterEnemy(GameObject enemy)
    {
        activeEnemies.Remove(enemy);
    }

    public int GetActiveEnemyCount()
    {
        // 적은 비활성화/파괴 시 항상 OnDisable에서 등록 해제되므로 매번 정리할 필요 없음
        // (HUD 등에서 매 프레임 호출되므로 람다/순회 비용 제거)
        return activeEnemies.Count;
    }
}
