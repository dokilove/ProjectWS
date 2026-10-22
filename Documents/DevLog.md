# ProjectWS 개발 일지 (2026-09-17)

## 1. 근접 공격으로 적 발사체 파괴 기능 구현
- **개요**: 플레이어가 근접 공격(콤보 및 차지 공격)을 휘두를 때 범위 내 적의 발사체(투사체)를 베어 삭제하고 피격 이펙트를 연출할 수 있도록 구현.
- **수정 파일**:
  - `Assets/Scripts/Projectile.cs`:
    - `DestroyByMelee(string effectTag)` 메서드 구현.
    - EffectPoolManager를 통해 피격 이펙트(`hit02`)를 생성하고 오브젝트 풀로 안전하게 반환/비활성화 처리.
  - `Assets/Scripts/Unit/UnitMeleeSystem.cs`:
    - `canDestroyProjectiles` (기본값: `true`), `projectileLayerMask` (미할당 시 `EnemyAttackLayer`(15) 자동 설정), `projectileDestroyEffectTag` ("hit02") 필드 추가.
    - `PerformMeleeAttack`의 충돌 판정 마스크에 `projectileLayerMask` 포함 및 `QueryTriggerInteraction.Collide` 옵션 적용.
    - `ProcessHitCollider` 헬퍼 메서드를 통해 적 피격 데미지(`EnemyHealth`)와 발사체 파괴(`Projectile`)를 분기 처리.
  - `Assets/Prefabs/Player/Player.prefab` & `Player_SDTest.prefab`:
    - `canDestroyProjectiles: 1`, `projectileLayerMask: 32768` (Layer 15) 설정 반영.

---

## 2. 플레이어/차량 발사체와 적 발사체 상호 파괴
- **개요**: 플레이어 또는 차량이 쏜 발사체와 적이 쏜 발사체가 공중에서 맞부딪히면 서로 충돌하여 동시 소멸하도록 구현.
- **수정 파일**:
  - `Assets/Scripts/Projectile.cs`:
    - `IsPlayerSide()`: 발사 주체가 플레이어(10) 또는 차량(11) 레이어인지 판별.
    - `IsOpposingProjectile(Projectile other)`: 아군 발사체와 적 발사체 간 충돌 관계인지 검증.
    - `CollideWithProjectile(Projectile other)`: 상호 충돌 시 맞부딪힌 지점에 충돌 이펙트를 재생하고 양쪽 발사체를 모두 안전하게 풀에 반환.
    - `OnTriggerEnter` 및 고속 투사체 터널링 방지용 `SphereCast` 루프에 발사체 간 충돌 처리 로직 통합.

---

## 3. 근접 공격 조준 및 방향 결정 우선순위 개편
- **공격 방향 결정 우선순위 체계 확립**:
  1. **1순위 (플레이어 조작 최우선)**: 이동 입력(WASD/스틱)이 있으면 카메라 기준 이동 방향으로 조준 및 공격.
  2. **2순위 (조건부 자동 조준)**: 이동 입력이 없고 자동 조준 조건(Combo 2+ 또는 차지)을 만족할 때 반경(6m) 내 가장 가까운 적 방향.
  3. **3순위 (전방 유지)**: 이동 입력도 없고 적도 없거나 첫 타(Combo 1) 대기 상태일 때는 캐릭터의 현재 전방(`transform.forward`) 유지.

---

## 4. Combo 1(1타) 자동 추적 제외 및 Combo 2+/차지 공격 자동 추적 구현
- **요구사항**:
  - 콤보 첫 타(Combo 1)에서는 적을 향해 캐릭터가 멋대로 돌아가지 않도록 자동 추적을 끄고, 2타 이상 연계 및 차지 공격에서만 주변 적을 향해 자동 회전 및 돌진하도록 구현.
