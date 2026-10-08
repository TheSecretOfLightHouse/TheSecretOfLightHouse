# 파일 위치 규칙

## 규칙은 하나

| 무엇 | 어디 |
|---|---|
| **게임 본편** — 실제 게임에 들어갈 코드 · 씬 · 에셋 | **git** |
| **데모** — 씬 · 데모 전용 스크립트 · 데이터 전부 | **`Assets/ThirdParty/1조 Unity Asset/_Demo/<사람>/<주제>/`** (SVN) |
| 산 에셋 (Synty 등) | `Assets/ThirdParty/1조 Unity Asset/ThirdParty/<에셋명>/` (SVN) |

- SVN 체크아웃 루트는 `Assets/ThirdParty/1조 Unity Asset/`.
- 데모 폴더 이름: `<사람>`은 영문 이름(예: `Jihun`), `<주제>`는 확인하려는 것(예: `GatherLoop`).
- 데모는 **스크립트·맵까지 데모 폴더에 통째로** 둔다. 크기는 신경 쓰지 않는다.
- 다른 데모의 맵을 쓰려면 데모 폴더로 복사해서 쓴다. 지형까지 고칠 거면 TerrainData도 복사한다 (씬만 복사하면 원본 지형이 바뀐다).
- 데모가 본편 코드를 쓰는 건 괜찮다. 반대(본편이 데모를 참조)는 안 된다.
- 데모 코드를 본편에 쓰기로 하면 그때 `01_Scripts/`로 옮겨 git에 올린다.
- 받을 때는 **git pull + SVN 업데이트 둘 다**.

## 커밋 검사 (git)

본편에 큰 파일이 실수로 들어가는 걸 막는다. GitHub 기준과 같다.

| 크기 | 결과 |
|---|---|
| 50MB 이하 | 통과 |
| 50 ~ 100MB | 경고 (커밋은 됨) |
| 100MB 초과 | 커밋 차단 → ThirdParty로 옮긴다 |

검사 훅은 `Tools/GitSetup/setup_git.bat`을 한 번 실행해야 켜진다. **clone 후 반드시 실행.**

## LFS

- **새로 쓰지 않는다.** (GitHub LFS 용량 한도 때문)
- 예전에 들어간 Mirror 파일 76개(약 14MB)만 LFS에 남아 있다. `.gitattributes`의 "기존 LFS 파일" 규칙은 지우지 않는다.
