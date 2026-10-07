# Claude Code 파일 구조

Claude Code는 아래 파일을 이름·위치로 자동 인식한다. 따로 불러올 필요 없다.

| 파일 | 범위 | 커밋 |
|---|---|---|
| `CLAUDE.md` | 팀 공용 지침 — AI가 매 세션 시작 시 읽는 진입점 | O |
| `.claude/settings.json` | 팀 공용 설정 (권한 · 훅 등) | O |
| `.claude/skills/` | 팀 공용 스킬 (`/스킬명` 으로 호출) | O |
| `.claude/agents/` | 팀 공용 서브에이전트 | O |
| `CLAUDE.local.md` | 내 PC에서만 읽히는 개인 지침 | **X** (gitignore) |
| `.claude/settings.local.json` | 내 PC에서만 쓰는 개인 설정 | **X** (gitignore) |

- 하위 폴더에 `CLAUDE.md`를 두면 그 폴더 작업 시 함께 읽힌다. 더 구체적인 쪽이 우선한다.
- 개인 취향 · 로컬 경로 · 실험 중인 규칙은 `*.local.*` 파일에 적는다.

## 문서 폴더 (팀 규칙)

| 폴더 | 누가 읽나 | 커밋 |
|---|---|---|
| `Docs/AI/` | AI — `CLAUDE.md`의 문서 표에 등록된 것만, 필요할 때 읽음 | O |
| `Docs/Human/` | 사람 — AI는 요청받을 때만 읽음 | O |
| `Docs.local/` | 개인 (작업 기록 · 메모) | **X** (gitignore) |

## CLAUDE.md 관리 원칙

- 매 세션 전부 로드되므로 **모든 작업에 해당하는 규칙만** 짧게 둔다.
- 특정 상황에서만 필요한 내용은 `Docs/AI/`에 문서로 쓰고, `CLAUDE.md` 문서 표에 "상황 → 경로" 한 줄만 추가한다.
- `@경로` import는 세션 시작 시 즉시 로드되므로 쓰지 않는다.
- 특정 폴더 전용 규칙은 그 폴더에 `CLAUDE.md`를 둔다.
- `Docs/AI/` 문서는 첫 줄에 범위를 적고, `##` 섹션으로 나눠 부분만 읽을 수 있게 쓴다.