- **수정 파일**:
  - `Assets/Scripts/Unit/UnitMeleeSystem.cs`:
    - `ComboCounter`, `IsMeleeChargePrimed` 프로퍼티 노출.
    - `GetCurrentAttackDirection(bool allowAutoAim = true)`:
      - `allowAutoAim == false`일 경우 적 방향을 무시하고 [1순위: 이동 입력 ➔ 2순위: 캐릭터 전방]만 반환.
    - `Update()`:
      - 평상시(`comboCounter == 0 && !isMeleeChargePrimed`)에는 적 탐색을 끄고 `AutoAimTargetPosition = Vector3.zero` 유지.
      - 2타 이상 연계 대기 중이거나 차지 중일 때만 `PerformAutoAim()` 실행.
    - `ComboStepCoroutine(int stepIndex)`:
      - `bool allowAutoAim = (stepIndex > 0);`
      - 1타(`stepIndex == 0`) 실행 시 `allowAutoAim = false` 적용.
      - 2타 이상(`stepIndex > 0`) 실행 시 `allowAutoAim = true` 적용 및 매 타격/서브히트마다 적 방향 갱신.
    - `HandleMeleeChargeInput()` & `ChargeAttackCoroutine()`:
      - 차지 공격은 자동 추적 대상(`allowAutoAim: true`)으로 유지.
    - `CancelCurrentAttack()`:
      - 회피(Evade) 또는 피격으로 공격 취소 시 `comboCounter = 0`, `AutoAimTargetPosition = Vector3.zero`로 초기화하여 항상 다음 첫 타가 수동 조작으로 시작되도록 보장.
  - `Assets/Scripts/Unit/UnitInput.cs`:
    - `HandleAiming()`의 근접 모드 조준 로직 수정:
      - 이동 입력이 있으면 즉시 이동 방향으로 조준.
      - 이동 입력이 없고 `ComboCounter > 0 || IsMeleeChargePrimed`일 때만 적 방향으로 자동 조준 및 `SetIsAutoAiming(true)`.
      - 평상시/Combo 1 대기 상태에서는 전방 방향 유지.

---

## 6. 피격 경직(Hit Stun) 시스템 구현
- **개요**: 근접 공격(일반 콤보/다단히트/차지 공격) 및 발사체 공격 적중 시 피격 대상(Enemy)이 일시적으로 멈추는 경직(Hit Stun / Flinch) 효과 구현 및 공격자/피격자 양방향 설정 체계 구축.
- **주요 구현 내용**:
  1. **피격자 동작 정지 (EnemyAI)**:
     - `stunTimer`, `IsStunned`, `ApplyStun(duration)`, `ResetStun()` 구현.
     - 경직 중 `NavMeshAgent` 즉시 정지 (`agent.isStopped = true`, `velocity = Vector3.zero`).
     - 경직 중 이동, 플레이어 추적 조준, 원거리 사격 모두 차단.
     - 애니메이터가 존재할 경우 `animator.speed = 0f`로 일시정지하여 히트스톱(Hit-stop) 연출, 경직 종료 시 복구.
     - 연타/다단히트 피격 시 `stunTimer = Mathf.Max(stunTimer, duration)`으로 타이머를 갱신하여 콤보 중 적이 빠져나가지 않도록 보장.
     - 경직 종료 직후 초근접 기습 사격을 방지하기 위해 사격 쿨타임 안전 여유 딜레이(0.2초) 적용.
     - 오브젝트 풀 반환 시 깨끗하게 상태를 리셋(`OnDisable`).
  2. **피격 연동 (EnemyHealth)**:
     - `TakeDamage(float damage, float hitStunDuration = 0f)`로 확장하여 체력이 남아있을 때 `EnemyAI.ApplyStun`을 호출하도록 연동.
  3. **발사체 피격 연동 (Projectile)**:
     - `ProjectileData`의 `hitStunDuration`을 피격 대상 `EnemyHealth`로 전달.
  4. **근접 공격 연동 (UnitMeleeSystem)**:
     - 단발 공격, 다단히트 개별 서브히트, 차지 공격 실행 시 각 단계에 설정된 `hitStunDuration`을 `PerformMeleeAttack` 및 `ProcessHitCollider`를 통해 전달.
  5. **데이터 설정 분리 및 인스펙터 지원**:
     - **공격자 데이터 (공격별 경직 시간)**:
       - `ProjectileData.cs`: `hitStunDuration` (초 단위, 기본 0.1초)
       - `MeleeData.cs`: `MeleeComboStep`(기본 0.2초), `MeleeSubHit`(기본 0.15초), `MeleeChargeAttackData`(기본 0.6초)
       - `MeleeDataEditor.cs`: 커스텀 에디터에 '피격 경직 시간 (Hit Stun)' GUI 필드 완벽 연동.
     - **피격자 데이터 (몬스터별 저항/슈퍼아머)**:
       - `EnemyData.cs`: `canBeStunned` (슈퍼아머 여부), `stunResistance` (경직 저항 계수 0~1)
     - **에셋 기본값 세팅**: `PlayerMeleeData.asset`, `PlayerProjectile.asset`, `PlayerProjectile_ShotGun.asset`, `DefaultEnemyData.asset`, `BigEnemyData.asset`에 적정 기본값 적용.

