using UnityEngine;

public interface IVehicle
{
    // PlayerPawnManager가 차량을 제어하기 위해 필요한 최소한의 기능들을 정의합니다.
    
    // 모든 MonoBehaviour는 transform과 gameObject를 가지고 있습니다.
    Transform transform { get; }
    GameObject gameObject { get; }

    /// <summary>
    /// 플레이어로부터 입력을 받아 조종이 가능한 상태로 만듭니다.
    /// </summary>
    void EnableControl();

    /// <summary>
    /// 플레이어 입력을 비활성화하고 AI나 기본 상태로 전환합니다.
    /// </summary>
    void DisableControl();

    /// <summary>
    /// 현재 이 차량이 플레이어에 의해 직접 제어되고 있는지 여부를 반환합니다.
    /// </summary>
    bool IsControlledByPlayer { get; }
}