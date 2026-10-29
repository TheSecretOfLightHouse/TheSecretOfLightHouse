# PolygonNatureBiomes 군도 맵 데모
작성자: 김지훈
작성일: 2026-10-24
갱신일: 2026-10-24
상태: 구현됨(빌더 실행·씬 생성·Play Mode 스모크 확인, 실제 플레이 연결 없음)
브랜치·기준 커밋: feature/jihun, eb4e78b (데모 파일은 SVN 관리 경로라 Git 변경 없음)

## 요청과 목표
PolygonNatureBiomes를 메인으로, 필요한 다른 Synty 에셋을 보조로 써서 기획에 맞는 메인 등대 섬과 여러 자원 섬을 갖춘 맵을 새 데모 씬으로 만든다. 지형은 에셋과 같은 Terrain 방식, 물·포스트프로세싱·셰이더·스카이박스는 에셋 구성을 그대로 쓴다. 위치는 데모 가이드를 따른다.

## 진행 과정과 결정 이유
- 2026-10-24: 데모 가이드·파일 위치 규칙 확인 → `_Demo/Jihun/NatureBiomesArchipelago/`(SVN)에 씬·스크립트·지형 데이터를 모두 둠. 본편 `01.Scripts`·원본 에셋은 수정하지 않음.
- 에셋 구성 확인: PNB 데모 씬은 400m Terrain(`Terrain_Tropical_01`, 레이어 15종, 나무·디테일 없음)에 식생을 GameObject로 배치하고, 물(`Water_Ocean_Day/Night`, 1600m 평면)·Global Volume·스카이돔·구름 링·안개 링·WeatherControl을 씬에 둠. 그래서 "원본 씬 복사 → 디오라마만 제거 → TerrainData 복사본을 다시 조각·페인팅 → 섬 배치" 방식으로 에셋 효과를 그대로 보존함. 기존 `LighthouseArchipelago` 빌더와 같은 접근이지만 새 폴더에서 자체 완결되게 새로 작성(네임스페이스 `Lighthouse.Demo.NatureBiomesArchipelago`).
- 기획 대조: v4 §8(등대 섬 중앙 고정, 내해·중간·외해, 자원 섬·난파선·특수 기능 섬 3종)과 본편 `MapGenerationConfig`(안전 70·내해 200·중간 360·외해 490m, 구역별 섬·암초 수)·`Island_*.asset`(일반/숲/광맥/모래/수정의 구역·최소 간격, 자원 구성)을 기준으로 21개 섬을 손으로 고정 배치. 매 판 랜덤 생성은 데모 범위 밖으로 둠.
- 섬 종류별 표현: 광맥=높은 바위 능선·암석 레이어·채굴 캠프, 모래=낮은 사구·모래 레이어, 숲=큰 나무·맹그로브·덩굴, 수정=Demo_Night와 같은 짝의 발광 재질(씬 인스턴스 덮어쓰기)·유적 광장. 수정 결정체 메시는 World(강민서) 폴더에만 있어 "Synty만 사용" 요청에 맞춰 쓰지 않음.
- 1차 빌드에서 광맥 섬을 Pirates `Rock_Huge`(약 33m)·`Rock_Cliff_03`(22m)이 덮어버려 제외하고 배치량을 줄임. 재빌드 시 확인창이 자동 실행을 막아 `BuildWithoutPrompt(bool)` 진입점을 분리.
- 밤 씬은 원본 선형 안개(10~200m)를 그대로 유지하고 기본 카메라만 등대 섬 근처로 당김. 포인트 라이트는 밤 씬에서만 켬.

## 변경 파일
SVN 경로 `Assets/ThirdParty/1조 Unity Asset/_Demo/Jihun/NatureBiomesArchipelago/` (전부 신규, .meta 포함)
- `Editor/NatureBiomesArchipelagoBuilder.cs`: 메뉴 `TheLightHouse/Demo/NatureBiomesArchipelago/Build Day Scene`·`Build Night Scene`
- `Scripts/IslandSite.cs`, `MapMarker.cs`, `IslandKind.cs`, `MapMarkerKind.cs`, `ArchipelagoZoneGizmos.cs`, `GizmoCircle.cs`, `DemoFlyCamera.cs`
- 생성물: `NatureBiomesArchipelago.unity`, `NatureBiomesArchipelago_Night.unity`, `TerrainData/TD_NatureBiomesArchipelago_Day.asset`, `TD_NatureBiomesArchipelago_Night.asset`
- `README.md`

Git: `docs/unity/DEMO_GUIDE.md` 데모 목록 1행, 이 원문, `docs/김지훈/POINTER.md`

## 실제 검증
- Unity 6000.0.83f1 에디터(MCP 연결): 스크립트 컴파일 오류 0, 낮·밤 빌더 실행 완료, 누락 프리팹 경고 0.
- 낮 씬 섬 21·표식(등대 코어 1·작업대 1·집결지 1·배 정박 4·특수 섬 3·난파선 3·스포너 후보 16) 개수 확인. Transform 약 12,000개.
- 카메라 캡처로 등대 섬·작업대·집결지·광맥·모래·수정·레이더·관측소·날씨 예측소, 밤 씬 모닥불·횃불 조명 육안 확인.
- 낮 씬 Play Mode 약 5초: 콘솔 오류·경고 0, 에디터 약 116fps.
- 미실행: 비행 카메라 키·마우스 직접 조작, 밤 씬 Play Mode, 빌드, 본편 시스템(배·채집·스포너) 연결.