---

## 7. 불변 제약 사항 준수
- `InputSystem_Actions.inputactions` 및 `InputSystem_Actions.cs` 파일은 전혀 수정하지 않음.

---

## 8. 범위 확산형 곡사 발사체(Arcing Explosive Projectile) 시스템 구현 (2026-10-22)
- **개요**: 인스펙터 설정만으로 직사/곡사 및 단일 타격/범위 폭발(스플래시)을 자유롭게 조합할 수 있도록 발사체 데이터와 비행/충돌 로직을 확장.
- **수정 파일**:
  - `Assets/Scripts/Data/ProjectileData.cs`:
    - `isArcing` (곡사 적용 여부, 기본값 `false`), `launchAngle` (상향 발사 각도, 기본 30도), `gravityMultiplier` (중력 가속도 배수, 기본 2.5) 추가.
    - `isExplosive` (범위 폭발 여부, 기본값 `false`), `explosionRadius` (폭발 피해 반경, 기본 3.5m), `explosionKnockback` (폭발 중심 넉백 거리, 기본 2.0m), `explosionEffectPrefab` (폭발 전용 이펙트) 추가.
  - `Assets/Scripts/Projectile.cs`:
    - **곡사 물리 궤적**: `isArcing` 활성화 시 수평 방향 속도와 중력 가속도가 적용된 수직 속도(`verticalVelocity`)를 실시간 계산하여 자연스러운 포물선 비행 및 탄두 낙하 방향 자동 회전 정렬(`Quaternion.LookRotation`).
    - **[충돌박스 최적화] 공중 간섭 방지 및 지면 착탄 감지**:
      - `isArcing` 활성화 시 발사체의 자체 `Collider`를 비활성화(`_collider.enabled = false`)하여 비행 중 앞쪽 적이나 장애물의 상단 콜라이더에 걸려 조기 공중 폭발하는 현상을 원천 방지 및 PhysX 오버헤드 제거.
      - 비행 중 공중 충돌 검사는 건너뛰고, 지면(`Ground`) 및 장애물(`Obstacle`) 레이캐스트(진행 방향 및 직하방)로만 정확하게 착탄을 감지하여 바닥에 닿는 순간 `Explode()`가 호출되도록 개선.
    - **범위 폭발 및 방사형 넉백 (`Explode`)**: `Physics.OverlapSphere`를 통해 반경 내 적/유닛/차량/스포너를 일괄 판정하고, 폭발 중심점 기준 방사형 넉백(`EnemyAI.ApplyKnockback`) 및 피격 경직(`hitStunDuration`) 동시 적용. 복수 콜라이더 단일 대상 중복 타격 방지(`HashSet`).
    - **다형성/확장성 지원**: 주요 수명 주기 메서드(`Init`, `Update`, `Move`, `OnTriggerEnter`, `HandleImpact`, `Explode`, `ApplyDamageToTarget` 등)를 `protected virtual`로 개방하여 향후 '마탄(Chaining/Ricochet)' 등의 특수 탄환 추가 시 깔끔하게 상속 확장 가능하도록 개선.

