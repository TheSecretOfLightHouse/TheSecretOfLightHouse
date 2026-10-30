# BC2 몬스터 부력 연결
작성자: 강민서 작업 기록
작성일: 2026-10-30
갱신일: 2026-10-30
상태: 구현·호스트 로컬 조건부 통과, M2/4인·전투 종단·성능 검증 대기
브랜치·기준 커밋: feature/minseo / 0a52a02c (기존 미커밋 변경 다수)

## 요청과 현재 결정
- 사용자 요청: BC2 몬스터 부력 연결 시작.
- 이번 대화에서 Monsters 담당 영역의 프리팹·Spinocroc 수정 합의를 확인했다. MosaWhale도 이번 범위에 포함한다.
- 종별 잠김 수치는 사용자 위임으로 조정 가능한 가안으로 준비한다. 확정 밸런스가 아니다.
- **앞선 조사 시점의 Unity Play 유지**가 사용자 지시였다. 이후 사용자가 Play 중지·구현 진행을 요청했으며 아래 재개 결과가 현재 상태다. 소스 반영·재컴파일·프리팹 저장·Play 종료는 하지 않았다. 구현 완료가 아니다.

## 조사 과정과 실제 결과
- WORK_POINTER → 강민서 POINTER → BC3 결과·부력 재설계 원문·단계별 인수인계를 읽었다.
- 소유권·CODEOWNERS·코딩 컨벤션·서버 권위·씬 안전·검증 절차와 관련 설계를 대조했다.
- Unity CLI 최신 확인: 1.0.0-beta.15, 설치 버전과 최신 버전 동일. Unity 6000.0.83f1 연결 ready.
- Unity eval로 **프리팹 에셋**을 읽었다. 런타임 인스턴스 값으로 혼동하지 않는다.
- 7종 모두 질량 1, 중력 false, constraints=84(FreezePositionY + FreezeRotationX/Z), BuoyancyObject 없음.
- 프리팹의 isKinematic은 Spinocroc만 true, 나머지 false. MonsterBase.SetPhysicsAuthority가 서버 실행 조건에 따라 바꾸므로 프리팹 값만으로 서버 물리를 단정하지 않는다.
- MainGame Play 중, 관측 시 frame=30698, 씬 dirty=false. 이 값은 BC2 플레이 시험이 아니다.

| 프리팹 | 실측 충돌체와 루트 스케일 |
|---|---|
| Angler | Capsule center(0,0.4,-0.3), height2.64, radius0.8, Z방향, scale1 |
| BlueMarline | Capsule center0, height1, radius0.5, Y방향, scale4 |
| BlueShark | Capsule center(0,0.2,0), height1, radius0.5, Y방향, scale4 |
| BubbleTurtle | Capsule center(0,0.15,0), height1, radius0.5, Y방향, scale3 |
| Manta | Capsule center0, height2, radius1, Z방향, scale2 |
| Spinocroc | Box center(0,0.25,0), size(0.3,0.3,1), scale20 |
| MosaWhale | Box center(-0.01,0.01,-0.15), size(0.5,0.42,1.08), scale20 |

## 기획·코드 대조와 연결안
### 공통 연결
- MonsterMotion.SetHorizontalVelocity는 기존 Y속도를 보존한다. 수평 AI 전체 교체는 불필요하다.
- BuoyancyObject는 양의 목표 깊이만 허용하고 IsAtTargetDepth는 MeanDepth 차이를 검사한다.
- 일반 5종은 기존 수평 이동을 유지하고 중력 켜기·Y 고정 해제·부력 연결부터 적용한다. 회전 X/Z 고정과 질량 1은 유지한다.
- 가안: 기본 수면형 부력비 2, 수직항력 5부터 시작하고 종별 값은 인스펙터에 둔다. Angler·Manta의 완전 잠김 여부는 가안으로 시험한 뒤 외형/공격 판정과 함께 조정한다.
- 부력점은 실제 고체 충돌체 경계에서 생성하고 서로 다른 루트 스케일을 반영한다. 큰 감지 트리거는 제외한다.
- Drift 풀 기본 용량 90점, Ship 풀32점. BC3 Player는 4명×3점=12점이다. 몬스터당 점 수와 실제 최대 동시 수를 확인한 후 용량을 정한다. 7종 존재가 동시 7마리를 뜻하지 않는다.
- WaveDriftBody는 IShipBody가 필요하다. WaterBody를 붙이면 WaveSlamSource와 기본 보트 부력점·COM도 함께 들어오므로 무조건 복사하지 않는다. 몬스터 어댑터 또는 WaterBody의 부수 추가 조건 분리를 검토한다.
- 현재 WaveDriftBody에는 깊이 감쇠가 없다. 파도 표류에 깊이 감쇠를 연결하고 배 회귀를 확인해야 한다. 질량1에 기본 ReferenceMass500을 적용한 표류 강도는 별도 실측 대상이다.

