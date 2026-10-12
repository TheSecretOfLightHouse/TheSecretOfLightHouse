# 도구별 작업 지침과 개인 설정

공통 규칙은 루트 [AGENTS.md](../../AGENTS.md), Claude 진입점은 [CLAUDE.md](../../CLAUDE.md)다. 상황별 문서는 [AI 작업 문서](README.md)에서 필요한 것만 읽는다.

| 경로 | 용도 | Git |
|---|---|---|
| `AGENTS.md` | 팀 공통 필수 규칙 | 포함 |
| `CLAUDE.md` | 공통 규칙 진입점 | 포함 |
| `.agents/skills/` | Codex 작업 스킬 | 포함 |
| `.claude/skills/` | Claude 작업 스킬 | 포함 |
| `.claude/agents/` | 팀 공용 Claude 에이전트 정의 (`perf-code-implementer.md` 등) | 포함 |
| `.claude/settings.json` | 공용 설정 | 포함 |
| `CLAUDE.local.md`, `.claude/settings.local.json` | 개인 지침과 설정 | 제외 |
| `docs/WORK_POINTER.md` | 세션 시작 진입점 | 포함 |
| `docs/{이름}/POINTER.md` | 날짜별 작업·상태·남은 일·다음 행동 | 포함 |
| `docs/{이름}/{날짜}/{내용}.md` | 본인 작업 과정과 결과 | 포함 |
| `docs/handover/{이름}/{날짜}/{내용}.md` | 인수인계 | 포함 |

루트 지침은 짧게 유지하고 특정 작업에만 필요한 절차는 관련 문서에 둔다. 문서 위치는 [문서 분류](../DOCUMENTATION_POLICY.md)를 따른다. 개인 PC 경로·도구 선호를 팀 필수 조건으로 만들지 않는다. 기록과 자동 읽기 절차는 [작업 기록과 재개](11_WORK_RECORDS.md)를 따른다. 자동 읽기는 저장소에 접근하는 AI가 루트 지침에 따라 수행하며 별도 백그라운드 프로그램을 설치하지 않는다.

스킬 구성과 실행 범위는 [AI 작업 문서](README.md#도구별-스킬)를 따른다. 설치된 워크플로가 기존 게임 기획이나 코드 담당 경계를 대신하지 않는다.