---

## 9. 차량 다중 자동 무기(직사/곡사) 및 '정차 중 발사' 시스템 구현 (2026-10-22)
- **개요**: 차량에 장착된 복수의 무기(직사 기관총, 곡사 박격포 등)가 플레이어 탑승 상태에서도 동작하되, 주행 중에는 사격을 멈추고 **'정차 중일 때만'** 각각 독립적으로 주변 적을 자동 탐색하여 사격하도록 시스템 개편.
- **수정 파일**:
  - `Assets/Scripts/Vehicle/VehicleMove.cs`:
    - `IsMoving` 프로퍼티 추가 (현재 속도 및 이동 입력 크기를 종합 판단하여 정차 여부 제공).
  - `Assets/Scripts/Vehicle/Vehicle.cs`:
    - `AllWeaponSystems` (자식에 붙은 모든 `VehicleWeaponSystem`을 리스트로 보관 및 일괄 초기화).
    - `IsMoving` 편의 프로퍼티 노출.
    - `UpdatePlayerTurretVisibility()`: 탑승/하차와 무관하게 차량에 장착된 모든 포탑 메쉬가 항상 유지되도록 수정.
  - `Assets/Scripts/Vehicle/VehicleWeaponSystem.cs`:
    - 플레이어 탑승 시 발사/조준이 무조건 차단되던 기존 임시 제한 해제.
    - `CanFire()`: 플레이어가 탑승 중일 때 `_vehicle.IsMoving` 상태이면 사격을 멈추고, 정차 상태일 때만 사격하도록 조건 개선.
    - `HandleAutoWeapon()`: 자동 무기 탄약 소진 시 자동 재장전(`HandleReloadInput`) 연동.

---

## 10. 목표 좌표 탄도학 역계산 기반 곡사 100% 명중 시스템 구현 (2026-10-22)
- **개요**: 기존의 고정 발사각 투사 방식에서 발생하는 거리별 명중률 저하 문제를 해결하기 위해, 타겟 좌표(Target Position)를 전달받아 해당 거리에 정확히 착탄하는 물리 탄도학 역계산 시스템 구축.
- **수정 파일**:
  - `Assets/Scripts/Projectile.cs`:
    - `Init()` 메서드에 `Vector3? targetPosition = null` 매개변수 확장 (기존 호출부 100% 호환).
    - **탄도학 역계산 (Ballistic Equation)**:
      - 수평 변위 및 높이차 $H$ 계산.
      - 비행 시간 $T = \text{수평거리} / \text{speed}$.
      - 등속 수평 속도 $V_{xz} = D_{xz} / T$.
      - 수직 초기 속도 $V_y = (H - 0.5 \cdot g \cdot T^2) / T$ 역계산 적용으로 $t=T$ 시점에 목표 지점에 정확히 도달.
      - 타겟이 지정되지 않은 경우(허공 사격) 전방 사거리 지점을 가상 목표로 산출하여 부드러운 포물선 유지.
  - `Assets/Scripts/Vehicle/VehicleWeaponSystem.cs`:
    - 자동 락온된 적의 위치(`target.position`)를 발사체 `Init()`의 `targetPosition`으로 전달.

---

