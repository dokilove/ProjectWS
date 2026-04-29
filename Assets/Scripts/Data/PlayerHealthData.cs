using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerHealthData", menuName = "Data/Player Health Data")]
public class PlayerHealthData : ScriptableObject
{
    [Header("Stats")]
    public float maxHealth = 100f;
}
