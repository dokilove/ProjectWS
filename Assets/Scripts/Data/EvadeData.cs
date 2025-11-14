using UnityEngine;

[CreateAssetMenu(fileName = "NewEvadeData", menuName = "Data/Evade Data")]
public class EvadeData : ScriptableObject
{
    [Header("Evade")]
    public float evadeForce = 10f;
    public float dodgeDuration = 0.5f;
    public int dodgingPlayerLayer;
    public float pushRadius = 2f;
    public float pushForce = 0.05f;
    public float pushSmoothTime = 0.2f;

    [Header("Charges")]
    public int maxEvadeCharges = 3;
    public float evadeChargeRegenTime = 2.0f; // Time to regenerate one charge

    [Header("Visuals")]
    public float trailOffset = 0.1f; // How much longer the trail should last than dodgeDuration
}