## 11. 적 발사체의 차량 피격 불가 버그 원인 분석 및 해결 (2026-10-22)
- **원인 분석**:
  - `EnemyProjectile.prefab`의 레이어는 **Layer 15 (`EnemyAttackLayer`)**, 차량 본체 콜라이더의 레이어는 **Layer 11 (`Vehicle`)**.
  - 유니티 물리 엔진 설정(`ProjectSettings/DynamicsManager.asset`)의 **Layer Collision Matrix(충돌 매트릭스)** 상에서 **`Layer 11`과 `Layer 15` 사이의 충돌 체크박스가 해제(`false`)**되어 있어, 물리 엔진이 두 오브젝트 간의 충돌 자체를 계산하지 않고 완전히 무시(통과)하고 있었음.
- **수정 파일**:
  - `ProjectSettings/DynamicsManager.asset`:
    - `m_LayerCollisionMatrix`에서 Layer 11(Vehicle)과 Layer 15(EnemyAttackLayer) 간의 상호 충돌 비트를 `true`로 활성화.
  - `Assets/Scripts/Projectile.cs`:
    - 직선 비행(`Move`) 시 지면/환경뿐만 아니라 `targetLayer` 및 `playerLayers`도 전방 레이캐스트 충돌 마스크(`hitMask`)에 포함하여, 탄속이 빠르거나 프레임 드랍이 생겨도 차량이나 플레이어를 뚫고 지나가는 터널링을 100% 방지하도록 2중 안전장치 구축.

---

## 12. 발사체 벽/장애물 착탄 시 소멸 처리 개선 (2026-10-22)
- **원인 분석**:
  - 기존 `HandleImpact`에서 일반 탄환(폭발형/곡사형이 아닌 기본 탄환)이 벽이나 장애물(`isEnvironment`)에 부딪혔을 때 비활성화(`gameObject.SetActive(false)`)를 호출하는 코드가 누락되어 있어, 벽에 닿아도 파괴되지 않고 수명이 다할 때까지 벽에 박힌 채 남아있었음.
  - 또한 Default(0) 레이어로 설정된 벽이나 환경 오브젝트가 환경 레이어 마스크에서 누락될 가능성이 존재했음.
- **수정 파일**:
  - `Assets/Scripts/Projectile.cs`:
    - `HandleImpact()` 개선:
      1. 폭발형 탄환(`isExplosive`): 타겟이든 벽/바닥이든 닿는 즉시 `Explode(hitPoint)` 호출.
      2. 단일 타격 대상(`isTarget`): 피해 및 피격 이펙트 재생 후 소멸.
      3. 타겟이 아닌 환경(벽, 기둥, 장애물, 바닥 등): 피격/착탄 이펙트 재생 후 **즉시 소멸(`gameObject.SetActive(false)`)**하여 벽에 박혀 남아있는 현상 완전 해결.
    - 기본 환경 레이어 마스크에 Default(0) 레이어도 포함하여 어떤 벽 오브젝트든 충돌을 감지하도록 보강.

---

## 13. 차량 발사체 본체/부품 자가 충돌(Self-Collision) 소멸 버그 해결 (2026-10-22)
- **원인 분석**:
  1. 차량(Vehicle) 프리팹은 메인 루트는 Layer 11(Vehicle)이지만, 범퍼/바퀴/메쉬 등 하위 부품 콜라이더가 Layer 0(Default)으로 설정되어 있는 경우가 많음.
  2. 이전 수정에서 벽 착탄 감지를 위해 환경 레이어(`environmentLayers`)에 Default(0) 레이어를 포함시켰는데, 발사체 전방 레이캐스트 검사 시 `hit.collider.gameObject.layer != shooterLayer` 조건만 검사하고 있어, 차량의 자식 부품(Layer 0)을 차량 본체(Layer 11)와 다른 "벽/장애물"로 잘못 판정하여 발사 즉시 충돌/폭발/소멸함.
  3. 또한 곡사(Arcing)의 경우 `environmentLayers` 레이캐스트에서 발사자 제외 조건이 없어 발사 원점 부근의 차체 콜라이더를 감지하여 즉시 폭발하는 문제 발생.
