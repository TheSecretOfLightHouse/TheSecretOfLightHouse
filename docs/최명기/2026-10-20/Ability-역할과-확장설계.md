# Ability 역할과 확장 설계
작성자: 최명기
작성일: 2026-10-19
갱신일: 2026-10-19
상태: 구현됨(미검증) — 액티브 전략 참조·옵저버 연결 변경, .NET 빌드 통과, Unity 실행 대기

## 요청과 목표
기존 능력 스크립트의 실제 역할과 파일별 확장 방향을 설명한다. 직군 세부는 D06·R17, 소환 후 잔여 효과는 R23·K07 결정 범위를 유지한다. 아래 새 타입은 제안이며 팀 API 합의가 아니다.

## 진행 과정과 결정 이유
- 2026-10-19: Abilities 전체와 Controller의 호출 지점, PlayerHFSM·MoveConfig·PlayerAnimationSource를 다시 읽었다.
- 이전 대화에서 작성했다고 보고한 2026-10-18/직군스킬-구현준비.md는 현재 체크아웃에 없다. 복구·삭제 원인을 추측하지 않고 이번 확인을 새 원문에 남긴다.
- UseAbilityState 참조 검색과 HFSM 코드에서 연결을 찾지 못했다. AnimationSource는 능력 이벤트를 구독하지만 Resolve에서 능력 상태를 사용하지 않는다. 따라서 능력 상태·애니메이션이 연결 완료됐다고 보지 않는다.

## 현재 역할
| 기존 타입 | 역할 |
|---|---|
| IPlayerAbility | 능력 실행·진행·완료·취소·이동 보정 인터페이스 |
| PlayerAbility | MonoBehaviour 기반 구체 능력의 추상 부모 |
| PlayerAbilityRunner | 한 액티브 실행, CanExecute·Begin·Tick 호출, 완료/취소 이벤트·이동 보정 제공 |
| IPlayerAbilityContext | Actor와 IsDowned를 실행 객체에 제공 |
| IPlayerAbilitySource | 외부 소비자가 실행 상태·이벤트를 관찰하는 계약 |
| PlayerAbilityKind | None만 있는 미완성 구분 값 |
| PlayerController | 입력 소비·직군 능력 참조·Runner 호출, 난파/교체 등 취소·이동 보정 합성 |
| UseAbilityState | 주입받은 MoveConfig를 반환하는 상태. 현재 연결 미발견 |
| PlayerAnimationSource | 능력 이벤트로 재평가하지만 현재 능력 애니메이션 선택 없음 |

## 확장 권장안
1. 기존 Runner는 소유 클라이언트의 실행 수명과 표현을 유지한다. CanExecute는 로컬 사전 검사이며 서버 승인으로 간주하지 않는다.
2. PlayerController의 입력 직후 TryExecute를 요청 제출로 바꾼다. 승인 수신 시 아직 유효한 실행인지 확인하고 Runner를 시작한다. 요청 대기·실행 ID·종료 버전으로 중복 승인/늦은 승인/소환 후 재시작을 차단한다.
3. Abilities/Server에 PlayerAbilityService를 추가해 직군·상태·대상·서버 시간·사용 횟수 검증과 실제 효과·중복 방지를 맡긴다. 선딜 완료도 서버가 관리한다. 실패/취소 비용 정책은 확정 기획에 맞춘다.
4. Abilities/Net에 전송 어댑터를 두되 신규 요청은 NETWORK_AUTHORITY 2.2에 따라 김지훈과 경로를 확인한다. 대상 상호작용은 JH02를 중복 구현하지 않는다. 개인 결과와 공개 연출 수신자를 구분한다.
5. Rules에 AbilityDefinition/JobDefinition과 순수 조건 판정을 추가한다. 정의와 플레이어별 실행 상태를 분리한다. 기존 인터페이스 문맥에 모든 도메인 의존성을 모으지 않는다.
6. 패시브는 별도 PlayerPassiveController가 적용/해제를 관리한다. 재장전·채집 속도 보정은 계산 지점에 연결하고 지도 면역은 정보 필터로 연결한다. 배 이동 권위는 기존 소유 클라이언트 방식을 유지한다.
7. 애니메이션은 PlayerAbilityKind 또는 별도 표현 키로 선택한다. 시작/완료 이벤트만 연결된 현재 상태를 구체 모션 재생으로 확장한다. HFSM에 UseAbilityState를 추가할지는 행동 제한 필요에 따라 결정하고 기존 Runner 이동 보정과 중복시키지 않는다.

## 변경 파일과 결과
이 기록과 담당자 pointer만 갱신. 코드·씬·공유 기획 변경 없음.

## 검증
실제 파일 읽기·타입/호출 검색·git 상태 확인·diff 공백 검사. Unity 컴파일/Play/다인 실행 미실행. 위 구조는 정적 설계다.

