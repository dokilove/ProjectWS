using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AmmoCounterController : MonoBehaviour
{
    private Label ammoLabel;
    private UnitController playerUnit;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        ammoLabel = root.Q<Label>("ammo-label");

        // 씬에서 플레이어 유닛을 찾습니다. 이 방법은 예제에는 적합하지만,
        // 더 큰 프로젝트에서는 플레이어 데이터에 접근하기 위한 더 견고한 시스템으로 개선될 수 있습니다.
        playerUnit = FindObjectOfType<UnitController>();
    }

    private void Update()
    {
        if (ammoLabel != null && playerUnit != null && playerUnit.IsControlledByPlayer)
        {
            int currentAmmo = playerUnit.CurrentAmmo;
            int maxAmmo = playerUnit.WeaponData.magazineSize;
            ammoLabel.text = $"탄약: {currentAmmo}/{maxAmmo}";
        }
    }
}
