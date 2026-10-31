# TheSecretOfLightHouse 작업 지침

모든 작업에 적용하는 규칙이다. 상황별 절차와 양식은 [AI 작업 문서](docs/ai/README.md)에서 찾는다.

## 세션 시작과 작업 기록

- 새 세션에서는 별도 읽기 요청을 기다리지 않고 [작업 pointer](docs/WORK_POINTER.md) → 확인된 담당자의 `docs/{이름}/POINTER.md` → 해당 작업 원문·연결된 인수인계 순서로 읽는다. 상세 절차는 [작업 기록과 재개](docs/ai/11_WORK_RECORDS.md)를 따른다.
- 첫 작업 응답에 확인한 진행 중 작업·남은 일·차단 조건·다음 행동을 짧게 알린다. 담당자가 불명확하면 필요한 정보만 확인하고, 기록이 없거나 접근할 수 없으면 그 사실을 알린다. 과거 기록만으로 작업을 실행하지 않는다.
- 의미 있는 작업을 마치거나 중단할 때 수행 과정·결정 이유·변경 파일·실제 검증·남은 일을 원문에 남기고 담당자 pointer를 함께 갱신한다. 사용자의 읽기 전용·기록 금지 요청이 우선한다.

## 프로젝트

- 저장소 루트가 Unity 프로젝트 루트다. Unity 6000.0.83f1, URP 17.0.4, Input System 1.19.0, Mirror 기반이다.
- PC 3인칭 4인 리슨 서버(호스트 1 + 원격 클라이언트 3) 등대 마피아 게임이다. [문서 읽기 안내](docs/00-reading-guide.md), [승패·투표 결정](docs/design/win-vote-decisions.md), [제공된 게임 기획 v4](docs/design/references/game-design-v4.md)와 최신 사용자 결정을 따른다. 새 결정이 v4와 충돌하면 해당 결정이 우선하며 미변경 범위는 v4를 따른다.
- 계획, 코드 존재, 실제 실행 검증을 구분한다. 파일·API·테스트·담당자를 추측하지 않는다.

## 작업별 필독

