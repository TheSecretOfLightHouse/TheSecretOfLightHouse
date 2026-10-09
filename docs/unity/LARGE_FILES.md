# 파일 위치 규칙

## 규칙은 하나

| 무엇 | 어디 |
|---|---|
| **게임 본편** — 실제 게임에 들어갈 코드 · 씬 · 에셋 | **git** |
| **데모** — 씬 · 데모 전용 스크립트 · 데이터 전부 | **`Assets/ThirdParty/_Demo/<사람>/<주제>/`** (SVN) |
| 산 에셋 (Synty 등) | `Assets/ThirdParty/ThirdParty/<에셋명>/` (SVN, 수정 금지) |

- 로컬 SVN 체크아웃 루트는 `Assets/ThirdParty/`다. `1조 Unity Asset/`은 SVN 저장소 쪽 경로이며 로컬 경로에 덧붙이지 않는다.
- 데모 폴더 이름: `<사람>`은 영문 이름(예: `Jihun`), `<주제>`는 확인하려는 것을 PascalCase로(예: `GatherLoop`, `SecretVote`).
- 데모는 **스크립트·맵까지 데모 폴더에 통째로** 둔다. 크기는 신경 쓰지 않는다.
- 데모가 본편 코드를 쓰는 건 괜찮다. 반대(본편이 데모를 참조)는 안 된다.
- 다른 데모의 맵 복사(TerrainData 포함)와 데모 코드의 본편 이관 절차는 [데모 가이드](DEMO_GUIDE.md)를 따른다.
- 받을 때는 **git pull + SVN 업데이트 둘 다**.

## 커밋 검사 (git)

본편에 큰 파일이 실수로 들어가는 걸 막는다. GitHub 기준과 같다. 훅은 파일 크기를 바이트로 읽어 `N * 1024 * 1024`와 비교하므로 단위는 MiB다.

| 크기 | 결과 |
|---|---|
| 50 MiB 이하 | 통과 |
| 50 MiB 초과 ~ 100 MiB 이하 | 경고 (커밋은 됨) |
| 100 MiB 초과 | 커밋 차단 → 참조와 공유 방식을 검토한 뒤 분리·축소하거나 SVN 관리 위치를 정한다. 파일만 임의로 옮겨 본편 참조를 깨뜨리지 않는다 |

검사 훅은 [Tools/GitSetup/setup_git.bat](../../Tools/GitSetup/setup_git.bat)을 한 번 실행해야 켜진다. **clone 후 반드시 실행.**

## LFS

- **새로 쓰지 않는다.** (GitHub LFS 용량 한도 때문)
- 예전에 들어간 파일 76개(`Assets/Mirror/` 75개 + `Assets/TutorialInfo/Icons/URP.png` 1개, 약 15 MB)만 LFS에 남아 있다. `.gitattributes`의 "기존 LFS 파일" 규칙은 지우지 않는다.

## 확인 상태 (2026-10-09)

설정 도구와 Tools/GitHooks/pre-commit의 존재 및 50 MiB 초과 경고·100 MiB 초과 차단 임계값을 정적으로 확인했다. 도구는 Git LFS 필터, 로컬 훅 경로, 전역·로컬 SmartMerge 설정을 변경한다. 이번 문서 개편에서 도구 실행이나 실제 커밋 차단을 시험하지 않았다. 기존 LFS 관리 파일은 `git lfs ls-files`로 76개(`Assets/Mirror/` 75개 + `Assets/TutorialInfo/Icons/URP.png` 1개)를 확인했고, `git lfs ls-files -s`가 표시한 크기를 더하면 약 15 MB다.

SVN 체크아웃 루트는 현재 로컬에 없다. 데모 작업 절차는 [데모 가이드](DEMO_GUIDE.md)를 따른다.
