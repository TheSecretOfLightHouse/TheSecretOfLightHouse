# 메인 흐름 파도 BGM
작성자: 김지훈
작성일: 2026-11-01
갱신일: 2026-11-01
상태: 구현됨(씬 전환·청취 미검증)
브랜치·기준 커밋: feature/jihun · 7b1e30f7

## 요청과 목표
메인 게임 씬 입장 전 `beach_waves_ocean_calm_loop_02`를 반복 재생한다.

## 진행 과정과 결정 이유
- 기존 세션 화면과 SoundManager를 확인했다. 첫 화면·로비용 BGM 호출은 확인하지 못했으며, 공용 SoundManager의 음악 볼륨·음소거 설정과 반복 기능을 재사용했다.
- 세션 화면이 첫 화면·로비·로딩 중 재생하고 네트워크 매니저에 설정된 게임 씬이 활성화되면 중지한다. 로비로 돌아오면 다시 시작한다. 서버 판정이나 공용 오디오 코드는 수정하지 않았다.
- 지정 WAV는 SVN 체크아웃의 `Assets/ThirdParty/1조 Unity Asset/jihunSound/`에 있는 원본을 참조한다. 다른 PC와 빌드에 해당 에셋과 meta가 있어야 한다.

## 변경 파일과 결과
- `Assets/01.Scripts/01.Network/Session/Net/LighthouseNetworkManager.cs`: 설정된 게임 씬 읽기 속성.
- `Assets/01.Scripts/01.Network/Session/View/SessionScreen.cs`: 세션 음악 시작·중지.
- `Assets/02.Prefabs/UI/Session/SessionEntryScreen.prefab`: WAV와 기존 SoundManager 프리팹 연결. 기존 미커밋 로고 변경을 보존했다.

## 검증
- Unity 컴파일 후 콘솔 error 0. MainOffline 플레이 모드에서 AudioSource의 클립 이름, 재생 중, loop=true, 볼륨 0.7 확인.
- 실제 청취, 반복 경계, 로비→게임 씬→로비 전환 및 원격 클라이언트 재생은 미검증.

## 남은 일과 차단 조건
- 본편 흐름에서 청취·반복 경계·음소거·게임 씬 진입 시 종료·로비 복귀 재생과 원격 클라이언트 확인.
- WAV와 meta의 SVN 공유 상태 확인. 미커밋.

## 다음 행동
MainOffline에서 방 생성 후 게임 씬까지 진행해 소리 전환과 볼륨을 확인한다.

## 관련 원문·인수인계
- [담당범위 사운드 조사](../2026-10-31/담당범위-사운드-조사.md)