- **수정 파일**:
  - `Assets/Scripts/Projectile.cs`:
    - `shooterTransform` 필드 및 `Init()`에 `Transform shooter = null` 매개변수 추가.
    - `Init()` 시 발사자(`shooterTransform`)의 모든 자식 콜라이더와 발사체 본체 콜라이더 간 `Physics.IgnoreCollision`을 선제적으로 등록하여 물리적 간섭 차단.
    - `IsShooter(Collider col)` 헬퍼 메서드 추가: 콜라이더가 발사자 본인(최상위 루트 또는 자식 트랜스폼)이거나 발사자 레이어와 일치하는지 종합 검증.
    - `Move()` 내 직사 레이캐스트, 곡사 환경 레이캐스트, 하강 레이캐스트, `OnTriggerEnter()`, `HandleImpact()`, `Explode()` 모든 충돌 판정 단계에 `IsShooter` 필터링을 철저히 적용하여 자가 충돌 완전 방지.
  - `Assets/Scripts/Vehicle/VehicleWeaponSystem.cs`:
    - 발사 시 `_vehicle.transform`을 `shooter`로 전달.
    - 발사 시 탄환 스폰 위치에 전방 미세 오프셋(`finalFireDirection * 0.1f`)을 부여하여 차체 파묻힘 방지.
  - `Assets/Scripts/Unit/UnitWeaponSystem.cs` 및 `Assets/Scripts/EnemyAI.cs`:
    - 플레이어 및 적 유닛 발사 시에도 각자의 루트 트랜스폼(`shooterRoot`)을 전달하여 플레이어/적 몬스터의 자가 충돌 역시 원천 차단.

---

## 14. 직사 발사체 스폰 즉시 지면(Ground) 충돌 소멸 버그 해결 (2026-10-22)
- **원인 분석**:
  1. 본 프로젝트의 발사체 프리팹들(`VehicleProjectile`, `PlayerProjectile` 등)은 탑다운 뱀서라이크 게임 특성상 높낮이 차이가 있는 적을 XZ 평면에서 맞추기 위해 **BoxCollider의 Y축 높이가 100m(`m_Size.y = 100`)**로 설계되어 있음.
  2. 직사 발사체는 활성화(`_collider.enabled = true`)된 채 생성되는데, 위아래 50m로 뻗은 콜라이더가 생성 즉시 **바닥 지면(`Ground`, Layer 12)**과 트리거 충돌(`OnTriggerEnter`)을 발생시킴.
  3. 12번 작업에서 비타겟/환경 오브젝트 착탄 시 탄환을 즉시 소멸(`gameObject.SetActive(false)`)하도록 처리했기 때문에, 직사 탄환이 생성되자마자 지면(Ground)에 부딪힌 것으로 판정되어 0프레임만에 소멸되고 차량 위치에서 폭발 이펙트가 발생해 "차체에 부딪혀 사라지는 것"처럼 보였음.
  4. 반면 곡사(Arcing) 탄환은 공중 비행 중 `_collider.enabled = false`로 꺼져 있어 지면 트리거가 발생하지 않았으므로 정상적으로 날아갔던 것임.
- **수정 파일**:
  - `Assets/Scripts/Projectile.cs`:
    - `_groundLayerMask` (Layer 12) 및 `_obstacleLayerMask` (Layer 13) 캐싱 분리.
    - `OnTriggerEnter()`: 직사 탄환(`!isArcing`)인 경우 지면(`Ground`)과의 충돌은 완전히 무시하고 관통하도록 예외 처리.
    - `HandleImpact()`: 직사 탄환이 지면 레이어에 닿더라도 소멸되지 않도록 2중 방어.
    - `Move()`: 직사 레이캐스트 마스크에서 지면(`_groundLayerMask`)을 제외하고 벽/장애물(`_obstacleLayerMask`) 및 적/플레이어만 검사하여 바닥이나 경사면 지면에 닿아 조기 폭발하는 현상 방지.