## 남은 일과 차단 조건
직군 세부·배정·소환 잔여 효과·네트워크 경로 합의 필요. 현재 구체 PlayerAbility 파생 구현은 Assets/01.Scripts 검색에서 발견하지 못했다.

## 다음 행동
첫 능력 규칙 결정 → 서버 요청/승인과 실행 ID → 구체 능력 하나 → 패시브 → 취소·중복·호스트/원격 검증 순으로 진행한다.

## 액티브 전략 소유와 옵저버 연결 변경 — 2026-10-19
- 사용자 요청으로 PlayerController의 _jobAbility 필드와 SetJobAbility 메서드를 PlayerAbilityRunner로 옮겼다. Runner가 전략을 보관·교체하며 교체 시 실행 중 능력을 취소한다.
- Controller는 기존 로컬 입력/난파/활성 검사 후 AbilityRequested(PlayerController) 이벤트를 발행한다. Runner는 OnEnable에서 구독, OnDisable에서 해제·취소하고 이벤트를 받아 현재 전략을 실행한다.
- 기존 Controller의 Tick·이동 보정·승하선/난파 취소 흐름은 유지했다. 서버 승인 경로는 새로 구현하지 않았으며 기존 로컬 실행을 이벤트 방식으로 이관했다. 권위 효과 구현 완료가 아니다.
- 검색 결과 SetJobAbility 외부 C# 호출은 없고 프리팹의 기존 _jobAbility 8개는 모두 null이었다. 씬·프리팹·에셋 YAML은 수정하지 않았다. 앞으로 Inspector의 Job Ability는 Runner에서 연결한다.
- 변경 코드: Assets/01.Scripts/05.Player/Control/PlayerController.cs, Assets/01.Scripts/05.Player/Abilities/PlayerAbilityRunner.cs. 기존 사용자 패시브·BoatCannon·프리팹·Animator 변경은 보존했다.
- 실제 검증: dotnet build Lighthouse.Runtime.csproj --no-restore --verbosity quiet 성공(오류 0, 경고 7: 참조 버전 충돌 및 WorkbenchScreen 미할당 필드), 호출부/구독 해제/diff 정적 점검, git diff --check.
- 미검증: Unity 프로세스 미검출, Unity 컴파일·Mirror Weaver·Play·다인 실행 미실행. 재활성화 후 단일 실행, 전략 교체/난파 취소, Inspector 연결 회귀가 필요하다.

## 정비공 대포 재장전 패시브 — 사용자 결정과 최소 구조
- 2026-10-19 사용자 결정: 정비공이 탑승 중일 때 재장전 패시브가 지속 적용된다. 횟수 제한과 별도 사용 입력은 없다. 이는 기존 미확정 기술자 후보를 정비공 패시브로 구체화한 이번 작업의 최신 결정이다. 직군 전체 명칭 변경·다른 능력 확정으로 확대하지 않는다.
- 정적 확인: BoatController.ReloadDuration은 기본 _reloadDuration(현재 초기값 2초)을 반환한다. BoatCannon.CmdRequestFire는 서버에서 송신자·탑승·난파·다음 발사 시간을 검사하고 `_nextServerFireTime = NetworkTime.time + _boat.ReloadDuration`으로 발사 간격을 설정한다.
- 권장: PlayerAbility/Runner를 거치지 않는다. 플레이어별 직군/패시브 조회 컴포넌트(제안명 PlayerPassiveController)가 서버 직군 상태와 현재 탑승 배를 확인해 재장전 시간 배율을 제공한다. 비적용 시 1을 반환한다. 정비공 여부를 클라이언트 요청 인자로 받지 않는다.
- BoatCannon은 기존 서버 발사 검사 통과 후 최종 재장전 시간 `baseDuration * durationMultiplier`를 계산해 다음 발사 시각에 사용한다. BoatController의 기본 수치는 수정하지 않는다. 예시 0.8배는 20% 시간 단축이며 확정 수치가 아니다. 재장전 속도 20% 상승과 구별한다.
- 이 패시브에 한해 별도 Net 요청·Runner 실행·Begin/Cancel·매 프레임 Tick은 필요 없다. 승하선 이벤트에서 기본값을 곱하거나 나누지 않고 발사 시 현재 서버 상태를 조회해 승하선 반복 시 누적을 막는다. 기존 소유 배만 탑승하는 제약은 유지한다.
- 소환 후 직군 비활성화 규칙도 조회 조건에 포함한다. 실제 직군 배정·소환 상태 제공 계약은 구현 전 확인해야 하며 있다고 가정하지 않는다. 직군 상태가 아직 없다면 서버에서 설정하는 시험용 직군 값과 본편 배정 연결을 명시적으로 구분한다.
- 미정: 단축 배율, 재장전 도중 승하선/직군 비활성화 시 진행 중 재장전 처리. 최소 변경 추천은 발사 순간 배율로 해당 재장전 종료 시각을 고정하고 이후 발사부터 새 상태 적용이다. 하선 즉시 남은 시간을 재계산하는 안은 추가 결정·시간 추적이 필요하다. 아직 승인된 규칙은 아니다.
- 검증 계획: 일반 직군과 정비공의 서버 발사 간격 비교, 연타 차단, 승하선 반복 비누적, 난파·소환 이후 차단/해제, 호스트·원격 동일 효과. 이번에는 코드 읽기와 구조 설계만 수행했다.

