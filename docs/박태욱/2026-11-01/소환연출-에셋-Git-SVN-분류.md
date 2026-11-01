# 소환 연출 에셋 Git / SVN 분류

작성일: 2026-11-01  
담당 작업: 박태욱 / 일반 소환 12초 + 마피아 특수엔딩 31초  
기준: feature/taewook / 895236f0 및 현재 작업본  
상태: **사용자 지정 경로로 6개/.meta 이동 완료, SVN 등록·커밋 미실행**

## 결론

| 구분 | 파일 수 | 처리 |
|---|---:|---|
| SVN 경로 이동 완료 | 6 | 사용자 지정 MonsterAudio/크툴루 폴더. 기존 GUID 유지 |
| Git 유지 | 70 | 사용자 Git 허용 모델/텍스처, 자체 연출/효과/설정 및 기존 공용 참조 |
| 이미 SVN 경로에 있음 | 31 | 기존 경로 유지. SVN 추가·원격 커밋 완료 여부는 별도 확인 |
| 보관 위치 미정 | 0 | 비/천둥 2개도 사용자 결정으로 SVN 확정. 원산지 증빙 여부와 구분 |
| 합계 | 107 | .meta 제외, 아래 조사 범위 기준 |

모든 파일별 분류·근거·참조 범위·일부 크기/GUID는 [전체 목록 JSON](소환연출-에셋-Git-SVN-분류.json)에 기록했다.

사용자 요청에 따라 직접 제작/Git 허용 묶음과 외부 원본·외부 녹음 파생물을 구분했다. 보관 위치 분류가 개별 에셋의 라이선스 증빙 확보 완료를 뜻하지는 않는다.

## 1. SVN 경로로 이동한 파일 6개

공통 현재 폴더: `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/` (이동 전: `Assets/08.Audio/Boss/SpecialEnding/`)

| 파일 | 출처 근거 | 이동 완료 경로 |
|---|---|---|
| arctsound-dark-opener-615561.mp3 | 이번 채팅에서 사용자 제공한 외부 음악. 원본과 복사본 SHA256 일치 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/arctsound-dark-opener-615561.mp3` |
| PressureCrack.wav | 기존 `PlayerAudio/01_Stone Hit 1.wav`를 조각내고 피치/간격/감쇠를 편집한 파생 효과음 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/PressureCrack.wav` |
| CollapseBurst.wav | 같은 돌 타격음 편집 파생 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/CollapseBurst.wav` |
| DebrisLand.wav | 같은 돌 타격음 편집 파생 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/DebrisLand.wav` |
| StormRainLoop.wav | 원본 생성 방식은 미확인, 사용자 지정으로 SVN 보관 확정 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/StormRainLoop.wav` |
| ThunderRoll.wav | 기존 번개음 기반 가공 기록, 사용자 지정으로 SVN 보관 확정 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/크툴루/ThunderRoll.wav` |

근거: [특수엔딩 제작 기록](마피아-특수승리-테스트씬과-메인연결.md)의 파괴 효과 사운드/배경음악 항목. 원본 돌 소리는 이미 `Assets/ThirdParty/1조 Unity Asset/PlayerAudio/01_Stone Hit 1.wav`에 있다.

- **편집·합성했다는 이유로 외부 녹음 파생물을 독립 자체 음원으로 분류하지 않았다.**
- 음악의 정확한 원본 페이지/다운로드 증빙/허가문은 아직 확보하지 않았다. 파일명의 ID 615561과 다른 곡의 라이선스를 혼동하지 않는다.
- 현재 돌 효과음 3개는 Git 추적 중이다. 실제 이동 때 기존 위치 제거와 SVN 새 위치 전달을 함께 확인해야 한다. 이동만으로 Git 과거 이력이 삭제되지는 않으며 이번 요청에서 이력 변경은 하지 않는다.
- 파일 6개와 각각의 **기존 .meta 6개**가 한 묶음이다.

## 2. Git에 남길 사용자 확인 묶음

### 크툴루 / 특수엔딩 모델