### Spinocroc (Monsters / 박태욱)
대상: Assets/01.Scripts/06.Monsters/AI/Spinocroc/Server/SpinocrocPreparation.cs
- SetHeight/MovePosition 절대 높이 조작을 SetTargetDepth로 바꾸고 준비 시작 시 기본 깊이를 보존한다.
- Diving은 목표 깊이 도달 + 실제 완전 잠김 확인 후에만 재등장 위치로 이동한다. 기존 시간만으로 이동하면 물 위에서 순간 이동할 수 있다.
- MeanDepth는 점별 깊이의 평균이므로 IsAtTargetDepth만으로 거대한 몸 전체 잠김을 보장하지 않는다. 가장 높은 고체 경계/부력점도 확인한다.
- 순간 이동은 목적지 수면 기준 깊이를 유지하고 속도·표본 유효성을 재설정한다. 이전 위치의 표본으로 힘을 가하는 프레임을 시험한다.
- Reappearing은 부력 목표를 기본 깊이로 복원하고 도달 후 Windup으로 넘어간다. Cancel은 절대 Y복귀 대신 목표 복원, 기존 제약 복원으로 Y고정이 돌아오지 않게 한다.
- DiveDuration·ReappearDuration을 도달 조건으로 바꾸면 SpinocrocAnimation의 시간 기반 표현도 대조해야 한다. 표본 실패·지형 걸림 시 영구 대기하지 않는 실패 경로를 둔다.

### MosaWhale (Monsters / 박태욱)
대상: Assets/01.Scripts/06.Monsters/AI/MosaWhale/Server/MosaWhaleMovement.cs 및 실제 단계 소비자
- Patrol/Diving/Telegraph/Recovering은 현재 position.y와 Y속도를 직접 덮어쓴다. 단순 부력 부착 시 이중 제어가 된다.
- 순찰·잠수 목표를 부력 입력으로 연결하고 phase 전환의 도달 조건과 순간 이동 은폐를 함께 확인한다.
- **추가 설계 공백:** 기본 API는 양의 수중 깊이만 지원한다. MosaWhale의 도약은 수면 위 높이가 필요하며 기존 머리 궤적·피해 창이 결합돼 있다.
- 수중 단계는 공통 부력, 도약은 기존 공격 궤적이 제어하는 방안과 공통 API를 수면 위 제어까지 확장하는 방안을 비교 후 구현한다. 이번 세션에서는 예외 정책을 확정하거나 공격 궤적을 바꾸지 않았다.
- 공격 중 궤적을 유지한다면 중력·부력·표류의 제어권 인수/복원과 취소·사망·서버 종료를 모두 검사해야 한다. 부력을 끄는 예외는 10/28 합의와의 차이를 명시해야 한다.

### 문제 대장
- N08: 같은 파도와 같은 물리 결과는 별도 계약, 현재 계약 확인 필요. 서버만 물리 계산·클라이언트 Kinematic과 동기화 위치를 각각 검증한다.
- W17: 일반 몬스터 안전지대 규칙은 부분 결정·구현, 실제 Play/M2 미검증. 부력 연결로 보호 대상 추적·공격 취소 조건을 바꾸지 않는다. 보스 예외를 새로 정하지 않는다.

