# 소환 연출 4단계 Additive 전환
작성자: 박태욱
작성일: 2026-10-30
상태: 기반 구현·로컬 호스트/클라이언트 검사 완료, 원격 및 실제 Timeline 연출 대기

## 요청과 결과
- 1~3단계의 임시 고정시간 종료를 보완하고, 메인 씬을 유지한 채 표시 전용 씬을 추가 로드·재생·제거하는 기반을 구현했다.
- 확정 정책: 새 입력/UI 클릭 차단, 서버 사전 승인 작업은 기존 조건으로 진행, 새 행동 요청은 거절. 로딩 실패자는 자기 연출만 생략하고 공통 Combat 전까지 보호/입력 잠금 유지. 월드·배 물리는 계속 진행한다.

## 서버 상태와 보호
- TowerMatchNet에 Preparing → Playing → Returning → Combat 및 Cancelled를 추가했다. 기존 IsSummonCinematicPlaying은 준비/재생/복귀 전체에서 true다.
- 현재 준비된 플레이어 연결 목록을 스냅숏으로 잡고 순번·송신 연결·identity·ready 상태를 검증한 준비/복귀 응답만 센다. 응답은 HashSet으로 중복 제거하며 과거 순번은 거절한다.
- 모두 준비/실패 응답을 보내거나 로딩 제한시간이 지나면 서버가 공통 재생 시각을 정한다. 개인 완료 콜백으로 전원 보호를 해제하지 않는다.
- 재생 시간이 끝나면 복귀 단계로 전환한다. 모두 복귀하거나 복귀 제한시간이 지난 후 공통 Combat 시각을 정하고, 해당 시점에 피해 보호/복구 타이머 정지를 해제한다.
- 설정 기본값은 연출10초, 로딩 제한10초, 복귀 제한5초, 재생 예고0.5초, 복귀 여유0.5초. 모두 구현 시험값이며 최종 연출 길이/밸런스 확정이 아니다.
- 연출 단계/시각/순번/씬 경로는 SyncVar이고 클라이언트는 서버 시각에 맞춰 표시한다. 실제 접속 종료 규칙은 변경하지 않았다.

## 클라이언트 로드·표시·복귀
- TowerMatchNet.Awake에서 SummonCinematicView를 연결하므로 기존 TowerMatchNet 프리팹/씬을 일괄 다시 저장하지 않았다.
- Additive 비동기 로드, 전용 카메라/PlayableDirector 검증, Manual Timeline 시간 평가, 기존 화면 복귀 후 unload를 처리한다.
- 없는 씬/제한시간 초과/잘못된 Director/평가 예외는 해당 클라이언트만 생략한다. 지연된 로드 완료는 별도 콜백에서 표시 객체를 비활성화하고 unload하여 뒤늦은 화면 전환을 방지한다.
- 카메라 Transform과 플레이어/배 물리는 변경하지 않는다. 렌더링용 Camera/AudioListener만 전환한다. Canvas/GameObject UI 상태는 일괄 종료하지 않고 Canvas 표시와 EventSystem 클릭만 잠근다. 원래 enabled 상태와 UI 선택 항목을 복구한다.
- CinematicPresentationLock은 로컬 키보드/포인터/게임패드/조이스틱을 잠시 비활성화하여 InputAction과 직접 키 조회를 함께 막는다. 직접 비활성화한 기기만 복원하고, 보호 중 새로 연결/활성화된 입력 기기도 처리한다. 다른 컴포넌트가 원래 비활성화한 기기는 임의 활성화하지 않는다.
- 승인된 작업을 키 해제 콜백이 취소하지 않도록 InteractionRelay의 연출 중 새 전송/취소를 막았다. 기존 InteractionSessions.Tick은 수정하지 않았다.

## 행동 요청/조작 변경
- InteractionRouter는 승인 결과 재전송 판정 뒤, 새 Execute 진입에서 거절하고 요청 순서를 정상 기록한다.
- 배 발사·대쉬·승하선과 직군 능력4종의 서버 실행 진입을 차단했다. 능력의 기존 요청번호 캐시/응답은 유지한다. 바다 소환/날씨 요청의 공통 권한 확인과 새 투표 변경도 보호 중 거절한다. 상태 조회·통신·세션 진행은 계속한다.
- PlayerInputReader/PlayerController는 기존 이동 입력과 대기 중 행동 플래그를 초기화한다. BoatController.Move는 잠금 중 목표속도0을 적용하지 않고 조작 갱신을 건너뛰므로 인위적인 제동을 추가하지 않는다. 부력/파도/관성은 기존 코드가 계속 계산한다.
- 수정 영역: 김지훈 공통/Interaction/Tower, 최명기 Player, 박태욱 Mafia. 사용자 4단계 승인 범위로 수정했으며 기존 staged 변경은 보존하고 커밋/인덱스 변경은 하지 않았다.

