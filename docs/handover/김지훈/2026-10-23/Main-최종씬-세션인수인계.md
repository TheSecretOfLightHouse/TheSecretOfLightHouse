# 인수인계 — 최종 통합 씬 Main 병합 이후 (몬스터·암초 확인부터 재개)

경로: `Assets/00.Scenes/Main/`(`MainOffline`·`MainLobby`·`MainGame`), `Assets/01.Scripts/04.Lighthouse/Tower/Net/TowerMatchFlow.cs`, `Assets/01.Scripts/07.Mafia/IdentityCheck/View/IdentityActionScreen.cs`
작성일: 2026-10-23
브랜치: feature/jihun (원격과 같음, 마지막 커밋 `e659467`) · **PR #89**(develop 대상, 열림)
담당 파트: Network · Interaction · Lighthouse — 김지훈

## 1. 목표
- 최종 상태: 강민서 `WorldSessionGame`을 바탕으로 한 최종 씬 `Main`에서 4인이 로비 → 맵 생성 → 작업대·모듈·등대 → 투표·의식 또는 정체 확인·소환 → 최후 전투 → 판 종료까지 진행된다. 배 스폰은 이후 최명기가 붙인다.
- 근거 문서:
  - 작업 원문: [판 진행 본편 연결·GameClock 통일·Main 병합](../../../김지훈/2026-10-23/판진행-본편연결-GameClock통일.md), [정체 확인·소환 UI](../../../김지훈/2026-10-23/정체확인-소환-UI-시안-적용.md)
  - 결정: [승패·투표 결정](../../../design/win-vote-decisions.md) K02(2026-10-23 마감 = 마지막 날 밤 끝)
  - 박태욱 요청: [최후 전투 촉수 공격 연결 요청](박태욱-최후전투-몬스터공격-연결요청.md)

## 2. 완료한 것
| 항목 | 증거 | 상태 |
|---|---|---|
| 판 진행 연결 본편 이전 (`TowerMatchFlow`) | 커밋 `43e1701`. JH04 M0: 자동 시험 `solo_mafia` 35/0, 마감 6일째 아침 `MafiaWin`·`Deadline` | 완료(검증됨, M0) |
| JH04에 `TowerMatchFlow`·`WorldClock`(`GameClock`+`WorldStateNet`) 연결 | 커밋 `3c400c8` | 완료(검증됨, M0) |
| develop 병합 | `8a1d7b3`(develop `1367783`까지). 이후 develop 새 커밋 없음(2026-10-23 마지막 확인) | 완료 |
| 최종 씬 `Main` | 커밋 `e659467`. `MainGame` M0(호스트 1, 플레이 중에만 시작 인원 1·포트 7778·투표 자동 초기화 끔): 섬 17개 → 판 시작, 등대 바닥 = 지면 1.7 | 완료(검증됨, M0) |
| 4인 개발 빌드 (UI 넣기 전 빌드) | 사용자 직접 시험, 호스트 로그: 1판 `Deadline`, 2판 `LighthouseDestroyed`, 예외 0 | 사용자 보고 + 로그 확인 |
| 정체 확인·크툴루 소환 UI | 커밋 `45b8dbb`. 1920×1080 렌더링 확인. `MainGame`·`JH04TestScene` 연결 | 구현됨(미검증 — 플레이) |
| PR #89 | 리뷰: 강민서·최명기·박태욱·김민성(CODEOWNERS 자동) | 열림 |

