# 데모 작업 가이드

데모는 기능·손맛을 빠르게 확인하는 씬이다. 본편(git)에 올리지 않는 것이 전제이며 SVN으로 관리한다.

## 위치·이름
SVN 체크아웃 루트, 산 에셋 위치, 데모 폴더 이름(`<사람>`·`<주제>`)은 [파일 위치 규칙](LARGE_FILES.md)이 기준이다. 데모 폴더 안의 구성은 다음과 같다.
```
Assets/ThirdParty/1조 Unity Asset/_Demo/<사람>/<주제>/   ← 데모. 이 폴더 안에서 완결
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
- 데모를 새로 만들거나 상태가 바뀌면 아래 **데모 목록**에 한 줄을 추가·갱신한다. 이 표는 git에 있어 SVN을 아직 업데이트하지 않은 PC에서도 어떤 데모가 있는지 알 수 있다. 다른 PC에서만 만든 데모도 SVN 커밋할 때 함께 적는다.
- 데모를 폐기하면 데모 목록에서 그 줄을 빼고 **폐기 목록**으로 옮긴다. 폐기일, 이유, 삭제 직전 SVN 리비전을 함께 적는다. SVN 이력에는 파일이 남으므로 이 리비전에서 되살릴 수 있다.

## 데모 목록
현재 있는 데모의 목록이다. 데모를 만들거나 상태가 바뀔 때마다 갱신한다. 상태 열은 기록 시점의 확인 수준이며 현재 실행 가능함을 보장하지 않는다.

| 경로 | 내용 | 상태 |
|---|---|---|
| `_Demo/Jihun/LighthouseArchipelago/` | 군도 맵. 본 맵은 `Map_LighthouseArchipelago_Far.unity` (생성기: `TheLightHouse/Build Archipelago Map (Far)`). 예전 규칙으로 만들어 네임스페이스(`TheLightHouse.*`)가 다르다 | 2026-10-14 로컬 체크아웃에 존재 확인 |
| `_Demo/Jihun/GatherLoop/` | 1단계 채집 루프 (배·채집·인벤토리·등대 수리도). Far 맵 복사 + `TheLightHouse/Demo/GatherLoop/Build Scene` | 2026-10-14 로컬 체크아웃에 존재 확인. 실행 미확인 |
| `_Demo/Jihun/LighthouseSearchlight/` | 기획 제안(확정 아님): 등대 탐조등 조종으로 안개를 걷고 괴물을 쫓아 귀항하는 배 엄호. 열린 맵 복사 + `TheLightHouse/Demo/LighthouseSearchlight/Build From Open Scene` | 2026-10-14 작성, Unity 컴파일 확인. 빌더·플레이 미확인. SVN 미커밋 |
| `_Demo/Jihun/CrewMultiplayer/` | 4인 멀티: 팀 캐릭터 4종·애니메이션, 방 만들기/찾기/입장, 서버 판정 채집·등대 투입. 데모 전용 Mirror 동기화(본편 무수정). 열린 맵 복사 + `TheLightHouse/Demo/CrewMultiplayer/Build From Open Scene`. 2026-10-17 마피아 미니판 추가: 본편 `07.Mafia` 투표·확인·심해 규칙 클래스를 읽기 전용으로 호출, 점화 의식·최후 전투·종료 후 비밀 공개·규칙 비교 옵션. 기존 씬에는 `.../Add Mafia Round To Built Scene`. 2026-10-19 항해 일지 추가(기획 제안): 판 종료 후 지도 위 항적 재생·사건 목록·사건 순간 근처 선원 | 2026-10-15 Unity 컴파일·빌더 실행 확인(현재 씬에 상자 8개 배치 확인). 2026-10-17 마피아 추가분 컴파일 확인. 2026-10-19 항해 일지 컴파일·패치 메뉴 실행·호스트 1인 강제 종료 스모크 확인. 실제 마피아 판·4인 플레이 미확인. SVN 미커밋 |
| `_Demo/Jihun/NatureBiomesArchipelago/` | PolygonNatureBiomes 메인 군도 맵(Terrain 방식, 에셋 물·볼륨·하늘 그대로). 중앙 등대 섬(등대·작업대·집결지·선착장) + 내해·중간·외해 자원 섬(일반·숲·광맥·모래·수정) + 특수 섬 3종, 낮·밤 2씬. `TheLightHouse/Demo/NatureBiomesArchipelago/Build Day Scene`·`Build Night Scene` | 2026-10-24 컴파일·빌더 실행·낮 씬 Play Mode 스모크(오류 0) 확인. 본편 시스템 미연결. SVN 미커밋 |
| `_Demo/Jihun/NatureMixArchipelago/` | NatureBiomesArchipelago의 다른 버전. 정글(PNB `Demo_Day`) 메인 + Synty 전 팩(Pirates·Nature·Generic·ParticleFX·Vikings·Adventure·Western) 보조 혼합: 배 건조대·채굴 캠프·이끼 유적·외해 고사목/안개 등. 배치 150° 회전. `TheLightHouse/Demo/NatureMixArchipelago/Build Scene` | 2026-10-24 정글 메인으로 재작성. 컴파일·빌더 실행 미확인. SVN 미커밋 |
| `_Demo/Jihun/LighthouseFloors/` | 기획 제안(확정 아님): JH04 등대 3층. 나무·철·수정 → 작업대 → 층별 장착, 검은 녹·빼돌리기, 속삭임·촉수 소환(일차−의심 수), 탐조등 공개 고발, 촉수 쟁탈 최후 전투. CrewMultiplayer 씬 복사 + `TheLightHouse/Demo/LighthouseFloors/Build From CrewMultiplayer Scene` | 2026-10-19 Unity 컴파일 확인, 빌더 산출물(씬·프리팹) 생성 확인. 플레이 미확인. SVN 미커밋 |
| `_Demo/Jihun/GhostShipLedger/` | 기획 제안(확정 아님): 유령선 힌트 = 선원 전체 자원 총량 장부. 시민은 총량 4줄+무작위 두 이름의 사라진 자원 합, 마피아는 진짜 총량+먼저 본 선원 목록+덧칠 1회(대가: 붉은 등불). GatherLoop 씬 복사 + Ghost Fog Night + `TheLightHouse/Demo/GhostShipLedger/Build Scene` | 2026-10-29 Unity 컴파일·빌더 실행·코드 호출 스모크(장부·덧칠) 확인. 키 입력·육안 미확인. SVN 미커밋 |

## 폐기 목록
더 이상 쓰지 않아 SVN에서 지운 데모다. 다시 필요하면 `마지막 리비전`에서 복원한다.

| 경로 | 내용 | 폐기일 | 이유 | 마지막 리비전 |
|---|---|---|---|---|
