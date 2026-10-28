# 난파 Player 분리와 부력 전환 준비
작성자: 최명기
작성일: 2026-10-28
갱신일: 2026-10-28
상태: 구현됨(실제 Play·M2 미검증)
브랜치·기준 커밋: feature/myeongki / fae7bd8

## 요청과 목표
- 사용자 요청: 난파 시 Player와 배를 분리하고 배 부력을 끈다. 나중에 Player 부력을 붙이면 난파 중에만 켜고 정상 상태에는 끄도록 준비한다.
- 첨부 인수인계는 검토 자료다. 공통 부력 코어·수영·일반 낙수 구현까지 요청으로 확대하지 않았다.

## 진행 과정과 결정 이유
- WORK_POINTER → 최명기 POINTER → 관련 단계형 난파 원문을 확인했다. 과거 `난파-배-분리모델.md`는 현 체크아웃에 없었다.
- 난파 배 부력 정지는 이미 구현됐으나 Player가 앵커를 추종하고 구조 완료가 탑승을 요구했다. 분리와 복귀 조건을 함께 변경했다.
- 공통 BuoyancySampleSession/BuoyancyForceSolver 및 CanSimulateLocalWorld는 현재 코드에 없다. World를 수정하지 않고 Player에 선택적 Behaviour 연결점만 추가했다.
- 서버가 실제 소유/탑승을 확인하고 Downed 후 분리한다. 실패 시 Downed를 해제하고 타이머는 시작하지 않는다. 정산 원점은 난파 위치다.
- 완료 실패 시 정산 계획·결과를 보존해 1초 간격으로 재시도한다. 새 배 생성·정산·위치 복귀·배 교체의 기존 순서는 유지했다.
- CLI 업데이트 거절 및 사용자의 MCP 지시에 따라 Unity 작업은 MCP로 진행했다.

## 변경 파일과 결과
- `BoatController.cs`: TryDetachForWreck 추가. 기존 배 부력 비활성/kinematic 고정 유지. 비탑승 구조 복귀 및 원격 위치 적용 연결.
- `PlayerWreckRecovery.cs`: 난파 분리 호출, 두 완료 조건 변경, 완료 실패 재시도.
- `PlayerController.cs`: 난파 배 재탑승 차단, ApplyRecoveryPosition, 선택적 Wreck Buoyancy 슬롯과 CanUseWreckBuoyancy 추가. 정상/탑승/비소유/kinematic/로딩·종료/비활성 상태에는 끈다.
- 기술설계·본 기록·POINTER 및 [격리 검사 소스](VerifyWreckDetach.cs).
- 기존 난파 연출 프리팹 4개·Build Profile·생성 코드·문서의 미커밋 변경은 보존했다. 이번 작업에서 씬과 프리팹을 저장하지 않았다.

## 이후 부력 연결 방법
1. 실제 Player 루트에 Player 전용 부력 컴포넌트를 추가한다.
2. PlayerController Inspector의 Wreck Buoyancy 슬롯에 지정한다. 비워 두어도 분리·복귀는 실행된다.
3. 부력 컴포넌트는 비활성화 시 표본 슬롯을 반환해야 한다. World 호스트·유효 표본·월드 준비의 세부 검사는 실제 컴포넌트가 맡는다. 현재 세션 준비 검사는 세션 흐름 사용 시 InProgress 여부다.
4. 배용 BuoyancyBody/BoatShipBody/BoatBuoyancyAuthority는 Player에 붙이지 않는다.
- 현재 Player 부력 계산은 없으므로 분리 후 실제 수면 유지는 제공하지 않는다.

## 검증
- MCP editor_status: Unity 6000.0.83f1, Play 중단·연결 없음. 최종 recompile: errors=[], compilationFailed=false.
- PrefabUtility.LoadPrefabContents 격리 검사: Captain 1/Tech 1/Tanker 1/Farm 1 각 21개, 총 84개 통과. 임시 AudioSource.enabled를 부력 대역으로 사용했으며 실제 부력 계산·Play 검증이 아니다.
- 정상 부력 꺼짐, 탑승 잠금, 배 부력 정지, 난파 분리·Downed, 소유자 물리/충돌 복원, 정산 원점, 중복 시작·재탑승 거절, 앵커 추종 해제, 비탑승 복귀 위치/속도, 완료 실패·재시도, 로딩/진행/종료 전환, Downed 해제, 원격 물리 차단 확인.
- 초기 검사에서는 Mirror 초기화 누락으로 실패했다. InitializeNetworkBehaviours 추가 후 통과했다. 후속 세션 fixture의 NetworkIdentity 누락 로그도 구성 순서를 고쳐 해결했고 최종 재검사 구간의 신규 경고·오류는 0개다. 기존 콘솔 이력은 지우지 않았다.
- 임시 서버 활성 플래그·manager singleton은 finally에서 원복하고 임시 객체는 폐기했다. 종료 시 Play=false/server=false/client=false/manager=null. 열려 있던 씬은 dirty였고 저장하지 않았다.
- 실제 Play, 구조/시간 초과의 새 배 생성·정산 끝까지, 원격 SyncVar/RPC 및 이전 스냅샷 경합, M2/M3, 실제 파도 부력은 미실행이다.

## 남은 일과 다음 행동
- 실제 탑승 배 난파에서 분리·충돌·카메라와 구조/시간 초과 복귀 확인.
- Player 부력 준비 후 슬롯 연결 및 수면 높이·표본 반환 확인.
- M2 이상에서 빠른 구조·원격 복귀·중복 정산 회귀 확인.
- 관련 W11/W12/W13 잔여 계약을 해결했다고 간주하지 않는다. 구조 규칙은 최신 사용자 결정과 기존 정산 구현을 유지했다.
