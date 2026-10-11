# AI 작업 문서

규칙 우선순위는 최신 사용자 결정 → [제공된 v4](../design/references/game-design-v4.md)다. [설계 문제 대장](../07-design-issue-register.md)은 차이와 미결 항목을 안내한다. v4 이전 기획은 [보존 자료](../design/archive/game-design-2026-10-08.md)이며 현행 규칙이 아니다.

TheSecretOfLightHouse의 작업 브리프, 검증, 인수인계와 협업 절차를 관리한다. 필요한 문서만 읽고 게임 규칙과 구현 상태를 구분한다.

## 처음이라면

| 상황 | 읽을 것 |
|---|---|
| 게임 규칙과 구현 방향 | [읽기 안내](../00-reading-guide.md) → [기획서](../design/references/game-design-v4.md) · [기술설계서](../03-technical-design.md) |
| AI에게 작업 요청 | [작업 브리프](02_TASK_BRIEF_TEMPLATE.md) |
| 완료 여부 확인 | [검증 플레이북](03_VERIFICATION_PLAYBOOK.md) |
| 네트워크 코드 수정 | [네트워크 권위](../unity/NETWORK_AUTHORITY.md) · [서버·클라이언트 협업](../unity/SERVER_CLIENT_WORKFLOW.md) |
| 다인 접속 검증 | [멀티플레이 테스트](04_MULTIPLAYER_TEST_PROTOCOL.md) |
| 작업 중단·전달 | [세션 인수인계](05_SESSION_HANDOFF.md) |
| 기획을 구현으로 연결 | [문서 분석](09_DOCUMENT_ANALYSIS_WORKFLOW.md) |
| 코드·커밋 작성 | [코딩 컨벤션](../coding-convention.md) · [커밋·PR 컨벤션](../commit-convention.md) |

공통 실행 지침은 루트 [AGENTS.md](../../AGENTS.md), 문서 위치와 현재성은 [문서 분류](../DOCUMENTATION_POLICY.md)를 따른다.

## 도구별 스킬

Codex는 [`.agents/skills/`](../../.agents/skills/), Claude Code는 [`.claude/skills/`](../../.claude/skills/)를 사용한다. 실제 템플릿과 담당자 규칙을 읽고, 없는 계정·CODEOWNERS·워크플로를 가정하지 않는다.

| 스킬 | 사용 시점 |
|---|---|
| [review-design-code-gaps](../../.agents/skills/review-design-code-gaps/SKILL.md) · [Claude](../../.claude/skills/review-design-code-gaps/SKILL.md) | 기능 구현·수정, 해야 할 일·남은 일·진척 요청 시 기획 공백·코드 차이·예상 문제 확인 |
| [audit-doc-quality](../../.agents/skills/audit-doc-quality/SKILL.md) · [Claude](../../.claude/skills/audit-doc-quality/SKILL.md) | CLAUDE.md·AGENTS.md·docs 문서의 품질 점수·등급, 깨진 링크·낡은 경로·중복 규칙 점검 (읽기 전용 보고서) |
| [audit-doc-code-sqale](../../.agents/skills/audit-doc-code-sqale/SKILL.md) · [Claude](../../.claude/skills/audit-doc-code-sqale/SKILL.md) | 문서–코드 일치율과 SQALE 기반 코드 품질·기술 부채 비율 측정 (읽기 전용 보고서) |
| [guided-commit](../../.agents/skills/guided-commit/SKILL.md) · [Claude](../../.claude/skills/guided-commit/SKILL.md) | 변경 검토·문서 동기화·커밋 |
| [guided-pull-request](../../.agents/skills/guided-pull-request/SKILL.md) · [Claude](../../.claude/skills/guided-pull-request/SKILL.md) | PR 준비·검증 기록 |
| [guided-issue](../../.agents/skills/guided-issue/SKILL.md) · [Claude](../../.claude/skills/guided-issue/SKILL.md) | 작업·버그 이슈와 재현 기록 |
| [check-code-ownership](../../.agents/skills/check-code-ownership/SKILL.md) · [Claude](../../.claude/skills/check-code-ownership/SKILL.md) | 다른 담당·공용 계약 수정 전 경계 확인 |
| [clarify-ambiguous-request](../../.agents/skills/clarify-ambiguous-request/SKILL.md) · [Claude](../../.claude/skills/clarify-ambiguous-request/SKILL.md) | 결과를 바꾸는 미확정 입력 확인 |
| [present-alternatives](../../.agents/skills/present-alternatives/SKILL.md) · [Claude](../../.claude/skills/present-alternatives/SKILL.md) | 기술 대안·비용 비교 |
| [write-comments](../../.agents/skills/write-comments/SKILL.md) · [Claude](../../.claude/skills/write-comments/SKILL.md) | 기존 코딩컨벤션에 맞춘 의도·계약 주석 |
| [write-handoff](../../.agents/skills/write-handoff/SKILL.md) · [Claude](../../.claude/skills/write-handoff/SKILL.md) | 미완료 작업·근거·다음 행동 기록 |