## 3. 완료하지 못한 것
| 항목 | 어디까지 했나 | 막힌 지점 |
|---|---|---|
| **`MainGame` 몬스터·암초 생성 확인** | 코드·씬 연결 확인만 함(아래 5절). 사용자는 "몬스터가 안 보인다"고 보고 | 세션 후반 Unity MCP 연결이 끊김(`ECONNREFUSED`). 실행으로 개수·위치를 세지 못함 |
| 정체 확인 UI 플레이 확인 | UI가 들어간 4인 빌드(`Builds/Main4`, 2026-10-23 19:55)를 띄움 | 사용자 시험 결과 미보고 |
| 판 종료 → 세션 종료·결과 화면 | 원인 확인: 등대 판이 끝나도 세션이 계속됨. `SessionEndReason`에 승패 종료 없음 | 최명기 MK06(최종 승패)과 연결 방식 합의 필요. 결과 화면은 김지훈 JH07 |
| 정식 빌드 최후 전투 피해 | 요청 문서 작성 | 박태욱 보스가 스스로 시작하지 않고 씬에도 없음. 요청 문서 전달(사용자) |
| 1~2인 판 | 원인 확인, 시작 인원 4로 설정 | 박태욱 `IdentitySettings`(시민 2명 이상 필요). 예외가 나면 호스트 연결이 끊김 |
| 문서 커밋 | 아래 4절의 미커밋 3개 | 커밋·push 승인 대기 |

## 4. 현재 저장소 상태
- 커밋한 것(이 세션): `a9b0dea`, `77b88a6`, `43e1701`, `3c400c8`, `8a1d7b3`(병합), `45b8dbb`, `e659467`. 모두 push 완료.
- 커밋하지 않은 변경:
  - `docs/김지훈/2026-10-23/판진행-본편연결-GameClock통일.md`: 정식 빌드 피해 원인 보충
  - `docs/김지훈/POINTER.md`: PR #89, 요청 문서, 이 인수인계 연결
  - `docs/handover/김지훈/2026-10-23/박태욱-최후전투-몬스터공격-연결요청.md`(신규), 이 문서(신규)
  - `.vscode/`, `output/`은 커밋하지 않는 개인 파일이다(`output/design/identity-ui/`에 시안 원본).
- 수정한 씬: `Assets/00.Scenes/Main/MainOffline.unity`, `MainLobby.unity`, `MainGame.unity`(신규), `Assets/00.Scenes/Jihun/JH04TestScene.unity`. 강민서 `Minseo/` 씬은 복사만 했고 수정하지 않았다.
- 신규 ScriptableObject: 없음. `MainGame`은 가안 데이터를 쓴다: `TowerBattleSettings_JH04Trial`(HP 1000·의식 60초·필요 표 2), `SpawnerPlacementConfig_Test`, 레시피 시험 데이터.
- 빌드 산출물(Git 제외): `Builds/Main4/Main.exe`(UI 포함), 로그 `Builds/Main4/logs/`, 이전 시험 로그 `Builds/Main4/logs-prev/`.

## 5. 검증 현황
- L1: 에디터 컴파일 오류 0(기존 `BoatController` CS0414 경고 1). 개발 빌드 2회 성공(오류 0).
- L2: JH04·Main 에디터 M0(위 2절).
- L3 / 멀티플레이 등급: 4인 로컬 개발 빌드는 사용자가 직접 시험했다(로그 확인). M2 기준 체크리스트로 정리한 시험은 아직 없다.
- 미검증 항목과 이유:
  - **몬스터·암초:** 코드와 씬으로 확인한 내용
    - 연결은 돼 있다: `MainGame`의 `WorldMonsterSpawnHost`는 `_mapState`·`_manager`·`_bindings`가 연결돼 있고, `ReefWorld`도 있다.
    - 처음에는 안쪽 해역만 열린다(`RegionGate`). 안쪽 스폰 지점은 1~3곳이고 지점당 최대 1마리다(`MonsterSpawnerData_Inner`). 위치는 중앙에서 70~200m이고, 등대 50m 안에는 생기지 않는다.
    - 배가 없어 섬 밖을 볼 수 없으므로 "멀어서 안 보임"으로 **추정**한다. 실행 확인은 안 했다.
  - 정체 확인 UI 플레이, 정식(릴리즈) 빌드, `GameClock` 시간 경과에 따른 날짜 전환, 2~3인 자동 시험 `ritual`·`final`(`TowerTrialAutomation`은 JH04 좌표 기준이라 Main에서는 못 씀).

