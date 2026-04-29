using UnityEngine;

[CreateAssetMenu(fileName = "NewVehicleHealthData", menuName = "Data/Vehicle Health Data")]
public class VehicleHealthData : ScriptableObject
{
    [Header("Stats")]
    public float maxHealth = 200f; // Vehicles might have more health
}