사용자 확인: **“크툴루는 AI로 직접 뽑은거야 이건 git에 올려도 돼.”**

- `Assets/04.Models/Boss/SpecialEnding/MafiaSpecialEnding_Combined.fbx`
- `Assets/06.Textures/Boss/SpecialEnding/AlienBoss_BaseColor.jpg`
- `Assets/06.Textures/Boss/SpecialEnding/AlienBoss_EyeEmission.png` — 위 텍스처에서 추출한 눈 마스크
- `Assets/07.Materials/Boss/SpecialEnding/tripo_node_294c0fd3-73d3-4e28-b465-00cf7e238359_material.mat`
- `Assets/05.Animations/Boss/SpecialEnding/MafiaSpecialEnding_Colossal.anim` — Blender/Unity에서 제작·조정한 연출 파생 클립

통합 FBX에는 등대 파괴용 표현도 포함된다. 연결된 Synty 지붕 텍스처/기존 원본 머티리얼은 계속 SVN 원본을 참조하며, AI 생성 확인을 그 외 구매 원본 전체로 확대하지 않는다.

### 일반 소환 촉수

등대 촉수·주변 채찍 촉수·Tentacle_Stylized 텍스처 묶음에 대해 사용자 확인: **“그거도 git에 올려도 돼.”** 구체적인 모델 생성 서비스를 추가 추정하지 않았다.

- `Assets/04.Models/Boss/LighthouseTentacle_Model.fbx`
- `Assets/04.Models/Boss/WhipTentacle_Model.fbx`
- `Assets/05.Animations/Boss/LighthouseTentacle_Emerge.fbx`
- `Assets/05.Animations/Boss/LighthouseTentacle_Squeeze.fbx`
- `Assets/05.Animations/Boss/WhipTentacle_Idle.fbx`
- `Assets/05.Animations/Boss/WhipTentacle_Attack.fbx`
- `Assets/06.Textures/Boss/Tentacle_Stylized_*` — BaseColor/Normal/Metallic/Roughness/MetallicSmoothness
- 해당 자체 프리팹·컨트롤러·파생 등장/Idle 클립·재질 설정

## 3. Git에 남길 자체 제작·연출 구성

| 묶음 | 주요 파일/내용 | 근거 |
|---|---|---|
| 공용 연출 구성 | MainSceneSummonRig.prefab, MainSceneSpecialEndingRig.prefab, 두 Timeline | 직접 조립한 배치·트랙·바인딩·효과 설정. 구매 원본은 참조로 연결 |
| 카메라 | SummonCinematic_Camera.anim, SpecialEnding_Camera.anim | 자체 카메라 이동/회전 곡선 |
| 런타임 코드 | SpecialEndingLighting/Storm/WaterCurtain/WaterFoamRing/WaterSurfaceAnchor/IslandBinding 등 | 프로젝트 자체 표현 코드 |
| 절차 먼지/파편 | Lighthouse_Dust.asset, Lighthouse_Fragment.asset 및 재질/프리팹 | [먼지·파편 제작 기록](../2026-10-31/등대-촉수-타격-먼지와파편.md)에 직접 생성 명시 |
| 물막/거품 | WaterCurtain.shader/.mat, WaterEmergenceFoam.shader, EmergenceFoamRing.asset 등 | 프로젝트에서 만든 표면 흐름·거품 표현 |
| 비 표현 | StormRainDrop.png, StormRain.mat | 작업 중 만든 빗방울 이미지와 재질 설정 |
| 후처리 | SpecialEnding_StormLook.asset | 자체 Volume 파라미터 구성 |
| 자체 합성 음향 | Summon_DeepRumble.wav, Summon_WaterRise.wav, Summon_CoilCreak.wav | [일반 소환 5단계](../2026-10-30/소환연출-5단계-Timeline.md)에 “자체 생성 PCM 음원 3종” 명시 |
| 하늘 셰이더 | SpecialEnding_StormSky.shader/.mat | 팀 데모 EerieNightSky.shader를 복제·확장. 원본은 `_Demo/Jihun/GatherLoop/Shaders/`에 있으며 외부 구매 셰이더라는 근거는 찾지 못함 |
| 연출용 재질 설정 | Fracture_Interior_Stone, StoneBands_003~005, IvoryPlaster_003~005, Gallery_*, LanternGlow_001, PolygonPirates_01_A_001 등 | 새로 생성/복제해 조정한 설정 파일. **외부 텍스처까지 자체 제작이라는 뜻은 아님** |
| 검증용 자체 씬·설정 | TentacleIslandTest / SpecialEndingIslandTest 및 관련 프리뷰 코드 | 에셋의 외부 저작물 여부와 씬의 테스트 용도는 별도. 이번에는 이동·삭제하지 않음 |

