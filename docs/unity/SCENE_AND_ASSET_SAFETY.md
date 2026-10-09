# 씬·에셋 안전 규칙

## 1. 씬 소유와 저장

수정할 씬·프리팹과 담당 범위를 [담당표](../CODE_OWNERSHIP.md)와 [CODEOWNERS](../../.github/CODEOWNERS)에서 먼저 확인한다. 다른 담당자의 씬을 임의 저장하거나 씬 전체를 재생성하지 않는다. 소유자가 불명확하면 이름만으로 정하지 않고 같은 곳에서 담당 범위를 확인한다. Play·임포트·에디터 자동 저장으로 생긴 변경도 diff에서 확인한다.

씬 연결은 사용 가능한 Unity 에디터 API·도구로 진행한다. 에디터 접근이 없으면 재현 가능한 에디터 스크립트 또는 수동 연결 안내를 제공하고 미실행으로 기록한다. 도구가 있다는 이유로 실행 성공을 주장하지 않는다. `.unity`·`.prefab`·`.asset` YAML은 직접 편집하지 않는다. GUID·fileID 연결은 에디터 API로 다룬다.

## 2. `.meta`와 직렬화

- 에셋 이동·이름 변경에는 기존 `.meta`와 GUID를 보존한다. Unity 에디터의 이동 API를 쓴다.
- 파일만 복사해 기존 GUID를 중복 생성하지 않는다. 새 복사본은 새 에셋으로 만들고 참조 대상을 확인한다.
- 에셋과 `.meta`를 함께 커밋한다. `Assets/` 아래 폴더의 `.meta`도 포함한다.
- Visible Meta Files·Force Text 설정을 유지한다. Missing 참조를 임의 새 GUID로 덮지 않는다.
- 필드 이름·타입·enum 변경은 기존 직렬화 데이터를 확인한 뒤 진행한다.

## 3. 폴더와 외부 자산

현재 본편 폴더 `00.Scenes`, `01.Scripts`, `02.Prefabs`, `03.Images`, `04.Models`, `05.Animations`, `06.Textures`, `07.Materials`를 유지한다. 코드의 이름·배치는 [코딩 컨벤션](../coding-convention.md), 어셈블리는 [아키텍처 §3 어셈블리](../04-architecture.md#3-어셈블리)를 따른다.

| 대상 | 위치·관리 |
|---|---|
| 본편 | 기존 `Assets/` 구조, Git |
| 구매 에셋·데모 | SVN. 폴더 구조는 [파일 위치 규칙](LARGE_FILES.md) |
| Mirror | `Assets/Mirror/`, 기존 Git·LFS 규칙 유지 |
| UPM 패키지 | `Packages/manifest.json`·잠금 파일, 임의 Assets 이동 금지 |

2026-10-09 로컬에는 SVN 체크아웃 루트가 없다. 구매 목록·설치·권리 보유를 추정하지 않는다.

## 4. Terrain과 데모

데모 씬을 복사할 때의 TerrainData 분리 절차는 [데모 가이드](DEMO_GUIDE.md#맵)를, 본편이 데모를 참조하지 않는 규칙은 [파일 위치 규칙](LARGE_FILES.md)을 따른다.

## 5. Git·대용량·병합

[파일 위치 규칙](LARGE_FILES.md)에 따라 신규 LFS 사용을 추가하지 않는다. 기존 LFS 패턴은 유지한다. [설정 도구](../../Tools/GitSetup/setup_git.bat)가 큰 파일 훅과 UnityYAMLMerge 설정을 연결한다. SmartMerge 성공만으로 씬·프리팹 참조 검증을 대신하지 않는다. 충돌 해결 후 실제 에디터 로드와 해당 기능을 확인한다.

## 6. 사고 처리와 완료 확인

참조가 끊기거나 남의 씬이 바뀌면 먼저 현재 diff와 원본 근거를 보존한다. 복구 대상 파일을 한정하고 타인의 변경을 함께 되돌리지 않는다. 에셋·meta 누락, 중복 GUID, Missing Script, 의도하지 않은 씬 변경과 외부 원본 수정을 확인한다. 실제 실행·시각 검증이 불가능했다면 [검증 플레이북](../ai/03_VERIFICATION_PLAYBOOK.md)에 맞춰 남긴다.
