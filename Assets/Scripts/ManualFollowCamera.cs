using UnityEngine;
using System.Collections; // Add this line

public class ManualFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target; // 플레이어 Transform
    [SerializeField] private float defaultDamping = 10f; // 평상시 카메라 추적 부드러움 (빠르게 반응)
    [SerializeField] private float evadeDamping = 1f; // 회피 시 카메라 추적 부드러움 (느리게 반응)
    [SerializeField] private float evadeDampingDuration = 0.5f; // 회피 댐핑 지속 시간
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f); // 플레이어로부터의 상대적 위치

    private float currentDamping; // 현재 적용되는 댐핑 값
    private Coroutine evadeDampingCoroutine;

    void Start()
    {
        currentDamping = defaultDamping;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("Camera target is not assigned.");
            return;
        }

        // 원하는 카메라 위치 계산
        Vector3 desiredPosition = target.position + offset;

        // 현재 카메라 위치에서 원하는 위치로 부드럽게 이동
        transform.position = Vector3.Lerp(transform.position, desiredPosition, currentDamping * Time.deltaTime);

        // 플레이어를 바라보도록 회전 (선택 사항)
        // transform.LookAt(target.position);
    }

    public void TriggerEvadeDamping()
    {
        if (evadeDampingCoroutine != null)
        {
            StopCoroutine(evadeDampingCoroutine);
        }
        evadeDampingCoroutine = StartCoroutine(EvadeDampingTransition());
    }

    private IEnumerator EvadeDampingTransition()
    {
        float timer = 0f;
        float startDamp = evadeDamping; // 회피 시작 시 댐핑 값 (느리게 반응)
        float endDamp = defaultDamping; // 회피 종료 시 댐핑 값 (빠르게 반응)

        while (timer < evadeDampingDuration)
        {
            currentDamping = Mathf.Lerp(startDamp, endDamp, timer / evadeDampingDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        currentDamping = endDamp; // 정확히 기본 댐핑 값으로 설정
        evadeDampingCoroutine = null;
    }
}
