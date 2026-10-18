# 인수인계 — 작업대 요청 형식과 조회 API, 작업대 화면 담당 변경

작성일: 2026-10-17 / 갱신일: 2026-10-18(레시피별 동시 진행으로 요청·조회 형식 변경)
작성자 → 수신자: 김지훈 → 최명기
상태: 작업대 서버·동기화 구현, 에디터 M0·같은 PC M2 통과. 제작 버튼(`Craft`)과 Canvas 화면 시안 추가, develop PR #58.
**2026-10-18 담당 변경: 작업대 화면 UI는 김지훈(JH03)이 맡는다(최명기와 합의 완료). MK06에서 작업대 화면을 만들 필요가 없다.** 아래 1·2절 API는 참고용이며, 최명기에게 남은 요청은 4절(입력 잠금 경로·디버그 창)뿐이다. [코드 소유권](../../../CODE_OWNERSHIP.md)·[06 MK06](../../../06-owner-work-plan.md) 갱신.
근거: [등대·작업대 모듈 결정](../../../design/lighthouse-module-decisions.md), [작업 원문](../../../김지훈/2026-10-17/JH03-작업대구현.md)

## 1. 요청 (InteractionRelay.TrySendToNetworkTarget(작업대 netId, 동작, Arg0, Arg1, Arg2))
2026-10-18 변경: 레시피마다 진행도를 따로 두고, 어떤 레시피를 볼지는 각 화면이 로컬로 정한다. `SelectRecipe`는 작업대에서 더 쓰지 않는다(값 4는 통신 형식이라 그대로 둠). `InteractionRequest`에 `Arg2`(기본 0)가 추가됐다.

| 동작 | Arg0 | Arg1 | Arg2 | 성공 조건 |
|---|---|---|---|---|
| `Deposit` | 레시피 ID(`ModuleRecipe.Id`) | 자원 ID(`ResourceDefinition.Id`) | 요청 수량 | 서버가 `Min(요청, 그 레시피의 남은 필요량)`으로 줄여 `TryRemoveTo`. 1개라도 빠지면 Success. 그 레시피의 첫 투입이면 존재 상한을 검사 |
| `Craft` | 레시피 ID | 0 | 0 | 그 레시피 재료가 모두 찼을 때만 Success. 다 차도 자동 완성되지 않으며, 완성되면 보관 개수가 늘고 그 레시피 진행도만 0이 된다. 버튼 활성 여부는 `WorkbenchNet.IsReadyToCraft(레시피 ID)` |

- 실패는 모두 `Rejected`(숨은 상태를 드러내지 않음). 쓰러짐·배 탑승 중 거절.
- 실제 빠진 수량은 응답에 없다. 인벤토리 `ResourceCountChanged`와 작업대 `Changed`로 갱신한다.
- 인벤토리는 서버가 `connection.identity`의 `PlayerInventory`에서만 차감한다. `InventoryData`·SyncDictionary를 직접 건드리지 않는다.
- 다른 레시피에 넣어도 기존 진행도는 그대로다. 첫 투입 후 레시피 고정과 첫 투입 확인창은 2026-10-18에 폐지됐다.

## 2. 조회 (`Lighthouse.Workbench.Net.WorkbenchNet`, 모든 PC)
- `Catalog`(레시피 목록). 2026-10-18부터 "현재 레시피"가 없어 `CurrentRecipeId`·`TryGetCurrentRecipe`는 삭제됐다.
- `GetDepositedAt(레시피 ID, 재료 순번)`, `GetDeposited(레시피 ID, 자원 ID)`, `GetRemaining(레시피 ID, 자원 ID)`
- `IsStarted(레시피 ID)` — 1개라도 들어갔는지(진행 중 표시)
- `IsReadyToCraft(레시피 ID)` — 재료가 모두 찼는지(제작 버튼 활성 기준)
- `GetStoredCount(레시피 ID)`, `StoredCounts`
- `event Action Changed` — 어느 레시피든 투입량이나 보관 개수가 바뀔 때마다
- 레시피: `ModuleRecipe.DisplayName`, `Icon`(2026-10-17 추가), `IngredientCount`, `GetIngredient(i)`(`Resource`·`ResourceId`·`Amount`), `MaxExisting`, `IsTestValue`(시험값이면 UI에 확정값처럼 쓰지 않기)

## 3. 참고 구현
- Canvas 화면 시안(2026-10-18, PR #58): `04.Lighthouse/Workbench/View/WorkbenchScreen.cs`(+ `WorkbenchRecipeCard`·`WorkbenchIngredientRow`·`WorkbenchItemSlot`), 프리팹 `02.Prefabs/UI/Workbench/WorkbenchScreen.prefab`, 스프라이트 `03.Images/WorkbenchUi/`. `JH03TestScene`에 배치. 범위 안에서 F로 열기, 카드를 누르면 내 화면만 그 레시피로 바뀜(진행 중·제작 가능·보유 한도 배지), 재료가 다 차면 `제작` 버튼 활성. 첫 투입 확인창은 2026-10-18에 프리팹에서 삭제. 2026-10-18부터 김지훈 담당 화면이다.
- 시험 GUI: `99.Dev/Workbench/WorkbenchTestPanel.cs` (레시피 보기 버튼, 재료별 +1·전부, 제작, 보관 목록). 시험 씬에서는 컴포넌트를 꺼 두었다.
- 시험 씬: `00.Scenes/Jihun/JH03Offline.unity`에서 시작(`Tech 1` 사용, 입장 시 시험 자원 지급)

## 4. 확인 요청
- ~~정식 UI 위치·입력은 최명기 결정 범위~~ → 2026-10-18 김지훈 담당으로 변경. 다른 UI·HUD와 겹치는 위치나 공통 스타일 규칙이 있으면 알려 달라.
- (2026-10-18) 플레이어 입력 잠금 경로가 필요하다. 시안은 화면이 열린 동안 로컬 `PlayerInput`을 꺼서 이동·카메라·좌클릭 채집을 멈추고, 닫으면 되돌린다. Player 쪽에 UI가 열린 동안 입력을 막는 공식 경로(잠금 요청·해제 등)가 생기면 그것으로 바꾸겠다.
- (2026-10-18) `InventoryDebugPanel`(OnGUI)이 Canvas 위에 그려져 작업대 화면 오른쪽 위와 겹친다. 시험용이면 끄는 방법(토글 키 등)이 있으면 좋겠다.
