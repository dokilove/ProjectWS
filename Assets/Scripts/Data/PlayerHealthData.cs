using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerHealthData", menuName = "Data/Player Health Data")]
public class PlayerHealthData : ScriptableObject
{
    [Header("플레이어 체력 설정 (Health Stats)")]
    [Tooltip("플레이어의 최대 체력")]
    public float maxHealth = 100f;
}
