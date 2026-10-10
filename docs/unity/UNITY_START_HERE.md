# 처음 프로젝트를 열 때

## 1. 저장소와 도구

Unity 프로젝트 루트는 저장소 루트다. `Assets/`, `Packages/`, `ProjectSettings/`가 있는 폴더를 Unity Hub에서 연다. 클론 후 [Git 설정 도구](../../Tools/GitSetup/setup_git.bat)를 한 번 실행하고 기존 LFS 파일을 `git lfs pull`로 받는다. 도구는 Git 전역·로컬 SmartMerge 설정과 로컬 `core.hooksPath`를 설정한다. LFS 사용과 파일 위치는 [파일 위치 규칙](LARGE_FILES.md)을 따른다.

SVN 에셋·데모가 필요하면 `Assets/ThirdParty/`에 팀 체크아웃을 준비한다. 2026-10-09 확인한 작업 폴더에는 이 경로가 없다. Git 클론만으로 구매 에셋과 데모가 갖춰졌다고 보지 않는다.

### GitHub CLI 설치와 로그인

이슈 등록·중복 확인과 PR 생성은 GitHub CLI(`gh`)로 진행한다. 팀 템플릿 본문을 UTF-8 파일 그대로 `--body-file`로 게시해 줄바꿈과 양식이 깨지지 않고, `gh issue list`·`gh label list`·`gh api user`로 중복·라벨·작성자를 추측 없이 확인할 수 있기 때문이다. [이슈](../../.claude/skills/guided-issue/SKILL.md)·[PR](../../.claude/skills/guided-pull-request/SKILL.md) 스킬이 이 경로를 전제로 하며, `gh`가 없거나 로그인하지 않으면 AI는 초안만 만들고 게시하지 못한다.

1. 설치(Windows PowerShell): `winget install --id GitHub.cli -e`. winget이 없으면 [cli.github.com](https://cli.github.com)의 설치 파일을 쓴다. 설치 후 터미널을 새로 열어야 PATH에 잡힌다.
2. 확인: `gh --version`이 버전을 출력한다.
3. 로그인: `gh auth login`에서 GitHub.com → HTTPS → 웹 브라우저 인증을 고른다. 토큰은 gh가 자격 증명 저장소에 보관하므로 문서·커밋·채팅에 붙여 넣지 않는다.
4. 검증: `gh auth status`의 Token scopes에 `repo`가 있는지 보고, `gh repo view TheSecretOfLightHouse/TheSecretOfLightHouse`가 저장소 정보를 출력하는지 확인한다. 계정이 여럿이면 `gh auth switch`로 이 저장소에 접근하는 계정을 활성화한다.

PR·이슈 게시 권한은 계정의 저장소 접근 권한을 따르며, 접근이 안 되면 저장소 관리자에게 초대를 요청한다.

## 2. 버전

| 항목 | 저장소 기준 |
|---|---|
| Unity | 6000.0.83f1 — `ProjectSettings/ProjectVersion.txt` |
| Universal RP | 17.0.4 |
| Input System | 1.19.0, Active Input Handling은 Both |
| AI Navigation | 2.0.14 |
| Test Framework | 1.6.0 |
| Mirror | 96.0.1 — `Assets/Mirror/version.txt` |
| ParrelSync | Git 의존성 — `Packages/manifest.json`과 `packages-lock.json` 확인 |

패키지 버전은 [manifest](../../Packages/manifest.json)와 잠금 파일을 기준으로 맞춘다. 에디터 자동 업그레이드를 진행하기 전에 프로젝트 지정 버전을 설치한다.

## 3. 프로젝트 설정과 첫 실행

1. Visible Meta Files와 Force Text를 확인한다. 저장소 설정 파일에서 `ProjectSettings/EditorSettings.asset`의 `m_SerializationMode`는 2(Force Text), `ProjectSettings/VersionControlSettings.asset`의 `m_Mode`는 `Visible Meta Files`다. 에디터 화면에서는 확인하지 않았다.
2. 담당 씬과 변경 범위를 확인한다. 현재 확인한 씬은 `Assets/00.Scenes/SampleScene.unity`, `Assets/00.Scenes/Myeongki/Player.unity`다. 이름만으로 통합 시작 씬이나 담당자를 확정하지 않는다.
3. Console의 컴파일·임포트 오류를 확인한 뒤 해당 씬에서 Play 검증을 한다.
4. 오류가 생기면 로그와 재현 경로를 보존하고 환경 문제·코드 문제를 구분한다.

파일 존재는 실행 성공을 뜻하지 않는다.

## 4. 여러 인스턴스 검증

기존 게임 방향은 4인 호스트-클라이언트다. ParrelSync 또는 별도 빌드를 이용해 호스트와 원격 클라이언트를 나누어 확인한다. 호스트 한 창에서의 성공만으로 다인 동작을 완료 처리하지 않는다. 구체적인 근거 기록은 [검증 플레이북](../ai/03_VERIFICATION_PLAYBOOK.md)을 따른다.

## 5. 다음에 읽을 것

| 작업 | 문서 |
|---|---|
| 코드 작성 | [기존 코딩 컨벤션](../coding-convention.md) |
| 씬·프리팹·에셋 | [씬·에셋 안전](SCENE_AND_ASSET_SAFETY.md) |
| 서버 판정·동기화 | [네트워크 권위](NETWORK_AUTHORITY.md), [협업 절차](SERVER_CLIENT_WORKFLOW.md) |
| 수치·정의 데이터 | [밸런스 데이터](BALANCE_DATA_WORKFLOW.md) |
| 데모 작업 | [데모 가이드](DEMO_GUIDE.md) |
| 커밋·PR | [커밋·PR 컨벤션](../commit-convention.md) |
| 작업 지침 전반 | [저장소 지침](../../AGENTS.md) |
