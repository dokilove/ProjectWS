// Assets/Scripts/EnemyHealth.cs
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    private float currentHealth;

    private void OnEnable()
    {
        if (enemyData != null)
        {
            currentHealth = enemyData.maxHealth;
        }
        
        // Register with the manager when the enemy becomes active
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.RegisterEnemy(gameObject);
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Current health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} has died.");
        // 여기에 죽음 애니메이션, 아이템 드랍 등의 로직 추가
        gameObject.SetActive(false); // 이 코드가 OnDisable을 호출하여 EnemyManager에서 등록 해제합니다.
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 (풀로 돌아가거나, 죽거나) EnemyManager에서 등록을 해제합니다.
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.DeregisterEnemy(gameObject);
        }
    }
}
