# 정체 확인 UI 점검 — 디버그 문구 노출·소환 권리 만료 처리

작성일: 2026-10-29 · 담당: 김지훈 · 상태: 코드 반영, dotnet 컴파일 오류 0, Unity 컴파일·플레이 검증 전

## 요청
투표 UI에 `CheckFailed`라는 디버그 문구가 인게임 화면에 그대로 뜬다. 다른 오류가 없는지, 정체 확인이 조건대로 되는지, 확인 후 소환하지 않았을 때 처리가 맞는지 기능까지 확인해 달라.

## 원인
- `CheckFailed`는 MainGame에 배치된 판 진행 시험 패널 `99.Dev/Tower/TowerTrialPanel`의 마피아 칸 `최근: {response.Result.ToString()}`에서 나왔다. 실제 UI(`IdentityActionScreen`)는 이미 한국어 문구로 바꿔 보여 준다.
- 같은 패널의 투표 거절 기본 문구도 `({response.Result})`로 enum 이름을 띄웠다.

## 기능 대조 (win-vote-decisions 2절·7절)
- 성공 판정: 서버가 요청을 처리하는 순간 마피아를 지목 중인 표 수 ≥ `IdentitySettings._requiredVoteCount`(2). 성공도 확인 횟수 1회를 쓴다. 맞음.
- 실패: 아직 공개되지 않은 시민 1명을 무작위 공개(중복 없음), `_revealedCitizens` SyncList로 전원에게 전달. 맞음.
- 10초 권리 중 재확인은 거절, 만료 뒤 남은 횟수가 있으면 재확인 가능, 마지막 확인권 성공도 10초 사용 가능, 난파 중 둘 다 불가, 의식 중 가능. 서버 규칙(`IdentityCheckService`)은 결정과 맞음.
- **결함(수정함)**: 권리가 쓰이지 않고 만료되어도 서버가 상태를 다시 보내지 않는다. 클라이언트는 마지막 상태(`CanSummon=true`, `CanCheck=false`)에 멈춰 "소환 가능 0초"가 남고, 소환 버튼은 켜진 채, 정체 확인 버튼은 계속 꺼져 재확인을 할 수 없었다(소환을 눌러 거절 응답을 받아야 풀림).

## 변경
- `07.Mafia/IdentityCheck/View/IdentityActionScreen.cs`: 클라이언트 서버 시각(`NetworkTime.time`)이 만료 시각을 넘으면 권리를 끝난 것으로 보고 소환 버튼·남은 초 표시를 내린다. 서버가 아직 권리를 보고하는 동안 0.5초 간격으로 `RequestOwnIdentityState`를 다시 요청해 확인 버튼을 되살린다. 소환 확인창이 열려 있었다면 "소환 권리 시간이 지났습니다"로 닫는다.
- `99.Dev/Tower/TowerTrialPanel.cs`: 정체 확인 결과를 한국어 문구로 표시(`IdentityText`), 투표 거절 기본 문구에서 enum 이름 제거.
- 07.Mafia는 박태욱 담당 폴더지만 `IdentityActionScreen`은 김지훈이 만든 UI라 그 안에서만 고쳤다. 서버(`VotingNetworkController`)는 손대지 않았다.

## 남은 문제 (수정 안 함, 결정·담당 협의 필요)
- 실패로 공개된 무고 시민이 실제 UI에는 나오지 않는다. 시민 화면에서 보이는 곳은 시험 패널(`플레이어 {actorId}는 시민`, 닉네임 아님)뿐이다. 마피아 알림은 "모두에게 공개되었습니다"라고 말한다. 투표 카드 표시나 전체 공지 등 표시 방식 결정 필요.
- 세션 자동 초기화(`VotingNetworkController.HandleMatchStarted`)는 설정값 2회·2표를 그대로 쓴다. 시민이 1명(2인 시험)이면 `IdentityCheckService` 생성자가 예외를 던지고, `MatchStarted?.Invoke` 안에서 나는 예외라 뒤에 등록된 판 시작 처리까지 멈출 수 있다. `TowerTrialRound`처럼 시민 수로 줄이는 처리가 필요(박태욱 영역).
- 서버 쪽에서 만료 시 상태를 밀어 주면 클라이언트 재요청이 필요 없다(박태욱 영역, 선택).

## 검증
- 실행: `dotnet build Lighthouse.Runtime.csproj` 오류 0(두 파일 포함 확인). Mirror 위버는 돌지 않은 빌드.
- 미실행: Unity MCP ping 무응답으로 에디터 컴파일·콘솔 확인 못 함. 마피아로 확인 성공 → 10초 대기 → 확인 버튼 복귀, 실패 시 시민 공개 플레이 확인 전.