## 변경 파일과 검증
- Tools/World/AuditBC2MonsterBuoyancy.cs: Assets 밖의 읽기 전용 Unity eval 스크립트. 에셋/런타임 수정·파일 저장 없음.
- 본 기록 및 강민서 POINTER에 적용 대기 상태를 기록한다.
- 실행한 확인: git 미커밋 상태, 실제 타입·호출자 정적 대조, Unity CLI 버전/연결, 프리팹 값·충돌체·Play 상태 읽기.
- 미실행: 런타임 소스 반영, 프리팹 연결, 재컴파일, 부력·잠수·도약 시험, M2/4인, 성능·GC·풀 최대용량. 현재 Play 유지 지시를 우선한다.
- 기존 Ocean/Clock·프리팹·Mirror·개인 pointer 등 다른 변경은 보존했다. 커밋·push 없음.

## 다음 행동
1. 현재 Play 유지 지시가 해제된 뒤 본 기록과 소스 차이를 다시 확인한다. 자동으로 Play를 종료하지 않는다.
2. 일반 5종/Spinocroc/MosaWhale의 제어 계약을 나누고 MosaWhale 도약 제어 방식을 정리한다.
3. 공통 깊이·표류 연결과 Spinocroc 도달 조건을 구현하고 Unity API로 프리팹7종을 편집한다(YAML 직접 편집 금지).
4. 컴파일 → 호스트 수면/잠수/취소/사망 → MosaWhale 도약·피해창 → M2 → 최대동시수·풀·GC 순으로 검증한다.

## 관련 원문
- [부력 재설계](../2026-10-25/범용부력과-자연파도-재설계-검토.md)
- [BC3 결과](../2026-10-29/BC3-난파-플레이어-부력-연결-결과.md)
- [설계 문제 대장](../../07-design-issue-register.md)

## 점검 도구 실행 확인
- Unity eval_file로 AuditBC2MonsterBuoyancy.cs 실행 성공, diagnostics 없음, 7종 모두 출력. BuoyancyObject/WaterBody/WaveDriftBody 없음 재확인.
- 실행 후 playing=true, frame=41601. 앞 관측30698보다 진행했으며 사용자 Play를 종료하거나 일시정지하지 않았다.
- 이는 읽기 전용 도구 실행 확인이다. 게임 런타임 재컴파일·BC2 물리 동작 검증은 아니다.
- 담당자 pointer diff --check 통과. 연결안은 문서이며 적용 가능한 런타임 패치나 프리팹 변경 스크립트는 아직 작성하지 않았다.


## 2026-10-30 재개 — 구현·실행 결과 (현재 상태)
사용자가 Play 중지를 알리고 구현·시연·커밋을 요청했다. 앞의 미적용 기록은 최초 조사 이력이며 이 절이 현재 상태다. 작업 중 저장소 HEAD가 병행 병합으로 1766ed00까지 바뀌었고 해당 변경을 보존했다.