| 상황 | 문서 |
|---|---|
| C# 작성·수정 | [코딩 컨벤션](docs/coding-convention.md) — 기존 프로젝트의 유일한 코드 스타일 기준 |
| 게임 규칙·수치·흐름 | [승패·투표 결정](docs/design/win-vote-decisions.md), [게임 기획 v4](docs/design/references/game-design-v4.md), [문제 대장](docs/07-design-issue-register.md) |
| 네트워크·서버 판정 | [네트워크 권위](docs/unity/NETWORK_AUTHORITY.md), [서버·클라이언트 협업](docs/unity/SERVER_CLIENT_WORKFLOW.md) |
| 씬·프리팹·에셋·메타 | [씬·에셋 안전](docs/unity/SCENE_AND_ASSET_SAFETY.md) |
| 데모 제작 | [데모 가이드](docs/unity/DEMO_GUIDE.md) |
| 커밋·PR | [커밋·PR 컨벤션](docs/commit-convention.md)와 실제 `.github/` 템플릿 |
| 이슈·PR 게시 | `gh` CLI가 기본 경로이며 설치·로그인은 [gh 설치와 로그인](docs/unity/UNITY_START_HERE.md#github-cli-설치와-로그인) |
| 완료 보고 | [검증 플레이북](docs/ai/03_VERIFICATION_PLAYBOOK.md) |

## 기능 요청과 작업 현황의 기획·코드 대조

기능 구현·수정 또는 해야 할 일·남은 일·다음 작업·우선순위·일정·진척을 요청받으면 `review-design-code-gaps` 스킬을 먼저 적용한다. Codex는 [Codex 스킬](.agents/skills/review-design-code-gaps/SKILL.md), Claude는 [Claude 스킬](.claude/skills/review-design-code-gaps/SKILL.md)을 읽는다. 요청 범위의 시스템 디자인·게임 기획·기술설계와 실제 코드를 비교해 빠진 결정, 불일치, 그대로 진행할 때 예상되는 문제를 근거와 함께 설명한다. 구현 요청은 검토 뒤 결정된 범위의 구현까지 이어가고, 현황 질문은 분석만 수행한다.

관련 기능의 설명·규칙·대안·버그 대화에서도 위 스킬을 적용한다. [설계 문제 대장](docs/07-design-issue-register.md)의 관련 ID와 현재 상태를 확인해 근거·예상 영향·필요한 결정을 설명한다. [승패·투표 결정](docs/design/win-vote-decisions.md)은 해당 범위에서 [v4 원문](docs/design/references/game-design-v4.md)보다 우선한다. 기존 문서와의 차이는 대장 V01~V10과 새 결정 반영 상태에서 확인하며, 목록 밖 차이도 최신 사용자 결정이 우선한다. 검토 보고서의 개선안은 사용자 승인이나 확정 규칙이 아니다.

## 담당과 실행 구조

기능별 상세 범위와 협업 경계는 [코드 소유권](docs/CODE_OWNERSHIP.md)이 단일 기준이다.

- 강민서(ri-ver-1): World(바다·날씨, 맵 생성·섬·파밍, 스포너 배치, 유령선·환영 섬·토네이도).
- 김지훈(zhun0922): Network·Interaction·Lighthouse(공용 접속, 상호작용 검증·라우팅, 작업대·모듈·등대)와 4인 테스트 리드.
- 최명기(cmg6991): Player(배 조작·내구도, 직군 능력, 인벤토리, 채집 행동).
- 박태욱(awe46566): Monsters·Mafia(몬스터 AI·스폰 실행, 투표, 심해 게이지).
- 시스템(게임 진행·승패·직군 배정)은 최명기(cmg6991), UI는 김지훈(zhun0922)이 맡는다. 인벤토리 화면과 배 모듈 장착 UI만 최명기 담당이다(2026-10-30 사용자 결정).
- 김민성(AidenKim923)은 다른 팀에서 온 일시 AX 지원만 맡으며 게임 코드에는 관여하지 않는다.
- 게임 런타임은 Lighthouse.Runtime에 포함한다. 기능 내부 Rules/Net/Server/View로 역할을 구분하며 리슨 호스트가 쓰는 서버 판정을 전용 서버 빌드로 제외하지 않는다. 상세는 [아키텍처](docs/04-architecture.md).

## 지킬 것

- 한국어로 결론부터 답한다. 코드 식별자와 코드 주석은 기존 코딩 컨벤션을 따른다.
- 작업 전 관련 코드·설정과 미커밋 변경을 읽고, 다른 작업자의 변경을 보존한다.
- 코드는 기능별 도메인으로 나누며 각 담당자가 자기 기능의 서버·클라이언트·동기화를 함께 맡는다. [담당 경계](docs/CODE_OWNERSHIP.md)와 [.github/CODEOWNERS](.github/CODEOWNERS)를 먼저 확인한다. 다른 영역 수정이 필요하면 대상·담당자·이유·영향을 수정 전에 알리고 필요한 합의를 확인한다. 사용자가 이미 허용한 같은 작업·실행은 반복 확인하지 않는다.
- `.meta`는 에셋과 항상 함께 관리하며 GUID를 직접 바꾸지 않는다. Unity 에셋 이동·이름 변경은 에디터에서 수행한다. `.unity`·`.prefab`·`.asset` YAML을 직접 편집하지 않는다.
- `Assets/Mirror/`, 외부 플러그인과 구매 에셋을 수정하지 않는다. `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 작업 대상에서 제외한다.
- 본편은 Git, 구매 에셋과 데모는 SVN으로 관리한다. 로컬 체크아웃 루트는 `Assets/ThirdParty/1조 Unity Asset/`이다(저장소 `Work/1조 Unity Asset`을 폴더 이름 그대로 받는다). 경로·대용량 규칙은 [파일 관리](docs/unity/LARGE_FILES.md)를 따른다.
- 브랜치는 `feature/<이름>`, PR 대상은 `develop`이다. 승인 없는 커밋·푸시·게시를 작업 완료의 기본 단계로 추가하지 않는다.
- 실제 실행한 검증과 실행하지 못한 검증을 구별한다. 에디터 사용 가능 여부는 세션에서 확인하며, 미실행을 통과로 보고하지 않는다.
- 공유 규칙은 해당 `docs/` 기준 문서에, 본인 작업 기록은 `docs/{이름}/{날짜}/{내용}.md`에, 인수인계는 `docs/handover/{이름}/{날짜}/{내용}.md`에 둔다. 의미 있는 작업 후 원문과 담당자 pointer를 함께 갱신한다. 상세 규칙은 [작업 기록과 재개](docs/ai/11_WORK_RECORDS.md)를 따른다.
- 비밀번호·키·토큰과 개인 PC 설정은 공유 문서나 커밋에 넣지 않는다. 개인 설정은 `CLAUDE.local.md`, `.claude/settings.local.json`에서 관리한다.
- Codex 작업 스킬은 `.agents/skills/`, Claude 작업 스킬은 `.claude/skills/`에서 읽는다. 규칙·양식은 문서와 템플릿의 단일 기준을 연결하고 중복 정의하지 않는다.