## 6. 차단 조건
- [ ] **Unity MCP 연결:** 사용자가 Unity 에디터에서 MCP for Unity 연결을 다시 켠다. 다음 세션 첫 확인 대상이다.
- [ ] 다인 접속 테스트: 정체 확인 UI 결과를 사용자가 보고해야 한다(3인 이상).
- [ ] 다른 파트:
  - 박태욱: 최후 전투 촉수 공격 본편 연결, 1~2인 투표 초기화 예외. 요청 문서 전달은 사용자.
  - 최명기: 판 최종 승패 확정(MK06)과 세션 종료 연결, 배 스폰.
  - 강민서: 등대 섬 등대 기준점 여부, 섬 회전 시 시작 지점 (±6, 4, 0)과 건물 겹침, 본편 몬스터 밀도.
- [ ] 게임 디자인 결정: K02 나머지(같은 순간의 마감·의식·소환), K03(의식 중 아침 정산. 현재 정산 안 함).

## 7. 다음 세션의 첫 행동
1. 이 문서와 [작업 원문](../../../김지훈/2026-10-23/판진행-본편연결-GameClock통일.md)을 읽고 `git status`·`git log --oneline -3`·`gh pr view 89`로 상태를 확인한다. 4절의 미커밋 문서가 그대로인지 본다.
2. Unity MCP 연결을 확인한다(`manage_scene get_active`). 안 되면 사용자에게 에디터에서 MCP 연결 재시작을 요청한다.
3. 포트 7777을 쓰는 빌드 창(`Main.exe`)이 떠 있는지 확인한다(`netstat -ano | findstr :7777`). 떠 있으면 에디터는 포트를 플레이 중에만 7778로 바꾼다.
4. `MainOffline`을 열고 플레이한다. 플레이 중에만 다음을 설정한다(저장 금지):
   - `LighthouseNetworkManager._startPlayerCount` = 1, `KcpTransport.port` = 7778
   - `SceneManager.sceneLoaded`에서 `MainGame`의 `VotingNetworkController._initializeOnMatchStart` = false (1인 투표 예외 회피)
   - `StartHost` → `LobbyPlayer.RequestSetReady(true)`
5. 판이 시작되면(`Match 1 started`) 다음을 기록한다:
   - `MonsterSpawnManager.Batches` 수와 각 `AliveCount`, `MonsterBase` 인스턴스 수와 위치(중앙에서 거리)
   - `ReefInstanceSet.Count`
   - `WorldMonsterSpawnHost.RegisteredCount`(또는 `WorldMonsterSpawnProbe` 수동 실행)
   - 콘솔 경고·오류
6. 결과를 분류해 사용자에게 보고한다. "생성됐지만 멀다"면 위치 목록과 함께, "생성 안 됨"이면 원인(스폰 위치 검증 실패, 바인딩, 해금)까지 확인한다. World·Monsters 코드는 수정하지 말고 강민서·박태욱에게 전달할 내용으로 정리한다.
7. 결과를 작업 원문에 추가하고 pointer를 갱신한다. 사용자가 승인하면 4절 문서와 함께 커밋·push해 PR #89에 반영한다.

## 8. 주의
- 되돌리면 안 되는 변경:
  - `MainOffline` 시작 인원 4(2인 이하 예외 때문)
  - `MainGame` 투표 `_initializeOnMatchStart` 켬, `TowerTrialRound._isVotingStartedHere` 끔(투표 이중 초기화 방지)
  - 등대 (0, 1.7, 3)·작업대 (2.5, 2.2, 2.5) 위치(섬 회전 때문에 중앙 4m 안)
  - 빌드 목록 기존 순서(`Main` 3개는 끝에 추가)
- 다른 사람이 병행 작업 중인 파일: `Minseo/` 씬(강민서, 원본), `07.Mafia/`(박태욱; 새 파일 2개만 추가함), 최명기 배 스폰은 `MainGame`에 들어올 예정(씬 YAML 충돌 주의).
- `.unity`·`.prefab` YAML 직접 편집 금지. 에디터 API로만 바꾼다. 에디터에서 다른 씬을 열기 전에 현재 씬 `isDirty`를 확인한다.
- 한글 경로 파일을 Python으로 고칠 때는 `PYTHONUTF8=1`을 쓴다(이번 세션에 인코딩 오류가 한 번 났음).