### 구현과 결정 이유
- `MonsterWaterBody`는 `IShipBody`를 구현하는 Monsters 어댑터다. 서버 초기화·생존·동적 물리 상태에서만 공통 부력·중력·표류를 켠다. 보트 전용 Slam/복원 코드를 붙이지 않는다.
- 7종 프리팹 모두 중력 true, Y고정 해제(constraints84→80), 질량1 유지. 기존 MonsterBase가 서버 동적/클라이언트 Kinematic 권위를 계속 결정한다.
- 실제 고체 충돌체 경계의 하단·중앙·상단 3점, Drift 풀을 사용한다. 목표 깊이는 **부력점 평균 위치 기준**이다. 가안: Angler2m·Manta3m, 수면형4종(BlueMarline/BlueShark/BubbleTurtle/Spinocroc)은 목표 깊이 없음·부력비2, MosaWhale은 기존 순찰 루트 깊이10m에 중심 오프셋을 보정한9.7817m.
- 선택적 `BuoyancyProfile.TargetStrength=16`·가속 상한40m/s²·수직항력5로 깊이를 제어한다. 기본 TargetStrength0은 기존 부력식을 사용한다. 깊이 제어 사용 때 MeanDepth는 부호와 깊이를 보존한다.
- WaveDriftBody의 선택적 깊이 감쇠 거리4m를 몬스터만 켰다. 깊어질수록 파도 표류/외력 영향이 지수 감쇠하며 기존 배 프로필은 기본0이다.
- Spinocroc의 절대 Y변경을 제거했다. 최소 연출 시간과 깊이 도달·상단 잠김 확인을 모두 만족해야 순간 이동하고, 목적지 수면 기준 깊이로 이동한 뒤 표본을 무효화한다. 취소는 목표 깊이를 복원한다. 전환 제한15초.
- 마지막 검토에서 잠김 검사를 부력점뿐 아니라 **각 고체 충돌체 AABB 상단 네 모서리**로 보강했다. 유한 표본 기반의 보수적 검사이며 렌더 메시 전체 파도 교차의 수학적 증명은 아니다.
- MosaWhale 수중 순찰/잠수/예고/회복은 부력이 Y를 제어한다. **도약·정점·재입수 구간은 기존 공격 궤적이 제어하므로 부력·중력·표류를 일시 중단하고 복원한다.** Spinocroc의 부력을 끄지 않는 합의는 유지한다. MosaWhale 도약은 추가된 예외이며 피해 곡선·수면 위 도약 높이를 보존하기 위해 선택했다.
- MosaWhale 단계도 깊이 도달을 기다리며 시간 초과는 `WaterUnavailable`로 공격 취소·순찰 복귀한다. 기존 enum값 뒤에 추가해 기존 번호를 보존했다.
- 최초 시험의 Awake 시 NetworkIdentity 접근 예외와 MosaWhale 종료 경로 컴파일 오류를 수정하고 재시험했다. 최종 시험에는 새 경고·오류가 없었다.

### 실제 실행 검증
| 검증 | 결과와 범위 |
|---|---|
| 컴파일 | 최종 Unity6000.0.83f1 compilationFailed=false. 최종 전용 시험 콘솔 오류0·경고0 |
| 에셋 재조회 | 7종 중력 true·constraints80·부력/표류/어댑터 존재, 새 스크립트/씬 meta 확인 |
| 전용 씬 호스트 | 실제 Mirror NetworkManager.StartHost·NetworkServer.Spawn, 7종 IsInitialized=true·서버 동적 물리 |
| 기본 파도 | 7종 유효 표본·슬롯, 수면형 잠김 비율 약0.52~0.56, Angler/Manta 목표 깊이 근처 유지 |
| 보스 전환 1차 | 959관측. Spin 잠수→재등장→돌진 준비, Mosa 잠수→예고→도약→재입수→회복, 시간 초과 없음 |
| 큰 파도 1차 | 진폭3m+1.5m·빈 실제 ForceFieldRegistry의 경사 표류, 1001관측·두 보스 전환 통과. Mosa 최저-28.83/최고9.76m |
| 상단 모서리 보강 후 재시험 | 동일 큰 파도,1183관측. Mosa Diving4.528→Telegraph8.444→Rising10.445→Apex13.447→Reentering13.652→Recovering16.457→회복 도달22.263초. Spin 준비 도달15.588초. stage6·spinDone=true·timedOut=false. 이후 콘솔 새 경고/오류0 |
| 제어권/사망 | Mosa scripted 모드 중 중력/부력/표류 off 및 복원 확인. Angler 실제 TryApplyDamage로 사망 후 슬롯 해제·중력 off 확인 |
| 풀 경계 | 7종×3=21점 점유 중 남은69점 할당 성공·추가1점 거부·반납 성공. 실제 최대 몬스터 수와 다른 사용자 합산 부하는 미검증 |
| 메인 씬 시연 | MainGame 원본 dirty=false 유지. 시연용 기본 NetworkManager 호스트·7종 런타임 배치·카메라, Mosa 도약 화면 캡처 관찰 |
| 이동/물살 | BlueShark 실제 MonsterMotion.BeginSmoothMovementLeg/StepSmoothMovement, 설정3m/s·관측수평속도2.993m/s. WaterContactEffectsView 추적 중심을 런타임 상어로 옮겨 접촉1·시뮬레이션 Step 증가·거품/잔물결 화면 관찰 |
| 정적 점검 | 이번 C# diff --check 통과. 새 FixedUpdate 경로에 매 프레임 컬렉션/LINQ 추가 없음. GC 실측은 아님 |