## 씬/에셋
- `Assets/00.Scenes/TaeWook/SummonCinematic.unity`: 전환 검증용 표시 씬. 기존 테스트 배치를 복제해 원점에서 떨어진 공간과 레이어2로 분리했다. Collider/Rigidbody/NetworkIdentity/NetworkBehaviour는 표시 복제본에서 제거했다. 카메라·리스너는 기본 비활성, 조명 cullingMask도 분리했다.
- `Assets/05.Animations/Boss/SummonCinematic_Placeholder.playable`: 길이10초의 빈 Timeline. 현재 화면은 정적인 배치 확인용이며 촉수 등장/카메라 컷/음향 연출이 완성된 것이 아니다.
- EditorBuildSettings는 기존 순서/활성 상태를 유지하고 위 씬만 마지막에 추가했다. 런타임/Editor asmdef와 패키지는 변경하지 않았다.
- 메인 테스트 씬은 시작부터 dirty=true였으며 저장하지 않았다. 구매 원본 섬과 실제 전투 프리팹도 수정하지 않았다.

## 실제 검사
- Unity 재컴파일 후 error0 및 git diff --check 통과.
- Play에서 임시 KCP 호스트와 로컬 클라이언트/플레이어 연결을 만들고 실제 NetworkIdentity로 타워를 스폰했다. 편집 원본 설정을 복제해 시험했다.
- 정상: Preparing→Playing, 추가 씬 loaded=true, 입력 잠금/보호=true, 키보드enabled=false, 메인 Camera=false/전용Camera=true, Canvas=false/EventSystem=false 확인. 이후 실제 Combat에서 추가씬 없음·메인Camera/UI 복구·입력/보호 해제·닫혀 있던 메뉴 유지·복귀응답1개 확인.
- 실패: 없는 씬 경로와 미응답 참가자 표본을 넣어 준비 대기를 확인했다. 개인 준비응답1개, 메인Camera/UI 유지, 잠금/보호 유지, Time.timeScale=1. 별도 Rigidbody가 x≈11.22m 이동하여 물리 시간 진행 확인(실제 소유 배 항해 전체 시험과 구분).
- 실패 재생 이후 Returning까지 보호/잠금과 메인 화면 유지, Combat 전환 시 보호 해제 확인. 실패 상태에서 등대 ApplyDamage가 HP를 줄이지 않음 확인. 과거 순번의 참가자 검증은 false.
- 지연 완료: 로딩 기한을 과거로 주입하고 실제 Additive 로드를 시작했다. Playing 중 late cleanup 등록=true, 연출씬 제거=true, 메인 화면 유지=true, 입력 잠금/보호=true 확인.
- 최종 복귀 보완 후 Cleanup 검사: 입력/키보드/카메라/EventSystem 복원, 원래 숨긴 Canvas 유지, 선택 버튼 복원, 닫힌 메뉴 유지 모두true.
- 초기 fixture의 비활성 벤치 NetworkIdentity는 sceneId 경고로 Play가 중단돼 결과에서 제외했다. 벤치 프리팹 에셋을 비스폰 참조로 사용하여 정상 재검사했다. 일부 후속 호출도 편집 모드 복귀 후 시험 객체를 조회해 실패했으며 기능 통과로 세지 않았다. 최종 편집 상태에서 입력 잠금=false, 키보드enabled=true, 추가 씬 없음 확인.

## 남은 작업
- 5단계: 촉수 등장 클립·실제 Timeline 컷/음향/연출 길이 제작. 현재 placeholder를 실제 연출로 교체한다.
- 6단계: 메인 촉수 숨김/최종 등장 자세/공통 Combat 후 피해 동작 정렬. 현재 메인 표현이 연출 복제본과 완전히 이어지는 것은 아니다.
- 별도 원격 클라이언트·네트워크 지연·실제 판 소환/배 항해·이미 승인한 채집/구조 세션 전체 흐름은 미검증이다. 이번 검사는 로컬 호스트/클라이언트와 격리 상태/기한 주입을 포함한다.
- 클라이언트 무한 로드 시 서버는 복귀 제한시간 이후 전투로 진행한다. 클라이언트는 메인 화면으로 복구하고 늦은 로드를 정리하도록 설계했으나 실제 저속 기기/빌드에서 재검증이 필요하다.
