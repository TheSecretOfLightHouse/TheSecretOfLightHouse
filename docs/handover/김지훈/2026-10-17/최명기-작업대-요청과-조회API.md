# 인수인계 — 작업대 요청 형식과 조회 API (정식 UI MK06 연결용)

작성일: 2026-10-17 / 갱신일: 2026-10-18
작성자 → 수신자: 김지훈 → 최명기
상태: 작업대 서버·동기화 구현, 에디터 M0·같은 PC M2 통과. 제작 버튼(`Craft`)과 Canvas 화면 시안 추가, develop PR #58.
근거: [등대·작업대 모듈 결정](../../../design/lighthouse-module-decisions.md), [작업 원문](../../../김지훈/2026-10-17/JH03-작업대구현.md)

## 1. 요청 (InteractionRelay.TrySendToNetworkTarget(작업대 netId, 동작, Arg0, Arg1))
| 동작 | Arg0 | Arg1 | 성공 조건 |
|---|---|---|---|
| `SelectRecipe` | 레시피 ID(`ModuleRecipe.Id`) | 0 | 아직 투입 전이고 상한 미달. 같은 레시피 재선택은 성공 |
| `Deposit` | 자원 ID(`ResourceDefinition.Id`) | 요청 수량 | 서버가 `Min(요청, 남은 필요량)`으로 줄여 `TryRemoveTo`. 1개라도 빠지면 Success |
| `Craft` | 0 | 0 | (2026-10-17 추가) 재료가 모두 찼을 때만 Success. 다 차도 자동 완성되지 않으며, 완성되면 작업대 보관 개수가 늘고 레시피가 비워진다. 버튼 활성 여부는 `WorkbenchNet.IsReadyToCraft()` |

- 실패는 모두 `Rejected`(숨은 상태를 드러내지 않음). 쓰러짐·배 탑승 중 거절.
- 실제 빠진 수량은 응답에 없다. 인벤토리 `ResourceCountChanged`와 작업대 `Changed`로 갱신한다.
- 인벤토리는 서버가 `connection.identity`의 `PlayerInventory`에서만 차감한다. `InventoryData`·SyncDictionary를 직접 건드리지 않는다.
- 자원을 한 번 넣으면 완성까지 레시피를 바꿀 수 없다. 화면은 첫 투입 전에 이 사실을 알리고 확인을 받는다(2026-10-17 결정).

## 2. 조회 (`Lighthouse.Workbench.Net.WorkbenchNet`, 모든 PC)
- `Catalog`(레시피 목록), `CurrentRecipeId`(0 = 없음), `TryGetCurrentRecipe(out ModuleRecipe)`
- `GetDepositedAt(재료 순번)`, `GetDeposited(자원 ID)`, `GetRemaining(자원 ID)`
- `IsReadyToCraft()` — 재료가 모두 찼는지(제작 버튼 활성 기준, 2026-10-17 추가)
- `GetStoredCount(레시피 ID)`, `StoredCounts`
- `event Action Changed` — 현재 레시피·투입량·보관 개수가 바뀔 때마다
- 레시피: `ModuleRecipe.DisplayName`, `Icon`(2026-10-17 추가), `IngredientCount`, `GetIngredient(i)`(`Resource`·`ResourceId`·`Amount`), `MaxExisting`, `IsTestValue`(시험값이면 UI에 확정값처럼 쓰지 않기)

## 3. 참고 구현
- Canvas 화면 시안(2026-10-18, PR #58): `04.Lighthouse/Workbench/View/WorkbenchScreen.cs`(+ `WorkbenchRecipeCard`·`WorkbenchIngredientRow`·`WorkbenchItemSlot`), 프리팹 `02.Prefabs/UI/Workbench/WorkbenchScreen.prefab`, 스프라이트 `03.Images/WorkbenchUi/`. `JH03TestScene`에 배치. 범위 안에서 F로 열기, 첫 투입 전 확인창, 재료가 다 차면 `제작` 버튼 활성. 사용자 이미지 시안 톤을 따른 **시안**이며 정식 화면으로 가져가거나 교체해도 된다.
- 시험 GUI: `99.Dev/Workbench/WorkbenchTestPanel.cs` (선택 버튼, 재료별 +1·전부, 제작, 보관 목록). 시험 씬에서는 컴포넌트를 꺼 두었다.
- 시험 씬: `00.Scenes/Jihun/JH03Offline.unity`에서 시작(`Tech 1` 사용, 입장 시 시험 자원 지급)

## 4. 확인 요청
- 정식 UI 위치·입력(작업대 앞 E키로 패널 열기 등)은 최명기 결정 범위다. 필요한 조회 API가 더 있으면 알려 달라.
- (2026-10-18) 플레이어 입력 잠금 경로가 필요하다. 시안은 화면이 열린 동안 로컬 `PlayerInput`을 꺼서 이동·카메라·좌클릭 채집을 멈추고, 닫으면 되돌린다. Player 쪽에 UI가 열린 동안 입력을 막는 공식 경로(잠금 요청·해제 등)가 생기면 그것으로 바꾸겠다.
- (2026-10-18) `InventoryDebugPanel`(OnGUI)이 Canvas 위에 그려져 작업대 화면 오른쪽 위와 겹친다. 시험용이면 끄는 방법(토글 키 등)이 있으면 좋겠다.
