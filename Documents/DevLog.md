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
