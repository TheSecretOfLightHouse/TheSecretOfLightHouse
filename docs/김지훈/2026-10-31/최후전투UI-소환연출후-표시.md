# 최후 전투 UI를 소환 연출 종료 뒤 표시

작성일: 2026-10-31 · 담당: 김지훈 · 상태: 구현, 컴파일 error 0, 플레이 미확인, 미커밋

## 요청
최후 전투 시네머신(소환 Timeline) 연출이 끝나면 최후 전투 UI 연출이 뜨게 시점을 조절한다.

## 원인
- `TowerMatchNet.TryBeginFinalBattle`은 소환 순간 `Phase = FinalBattle`로 바꾸고 연출을 `Preparing`부터 시작한다.
- `TowerMatchEffects`(“크툴루가 깨어났다 — 최후 전투 시작” 알림·붉은 가장자리·소리)와 `FinalBattleHud`(제목·내구도 낙하)는 `Phase == FinalBattle`에 바로 반응해, 연출 준비·재생 중(`SummonCinematicView`가 Canvas를 끔)에 소모됐다.

## 변경
- `TowerMatchNet.IsFinalBattleRevealed` 추가: `FinalBattle`이면서 연출이 끝났거나(`!IsSummonCinematicPlaying`) 카메라가 돌아온 `Returning` 단계일 때 true.
  - `Returning`부터 연 이유: 각자 화면은 재생 종료 때 복귀하고, `Combat`은 다른 참가자 복귀 보고+여유 시간을 기다리므로 그만큼 빈 화면이 생긴다.
- `FinalBattleHud`: 전투 그룹 표시·낙하를 `IsFinalBattleRevealed` 기준으로 변경.
- `TowerMatchEffects.ObservePhase`: `FinalBattle`이 아직 공개 전이면 표시 단계 전환을 보류. 보류 중 점화 의식 카운트다운이 0초로 남지 않도록 `UpdateCountdown`은 실제 단계도 `Ritual`일 때만 표시.
- 늦게 들어온 클라이언트의 첫 단계 처리(재생 없이 상태만 반영)는 그대로다.

## 변경 파일
- `Assets/01.Scripts/04.Lighthouse/Tower/Net/TowerMatchNet.cs`
- `Assets/01.Scripts/04.Lighthouse/Tower/View/FinalBattleHud.cs`
- `Assets/01.Scripts/04.Lighthouse/Tower/View/TowerMatchEffects.cs`

## 검증
- 실행: Unity 스크립트 재컴파일 후 콘솔 error 0.
- 미실행: 플레이에서 소환 → 연출 → 복귀 직후 알림·HUD 낙하 확인, 원격 클라이언트 확인.

## 남은 일
- JH04에서 소환해 연출 종료 직후 UI가 뜨는지 플레이 확인.
