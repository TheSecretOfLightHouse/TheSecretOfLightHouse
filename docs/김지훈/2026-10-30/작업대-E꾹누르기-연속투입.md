# 작업대 기본 자원 E 꾹 누르기 연속 투입

작성일: 2026-10-30 · 담당: 김지훈

## 요청
기본 자원 투입을 E 1회당 1개가 아니라 E를 누르고 있으면 계속 들어가게 하고, 누를수록 빨라지게(일반적인 게임의 홀드 반복 방식).

## 수행
- `WorkbenchScreen`(스케치 레이아웃) 입력: E를 누른 순간 1개 투입(기존과 같음) → `_holdDelay`(0.35초) 뒤부터 반복 → 반복 속도가 `_holdRampSeconds`(1.5초) 동안 초당 `_holdStartRate`(5)에서 `_holdMaxRate`(25)로 상승.
- 릴레이는 결과를 받을 때까지 다음 요청을 막는다(`InteractionRelay.IsAwaitingResult`). 그래서 속도만큼 쌓인 수량을 릴레이가 비면 한 요청에 묶어 보낸다(최대 `_holdMaxBatch` 10). 왕복 지연과 관계없이 체감 속도가 유지되고 서버 요청 수는 늘지 않는다. 서버 `Deposit`은 기존대로 보유·남은 수량으로 자른다.
- E를 떼거나, 선택이 보관함으로 바뀌거나, 더 넣을 수 없으면(보유 0·필요량 충족·생성 상한) 반복 중지. 창을 닫으면 초기화.
- 반복 중에는 릴레이가 바쁘면 보내지 않아 "이전 요청 처리 중" 메시지가 뜨지 않는다.
- 묶음이 2개 이상이면 행 숫자에 `+N` 표시. 연속 전송 시 아직 매칭 안 된 본인 투입분(`_ownCredit`)을 누적해 다른 사람 투입 연출로 오인하지 않게 함.
- 투입 전송 로직을 `SendDeposit(resourceId, amount)`로 분리(행 버튼 `HandleDepositClicked`도 사용).

- 후속(사용자 보고: 자원이 있는데 개수가 빨간색): `WorkbenchIngredientRow`는 보유량이 남은 필요량보다 적으면 빨간색이었다(`WorkbenchScreen_Sketch`만 `_isShortageTinted` 켜짐). 보유 0개일 때만 빨간색으로 변경. 컴파일 error 0, 재빌드 미실행.

## 변경 파일
- `Assets/01.Scripts/04.Lighthouse/Workbench/View/WorkbenchScreen.cs`
- `Assets/01.Scripts/04.Lighthouse/Workbench/View/WorkbenchIngredientRow.cs`

## 검증
- Unity 컴파일 후 콘솔 error 0 (실행).
- `Builds/JH04/JH04.exe` 개발 빌드(`JH04Offline`→`JH07Lobby`→`JH04TestScene`) `Build Finished, Result: Success`, DLL에 `TickDepositHold` 포함 확인. 960×540 창 4개(`-roleOverlay`, `Builds/JH04/log1~4.txt`) 실행, 시작 로그 예외 0 (실행).
- 실제 E 홀드 플레이 확인은 사용자 테스트 대기.

## 남은 일
- Play로 호스트/원격 각각 E 꾹 누르기 속도·`+N` 연출 확인, 수치는 인스펙터 `Hold to deposit` 항목에서 조정.
