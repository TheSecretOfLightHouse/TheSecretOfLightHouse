# 데모 작업 가이드

데모는 기능·손맛을 빠르게 확인하는 씬이다. 본편(git)에 올리지 않는 것이 전제이며 SVN으로 관리한다.

## 위치·이름
SVN 체크아웃 루트, 산 에셋 위치, 데모 폴더 이름(`<사람>`·`<주제>`)은 [파일 위치 규칙](LARGE_FILES.md)이 기준이다. 데모 폴더 안의 구성은 다음과 같다.
```
Assets/ThirdParty/_Demo/<사람>/<주제>/   ← 데모. 이 폴더 안에서 완결
├─ <주제>.unity
├─ README.md
├─ Scripts/   (런타임 코드)
├─ Editor/    (에디터 스크립트)
└─ Prefabs/ Data/ TerrainData/ …  (필요한 것만)
```
- 남의 `<사람>` 폴더는 수정하지 않는다 (SVN 씬 충돌은 병합 불가). 필요하면 사용자에게 묻는다.

## 코드
- 데모 코드는 전부 데모 폴더에 둔다. `01.Scripts/`(git)에는 만들지 않는다.
- 데모 코드는 Assembly-CSharp. `Lighthouse.Runtime` asmdef가 autoReferenced라서 본편 코드를 가져다 쓸 수는 있다. 반대 방향(본편이 데모를 참조)의 금지는 [파일 위치 규칙](LARGE_FILES.md)을 따른다.
- 네임스페이스: `Lighthouse.Demo.<주제>`. 작성 규칙은 [코딩 컨벤션](../coding-convention.md)을 따른다 (asmdef 분리 규칙은 데모에 적용하지 않는다).
- 본편에 쓰기로 결정되면 그때 `.meta`와 함께 `01.Scripts/`로 옮기고, 담당 도메인과 기능의 책임에 맞게 나눈다. 기존 참조·직렬화가 있는 타입의 네임스페이스는 영향 검증 없이 바꾸지 않는다.

## 맵
- 다른 데모의 맵을 쓸 때는 맵 씬을 **데모 폴더로 복사**해서 `<주제>.unity`로 쓴다. 원본 맵은 건드리지 않는다.
- 주의: 씬을 복사해도 Terrain은 원본 `TerrainData` 에셋을 그대로 참조한다. 지형을 고칠 거면 `TerrainData`도 데모 폴더로 복사하고 씬의 Terrain·TerrainCollider 참조를 복사본으로 바꾼다.

## 씬 구성
사용 가능한 Unity 에디터 도구와 연결 상태를 먼저 확인한다. 에디터 API로 안전하게 배치·연결할 수 있으면 그 경로를 사용한다. 직접 조작할 수 없거나 재현 가능한 구성이 필요하면 아래 방법을 사용한다. `.unity`·`.prefab`·`.asset` YAML은 직접 편집하지 않는다.
- **에디터 스크립트**: `Editor/`에 메뉴(`MenuItem("TheLightHouse/Demo/<주제>/...")`)를 만들고, 사용자가 메뉴를 한 번 누르면 오브젝트 생성·컴포넌트 연결이 자동으로 된다. 예: `LighthouseArchipelago/Editor/ArchipelagoMapBuilder.cs`. 배치할 게 많거나 다시 만들 일이 있으면 이쪽.
- **수동 안내**: 몇 개 안 되면 "빈 오브젝트 만들고 → 컴포넌트 추가 → 필드 연결" 순서를 적어서 안내한다.
- 컴파일·실행을 확인하지 못했으면 그렇다고 명시한다.

## README.md (데모마다)
- 목적: 무엇을 확인하는 데모인가
- 실행: 열 씬, 필요한 메뉴·키, 조작법
- 출처: 복사해 온 맵·에셋, 사용하는 본편 코드
- 상태: 동작 확인 여부, 남은 일

## 커밋
- 데모 폴더는 git에 올라가지 않는다. SVN 커밋은 사용자가 한다. 작업이 끝나면 SVN에 추가할 경로를 알려준다.

## 기존 데모 기록
2026-10-09 로컬에는 `Assets/ThirdParty/` 체크아웃이 없다. 아래는 기존 데모 기록이며 파일 존재·메뉴 실행·조작 가능성을 이번에 검증하지 않았다.

| 경로 | 내용 |
|---|---|
| `_Demo/Jihun/LighthouseArchipelago/` | 군도 맵. 본 맵은 `Map_LighthouseArchipelago_Far.unity` (생성기: `TheLightHouse/Build Archipelago Map (Far)`). 예전 규칙으로 만들어 네임스페이스(`TheLightHouse.*`)가 다르다 |
| `_Demo/Jihun/GatherLoop/` | 1단계 채집 루프 (배·채집·인벤토리·등대 수리도). Far 맵 복사 + `TheLightHouse/Demo/GatherLoop/Build Scene` |
