# JH04 최후 전투를 수리도 승계·고정 HP로 교체

작성일: 2026-10-22 · 담당: 김지훈 · 상태: 구현, M0 자동 시험 3종·3인 M2 `final`·`ritual` 통과. 미커밋

## 요청과 범위
- 사용자 요청: [수리도 승계·고정 HP 제안](../../design/jh04-repair-carryover-fixed-hp-proposal.md)대로 시스템을 구현하고, JH04Offline에서 방을 만들면 기존 시스템은 버리고 이 시스템으로 이어지게 한다.
- 경로: JH04Offline [방 생성](호스트) → JH07Lobby → JH04TestScene(`LighthouseNetworkManager._gameScene`). 판 시작은 기존처럼 시험 패널(`TowerTrialPanel`)의 [판 시작]이다.
- "기획서대로"이므로 부속 제안 두 건(100% 상태 소환 = 시민 승리, 고공격력 특수 종료 제외)도 함께 넣었다.

## 결정과 이유
- **버린 것**: 소환 HP 환산(`base + (max-base) × 수리도`), 소환 시 장착 마감(`TowerNet.CloseInstalls`), 심연 결정 줍기·등실 재점화(`AbyssCrystalSpot`, 행동 14·15, 점화 비용), 특수 종료(`SpecialEnding`). JH04TestScene의 `AbyssCrystalSpots` 그룹(지점 6개)은 에디터 API로 지우고 씬을 저장했다.
- **새 규칙**(`TowerMatchNet`):
  - 소환 시 수리도·장착 모듈 유지, HP = 최대 HP = `TowerBattleSettings.MaxHealth`(시험값 1000, 기존 값 유지).
  - 소환 후에도 장착 가능. 서버 `TowerNet.ModuleInstalled`에서 최후 전투 중 완성되면 `CitizensWin / RepairCompleted`.
  - 수리도 100% 상태에서 유효한 소환이면 최후 전투로 가지 않고 즉시 `CitizensWin / RepairCompleted`.
  - HP 0 → `MafiaWin / LighthouseDestroyed`. 장착으로 HP 회복·최대 HP 증가 없음. 먼저 확정된 결과가 유지된다.
  - 공격력은 등대가 받지 않는다(`TryBeginFinalBattle()` 인자 삭제). 값은 기존처럼 소환 순간 심해 스냅숏 → 시험 라운드의 촉수 대체 공격에서 고정.
- `TowerEndReason`: 3을 `RepairCompleted`로 바꾸고 5(특수 종료)는 비웠다. `InteractionAction` 14·15는 재사용 금지 주석만 남겼다.
- **공개된 마피아 모듈 들기 차단**: 장착이 계속 열리면서, 공개된 마피아가 작업대 모듈을 들고 버티는 길이 생겼다. 기존 2026-10-20 결정(소환 시 마피아 인벤토리 닫힘)을 서버에서 강제하려고 `ModuleInteractionHandler.CarryCheck`를 추가하고 `TowerTrialRound`가 소환 후 마피아를 거절하게 했다. 새 규칙이 아니라 기존 결정의 구현이다.
- 마피아 투표·확인·심해 계산(박태욱 영역)은 수정하지 않았다. 공격력 식 `max(최소, 기본 + 오투표 − 봉인)`은 `AbyssGaugeSnapshot`에 이미 있다. 보스 쪽 `BossTestEnvironment`의 특수 종료 경로는 박태욱 영역이라 그대로 두었으며 JH04 시험 경로는 이를 쓰지 않는다.

