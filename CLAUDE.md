# TheSecretOfLightHouse — 프로젝트 규칙

팀 전원이 공유하는 Claude Code 지침. 프로젝트 공통 규칙만 적는다.

---

## Claude 파일 구조

Claude Code는 아래 파일을 이름·위치로 자동 인식한다. 따로 불러올 필요 없다.

| 파일 | 범위 | 커밋 |
|---|---|---|
| `CLAUDE.md` | 팀 공용 지침 (이 파일) | O |
| `.claude/settings.json` | 팀 공용 설정 (권한 · 훅 등) | O |
| `.claude/skills/` | 팀 공용 스킬 (`/스킬명` 으로 호출) | O |
| `.claude/agents/` | 팀 공용 서브에이전트 | O |
| `CLAUDE.local.md` | 내 PC에서만 읽히는 개인 지침 | **X** (gitignore) |
| `.claude/settings.local.json` | 내 PC에서만 쓰는 개인 설정 | **X** (gitignore) |

- 개인 취향 · 로컬 경로 · 실험 중인 규칙은 `*.local.*` 파일에 적는다.
- 팀 전체가 따라야 하는 규칙만 공용 파일에 올린다.
- 하위 폴더에 `CLAUDE.md`를 두면 그 폴더 작업 시 함께 읽힌다. 더 구체적인 쪽이 우선한다.

---

## 파일 위치 — 본편은 git, 데모는 ThirdParty

| 무엇 | 어디 |
|---|---|
| 게임 본편 (코드 · 씬 · 에셋) | git |
| 데모 (씬 · 데모 전용 스크립트 · 데이터 전부) | `Assets/ThirdParty/_Demo/<이름>/` (SVN) |
| 산 에셋 | `Assets/ThirdParty/<에셋명>/` (SVN) |

- 본편 코드는 데모를 참조하지 않는다. 데모 코드를 본편에 쓰려면 `01_Scripts/`로 옮긴다.
- 커밋 시 50MB 초과 경고, 100MB 초과 차단. Git LFS는 새로 쓰지 않는다. → `Docs/LARGE_FILES.md`
- clone 후 `Tools/GitSetup/setup_git.bat`을 한 번 실행해야 검사가 켜진다.
- 군도 맵 데모: `ThirdParty/_Demo/Jihun/LighthouseArchipelago/` (메뉴 `TheLightHouse > Build Archipelago Map`)
