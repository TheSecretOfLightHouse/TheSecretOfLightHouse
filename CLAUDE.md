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
