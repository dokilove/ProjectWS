using UnityEngine;

public class JustDodgeTrigger : MonoBehaviour
{
    private Unit _playerUnit;

    private void Awake()
    {
        // 부모 오브젝트에서 Unit 컴포넌트를 찾습니다.
        _playerUnit = GetComponentInParent<Unit>();
        if (_playerUnit == null)
        {
            Debug.LogError("JustDodgeTrigger: Unit component not found in parent!", this);
            enabled = false; // Unit이 없으면 스크립트 비활성화
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어가 저스트 회피 판정 시간 내에 있는지 확인합니다.
        if (_playerUnit != null && _playerUnit.IsJustDodgeWindowActive)
        {
            Debug.Log("Just Dodge (Graze) Successful!");
            _playerUnit.ActivateJustDodgeEffects(); // 불릿 타임 및 버프 발동

            // 발사체 처리 (파괴 또는 튕겨나가기)
            Projectile projectile = other.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.OnGraze();
            }
            else
            {
                // Projectile 컴포넌트가 없으면 그냥 파괴 (오브젝트 풀링 사용 시 SetActive(false))
                other.gameObject.SetActive(false); 
            }
        }
    }
}