## 변경 파일
| 파일 | 내용 |
|---|---|
| `04.Lighthouse/Tower/Net/TowerMatchNet.cs` | 최후 전투 교체, 결정·재점화·특수 종료 삭제, 완성 승리 |
| `04.Lighthouse/Tower/Net/TowerNet.cs` | 소환 후 장착 마감 삭제 |
| `04.Lighthouse/Tower/Net/AbyssCrystalSpot.cs`(+meta) | 삭제 |
| `04.Lighthouse/Tower/Rules/TowerBattleSettings.cs` | 기본 HP·특수 종료·점화 값 삭제, 고정 최대 HP만 유지 |
| `04.Lighthouse/Tower/Rules/TowerEndReason.cs` | `RepairCompleted`, 특수 종료 삭제 |
| `03.Interaction/Requests/Net/InteractionAction.cs` | 14·15 삭제(번호 재사용 금지) |
| `04.Lighthouse/Modules/Server/ModuleInteractionHandler.cs` | `CarryCheck` |
| `99.Dev/Tower/TowerTrialRound.cs` | 새 API 연결, 소환 후 마피아 들기 거절 |
| `99.Dev/Tower/TowerTrialPanel.cs` | 최후 전투 HP·수리도 표시, 100% 소환 안내, 종료 사유 |
| `99.Dev/Tower/TowerTrialAutomation.cs` | `final` 시나리오 갱신, 1인 시나리오 `solo_final`·`solo_full`·`solo_mafia` 추가 |
| `Assets/00.Scenes/Jihun/JH04TestScene.unity` | 결정 지점 그룹 삭제(에디터 저장) |
| `docs/design/jh04-repair-carryover-fixed-hp-proposal.md` | 상태·코드 상태·기록 갱신 |

## 검증
- 실행함(에디터, M0 1인, JH04Offline 플레이 → [방 생성] 버튼 호출 → 로비 준비 → JH04TestScene):
  - 컴파일 오류 0, 콘솔 오류 0.
  - `solo_final` pass 80 / fail 0: 25%(2/8)에서 강제 소환 → 모듈 2개 유지, HP 1000/1000 → 소환 후 6개 장착(HP 증가 없음) → 8/8에서 `CitizensWin / RepairCompleted`.
  - `solo_full` pass 78 / fail 0: 8/8에서 소환 → 최후 전투 없이 즉시 `CitizensWin / RepairCompleted`(MaxHealth 0 유지).
  - `solo_mafia` pass 35 / fail 0: 마피아 혼자 2/8에서 소환 → 유지·HP 1000, 작업대에 프레임이 있어도 `TakeModule` 거절. 이후 촉수 대체 공격으로 HP 1000 → 980 확인.
- 미실행: 투표·정체 확인을 거치는 실제 소환(2인 이상, `final` 시나리오), 원격 클라이언트 HP·수리도 동기화(M2), 의식 중 100% 소환, 사용자 직접 조작. 강제 소환은 정체 확인을 건너뛴다.
- 참고: JH04Offline의 사용자 미저장 변경은 저장하지 않았다. JH04TestScene은 추가(additive)로 열어 저장 후 닫았다.

### 3인 M2 (같은 PC, 에디터 호스트 1 + 개발 빌드 클라이언트 2)
- 빌드: 빌드 목록은 바꾸지 않고 `JH04Offline`·`JH07Lobby`·`JH04TestScene`을 직접 넘겨 `Builds/JH04/JH04.exe` 개발 빌드(성공). 클라이언트 인수 `-autoJoin -jh04Auto -jh04Scenario=<이름> -jh04Players=3`. 에디터는 Play 중 리플렉션으로 시작 인원 3, [방 생성] 버튼 호출 → 클라이언트 준비 뒤 호스트 준비. 로그는 `Builds/JH04/logs/final|ritual/`(Git 제외).
- `final`: 호스트(마피아) pass 38 / fail 0, 클라이언트 actor 2 pass 67 / fail 0(나머지 3층 건설), actor 3 pass 9 / fail 0. 시민 2명 지목 → 정체 확인 성공 → **실제 소환 승인** → 원격 두 곳 모두 `FinalBattle`, HP 1000/1000, 장착 2개 유지, 마피아 공개·표 잠김 동기화 → 공개된 마피아 `TakeModule` 거절 → 원격 시민이 8/8 완성 → 세 곳 모두 `CitizensWin / RepairCompleted`.
- `ritual`(회귀): 호스트 pass 86 / fail 0, 클라이언트 9/0·9/0. 8/8 → 의식 시작 → 표 잠김·변경 거절 → 의식 종료 `CitizensWin / RitualSucceeded`.
- 에디터 콘솔 오류 0, 클라이언트 로그 예외 0.
- 확인 못 한 것: `final`은 촉수 대체 공격의 첫 타(감기 약 9초 + 주기 3초) 전에 끝나 **M2에서 HP 감소·HP 0 패배는 보지 못했다**(HP 감소는 M0에서만 1000→980). 의식 중 소환, 100% 소환의 M2, 소환 뒤 마피아 자원 빼기(서버 미차단, 결정 대기), 다른 PC(M3), 사람 직접 조작.

