# 인수인계 — 김지훈 채집·픽업 Interaction 연결

> 이 문서의 입력 어댑터·Selector·원본 netId 설명은 이전 조사 상태다. 최신 체크아웃의 E키 Selector 재연결·레이어와 SpotId 시험 경로는 [최신 전달 문서](../2026-10-16/김지훈-Interaction-Network-인수인계.md)를 먼저 따른다. 과거 실행 결과는 당시 증거로 보존한다.

경로: `Assets/01.Scripts/05.Player/Gathering/`, `Assets/01.Scripts/05.Player/Inventory/`
작성일: 2026-10-15
브랜치: feature/myeongki
담당 파트: Player·인벤토리·UI(최명기) ↔ Interaction·접속 기반(김지훈)
작성자: 최명기
수신자: 김지훈
상태: 구현 연결 전달 및 본편 계약 확인 요청, M2 미검증

## 1. 목표
- 현재 사용자 흐름: 왼쪽 클릭으로 채집 → 서버에서 자원 드롭 생성 → E키로 드롭 획득 → 서버 인벤토리 지급 성공 후 드롭 제거.
- 채집 순간 인벤토리에 직접 지급하지 않는다. 실제 지급은 PickUp 처리에서만 한다.
- 김지훈의 공용 Interaction 코드는 변경하지 않고 기존 Relay/Router를 사용했다. 공용 대상 선택과 본편 채집 지점 연결은 아래 확인 요청으로 남긴다.
- 근거: [공용 연결 안내](../../김지훈/2026-10-14/JH02-상호작용-최명기-연결안내.md), [담당 경계](../../../CODE_OWNERSHIP.md), [작업 원문](../../../최명기/2026-10-15/E키-공용상호작용-돌획득.md).

## 2. 완료한 것
| 항목 | 증거 | 상태 |
|---|---|---|
| 입력 분리 | InputActions의 왼쪽 클릭 Harvest/E키 Interact. PlayerInputReader.OnHarvest → PlayerController.HarvestRequested, OnInteract → InteractionRequested | 코드 반영 |
| 왼쪽 클릭 채집 요청 | PlayerHarvestProbe가 HarvestRequested만 구독. 커서로 고른 시험 대상의 netId를 TrySendToNetworkTarget(..., Harvest)로 요청 | 코드 반영 |
| 단발 채집 처리 | HarvestInteractionHandler : IInteractionHandler, HarvestDropSpawner로 Spawn 전 드롭 ID·수량 초기화 후 NetworkServer.Spawn | 코드 반영. 잔량 차감은 미구현 |
| E키 자원 전용 선택 | PlayerPickupInput이 InteractionRequested 구독. InteractionTarget.Active 중 ResourcePickup 및 Supports(PickUp), 범위 내 최근접 대상만 선택하여 PickUp을 명시해 요청 | 호스트 회귀 확인 |
| 서버 지급·제거 | PickupInteractionHandler : IInteractionHandler → ResourcePickup.TryCollect → PlayerInventory.TryAdd 성공 후 NetworkServer.Destroy | 호스트 기능 확인 |
| 인벤토리 표시 | PlayerInventory의 소유 클라이언트 수량 동기화·변경 이벤트. InventoryDebugPanel은 본인에게만 표시하도록 작성 | GUI 최종 화면·원격 검증 미실시 |

### 플레이어 프리팹 연결 주의
- 현재 tech 프리팹에는 PlayerPickupInput이 있고, 기존 PlayerInteractionInput 컴포넌트는 제거했다. 두 입력 어댑터를 동시에 붙이면 E키가 PickUp과 대상 기본 Action을 함께 요청할 수 있으므로 중복 부착하지 않는다.
- InteractionRelay는 재사용한다. InteractionSelector 컴포넌트는 남아 있지만 현재 E키 전송은 PlayerPickupInput이 담당한다.
- 따라서 기존 InteractionSelector.Selected/SelectedChanged를 UI에 사용하면 실제 픽업 후보와 다를 수 있다. E키 표시·작업대 연결 시 선택 경로를 함께 정리해야 한다.
- 공용 PlayerInteractionInput.cs/InteractionSelector.cs/Relay/Router의 원본은 수정하지 않았다.

## 3. 완료하지 못한 것
| 항목 | 어디까지 했나 | 막힌 지점 |
|---|---|---|
| 본편 HarvestSpot 연결 | 현재는 시험용 네트워크 Sphere + HarvestTarget + InteractionTarget + NetworkIdentity | 공용 안내의 비네트워크 HarvestSpot/SpotId/HarvestTargetAdapter 경로와 연결 필요 |
| 채집 세션 | 현재 사용자 요구의 클릭마다 단발 드롭 생성 | 기존 안내의 IInteractionSessionHandler/Complete 방식과 적용 범위 확인 필요 |
| 잔량·복구 | 드롭의 일회 지급과 지급 실패 시 유지 구현 | World 잔량 차감과 생성 실패 복구 계약 미연결. 강민서와 함께 확인 |
| E키 범용 상호작용 | 자원 드롭만 선택 | 작업대 등 다른 E키 행동은 미연결. 공용 Selector를 손대지 말라는 사용자 요청으로 Player 안에 제한 선택 구현 |
| 다인 검증 | 호스트 기능만 직접 확인 | 원격 인벤토리·양쪽 드롭 제거·동시 획득 M2/M3 미검증 |

