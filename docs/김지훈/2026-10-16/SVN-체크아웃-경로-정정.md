# SVN 체크아웃 경로 정정

작성일: 2026-10-16

## 배경
`SearchlightSceneBuilder` 실행 중 `맵 복사 실패: Assets/ThirdParty/_Demo/Jihun/GatherLoop/GatherLoop.unity` 오류가 났다. 김지훈 PC는 SVN 저장소 `Work/1조 Unity Asset`의 내용을 `Assets/ThirdParty/`에 바로 받은 상태였고, 빌더 스크립트와 팀원 PC는 `Assets/ThirdParty/1조 Unity Asset/` 구조를 쓴다. 대상 폴더 `Assets/ThirdParty/1조 Unity Asset/_Demo/Jihun/LighthouseSearchlight`가 없어 `AssetDatabase.CopyAsset`이 실패했다.

## 과정과 결정
- 처음에는 [파일 위치 규칙](../../unity/LARGE_FILES.md)의 "`1조 Unity Asset/`을 로컬 경로에 덧붙이지 않는다"를 따라 빌더 경로 상수 5개 파일에서 `1조 Unity Asset/`을 뺐다. 팀원 PC 구조와 반대여서 잘못된 수정이었다.
- 사용자가 팀원과 같은 구조로 다시 체크아웃했다(Checkout directory 끝에 `\1조 Unity Asset`). 새로 받은 r331 스크립트가 덮어써 위 수정은 사라졌고, 원래 경로 상수가 그대로 맞다.
- 결정: 로컬 체크아웃 루트는 `Assets/ThirdParty/1조 Unity Asset/`이다. 기준 문서를 팀 실제 구조에 맞췄다.

## 변경 파일
- `docs/unity/LARGE_FILES.md`: 데모·산 에셋 경로, 체크아웃 루트 규칙, 확인 상태
- `docs/unity/DEMO_GUIDE.md`: 데모 폴더 구성 경로
- `AGENTS.md`: SVN 체크아웃 루트 문장
- `docs/ai/01_AGENT_EXECUTION_POLICY.md`, `docs/ai/07_PROJECT_KNOWLEDGE_MAP.md`, `docs/commit-convention.md`, `docs/unity/UNITY_START_HERE.md`: 경로 표기
- SVN 데모 스크립트: 변경 없음(재체크아웃본 그대로)

## 검증
- `Assets/ThirdParty/1조 Unity Asset/.svn/wc.db` 읽기: 저장소 경로 `Work/1조 Unity Asset`, r331, depth infinity, 작업 대기열 0
- `_Demo/Jihun` 아래 `.cs`의 `"Assets/..."` 문자열 13개 모두 실제 파일·폴더 존재 확인
- 미실행: Unity에서 LighthouseSearchlight 빌더 메뉴 재실행

## 남은 일
- Unity에서 원본 맵을 열고 `TheLightHouse/Demo/LighthouseSearchlight/Build From Open Scene` 재실행
- 공유 문서(`docs/`, CODEOWNERS 기본 담당 @AidenKim923) 변경은 커밋·PR 때 검토 필요
- `docs/unity/SCENE_AND_ASSET_SAFETY.md:28`의 2026-10-09 "체크아웃 루트 없음" 기록은 날짜가 붙은 과거 확인이라 그대로 뒀다
