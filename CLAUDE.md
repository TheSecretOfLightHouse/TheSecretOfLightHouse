# TheSecretOfLightHouse

Unity 6000.0.83f1 · URP 17 · Mirror 96 (멀티플레이) · Input System. 등대 배경 마피아류 게임.

## 응답
- 한국어로 답한다. 코드 식별자·주석은 영어.
- 작업 전 관련 코드를 먼저 읽는다. API·필드명을 추측으로 만들지 않는다.

## 코드 구조 (Assets/01_Scripts)
- 어셈블리: `_Net`(Lighthouse.Net) ← `_Game`, `_Local`. Game ↔ Local 서로 참조 금지.
  - `_Net`: Mirror 동기화·네트워크 메시지
  - `_Game`: 서버 권한 게임 규칙
  - `_Local`: 클라이언트 전용 (입력·카메라·연출·URP)
- 그 밖의 폴더(`Managers` `Player` `Utils` 등)는 asmdef 없음 → Assembly-CSharp. asmdef 쪽에서 참조 불가.
- asmdef 추가·참조 변경이 필요하면 먼저 묻는다.

## Unity
- `.meta`는 에셋과 항상 같이 이동·삭제·이름변경. GUID 직접 수정 금지.
- `.unity` `.prefab` `.asset` YAML은 직접 편집하지 않는다. 에디터 스크립트나 수동 작업 안내로 대신한다.
- `Library/` `Temp/` `Logs/` `UserSettings/` 는 다루지 않는다.
- `Assets/ThirdParty/` 는 SVN 관리(git 제외) — 산 에셋·데모(`_Demo/<이름>/`)는 여기에. 외부 코드라 수정 금지.
- `Assets/Mirror/` 는 외부 코드 — 수정 금지.
- 에디터를 실행할 수 없으므로, 컴파일·동작을 확인 못 한 부분은 그렇다고 명시한다.

## 문서
- `Docs/AI/`: 작업용 문서. 아래 표에서 해당 상황일 때만 읽는다.
- `Docs/Human/`: 사람용. 요청받지 않으면 읽지 않는다.
- 문서 작성 위치: AI용 → `Docs/AI/` (추가 시 아래 표에 등록), 사람용 → `Docs/Human/`, 개인 → `Docs.local/`.
- 개인 지침·설정은 `CLAUDE.local.md`, `.claude/settings.local.json` 에.

| 상황 | 읽을 문서 |
|---|---|
| C# 스크립트 작성·수정 | `Docs/AI/CODE_CONVENTION.md` |
| 게임 시스템(규칙·수치·흐름) 구현·수정 | `Docs/AI/SYSTEM_DESIGN.md` |
