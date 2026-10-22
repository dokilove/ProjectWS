using UnityEngine;

[CreateAssetMenu(fileName = "NewVehicleHealthData", menuName = "Data/Vehicle Health Data")]
public class VehicleHealthData : ScriptableObject
{
    [Header("차량 내구도 설정 (Vehicle Stats)")]
    [Tooltip("차량의 최대 내구도/체력")]
    public float maxHealth = 200f;
}
