# BC4 4b 마무리: 질량 1 재측정·껍데기 `BuoyancyBody` 삭제 결과

경로: docs/강민서/2026-10-28/BC4-4b-마무리-결과.md
작성일: 2026-10-28
브랜치: feature/minseo
선행: [4b 1~2단계](BC4-4b-1단계-기준질량-전환-결과.md)

## 1. 한 일

| 항목 | 방법 | 결과 | 상태 |
|---|---|---|---|
| 순수 검증 | `BuoyancyChecks`·`WaveDriftChecks`·`BuoyancyHeaveChecks` `RunAll()` | 3종 모두 true(작업 전·껍데기 삭제 후 두 번) | 실행 검증(에디터) |
| 질량 1 기준 | `WorldSessionGame` Play, `captainBoat` 스폰 후 `TrySetOwner` | 질량 1·중력 켜짐, 비키네마틱으로 풀린 뒤 y≈-0.22 m에서 안정(4a 질량 500 값 -0.21과 일치), 발사·침몰 없음 | 실행 검증(1인) |
| 폭풍벽 `Impulse` | 일 단위를 1일차로 고정(`Resync`+`Stop`, 해금 `Inner`)한 뒤 x=365에 배치 | 약 4초에 x=334.5(속도 -7.0 m/s), 최대 -12.2 m/s. 4a의 질량 500 기준(4초 333.9 m)과 같음. 해금 전체(`All`)일 때는 힘 0 | 실행 검증(1인) |
| `WaveSlam` 임계 | 부력 읽기 `MeanDepth` 변화율을 프레임마다 기록 | 20초 폭풍 파도: 최대 변화율 0.97/s(임계 3 미달). 6 m 낙하: 최대 6.58/s(4a 7.01). 임계 3 유지 | 실행 검증(변화율 대용 지표, `SlamReporter` 이벤트 자체는 미관찰) |
| `WorldSandbox` 씬 | 배 2개 확인 | 질량 1·중력 켜짐 → 보정 불필요 | 실행 검증(에디터 상태) |
| 껍데기 삭제 | 스크립트 참조(`m_Script`)를 `SerializedObject`로 `BuoyancyObject`에 교체해 필드값 유지 | 프리팹 5개·`WorldSandbox`·백업 씬 모두 교체, GUID `dc0f9969…` 참조 0, `BuoyancyBody.cs(.meta)` 삭제, 컴파일 오류 0, 프리팹 Missing Script 0·`BuoyancyObject`/`BoatBuoyancyAuthority` 각 1개 | 파일 확인·에디터 확인 |

## 2. 변경 파일

- 삭제: `Ocean/BuoyancyBody.cs`(+`.meta`).
- `02.Prefabs/Player/` 프리팹 5개: 스크립트 GUID 교체와 낡은 `_mass: 500` 직렬화 필드 제거(파일당 3줄 diff).
- `00.Scenes/Minseo/WorldSandbox.unity`: 껍데기 2개 교체. 같은 씬에 남아 있던 4a 이전 필드(`_tuning`, `_slamThreshold`)가 새 기본값 프로필로 바뀜. 샌드박스 배는 프리팹의 판 두께 0.5·배율 4가 아니라 기본 프로필(0·3)이다.
- `WorldSandbox_BeforeBC4Verify_20261028.unity`(미추적 백업): 같은 교체. 필요 없으면 삭제해도 된다.

## 3. 미검증과 주의

- **교체 후 Play는 확인하지 못했다.** 프리팹 교체 뒤 Play를 다시 켰을 때 세션이 로비에서 멈춰 플레이어 오브젝트가 없었다(앞선 Play는 게임 씬에서 바로 시작했다). 교체 전과 같은 기반 클래스이고 정적 검사는 통과했지만, `Boat` 탑승·부력 구동은 다음 Play에서 확인해야 한다.
- 부력비 0.6 침몰, Maelstrom, 해안 파도, 도크 탑승(`DockZone` 없음), 체감 멀미, 4인 네트워크, `MainGame`은 미검증이다.
- 플레이어 담당 파일(프리팹 5개)을 수정했다. 사용자가 "다 진행 가능"이라 답해 진행했으며, 알림 3(껍데기 삭제)·알림 5(`SailArea`·`RightingStrength` 정리 안내)의 전달 여부는 사용자가 확인한다.
- 시험 보트 소유 설정은 `BoatController.TrySetOwner`로 했다(소유자가 없으면 키네마틱 유지). 4a 문서의 "스폰만 하면 된다"는 설명은 이것이 빠져 있다.
- 미커밋. 위 변경은 작업 트리에만 있다.
