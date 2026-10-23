# 최후 전투 HUD (수리도·내구도)

작성일: 2026-10-23

## 목표
사용자 시안(등대 수리도 / 최후 전투 / 등대 내구도 상단 띠)을 분리된 스프라이트로 조립하고, 실제 동기화 값과 연결한다.

## 수행
- `output/hp-ui-separated-v1`의 분리 PNG 9장을 `Assets/03.Images/FinalBattleHud/fb_*.png`로 복사(스프라이트 설정은 빌더가 적용).
- 런타임 `Assets/01.Scripts/04.Lighthouse/Tower/View/FinalBattleHud.cs`
  - `TowerMatchNet.Phase == FinalBattle`일 때만 표시.
  - 수리도 = `TowerNet.RepairRatio`(설치 모듈 기준), 내구도 = `Health / MaxHealth`. 둘 다 SyncVar·SyncList라 모든 기기에 같은 값.
  - SyncVar에 훅이 없어 LateUpdate에서 읽고, 표시 숫자가 바뀔 때만 문자열을 만든다. 게이지는 `_fillSpeed`로 부드럽게 따라가고, 내구도가 줄면 빨간 게이지가 잠깐 밝아진다.
  - 한글 표시는 기존 화면과 같은 OS 폰트(맑은 고딕) 방식.
- 에디터 빌더 `Assets/01.Scripts/04.Lighthouse/Workbench/Editor/FinalBattleHudSetup.cs`
  - `Lighthouse/Tower/Build Final Battle HUD Prefab` → `Assets/02.Prefabs/UI/Tower/FinalBattleHud.prefab` (1920×1080 기준, 상단 중앙 1500×180 띠, 정렬 60 = 스테이션 화면 80 아래).
  - `Lighthouse/Tower/Use Final Battle HUD In Open Scene` → 씬의 유일한 `TowerMatchNet`에 연결.
- `JH04TestScene`에 프리팹 인스턴스를 추가하고 `_match` = `Lighthouse` 연결 후 저장(열려 있던 `JH04Offline`의 미저장 변경은 건드리지 않으려고 추가 로드로 처리).

## 검증
- 실행: 컴파일 오류 없음, 프리팹 빌드 로그 확인, 62.5% / 680/1000 값으로 오프스크린 렌더해 시안과 배치 비교.
- 미실행: 플레이 모드에서 소환 → 피격·모듈 설치 시 게이지 변화, 원격 클라이언트 표시.

## 남은 일
- M0 플레이: TowerTestPanel/TowerTrialRound로 소환·피해·설치를 넣어 게이지·숫자·피격 반짝임 확인.
- `TWJH04TestScene`(박태욱)에도 붙일지 결정 — 다른 담당자 씬이라 합의 필요.
- 경기 종료(Ended) 후에는 HUD가 숨는다. 결과 화면은 MK06 담당.