**시험 한계:** BT를 중지하고 테스트 도구가 보스 단계 진입을 주입했다. 생산 단계 핸들러·Rigidbody·Mirror 서버·공통 부력 계산은 실제 실행하지만 실제 보트 탐지/대상 선택/피해 적용/전체 BT/자동 스폰 종단을 검증한 것은 아니다. 호스트 로컬 클라이언트는 별도 원격 클라이언트가 아니다. 깊이 모드 전환/순간 이동 직후 재표본 대기 중 무효 표본 1회를 관측했으며 그때 힘을 적용하지 않는다.

### 시연과 재현
- 전용 씬: `Assets/00.Scenes/Minseo/BC2/MonsterBuoyancyChecks.unity` (게임 빌드 목록 변경 없음).
- 전용 씬 Play → `Tools/World/StartBC2Checks.cs`를 Unity eval_file로 실행 → `RunBC2Scenario.cs` 실행 → AppDomain의 `BC2.Scenario`에서 done/stage/spinDone/timedOut/events/samples를 읽는다. 기본은 1회 시험, PreviewLoop는 시연 때만 켠다.
- `AttachBC2MonsterBuoyancy.cs`는 edit모드에서만 실행해 7종 입력을 재현한다. `AuditBC2MonsterBuoyancy.cs`는 읽기 전용이다.
- `ShowBC2Movement.cs`는 MainGame의 BC2 런타임 배치가 있을 때만 사용한다. 시연 종료 후 객체가 사라지는 것을 정상으로 취급한다.
- MainGame 시연은 기존 1인 투표 초기화 오류를 피하도록 런타임 시험용 기본 매니저를 썼고, 기존 HUD/자동 몬스터 스폰을 시연에서 중지했다. 본편 정상 접속·승패 검증이 아니다. 씬·기존 게임 설정에 저장하지 않았다.

### 변경 파일과 남은 검증
- 공통: Ocean/BuoyancyObject.cs, Rules/BuoyancyProfile.cs·BuoyancySolver.cs, WaveDriftBody.cs.
- Monsters: Shared/Server/Components/MonsterWaterBody.cs(+meta), SpinocrocPreparation.cs, MosaWhaleMovement.cs·MosaWhaleCycle.cs·MosaWhaleStopReason.cs, Monsters 프리팹7종.
- 검증: BC2 씬(+meta/폴더meta), Tools/World의 Audit/Attach/Start/Run/Show BC2 도구, 본 원문·pointer·인수인계·기준 설계/지식맵 연결.
- **남음:** M2·4인/별도PC 서버·원격 위치와 슬롯, 실제 보트 대상의 전투 종단·지형 걸림/표본 실패 시간 초과, 배·Player 부력/구조/승하선 회귀, 최대 동시 수·장기 반복·프레임 비용·GC. 기존 Spin 애니메이션은 최소 시간 뒤 마지막 포즈를 유지하는 구조이며 실제 네트워크 연속성은 미검증.
- 기존 Clock·WaterSampler/F3·Player·지도·MainOffline·Mirror·다른 작업 기록 변경은 BC2 커밋에서 제외한다. MainOffline의 startPlayerCount4→1은 이번 세션에서 편집/저장하지 않았으며 되돌리지 않는다.
- 사용자 요청으로 BC2 구현 범위만 로컬 커밋한다. push는 하지 않는다.

### 커밋 전 최종 점검
- 마지막 보강 후 시험 stage6·spinDone=true·timedOut=false,1183관측, console 오류0·경고0 재확인.
- 시연과 시험 Play를 종료하고 MainGame을 edit모드로 열어 dirty=false 확인했다. 런타임 임시 카메라·배치·추적 중심은 종료와 함께 제거됐다.
- C#/문서의 공백 검사는 통과했다. 전체 staged diff --check에서는 Unity 에디터가 생성한 YAML/meta 빈 필드 뒤 공백만 검출됐다. YAML 직접 편집 금지 규칙에 따라 유지하고 이 한계를 기록했다.
- 공동 pointer는 BC2 항목만 index에 분리하며 다른 작업 기록은 작업 트리에 보존했다.
