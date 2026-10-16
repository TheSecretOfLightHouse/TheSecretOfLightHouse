# 인수인계 — 김지훈 Interaction·Network 연결 확인

작성일: 2026-10-16
갱신일: 2026-10-16
브랜치·기준: feature/myeongki / HEAD 5828650 + 최신 작업 트리
작성자: 최명기
수신자: 김지훈
상태: 현재 폴더 정적 재대조 완료. 컴파일·원격 검증·계약 확인 대기. 외부 전송 없음.

## 1. 목표
- 최신 Player 입력과 Interaction 연결을 공유하고 본편 World 채집·월드 준비 게이트 및 M2 검증을 조율한다.
- 게임 규칙은 [자원 채집 확정 기획](../../../design/resource-gathering-decisions.md), 상세 코드·설정은 [구현 참고](../../../최명기/2026-10-16/자원채집-구현참고.md)를 따른다.

## 2. 완료한 것 — 코드·설정 확인
| 항목 | 현재 상태 |
|---|---|
| 좌클릭 채집 | Probe가 커서 대상 LocalHarvestTestSpot.SpotId로 비네트워크 HarvestSpot 요청. 서버 HarvestInteractionHandler가 Adapter+IInteractionHandler로 시험 지점을 조회해 드롭 생성 |
| E키 입력 | PlayerPickupInput은 삭제 상태. `05.Player/Control/PlayerInteractionInput.cs`의 전역 타입이 InteractionRequested를 받아 공용 Selector.TryInteract 호출 |
| 대상 선택 | 공용 InteractionSelector가 레이어 마스크·범위·기본 Action 지원으로 최근접 대상을 고르고 요청 직전 갱신. tech 마스크8＝Interaction 레이어3,stone 루트 레이어3 |
| 서버 픽업 | stone의 DefaultAction은 PickUp. Router → PickupInteractionHandler → ResourcePickup.TryCollect → TryAdd 성공 시 드롭 제거, 실패 시 유지 |
| 개인 수량 | PlayerInventory의 Owner 동기화·이벤트와 InventoryDebugPanel 코드 있음. 원격 표시 검증과 구분 |

**이전 ‘Selector 미사용·PlayerPickupInput 자원 전용’ 설명은 과거 구성이다. 현재는 Selector를 사용한다.** Handler는 계속 서버에서 기능을 실행하며 로컬 선택과 역할이 다르다.

같은 이름의 `03.Interaction/Relay/View/PlayerInteractionInput.cs`(Lighthouse.Interaction.Relay.View 네임스페이스)도 남아 있다. tech에는 Player 영역의 전역 타입이 연결된 것을 GUID로 확인했다. 두 어댑터를 동시에 붙이지 않는다.

## 3. 완료하지 못한 것
- 본편 World: 시험 LocalHarvestTestSpot은 World HarvestSpotRegistry와 별개. 원본 파괴·잔량 차감·실패 복구·일일 원본 복구 미연결.
- Adapter 충돌: 시험 Handler와 World HarvestHost는 HarvestSpot 종류를 등록한다. 같은 실행 환경에 함께 켜면 중복 등록 실패 가능. 사용할 Adapter·Handler 구성을 세 담당자가 맞춘다.
- E키 범용 행동: 현재 선택 경로는 공용으로 바뀌었지만 작업대 등 실제 대상 레이어·Action·UI/입력 통합 검증은 남는다. 자원 원본을 E키로 채집하지 않도록 구성·회귀 시험 필요.
- 월드 준비: MapStateNet/WorldReadyTracker 신호는 있으나 TryStartMatch는 씬 로딩만 검사하는 TODO가 남아 있다. 강민서와 준비 의미·불일치/시간 초과 정책 확인 필요.
- M2: 원격 표시·동시 획득·수량 비노출·새 Selector 레이어 회귀 미검증.

## 4. 현재 저장소 상태
- 위 HEAD와 로컬 미커밋 변경을 기준으로 작성했다. 원격 develop/다른 PC 상태는 미조회.
- 기존 미커밋에는 공용 Selector, Player Probe/Handler/디버그 패널, tech/stone, MKTestScene, TagManager 변경과 PlayerPickupInput 삭제, 새 전역 입력 어댑터·LocalHarvestTestSpot이 포함된다.
- 이번 작업에서는 문서만 갱신했다. 코드·씬·프리팹·설정은 기존 상태를 보존했으며 커밋·푸시·외부 전달 없음.

## 5. 검증 현황
- L1: 코드·메타 GUID·직렬화 참조·레이어 설정·Git 상태 정적 대조.
- L2 과거 기록: 호스트 픽업 수량 0→1·드롭 제거, 잘못된 ID 거절/유지, 제거된 netId 재요청 거절. 이번 Selector/SpotId 변경을 재실행한 결과가 아님.
- Unity 컴파일·Play·빌드 및 L3/M2·M3는 이번 작업에서 미실행.

## 6. 차단 조건
- [ ] 김지훈·최명기: 새 레이어 Selector의 대상 구성·UI와 어댑터 중복 없는 연결을 검증한다.
- [ ] 김지훈·최명기·강민서: World 지점·파괴·잔량·실패 복구 계약과 단발/세션 범위를 맞춘다.
- [ ] 김지훈·강민서: 준비 게이트와 실패 정책을 확인한다.
- [ ] 김지훈·최명기: 2인 새 연결 회귀 시험을 수행한다. 인원·일정 미확정.

## 7. 다음 세션의 첫 행동
1. 최신 git status와 위 기준을 대조하고 컴파일한다. tech의 Player 입력 어댑터 GUID·Selector 마스크8,stone 레이어3·PickUp과 스폰 등록을 확인한다.
2. Offline→Lobby→MKTestScene에서 2인 InProgress 진입 후 원격 채집·드롭 표시·E키 획득을 확인한다.
3. 원본 E키 미실행, 레이어 제외/범위 밖/난파·탑승 거절, 동시 획득·잘못된 ID·제거된 대상 재요청을 시험한다.
4. Adapter 등록 충돌을 피할 본편 구성과 World 파괴/잔량/복구 계약을 맞춘다. 이후 월드 준비 지연·해시 불일치 시험을 진행한다.

## 8. 주의
- 강민서의 월드 스폰 자원은 파괴형 원본이다. 생성된 결과 드롭의 E키 획득과 구분한다.
- Selector는 레이어/기본 Action 선택이므로 ResourcePickup 전용 필터라고 설명하지 않는다. 픽업 Handler의 탑승 거절과 입력 어댑터 조건도 구분한다.
- 공용 Router의 진행 단계·송신자·거리·중복 검사를 제거하지 않는다. 이 문서로 다른 담당의 코드 변경 합의·실행 검증이 완료되지는 않는다.
- 이전 상세 시험 이력은 [10/15 전달 자료](../2026-10-15/김지훈-채집픽업-Interaction-연결.md)를 참고하되 현재 부착 안내로 적용하지 않는다.
