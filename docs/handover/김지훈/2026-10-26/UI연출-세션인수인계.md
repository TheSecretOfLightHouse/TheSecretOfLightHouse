# 인수인계 — 등대 판 진행 UI 연출 ①~⑤ 구현

> **2026-10-26 완료 처리:** 같은 날 다음 세션에서 구현·M0 확인을 마쳤다. 계획과 달라진 점(SyncVar hook 대신 매 프레임 읽기, 알림 띠를 공용 `ScreenNotice`로 분리, 추가 연출 A~D)과 현재 남은 일은 [작업 원문](../../../김지훈/2026-10-26/UI연출-기획.md#남은-일-최종)을 따른다. 아래는 당시 기록으로 보존한다.

경로: (예정) `Assets/01.Scripts/04.Lighthouse/Tower/Net/`, `Assets/01.Scripts/04.Lighthouse/Tower/View/`, `Assets/00.Scenes/Jihun/JH04TestScene.unity`, 이후 `Assets/00.Scenes/Main/MainGame.unity`
작성일: 2026-10-26
브랜치: feature/jihun
담당 파트: Lighthouse(등대) — 김지훈
수신: 다음 세션(김지훈, UnityMCP 연결 후)

## 1. 목표
- 최종 상태: 등대 진행 이벤트 7가지(장착, 층 완성, 수리 100%, 의식 시작, 소환→최후 전투, 피격, 판 종료)에 화면 UI 연출이 모든 기기(호스트·원격 클라이언트)에서 재생된다. 연출은 Canvas 프리팹 하나로 묶여 `JH04TestScene`에서 확인되고 `MainGame`에 배치된다.
- 근거: [UI 연출 기획 원문](../../../김지훈/2026-10-26/UI연출-기획.md) — 연출 표·문구·제외 범위가 전부 여기 있다. 사용자가 7가지 전부 승인.

## 2. 완료한 것
| 항목 | 증거 | 상태 |
|---|---|---|
| 연출 기획 확정 | 원문 표, 사용자 "다 할거야" | 완료(결정) |
| 현재 코드의 신호 조사 | 아래 4절 | 완료(코드 읽기) |

## 3. 완료하지 못한 것
| 항목 | 어디까지 했나 | 막힌 지점 |
|---|---|---|
| 클라이언트 신호 추가 | 설계만 | 없음(다음 세션 첫 작업) |
| 연출 스크립트·프리팹 | 미착수 | 프리팹 생성·씬 배치에 에디터 필요. 이번 세션 UnityMCP 연결 실패(ECONNREFUSED) |
| `JH04TestScene` 확인, `MainGame` 배치 | 미착수 | 위와 같음 |

## 4. 현재 코드 상태와 구현 계획

**신호가 부족하다(코드 확인 결과).**
- `TowerMatchNet`의 `RitualStarted`·`FinalBattleStarted`·`Ended`는 서버 코드 안에서만 호출되는 C# 이벤트다. ClientRpc도 SyncVar hook도 없다. 값(`_phase`, `_health`, `_outcome`, `_endReason`, `_ritualEndsAt`, `_maxHealth`)은 SyncVar로 원격에도 오지만 "바뀐 순간" 신호가 없다. `FinalBattleHud`는 `LateUpdate`에서 값을 매 프레임 읽고, 체력 감소도 직전 값과 비교해 감지한다.
- `TowerNet.Changed`는 모든 기기에서 발생하지만 몇 번 칸인지 없다. `HandleInstalledChanged(op, index, value)`(`TowerNet.cs` 맨 아래)가 index를 받으므로 여기서 칸 신호를 낼 수 있다. 초기 상태 수신 때는 OnChange가 오지 않고 `OnStartClient`에서 `Changed`만 한 번 발생 → 늦게 들어온 사람에게 장착 연출이 몰아서 터지지 않는다.
- 보상 구간 `TowerLayout.GetRepairRatio`·`_rewardPercents`(25/50/75), 층 열림 `IsFloorOpen(floor)`, 층 이름 `GetFloorName`. 층 완성 판단은 둘 중 무엇을 쓸지 설계 문서 확인 후 결정.

**계획(새 동기화 데이터는 추가하지 않는다):**
1. `TowerNet`: 클라이언트용 `SlotInstalled(int slot)` 이벤트를 `HandleInstalledChanged`에서 `value == true`일 때 발생.
2. `TowerMatchNet`: `_phase`·`_health`에 SyncVar hook → `PhaseChanged(old, new)`, `HealthChanged(old, new)` 이벤트(모든 기기). 기존 서버 전용 이벤트는 그대로 둔다(로그·시험이 사용).
3. 새 View 스크립트(이름은 설계 문서 확인 후 결정, 가칭 `TowerFx`) + 공용 부품 3개: 상단 알림 띠, 중앙 큰 제목, 화면 테두리 번쩍임(비네트 이미지). DOTween 없음 — 간단한 보간을 직접 작성.
4. `FinalBattleHud`에 피격 흔들림·붉은 깜빡임·피해 숫자, "위에서 내려옴" 진입, 30% 미만 맥박 추가(또는 `TowerFx`가 담당 — 겹치지 않게 한쪽으로).
5. 오디오 클립 칸만 두고 비면 건너뛴다.
6. 에디터 빌더 메뉴로 Canvas 프리팹 생성(기존 `Workbench/Editor/*Setup.cs` 방식). `.prefab`·`.unity` YAML 직접 편집 금지.

## 5. 현재 저장소 상태
- 커밋한 것: 이번 세션 없음.
- 커밋하지 않은 변경(이번 세션 아님, 건드리지 말 것): `ProjectSettings/ProjectSettings.asset`, `Assets/Resources.meta`. 10-23 미커밋 작업(정체 확인 UI, 최후 전투 HUD, 판 진행 본편 연결, 나무 작업대)은 pointer 참고.
- 이번 세션 추가: 이 문서, [기획 원문](../../../김지훈/2026-10-26/UI연출-기획.md), 담당자 pointer 행.
- 수정한 씬·신규 ScriptableObject: 없음.

## 6. 검증 현황
- L1~L3: 해당 없음(코드 무변경).
- 다음 세션 목표: L1 컴파일 → M0 `JH04TestScene` 1인으로 ①~⑤ 각각 재생 → M2 호스트+원격 클라이언트에서 원격에도 재생되는지(hook 확인의 핵심).

## 7. 차단 조건
- [ ] UnityMCP 연결 — 사용자가 Unity 에디터를 열고 MCP 서버를 켜야 프리팹 생성·씬 배치·화면 확인 가능. 코드 작성(4절 1~5)은 연결 없이 먼저 할 수 있다.
- [ ] ④ 피격 시험 수단 — `TowerTrialPanel`에 등대 피해 버튼이 없다(`ApplyDamage`는 몬스터 쪽 호출). `99.Dev/Tower/TowerTrialPanel.cs`(본인 영역)에 "등대 피해 100" 개발 버튼 추가 필요.
- [ ] `MainGame` 배치 시 다른 팀원 씬 변경과 충돌하지 않게 develop 최신 받은 뒤 배치.

## 8. 다음 세션의 첫 행동
1. [기획 원문](../../../김지훈/2026-10-26/UI연출-기획.md)을 읽고, `docs/design/jihun-system-design.md`의 JH04 절 전체를 읽어 폴더·타입 이름을 맞춘다. 새 파일 이름으로 `docs/`를 검색한다.
2. `git status`로 미커밋 상태를 확인한다(위 5절과 다르면 사용자에게 알린다).
3. 4절 1~2(신호 추가)를 구현하고 컴파일(L1).
4. 4절 3~5(연출 스크립트)와 `TowerTrialPanel` 피해 버튼을 구현한다.
5. MCP 연결을 확인하고 빌더 메뉴로 프리팹 생성 → `JH04TestScene` 배치 → 패널 버튼(장착·"점화 의식 시작"·"소환 강제"·피해·"다음 날로")으로 ①~⑤ 확인.
6. 작업 원문·pointer 갱신 후 `MainGame` 배치는 사용자 확인을 받고 진행.

## 9. 주의
- 장착자 이름 표시 금지(모듈 보유 비공개 결정).
- 카메라(`05.Player/Camera/PlayerCamera.cs`, 최명기)는 수정하지 않는다. 흔들림은 UI 요소에만.
- 날짜 전환(`02.World/Clock`, 강민서)·무고 공개(`07.Mafia`, 박태욱) 알림은 범위 밖. 하려면 담당자 협의 먼저.
- "탈출"은 결과 문구로만 사용(사용자 답 미확인).
- Mirror `SyncList.OnChange`가 호스트(서버 겸 클라이언트)에서 몇 번 호출되는지 확인할 것 — 호스트에서 연출이 두 번 재생되지 않아야 한다.
