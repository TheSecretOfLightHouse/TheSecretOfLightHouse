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
- 씬 미연결: JH04 `Lighthouse` 오브젝트에 `TowerMatchFlow`가 아직 없다. **연결 전까지 JH04에서는 `TowerTrialRound`가 오류를 기록하고, 의식·소환을 시작할 수 없다.** 미커밋 HUD·작업대 씬 변경과 섞이지 않도록 씬 작업은 사용자 커밋 뒤로 미뤘다.

## 남은 일

1. 사용자: JH04Offline 저장, HUD·작업대 변경 커밋.
2. JH04TestScene 연결:
   - `Lighthouse`에 `TowerMatchFlow` 추가(`_modules`, `_voting`)
   - `GameClock`(`DayCycleConfig_Test`)과 `WorldStateNet` 추가
   - 기존 `TowerTrialRound`의 삭제된 필드는 저장 시 정리됨
   - 2인 회귀
3. `MainOffline`·`MainLobby` 생성(`00.Scenes/Main/`, CODEOWNERS 항목 추가), JH04 프리팹 묶기.
4. 박태욱에게 알릴 것: 아침 정산을 김지훈 `TowerMatchFlow`가 호출함, 본편 씬은 `_initializeOnMatchStart` 켬.
5. 강민서: 섬 프리팹 6개, 남은 커밋 병합.
6. 미정: 같은 순간의 마감·의식·소환 경합(K02 나머지), 의식 중 일일 효과(K03).