Player의 CollisionWater 셰이더/거품·물방울 재질과 PlayerSoundManager, 몬스터 설정처럼 기존 공용 참조인 파일도 현 Git 위치를 유지한다. 다른 담당 원본을 이번 작업에서 이동하지 않는다.

`MafiaSpecialEnding_Colossal.anim`은 **94.93 MiB**로 Git의 50 MiB 경고 구간에 있다. 크기 경고와 라이선스 분류는 별개이며, 이번 사용자 Git 허용을 무시하고 SVN으로 분류하지 않았다.

## 4. 이미 SVN 경로에 있는 참조 31개

| 묶음 | Rig 재귀 의존성의 파일 수 | 현재 위치 |
|---|---:|---|
| NamuFX 물보라 | 9 | `Assets/ThirdParty/1조 Unity Asset/ThirdParty/NamuFX/` |
| 기존 PlayerAudio | 10 | `Assets/ThirdParty/1조 Unity Asset/PlayerAudio/` |
| 주변 촉수 등장/타격음 | 2 | `Assets/ThirdParty/1조 Unity Asset/MonsterAudio/등대주변촉수/` |
| Synty 지붕 텍스처/재질/셰이더 | 4 | `Assets/ThirdParty/1조 Unity Asset/ThirdParty/Synty/` |
| 기존 등대 공유 재질 | 6 | `Assets/ThirdParty/1조 Unity Asset/World/Final/Shared/LighthouseReference/` |

일반 촉수 등장음은 자체 `Summon_WaterRise.wav`에서 외부 `koiroylers-water-splash-2-355957.mp3`로 교체된 이력이 있다. 이 MP3는 이미 SVN 경로에 있으므로 다시 Git/Boss 폴더로 복사하지 않는다. 근거: [주변 촉수 사운드 기록](../2026-10-31/버블터틀과-주변촉수-사운드.md).

NamuFX는 기존에 Unity API로 SVN 경로로 이동한 기록이 있다. 근거: [Bubbles Burst 교체 기록](../2026-10-31/소환연출-BubblesBurst-교체.md).

여기서 **“SVN 경로”는 실제 로컬 위치 확인**이다. .svn 체크아웃 존재는 확인했지만 svn CLI는 PATH에서 찾지 못했고, 모든 파일의 add/원격 commit 상태는 검증하지 않았다.

## 5. 비/천둥 보관 위치 후속 결정

공통 폴더: `Assets/08.Audio/Boss/SpecialEnding/`

| 파일 | 확인된 것 | 아직 필요한 것 |
|---|---|---|
| ThunderRoll.wav | 작업 기록에 “기존 번개 충격음 기반 저음 롤”이라고 명시 | 기존 번개 충격음의 정확한 원본 경로/제작자. 외부 녹음이면 가공본도 SVN 후보 |
| StormRainLoop.wav | 작업 중 생성·합성해 저장한 빗소리 루프 | 외부 빗소리 녹음 편집인지, 독립적인 노이즈/절차 합성인지 |

