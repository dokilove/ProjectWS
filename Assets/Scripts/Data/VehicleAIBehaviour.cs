using UnityEngine;

[CreateAssetMenu(fileName = "New VehicleAI", menuName = "Data/Vehicle AI Behaviour")]
public class VehicleAIBehaviour : ScriptableObject
{
    public float followSpeed = 3f;
    public float followStopDistance = 5f;
}
