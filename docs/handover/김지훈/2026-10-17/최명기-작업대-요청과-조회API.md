# 인수인계 — 작업대 요청 형식과 조회 API (정식 UI MK06 연결용)

작성일: 2026-10-17
작성자 → 수신자: 김지훈 → 최명기
상태: 작업대 서버·동기화 구현, 에디터 M0 통과. M2 미실행.
근거: [등대·작업대 모듈 결정](../../../design/lighthouse-module-decisions.md), [작업 원문](../../../김지훈/2026-10-17/JH03-작업대구현.md)

## 1. 요청 (InteractionRelay.TrySendToNetworkTarget(작업대 netId, 동작, Arg0, Arg1))
| 동작 | Arg0 | Arg1 | 성공 조건 |
|---|---|---|---|
| `SelectRecipe` | 레시피 ID(`ModuleRecipe.Id`) | 0 | 아직 투입 전이고 상한 미달. 같은 레시피 재선택은 성공 |
| `Deposit` | 자원 ID(`ResourceDefinition.Id`) | 요청 수량 | 서버가 `Min(요청, 남은 필요량)`으로 줄여 `TryRemoveTo`. 1개라도 빠지면 Success |

- 실패는 모두 `Rejected`(숨은 상태를 드러내지 않음). 쓰러짐·배 탑승 중 거절.
- 실제 빠진 수량은 응답에 없다. 인벤토리 `ResourceCountChanged`와 작업대 `Changed`로 갱신한다.
- 인벤토리는 서버가 `connection.identity`의 `PlayerInventory`에서만 차감한다. `InventoryData`·SyncDictionary를 직접 건드리지 않는다.

## 2. 조회 (`Lighthouse.Workbench.Net.WorkbenchNet`, 모든 PC)
- `Catalog`(레시피 목록), `CurrentRecipeId`(0 = 없음), `TryGetCurrentRecipe(out ModuleRecipe)`
- `GetDepositedAt(재료 순번)`, `GetDeposited(자원 ID)`, `GetRemaining(자원 ID)`
- `GetStoredCount(레시피 ID)`, `StoredCounts`
- `event Action Changed` — 현재 레시피·투입량·보관 개수가 바뀔 때마다
- 레시피: `ModuleRecipe.DisplayName`, `IngredientCount`, `GetIngredient(i)`(`Resource`·`ResourceId`·`Amount`), `MaxExisting`, `IsTestValue`(시험값이면 UI에 확정값처럼 쓰지 않기)

## 3. 참고 구현
- 시험 GUI: `99.Dev/Workbench/WorkbenchTestPanel.cs` (선택 버튼, 재료별 +1·전부, 보관 목록)
- 시험 씬: `00.Scenes/Jihun/JH03Offline.unity`에서 시작(`Tech 1` 사용, 입장 시 시험 자원 지급)

## 4. 확인 요청
- 정식 UI 위치·입력(작업대 앞 E키로 패널 열기 등)은 최명기 결정 범위다. 필요한 조회 API가 더 있으면 알려 달라.