## 4. 현재 저장소 상태
- 확인 기준: `feature/myeongki`, HEAD `cbe79cc` (`Merge pull request #43 from TheSecretOfLightHouse/feature/myeongki`). 원격 게시 상태를 추가 확인한 것은 아니다.
- 문서 작성 직전 git status는 깨끗했다. 이번 인수인계와 연결 원문/pointer 변경은 새 미커밋 문서 변경이다.
- 개인 시험 흐름: Myeongki/Offline → Lobby → MKTestScene. 시작 인원 저장값은 2였으며 시험 중 1인 설정은 실행 중에만 사용했다.
- 관련 프리팹: Player/tech.prefab, Player/stone.prefab. stone은 루트 ResourcePickup·NetworkIdentity·InteractionTarget(PickUp) 구성.
- 관련 데이터: Resource_Stone의 PickupPrefab 연결. 새 ScriptableObject 타입은 추가하지 않았다.
- 현행 픽업 타입은 전역 ResourcePickup/PickupInteractionHandler/PlayerPickupInput이다. 이전 대화 예제의 Lighthouse.Player.Inventory.* 네임스페이스로 연결하지 않는다.

## 5. 검증 현황
- L1: 기능 구현 당시 Unity 컴파일 통과. 병합 후 현재 최종 버전 전체 재컴파일를 이번 문서 작성에서 실행하지 않았다.
- L2: 기록된 호스트 E키 시험에서 수량0→1, 동기화 수량1, 드롭 제거. 잘못된 자원 ID는 Rejected 및 드롭 유지, 제거된 netId 재요청은 InvalidTarget 및 추가 지급 없음.
- L2 회귀: Sphere 1.35m, stone 2.27m로 공용 Selector가 Sphere를 고른 상황에서도 E키는 PickUp:Success만 발생. 돌이 없을 때 E키 재입력으로 Harvest/추가 생성 없음.
- L3 / 멀티플레이 등급: 이번 획득 기능은 M2/M3 통과로 보고하지 않는다. 호스트 시험의 임시 바닥·위치·시작 인원은 저장하지 않았다.
- 사용자 보고: 원격 요청에 따른 서버 드롭 생성은 관찰됐으나 클라이언트 표시 문제도 보고됐고, Offline의 Spawnable Prefabs 연결을 안내했다. 이것을 최종 원격 획득 검증 완료로 간주하지 않는다.
- GUI·물리·최종 원격 동작은 별도 확인 필요. 상세 실행 과정은 작업 원문 참조.

## 6. 차단 조건
- [ ] 김지훈·최명기: 왼쪽 클릭 Harvest / E키 Interact 분리와 E키 대상 선택·표시·작업대 통합 경로 확인.
- [ ] 김지훈·최명기·강민서: 네트워크 시험 대상에서 비네트워크 HarvestSpot/SpotId로 이관할 요청 경로와 채집 완료 후 드롭 생성·잔량 차감 계약 확인.
- [ ] 김지훈·최명기: Harvest 단발 처리기와 기존 세션 처리기를 같은 Action에 중복 등록하지 않을 구성 확인. DevSessionHandler는 기본 Harvest, DevInteractionHandler는 기본 PickUp이므로 본편 처리기와 동시에 등록하지 않는다.
- [ ] 다인 접속 테스트(M2 이상) 필요: 원격 획득·본인 수량 동기화·동시 획득 확인.

## 7. 다음 세션의 첫 행동
1. 위 Player 입력 구독과 tech 프리팹의 어댑터 구성부터 확인한다. 예전 부착 안내대로 PlayerInteractionInput을 다시 붙이기 전에 현재 E키 어댑터와 연결을 조율한다.
2. E키 자원 선택과 작업대 등 일반 상호작용을 공용 Selector로 통합할지, 입력별 정책을 제공할지 김지훈·최명기가 합의한다. 이 문서가 공용 코드 수정 승인은 아니다.
3. 본편 HarvestSpot에 대한 서버 완료·잔량·드롭 생성 경로를 최명기·강민서와 맞춘다. 최신 사용자 요구의 채집→드롭→E키 지급 흐름을 보존한다.
4. 두 플레이어로 접속해 준비 후 InProgress에서 드롭 생성·각자 획득·같은 드롭 동시 요청을 시험한다.

## 8. 주의
- 채집 원본을 E키가 선택하여 Harvest를 실행하던 문제가 있어 자원 전용 선택을 추가했다. 기본 Action만 실행하는 어댑터를 단순 복구하면 같은 문제가 재발할 수 있다.
- PickUp 실제 지급은 서버에서 요청한 사람의 PlayerInventory에만 적용한다. 클라이언트가 자원 ID·수량을 지정해 지급하게 하지 않는다.
- 공용 Router의 InProgress·송신자·거리·중복 검사를 시험 편의를 위해 제거하지 않는다. 씬 전환 시 Offline에서 유지되는 NetworkManager의 Spawnable Prefabs도 확인한다.
- 다른 사람이 병행 작업 중인 파일은 현재 확인되지 않았다. 수정 전에 최신 git status와 담당자 변경을 다시 확인한다.
- 인수인계 파일만 작성했으며 김지훈에게 메시지를 전송하지 않았다.

## 2026-10-16 문서 갱신 안내
- 연결 원문의 현재/과거 상태와 입력·프리팹 안내를 정리하고 전체 pointer의 최명기 진입점을 복구했다. [갱신·검증 기록](../../../최명기/2026-10-16/문서품질-보완.md) 참조. 이번 문서 갱신으로 기능·M2/M3·담당자 합의가 완료된 것은 아니다.