## 추가: 투표 전 확인창·마피아 동일 화면 (2026-10-22)
- 사용자 결정(대화): 버저·공동 투표안은 버리고 기존 개인 지목(판당 2회, 원하는 시점)에 확인창만 더한다. 2표가 모두 틀려도 판은 계속되고 결과는 기존 종료 시점에 알린다. 마피아도 같은 화면(권장안 1), 같은 대상·0회 처리와 잠금·거절 시 창 닫기(권장안 3·4). '단축키 1초' 요구 충돌은 무시하기로 했다.
- `99.Dev/Tower/TowerTrialPanel.cs`: 다른 대상을 누르면 확인창(1회 차감·남은 n→n-1·회복/철회 불가) → [지목 확정]일 때만 `RequestVote`. 같은 대상은 '이미 지목', 0회는 '남은 기회 없음'. 잠기면(서버 `IsLocked` 또는 의식·최후 전투 단계) 창을 닫고 안내. 서버 응답 `VotingLocked`·`NoVotesRemaining` 등은 창을 닫고 사유 표시. 마피아는 서버로 보내지 않는 가짜 지목·가짜 남은 2회로 같은 화면을 본다. 판 시작 전 화면 상태 초기화.
- 서버(박태욱 `VotingNetworkController`) 수정 없음: 투표 응답·내 상태는 요청자에게만 가고, 잠금·종료 상태는 마피아를 포함한 참가자 전원에게 간다(코드 확인).
- 문서: [승패·투표 결정](../../design/win-vote-decisions.md) 1절 3행 추가, [문제 대장](../../07-design-issue-register.md) R04 상태 갱신.
- 검증: 컴파일 오류 0. **플레이 미확인** — 사용자 수동 테스트용 JH04 빌드 창 3개가 7777 포트를 쓰고 있을 수 있어 에디터 호스트를 띄우지 않았다. 빌드(`Builds/JH04`)는 이 변경 전 것이다.
- 남은 일: 다인 플레이로 확인창·마피아 가짜 화면 확인(재빌드 필요), 정식 투표 UI(최명기 MK06)에 같은 규칙 전달. 시험 패널 자체가 머리글·마피아 칸에 역할을 표시하므로 동일 화면 확인은 투표 칸 범위다.

## 남은 일
1. M2 추가: HP 0 패배 경로(시민이 건설하지 않는 시나리오), 의식 중 소환, 완성까지 시간과 HP 소모 속도 비교. 3인 `final`·`ritual`은 통과(위 절).
2. 기준 문서 반영: 제안은 아직 [승패·투표 결정](../../design/win-vote-decisions.md)과 문제 대장(R08·R09·R10)에 반영되지 않았다. 채택을 기준 문서로 확정할지 사용자 결정 필요.
3. 수치: 고정 HP 1000·촉수 주기·최소 공격력은 시험값. 제안 13절의 늦은 소환(99%) 실효성 시험.
4. 박태욱 공유: 보스 쪽 특수 종료 경로는 이 안에서 쓰지 않는다는 점, 소환 후 피해가 수리도가 아닌 HP를 깎는다는 점.