## 남은 일
- SVN 추가·커밋(사용자): 위 데모 폴더 전체.
- 본편에 쓰려면 섬 단위 프리팹화와 맵 생성기(강민서 World 영역) 연결 합의가 필요함. 현재는 고정 배치 데모.

## 후속: 혼합 버전 NatureMixArchipelago (2026-10-24)
- 요청: Synty/PolygonNature/Demo 씬을 섞은 다른 버전 데모 추가.
- SVN 업데이트로 PolygonNature·Adventure·Vikings·Western 팩이 새로 들어온 것을 확인. PolygonNature `Demo.unity`를 읽기 전용으로 분석: 루트는 `DemoScene`(지형·숲·늪·WindZone 포함), `Clouds`(32), 산맥 스카이박스 3, 스카이돔, Global Volume, 방향광 2, `FX`. 안개 선형 20~600m, 베이크 LightingData 있음. 두 팩 지형 모두 URP 기본 TerrainLit 재질(같은 GUID)이라 한 TerrainData에 레이어 혼합 가능.
- 결정: 환경은 PolygonNature Demo 씬 복사(디오라마 제거, 지형·WindZone은 분리해 유지, 베이크 조명 데이터 분리), 바다·WeatherControl·해저/해변은 PNB, 지형은 PolygonNature 지형 복사본(잔디 디테일 프로토타입 재사용) + PNB 레이어. 섬 테마는 아키타입별로 온대/열대(모래)/늪(수정), 외해일수록 고사목 증가. 같은 구역 규칙 유지를 위해 배치는 회전만.
- 새 Synty 팩과 프리팹 이름이 겹칠 수 있어 첫 데모 빌더의 프리팹 우선순위에 Generic을 기타 팩보다 앞에 두도록 1줄 보강(재실행은 안 함).
- 변경: `_Demo/Jihun/NatureMixArchipelago/` 신규(Editor/NatureMixArchipelagoBuilder.cs, Scripts 7개 네임스페이스만 바꾼 복사본, README.md), `NatureBiomesArchipelago/Editor/NatureBiomesArchipelagoBuilder.cs` 1줄, `docs/unity/DEMO_GUIDE.md` 1행.
- 검증: **미실행.** 작성 중 Unity MCP 연결이 끊겨(ECONNREFUSED) 컴파일·빌더 실행·화면 확인 불가. 새 스크립트 .meta는 Unity가 다음 임포트 때 생성.
- 사용자 실행 로그로 컴파일 성공 확인. 단, 원본 `Demo.unity`가 열린 채 미저장(dirty) 상태라 시작 가드에서 중단됨. 원본 씬은 빌드 중 디스크에서 다시 열리므로 원본 씬의 미저장 변경은 무시하고 진행하도록, 그 밖의 씬은 저장 여부를 묻도록 두 빌더 모두 수정(원본 에셋에 저장하지 않음). 수정 후 재실행 결과는 미확인.
- 사용자 빌드 결과 화면: 온대 섬 지면에 분홍·노랑·주황·흰 사각 판이 깔림. 원인 추정: 원본 지형의 디테일 프로토타입 전부를 같은 비율로 칠해, 메시형(꽃잎·낙엽 평면) 프로토타입이 URP 디테일 셰이더에서 알파 컷 없이 사각형으로 렌더링. 텍스처 잔디(`usePrototypeMesh == false`)만 칠하고 프로토타입 목록을 콘솔에 남기도록 수정. 재빌드 결과 미확인.
- 사용자 요청으로 방향 변경: "Synty 폴더 전부 사용, 메인 느낌은 Jungle". 베이스를 PNB `Demo_Day`·PNB 지형으로 되돌리고(디테일 사각형 원인 제거), PolygonNature 지형 레이어 3종(암벽·낙엽·진흙)을 보조로 추가. Pirates·Nature·Generic·ParticleFX·Vikings·Adventure·Western을 등대 섬 배 건조대·작업대, 채굴·어부 캠프, 수정 섬 이끼 유적·발광, 외해 고사목·안개, 모래 섬 부서진 배·뼈, 수평선 바위산 등에 배치. Adventure·Vikings·Western은 FBX만 있고 `importMaterials: 0`이라 배치 시 팩 재질을 씬 인스턴스에 지정. 세 팩 재질과 ParticleFX 모두 PolygonGeneric URP 셰이더 그래프 사용 확인(분홍 깨짐 위험 낮음). 빌더 전면 재작성, 컴파일·실행 미확인.
- 다음 행동: Unity MCP 재연결 → `Build Scene` 실행 → 나무·바위 크기, 산맥·구름 위치, 잔디 밀도 캡처 확인 후 수치 조정.

## 관련 원문·인수인계
- [데모 가이드](../../unity/DEMO_GUIDE.md)