명시적인 초안 요청은 게시·커밋 요청으로 바꾸지 않는다. 이미 허용된 실행의 반복 확인 여부는 [AGENTS.md](../../AGENTS.md)를 따른다.

공용 작업 스킬 11종을 제공한다. 각 스킬의 실행 조건은 해당 `SKILL.md`를 읽고 현재 요청 범위에 맞춰 적용한다. 스킬 설치가 게임 기획·수치·코딩 스타일 변경을 승인하지 않는다.

## 문서 지도

### 게임·기술·Unity

| 문서 | 용도 |
|---|---|
| [00 읽기 안내](../00-reading-guide.md) | 역할별 읽기 순서 |
| [01 시스템 개요](../01-system-design.md) | 게임 루프와 범위 |
| [게임 기획 v4](../design/references/game-design-v4.md) | 확정·가안·미정 규칙 |
| [03 기술설계서](../03-technical-design.md) | 권위와 데이터 경계 |
| [코드 소유권](../CODE_OWNERSHIP.md) | 기능별 담당·계정·협업 경계의 단일 기준 |
| [04 아키텍처](../04-architecture.md) | 코드·씬·어셈블리 위치 |
| [05 구현 로드맵](../05-implementation-roadmap.md) | 구현 상태·선행 조건·검증 |
| [06 담당자별 구현 작업](../06-owner-work-plan.md) | 담당별 작업 ID·협업·완료 기준 |
| [Unity 시작](../unity/UNITY_START_HERE.md) | 환경과 첫 실행 |
| [씬·에셋 안전](../unity/SCENE_AND_ASSET_SAFETY.md) | `.meta`·씬·외부 에셋 |
| [데모 가이드](../unity/DEMO_GUIDE.md) | SVN 에셋 데모 |

### AI 작업 운영

| 문서 | 용도 |
|---|---|
| [01 실행 정책](01_AGENT_EXECUTION_POLICY.md) | 실행·승인·중단 경계 |
| [02 작업 브리프](02_TASK_BRIEF_TEMPLATE.md) | 요청 범위와 완료 조건 |
| [03 검증 플레이북](03_VERIFICATION_PLAYBOOK.md) | 증거와 완료 상태 |
| [04 멀티플레이 테스트](04_MULTIPLAYER_TEST_PROTOCOL.md) | 호스트·원격 클라이언트 검증 |
| [05 세션 인수인계](05_SESSION_HANDOFF.md) | 작업 재개·담당자 전달 |
| [06 컨텍스트 관리](06_CONTEXT_HYGIENE.md) | 읽기·검색·긴 세션 |
| [07 프로젝트 지식 맵](07_PROJECT_KNOWLEDGE_MAP.md) | 기능별 실제 진입점 |
| [08 병렬 작업 분업](08_SUBAGENT_ORCHESTRATION.md) | 배타적 쓰기와 통합 |
| [09 기획 문서 → 구현](09_DOCUMENT_ANALYSIS_WORKFLOW.md) | 요구사항과 구현 근거 추적 |
| [10 응답 스타일](10_RESPONSE_STYLE_GUIDE.md) | 결과 보고 형식 |

## 프로젝트 전제

| 항목 | 값 |
|---|---|
| 엔진 | Unity 6000.0.83f1 · URP |
| 네트워크 | Mirror, 호스트-클라이언트, 총 4인 |
| 게임 | PC 3인칭 등대 배경 마피아류 게임, 시민 3 vs 마피아 1 |
| 코드 | `Assets/01.Scripts/` 기능별 폴더, 단일 `Lighthouse.Runtime` (`Unity.InputSystem`·`Mirror` 참조) |
| 담당 경계 | [코드 소유권](../CODE_OWNERSHIP.md), 기능마다 Rules·Net·Server·View 전체 책임 |
| 개인 기록 | `.claude/docs/YYYY-MM-DD/`, `.claude/handover/YYYY-MM-DD/` |

## 유지보수

- 실제 동작은 현재 코드·설정으로 확인하고 기획 의도와의 차이를 기록한다.
- 기획과 코드가 다르다는 이유만으로 기획을 임의 변경하지 않는다.
- 한 규칙은 한 문서에서 관리하고 관련 문서는 링크한다.
- 테스트 수·환경·구현 완료 여부를 추측하지 않는다.
- 폐기 문서는 유효한 링크를 정리한 뒤 처리하고 개인 이력은 공유 문서와 분리한다.

개인 설정과 도구별 지침 위치는 [도구 설정 안내](TOOL_SETUP.md)에서 확인한다.

기능 구현·관련 논의에는 [설계 문제 대장](../07-design-issue-register.md)의 해당 ID를 확인한다. 보고서의 제안과 v4의 실제 규칙을 구분한다.
