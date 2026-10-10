# 코드 컨벤션

범위: C# 스크립트 작성·수정 시 따르는 네이밍 · 구조 · Unity/Mirror 규칙. (임시안 — 팀 합의로 갱신)

## 파일 · 네임스페이스
- 코드 배치와 담당 경계는 [아키텍처](04-architecture.md)와 [담당표](CODE_OWNERSHIP.md)를 따른다. 기능별로 나누고 각 담당자가 자기 서버·클라이언트·동기화 코드를 함께 맡는다.
- `Assets/01.Scripts/Lighthouse.Runtime.asmdef`가 도메인 런타임을 포함한다. 참조는 Unity.InputSystem과 Mirror다. 리슨 서버 판정도 동일한 일반 빌드에 포함한다.
- asmdef에서 Assembly-CSharp의 타입을 참조하지 않는다. 에디터·테스트를 추가할 때는 별도 asmdef로 런타임에서 제외한다.
- asmdef 추가·참조 변경은 영향받는 담당자와 먼저 합의한다. 이미 사용자 요청에 포함된 구조 변경은 반복 승인받지 않는다.
- 파일 하나에 public 타입 하나. 파일명 = 타입명.
- 신규 네임스페이스는 `Lighthouse.{도메인}.{기능}[.{층}]`으로 두며 숫자 폴더 접두와 담당자 이름을 넣지 않는다. 예: `02.World/Weather/Net/WeatherState.cs` → `Lighthouse.World.Weather.Net`.
- 기존 타입·네임스페이스·직렬화 필드는 이번 경계 이관에서 유지한다. 특히 기존 전역 Player 타입은 파일 위치만으로 이름을 바꾸지 않는다. 이름 이관은 소비자·직렬화 영향을 확인하는 별도 작업이다. 기존 `Lighthouse.Map.Net.*` 계약 9개는 소비자가 없음을 확인하고 도메인 네임스페이스로 이관했다([아키텍처 §5](04-architecture.md#5-기존-참조-보존)).
- 에디터 전용 코드는 `Editor/` 폴더 + 별도 Editor asmdef. 런타임 코드에 `#if UNITY_EDITOR` 남발 금지.
## 네이밍
| 대상 | 규칙 | 예 |
|---|---|---|
| 타입 · 메서드 · 프로퍼티 · public 필드 | PascalCase | `PlayerHealth`, `TakeDamage()` |
| private · protected 필드 | `_camelCase` | `_moveSpeed` |
| 지역 변수 · 매개변수 | camelCase | `targetPos` |
| 상수 · static readonly | PascalCase | `MaxPlayers` |
| 인터페이스 | `I` 접두 | `IInteractable` |
| bool | `Is` `Has` `Can` 접두 | `IsDead`, `CanVote` |
| 이벤트 | 동사 과거형/진행형 | `event Action<int> HealthChanged` |
| 이벤트 핸들러 | `Handle` + 이벤트명 | `HandleHealthChanged` |
| enum | 단수형, 값 PascalCase | `Role.Mafia` |

약어도 단어로 취급한다: `UiPanel`, `NetId` (`UIPanel` X).

## 클래스 내부 순서
1. 상수 · static
2. `[SerializeField]` 필드 → private 필드
3. 프로퍼티 · 이벤트
4. Unity 메시지 (`Awake` → `OnEnable` → `Start` → `Update` → `OnDisable` → `OnDestroy`)
5. public 메서드 → private 메서드

## 서식
- 들여쓰기 4칸, 중괄호는 다음 줄 (Allman).
- `if` 한 줄이어도 중괄호 생략 금지.
- `var`는 우변에서 타입이 보일 때만.
- 주석은 영어. "무엇"이 아니라 "왜"를 쓴다. public API 중 어셈블리 경계를 넘는 것만 `///` 요약.

## Unity
- 인스펙터 노출은 `[SerializeField] private`. public 필드 금지.
- 컴포넌트 참조는 인스펙터 할당 또는 `Awake`에서 캐싱. `Update`에서 `GetComponent` 금지.
- 런타임에 `Find*` · `FindObjectOfType` · `SendMessage` 금지.
- `UnityEngine.Object`에 `?.` `??` 사용 금지 (파괴된 객체 판정이 안 됨). `== null` 또는 `if (obj)`.
- `Update` 계열에서 할당 금지: LINQ, 문자열 결합, `new` 컬렉션, 람다 캡처.
- 매 프레임 필요 없으면 `Update` 대신 이벤트 · 코루틴 · `Awaitable`.
- 입력은 Input System만 (`InputSystem_Actions`). 레거시 `Input` 클래스 금지.
- 매직 넘버 · 밸런스 값은 `[SerializeField]` 또는 ScriptableObject로 뺀다.
- 로그는 `Debug.Log($"[{nameof(ClassName)}] ...")` 형식. 커밋 전 디버그용 로그 제거.

## Mirror
- `[Command]` 메서드는 `Cmd` 접두, `[ClientRpc]`는 `Rpc`, `[TargetRpc]`는 `Target`.
- `[SyncVar(hook = nameof(OnXxxChanged))]` — 훅은 `OnXxxChanged(T oldValue, T newValue)`.
- 서버 전용 메서드에 `[Server]`, 클라 전용에 `[Client]`를 붙인다.
- `Cmd`의 인자는 신뢰하지 않는다. 서버에서 권한 · 범위 · 쿨타임을 검증한다.
- 역할 · 투표 결과 등 숨겨야 할 정보는 `SyncVar`/`ClientRpc`로 전체에 보내지 않는다. `TargetRpc`로 해당 클라에만.
- 연속 값(위치 등)은 `NetworkTransform`에 맡기고 직접 Rpc로 매 프레임 보내지 않는다.
