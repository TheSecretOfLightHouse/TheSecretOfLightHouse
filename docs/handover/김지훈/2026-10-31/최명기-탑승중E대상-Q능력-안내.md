# 최명기 안내: 탑승 중 E 대상, Q 능력 안내, 소지품 운반 카드 옵션

작성: 김지훈 · 2026-10-31 · 대상: 최명기(Player)

## 바뀐 것 (커밋됨, feature/jihun)

1. **탑승 중 E 대상에서 타고 있는 배 제외** — `90f6d63`
   - `InteractionSelector.FindNearest`가 로컬 플레이어의 `PlayerController.CurrentBoat`와 그 자식의 `InteractionTarget`을 E 후보에서 뺀다.
   - 이유: 모든 배 프리팹의 `InteractionTarget`(기본 `RepairBoat`, 사거리 3)이 탑승 중 항상 가장 가까워, 유령선(`InspectGhostShip`, 사거리 20) 등 바다의 E 대상이 선택되지 못했다.
   - 영향: 배에 탄 채 E로 자기 배를 수리하는 경로가 없어진다. 소지품 화면의 수리 버튼(`InventoryChestScreen.Layout`)은 `RepairBoat`를 직접 보내므로 그대로 동작한다. 육지에서 배 옆 E 수리도 그대로다.
2. **상시 HUD의 Q 능력 안내는 탑승 중에만 표시** — `0372158` (09.UI, 김지훈)
   - 사용자 결정: Q 능력은 배에 탔을 때만 쓰는 기능.
3. **소지품 화면 운반 카드 끄기 옵션** — `a67bb32`
   - `InventoryChestScreen.Hud.cs`에 `_isCarriedHudShown`(기본 true) 추가. 상시 HUD가 운반 카드를 보여 주는 씬에서 겹치지 않게 끄는 용도. 아직 어느 씬에서도 끄지 않았다.

## 확인 요청 (최명기 영역, 수정하지 않음)

- Q 입력(`PlayerController.ProcessActionRequests` → `AbilityRequested`)은 탑승을 검사하지 않는다. 액티브 능력 중 탑승 검사는 `GuardShieldAbility`뿐이라 정비공·항해사·채집가는 육지에서 Q가 실행될 수 있다. "Q는 배에서만"을 입력·능력 쪽에서도 막을지 결정 필요.
- 탑승 중 E로 자기 배 수리가 필요한 설계라면 알려 달라. 선택기 제외 규칙을 조정한다.

## 검증

- Unity 컴파일 error 0, 메인씬 빌드·4창 실행. 탑승 중 유령선 E·장부 Play 확인은 아직.
