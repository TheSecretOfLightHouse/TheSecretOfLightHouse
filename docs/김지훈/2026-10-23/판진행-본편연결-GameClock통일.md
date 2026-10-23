# 판 진행 연결 본편 이전과 GameClock 통일 (2026-10-23)

## 배경과 결정 이유

- 목표: 강민서 `WorldSessionGame`을 바탕으로 최종 씬(`Main`)을 만든다. 그 전에 JH04 시험 판(`TowerTrialRound`, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`)에만 있던 판 진행 연결을 본편 코드로 옮긴다. 정식 빌드에서는 이 파일이 빠지므로, 옮기지 않으면 `TowerMatchNet.Configure`를 아무도 부르지 않아 의식·소환을 시작할 수 없다.
- 사용자 결정(2026-10-23):
  - 날짜 기준은 강민서 `GameClock` 하나로 통일한다.
  - 마감(K02)은 마지막 날 밤이 끝날 때다. `win-vote-decisions.md`와 문제 대장 R09에 반영했다.
  - 아침 심해 게이지 정산(`VotingNetworkController.SettleMorning`)은 김지훈 연결 코드에서 호출한다. 박태욱 코드는 수정하지 않고 공개 API만 호출한다.
  - `Main` 씬 폴더는 김지훈이 관리한다. 미커밋 JH04 작업(최후 전투 HUD, 나무 작업대)은 사용자가 먼저 저장·커밋한 뒤 씬 작업을 한다.
- 투표 시작은 박태욱이 본편에서 이미 처리한다(`_initializeOnMatchStart`, 마피아 기절 감지). 그래서 옮기지 않았다.
- 모듈 마피아 판정은 `ModuleInteractionHandler`가 설정이 없으면 세션 역할을 쓰도록 이미 돼 있다.
- 자동 시험: 원격 시민은 마피아를 알 수 없으므로(비밀 정보) "역할에 맞춰 시나리오 선택"만으로는 시민의 마피아 투표를 자동화할 수 없다. 그래서 개발 빌드 전용으로 호스트를 마피아로 고정하는 옵션을 매니저에 두었다.

## 수행

- 새 파일 `Assets/01.Scripts/04.Lighthouse/Tower/Net/TowerMatchFlow.cs` (본편, `TowerMatchNet`과 같은 오브젝트):
  - 세션 흐름이면 `MatchStarted`(이미 배정됐으면 즉시)에서 `Begin(roster, mafiaId)`를 호출한다. 이때 `Configure`, 모듈 `MafiaCheck`·`CarryCheck`를 설정하고 `GameClock`(`WorldServices`의 `IGameClock`)에 연결한다.
  - `DayStarted(n)` → `ApplyDayStarted(n)`: 끝난 날을 차례로 `SettleMorning`하고, `n-1 >= TotalDays`이면 마피아 승리(Deadline)로 판을 끝낸다. 의식이 시작된 뒤에는 정산·마감을 하지 않는다(시험 판 동작 유지, K03 미정).
  - 의식 시작 → 기한 정지 + `BeginIdentityRitual`
  - `SummonApproved` → `TryBeginFinalBattle`
  - 최후 전투 시작 → 마피아 공개·들고 있던 모듈 폐기
  - 판 종료 → `EndGame`
  - 의식 판정자(`IRitualVoteJudge`) 역할도 한다.
  - 기한 일수는 `GameClock.TotalDays`(`DayCycleConfig` 5일)를 쓴다. 씬에 `GameClock`이 없으면 경고를 남기고 날짜·기한 없이 진행한다.
- `TowerTrialRound`(개발용)에 남긴 기능: 단일 씬 마피아 선택, 투표 직접 시작(`_isVotingStartedHere`, 기본 켬, JH04는 박태욱 자동 시작이 꺼져 있음), 각자 역할 전달, 다음 날로(`Flow.ApplyDayStarted(Day+1)`), 소환 강제, 촉수 대역 공격(최후 전투 시작 다음 프레임에 공격력 읽음). 자체 180초 시계와 `_dayLengthSeconds`·`_deadlineDays`·`DayEndsAt`은 삭제했다. `Day`·`DeadlineDays`·`IsClockStopped`·`RevealedMafiaId`는 Flow 값을 그대로 넘긴다.
- `TowerTrialPanel`: 남은 시간 대신 `GameClock` 낮·해질녘·밤과 "기한 N일 밤까지"를 표시한다.
- `LighthouseNetworkManager`: 개발 빌드 전용 `DevPinnedMafiaActorId`를 추가했다. 명단에 있으면 무작위 대신 그 사람을 마피아로 정한다.
- `TowerTrialAutomation`: 로비 흐름이면 게임 씬 `Start`에서 호스트 자신을 고정하고, 종료 시 해제한다. 이미 배정됐으면 `PIN_LATE`를 기록한다. 로비 흐름에서는 혼자 하는 시민 시나리오가 불가능하다는 내용을 주석에 적었다.

## 검증

- 실행: 에디터 새로 고침 후 컴파일 오류 0. 경고는 기존 `BoatController` CS0414 1건뿐이다.
- 미실행: 플레이 검증 전부(M0·M2), JH04 회귀, 자동 시험, 정식 빌드.
- 커밋: `a9b0dea`(최후 전투 HUD), `77b88a6`(나무 작업대·개발자 창 스위치), `43e1701`(이 작업 코드·문서). push 안 함.

### JH04 씬 연결 (같은 날 이어서)

- 에디터에서 `JH04TestScene` 연결을 마쳤다.
  - `Lighthouse`에 `TowerMatchFlow`를 추가했다(`_modules` = ModuleInteractionHandler, `_voting` = Voting).
  - 새 오브젝트 `WorldClock`을 만들었다: `GameClock`(`DayCycleConfig`, 낮 300초 + 밤 120초, 자동 시작 끔) + `NetworkIdentity`(sceneId 부여) + `WorldStateNet`(`_gameClock` 연결, 서버 시작 시 자동 시작 켬 → 단일 씬 모드에서도 시계가 돈다).
  - 씬을 저장했다. 저장하면서 삭제된 `TowerTrialRound` 필드가 정리됐다.
- 시험용 설정(`DayCycleConfig_Test`, 하루 18초)을 쓰지 않은 이유: 5일이 90초라 시간이 긴 자동 시험 시나리오(대기 최대 240초)가 마감에 걸린다. 기한은 "다음 날로"로 시험한다.
- **M0 실행 (에디터, 호스트 1명, `JH04Offline` → `JH07Lobby` → `JH04TestScene`):**
  - (가) 자동 시험 `solo_mafia`: 35/35 통과, 실패 0, 게임 오류·경고 0. 판 시작(Flow Begin) → 소환 강제 → 최후 전투, 마피아 공개(`mafia=1`), 마피아 손 닫힘(`mafia_take_closed` Rejected). 시험 종료 후 `DevPinnedMafiaActorId` = 0으로 풀린 것도 확인.
  - (나) 마감: 시작 시 `GameClock` 1일차 낮, `TotalDays` 5, Flow `DeadlineDays` 5. "다음 날로"를 4번 눌러 5일째가 돼도 판은 `Repairing`으로 진행. 5번째(6일째 아침)에 `Ended`, `MafiaWin`, `Deadline`, 마피아 공개. 오류 0.
- 미실행:
  - 2인 이상(M2): 투표 켬, 아침 정산 실제 값, 의식 → 투표 잠금, 승인된 소환, 원격 클라이언트에서 날짜 표시·자동 시험 시민 경로
  - 정식 빌드
  - `GameClock` 자연 경과에 따른 날짜 전환(빠른 넘김만 확인)

## 남은 일

1. JH04 씬 연결 커밋(사용자 승인 후).
2. 2인 M2: 자동 시험 `ritual`·`final` 시나리오(호스트 마피아 고정 경로).
3. `MainOffline`·`MainLobby` 생성(`00.Scenes/Main/`, CODEOWNERS 항목 추가), JH04 프리팹 묶기.
4. 박태욱에게 알릴 것: 아침 정산을 김지훈 `TowerMatchFlow`가 호출함, 본편 씬은 `_initializeOnMatchStart` 켬.
5. 강민서: 섬 프리팹 6개, 남은 커밋 병합.
6. 미정: 같은 순간의 마감·의식·소환 경합(K02 나머지), 의식 중 일일 효과(K03).
