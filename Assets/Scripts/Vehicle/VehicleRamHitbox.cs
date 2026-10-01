using UnityEngine;

public enum BumperType
{
    Front,
    Rear
}

/// <summary>
/// 차량의 전/후방 범퍼에 부착되어 적과의 충돌을 감지하는 트리거 히트박스 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class VehicleRamHitbox : MonoBehaviour
{
    [SerializeField] private BumperType bumperType;
    private VehicleRamSystem _ramSystem;

    public BumperType Type => bumperType;

    public void Init(VehicleRamSystem ramSystem, BumperType type)
    {
        _ramSystem = ramSystem;
        bumperType = type;

        // Ensure collider is marked as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_ramSystem != null)
        {
            _ramSystem.OnBumperTrigger(bumperType, other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (_ramSystem != null)
        {
            _ramSystem.OnBumperTrigger(bumperType, other);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = bumperType == BumperType.Front
            ? new Color(0.1f, 0.7f, 1f, 0.35f)
            : new Color(1f, 0.35f, 0.1f, 0.35f);

        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = bumperType == BumperType.Front ? Color.cyan : Color.red;
        Gizmos.DrawWireCube(box.center, box.size);
    }
#endif
}
