# TheSecretOfLightHouse

등대와 섬을 배경으로 한 PC 3인칭 4인 리슨 서버 마피아 게임.
시민은 자원을 모아 등대를 활성화하고, 마피아는 정체를 숨기며 이를 방해한다.

## 시작하기

1. [Unity 첫 실행](docs/unity/UNITY_START_HERE.md)에서 엔진·패키지·Git/SVN 준비를 확인한다.
2. [문서 읽기 안내](docs/00-reading-guide.md)에서 맡은 작업에 필요한 기획과 기술 문서를 찾는다.
3. 작업 전 [공통 지침](AGENTS.md), [코딩 컨벤션](docs/coding-convention.md), [커밋·PR 컨벤션](docs/commit-convention.md)을 확인한다.

## 폴더

| 경로 | 용도 |
|---|---|
| `Assets/` | 기존 게임 코드·씬·에셋과 외부 플러그인 |
| `Packages/`, `ProjectSettings/` | Unity 패키지와 프로젝트 설정 |
| `docs/` | 기획·기술·아키텍처·작업 현황 |
| `docs/ai/` | 실행 정책·작업 브리프·검증·인수인계 양식 |
| `docs/unity/` | 첫 실행·네트워크·씬·에셋·데모 운영 |
| `docs/design/` | 기획 보존본과 관련 자료 |
| `docs/licenses/` | 에셋 설치·사용 조건 기록 |
| `.github/` | PR·이슈 템플릿 |
| `.agents/skills/`, `.claude/skills/` | 도구별 작업 절차 |
| `Tools/` | Git 훅과 설치 스크립트 |

현재 코드와 문서상 계획은 [아키텍처](docs/04-architecture.md)와 [구현 로드맵](docs/05-implementation-roadmap.md)에서 구분한다. 문서 작성·갱신은 [문서 분류](docs/DOCUMENTATION_POLICY.md)를 따른다.

각자의 구체적인 작업과 연결할 기능은 [담당자별 구현 작업](docs/06-owner-work-plan.md)에서 확인한다.

## 코드 담당

기능별 폴더와 GitHub 계정은 [담당 경계](docs/CODE_OWNERSHIP.md)를 따른다. 각 담당자가 자기 기능의 서버·클라이언트를 함께 맡으며 다른 영역 변경 전 대상·이유·영향을 안내한다. 시스템&UI는 최명기(cmg6991) 담당, 김민성은 AX 지원만 맡는다.