사용자 후속 결정: **두 파일도 SVN에 보관한다.** 위 원본 출처의 미확인 상태는 그대로 기록하되, 보관 위치 결정을 보류하지 않는다. 두 파일 모두 `MonsterAudio/SpecialEnding/WeatherDerived/` 이동 대상으로 확정했다. 후속 사용자 지정에 따라 크툴루 폴더로 실제 이동을 완료했다. SVN 등록·커밋은 아직 수행하지 않았다.

## 6. 조사 범위와 이동 시 유의점

- Unity `AssetDatabase.GetDependencies`로 일반 Rig **44개**, 특수 Rig **58개**, 중복 제거 **97개**의 비-C# Assets 의존성을 추출했다.
- Boss의 모델/애니메이션/텍스처/재질/오디오/프리팹 폴더 **71개**도 대조했다. 현재 두 Rig가 직접적으로 보유하지 않는 제작 원본·테스트 파일10개를 더해 분류 목록은107개다.
- 테스트 씬의 정적 의존성은 각각317/760개다. World 설정을 통해 다른 섬·파밍 정의까지 확장되므로 그 전부를 “이번 컷씬이 새로 사용한 외부 에셋”으로 간주하지 않았다. 전체 스냅샷은 JSON에 보존했다.
- 정적 의존성에는 FBX 기본 재질·공용 프리팹을 통해 따라오는 간접 참조도 있다. 실제 화면 노출/오디오 재생 여부와 구분한다. Packages 및 C# 의존성은 미디어 파일 수에서 제외했다.
- 실제 이동은 Unity `AssetDatabase.MoveAsset` 등 에디터 API로 수행하고 **기존 GUID/.meta를 유지**해야 한다. 이동 뒤 Timeline/프리팹 참조·음악 페이드·사운드 출력·Missing 참조를 재검증한다.
- 기존 사용자 변경·staged 상태 보존. 분류 문서/목록 외 실제 에셋은 이번 요청에서 수정하지 않았다. Git/SVN 커밋·푸시·이력 정리 미실행.

## 7. 씬을 통해 따라온 별도 확인 사항

두 Rig 자체 외에 테스트 씬의 비-SVN 원본 미디어도 추가로 살폈다. 아래는 위107개 분류 수에 더하지 않은 다른 담당/패키지 참고 항목이다.

- `Assets/04.Models/Object/wooden workbench 3d model low/wooden_workbench_3d_model_low.fbx`
- `Assets/04.Models/Object/wooden workbench 3d model low/wooden_workbench_3d_model_low.fbm/wooden_workbench_3d_model_low_basecolor.jpg`

이2개는 기존 섬의 작업대 경로에서 따라온 간접 참조다. 파일명만으로 외부 다운로드/AI 생성 여부를 확정할 수 없으므로 Lighthouse 담당의 출처 확인 대상으로 기록했다. 이번에 새로 가져온 컷씬 에셋으로 간주하거나 임의 이동하지 않았다.

추가로 나온 `Assets/Mirror/Editor/Icon/MirrorIcon.png`는 기존 Mirror 패키지 항목이므로 패키지와 LICENSE의 기존 관리를 유지한다. 일반 촉수 Idle FBX도 씬에서 발견됐지만 이미 위 사용자 Git 허용 묶음에 포함했다.

목록 검증: 분류107개 에셋과 대응 .meta가 모두 실제 존재함을 확인했고, blank-at-eol 제외 `git diff --check`를 통과했다.

## 8. 사용자 지정 경로로 이동 완료

2026-11-01 후속 요청에 따라 확정6개를 MonsterAudio/크툴루 폴더에 하위 폴더 없이 이동했다. Unity AssetDatabase.MoveAsset으로 기존 .meta6개와 GUID6개를 보존했고, 새 폴더.meta도 Unity가 생성했다. 원래 위치의 파일/meta는 남아 있지 않다. 이동 후 Timeline 오디오 클립11개의 누락0, 이동한 음원6개 모두 새 경로 참조, 연출31초 유지 및 콘솔 오류0을 확인했다. 실제 Play 재청취는 이번 경로 이동 검증에서 수행하지 않았다. SVN 추가/커밋과 Git 커밋/푸시는 실행하지 않았다.