## 엄호 태세 액티브 설계 — 2026-10-19
- 사용자 결정: 가까운 배 하나 지정, 본인 배 허용, 10초 동안 몬스터에게 받는 피해 50% 감소. 직군/사용거리/쿨다운/횟수/중복/시전자 상태와 이미 부여된 효과의 수명 정책은 미정.
- 확인: BoatDurability.ApplyDamage(float)는 피해 출처를 구분하지 않음. ReefCollisionDamage와 BoatMonsterTarget이 같은 API 사용. 전체 피해에 0.5배를 적용하면 암초까지 감소하므로 요구와 불일치.
- 제안: CoverStanceAbility(기존 PlayerAbility 전략), 서버 요청 어댑터, 배의 BoatDamageModifiers(출처별 감소·서버 종료시각)로 분리. 기존 Runner는 입력/요청 대기/짧은 발동 표현까지만 관리하고 대상 배의 10초 효과 수명과 분리한다.
- 서버가 대상 netId·거리·직군·난파·단계·재사용 제한을 검증해 효과 부여. 피해 직전 서버 시각으로 만료 검사. 피해 출처를 도입하고 기존 일반 ApplyDamage 호출은 미분류로 유지, 몬스터 경로만 명시적으로 분류하는 방안. Monsters 코드 변경은 박태욱과 경계 확인 필요하며 이번에는 수정하지 않음.
- 다음: 사용거리·선택 방식·횟수/쿨다운·재적용 정책 결정 후 대상 배 효과와 서버 피해 시험부터 구현. 정적 코드 읽기만 수행, 실행 검증 없음.

## 2026-10-25 직군 스킬 현황 재확인
- 요청: 최명기 사용자 확인. 캐릭터 직군 능력의 현재 구현과 다음 작업 설명. 구현 변경 요청으로 확대하지 않음.
- 기준: feature/myeongki / 7a7b748. 기존 Build Profile 수정과 미추적 10/24 기록 보존.
- 확인 범위: Abilities 전체 C#, PlayerController 호출, BoatCannon/BoatDurability/암초/몬스터 피해 경로, Player 프리팹 직렬화 참조, v4 3.2~4.2, 시스템/기술 설계, 승패 결정, R17/R23 원문과 대장.
- 액티브: 입력→Controller 이벤트→Runner 실행/취소 틀 존재. 전체 Assets/01.Scripts에서 PlayerAbility 파생 구현·SetJobAbility 외부 호출 미발견. Player 프리팹 8개의 _jobAbility는 null. 구체 액티브·서버 승인/효과 경로는 미구현.
- 패시브: PlayerPassiveController·ReloadPassive 구현, BoatCannon 서버 발사 시 배율 조회 연결. Tech 1 프리팹에 참조·0.5배 설정 존재. 직군 배정에 의한 부여/회수 완료는 확인되지 않음. SetJobAbilityEnabled 외부 호출 미발견으로 소환 시 차단 연결은 남음. 0.5는 현재 설정이며 확정 밸런스로 단정하지 않음.
- 엄호 태세: 이전 사용자 결정(근처 배 하나·자기 배 허용·10초·몬스터 피해50% 감소) 유지. BoatDamageType 및 Monster/Reef 호출 분류는 현재 존재하지만 ApplyDamage 내부는 damageType을 소비하지 않고 그대로 차감. CoverStanceAbility/BoatDamageModifiers는 검색에서 미발견.
- 다음 권장: 서버 피해 계산에 몬스터 전용 임시 감소/만료를 먼저 구현→엄호 선택·거리·재사용/중첩·취소 규칙 확정→서버 요청 검증 및 구체 액티브→직군/소환 연결→호스트·원격 회귀. R17 배정과 R23 잔여 효과 정책은 미확정 유지.
- 실제 검증: 파일/호출/직렬화 텍스트 읽기와 git 상태 확인만. Unity 프로세스 조회 결과 없음. 컴파일·Play·Mirror Weaver·M2는 이번 조사에서 미실행. 에디터 접속 도구로 연결 가능 여부까지 확인한 것은 아님.
- 변경: 본 원문과 담당자 pointer의 현황 기록만. 게임 코드·씬·프리팹 수정, 커밋 없음.
