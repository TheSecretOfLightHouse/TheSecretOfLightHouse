# develop 갱신 후 Selector 정리와 최명기·강민서 인계 회신

작성일: 2026-10-17
브랜치: `feature/jihun` (`249d9c4` = develop `4b4c502` 병합)
담당: 김지훈

## 목표

develop 병합으로 들어온 최명기 인벤토리·E키 변경(`7e679dd`, `c91c2b9`)을 확인하고, 내 영역(Interaction) 영향 정리, 다음 작업(JH03 작업대) 선행 조건 합의 준비를 한다. 사용자 결정: 2인 회귀 시험은 이번에 제외.

## 확인한 사실

- 인벤토리: 획득(`TryAdd`)·소유자 동기화만 있고 차감·용량 없음(`05.Player/Inventory/Net/PlayerInventory.cs`, `Rules/InventoryData.cs`). 최명기 이번 변경은 컴파일·M2 미실행(본인 기록).
- 최명기가 내 `InteractionSelector`에 레이어 마스크·`NetId == 0` 제외·`Supports(DefaultAction)` 필터·요청 직전 갱신을 추가.
- `NetworkTestPlayer.prefab`에는 새 `_interactionLayers` 값이 저장돼 있지 않음 → 마스크 0. `JH07Game.unity`의 `InteractionTarget_*`는 Default 레이어(0) → 시험 씬에서 선택 불가(정적 확인, 실행 미확인).
- 같은 이름 `PlayerInteractionInput` 두 개: Interaction 쪽은 프리팹·씬 GUID 참조 없음, Player 쪽(`b7cf49a4…`)만 `tech.prefab`에 부착.
- 판 시작 게이트는 develop에 있으나 World 등록 코드 없음(`origin/feature/minseo` `f11c89c`까지 조회).
- `HarvestSpot` 변환기를 `HarvestHost`와 시험 `HarvestInteractionHandler`가 둘 다 등록.
- `04.Lighthouse/` 하위는 `.gitkeep`만 있음. JH03 미착수.

## 채집 SpotId 경로 점검 (같은 날 추가)

| 단계 | 만든 사람 | 현재 최명기 사용 여부 |
|---|---|---|
| 비네트워크 대상 요청 경로 `InteractionRelay.TrySendToNonNetworkTarget`, `TargetResolver`·`INonNetworkTargetAdapter`(JH02 A-1) | 김지훈 | 사용(`PlayerHarvestProbe.cs:140`) |
| SpotId→서버 위치 변환기 `HarvestTargetAdapter`·`HarvestHost`, `HarvestSpotRegistry.TryConsume` | 강민서 | 미사용. 처리기가 직접 변환기 역할(`HarvestInteractionHandler.cs:32,66`) |
| 클라이언트 SpotId 출처 | — | World `HarvestSpot.SpotId`가 아니라 시험용 `LocalHarvestTestSpot.SpotId`(`PlayerHarvestProbe.cs:124`) |
| 잔량 차감 | — | 없음. 유효한 클릭마다 드롭 1개(`HarvestInteractionHandler.cs:110`) |

2026-10-16 사용자 결정(좌클릭 SpotId → 서버 `TryConsume` → 드롭 N개)과 비교하면 SpotId 전송까지만 맞고 World 지점·잔량 차감은 미연결. 최명기 본인 인계 §3에도 미연결로 적혀 있어 시험 단계로 본다.

## 결정과 이유

- `_interactionLayers` 기본값: 처음에 `~0`으로 바꿨으나 시험 때문에 본편 기본값을 바꾸지 않기로 사용자 결정, 최명기 버전 0으로 되돌림. `NetworkTestPlayer.prefab`은 `~0`이 기본값이던 동안 에디터에서 저장되어 마스크 Everything(`m_Bits: 4294967295`)이 명시 저장됐다. Everything은 Default를 포함하므로 시험 대상 선택에 충분해 그대로 둔다. 같은 저장으로 Mirror가 `_assetId: 3488472377`을 기록했다.
- Interaction 쪽 `PlayerInteractionInput` 삭제: 미사용이며 같은 이름 어댑터 동시 부착 시 요청 중복 위험.
- 인벤토리 차감은 최명기 영역. 처음에 API 제안을 적었으나 사용자 지시로 삭제했다(최명기가 바로 진행, 김지훈은 대기).

## 변경 파일

- 삭제: `Assets/01.Scripts/03.Interaction/Relay/View/PlayerInteractionInput.cs`, `.cs.meta`
- `docs/05-implementation-roadmap.md`, `docs/06-owner-work-plan.md`, `docs/design/jihun-system-design.md` (E키 어댑터 설명 현행화)
- 추가: [최명기 인계 회신](../../handover/김지훈/2026-10-17/최명기-인계회신.md), [강민서 확인 요청](../../handover/김지훈/2026-10-17/판시작게이트등록-채집변환기-강민서-확인요청.md)

## 검증

- 실행: Unity 밖 임시 프로젝트(현재 `Assets/01.Scripts/**` 전체, 저장소 `Lighthouse.Runtime.csproj`의 참조를 절대 경로·`Library/ScriptAssemblies` DLL로 바꿈)로 컴파일, 오류 0. 저장소의 `.csproj`는 병합 전 파일 목록이라 그대로는 판정 불가했다.
- 미실행(1차 시점): Unity 에디터 컴파일·Mirror weaver(UnityMCP 연결 안 됨), JH02 시험 자동화 재실행, Play·M2·M3.

### 에디터 검증 (같은 날 추가, UnityMCP 연결 후)

- 에디터 컴파일: 강제 새로고침·컴파일 요청 후 `scriptCompilationFailed=False`, 콘솔 오류·경고 0(MCP 자체 WebSocket 경고 1건 제외). 로드된 `Lighthouse.Runtime`에 Interaction 쪽 `Lighthouse.Interaction.Relay.View.PlayerInteractionInput` 타입 없음(리플렉션). Mirror weaver 포함 에디터 컴파일 통과.
- 프리팹 값 확인: `NetworkTestPlayer.prefab`의 `InteractionSelector._interactionLayers` = −1(Everything), `InteractionTestAutomation` 부착.
- JH02 자동화 회귀: M0(에디터 호스트 단독). `JH07Offline`에서 Play → `IsEnabledInThisProcess=true`, 시작 인원만 Play 중 리플렉션으로 1 → `StartHost` → 로컬 `LobbyPlayer.RequestSetReady(true)` → `JH07Game`. 결과 `ALL_DONE actor=1 pass=34 fail=0`(A-1 12개 + A-2 22개). `selector_picks_near`(InteractionTarget_Near)·`selector_interact` PASS로 최명기 Selector 변경 + 마스크 Everything 조합에서 선택·요청 회귀 없음. Play 중 오류·경고 0. 씬 저장 없음, Play 후 작업 트리 변화 없음.
- M0 한계: `early_loading`은 시작 인원 1이라 즉시 진행 중이 되어 SKIP. 2번 액터 단계(`exclusive_seen_taken`·`exclusive_busy`)와 원격 클라이언트 경로는 미실행(M2 필요, 2인 회귀 시험은 별도). M3 미실행.

## 남은 일

- 최명기 인벤토리 차감이 develop에 들어오면 JH03 구현 시작.
- 강민서 답변(게이트 등록 시점·실패 정책, 변환기 소유).
- ~~에디터에서 컴파일 확인, JH07Game에서 JH02 자동화로 Selector 회귀 확인~~ → 완료(M0 34/34 PASS, 위 검증 참고).
- 2인 회귀 시험(최명기와 일정).
