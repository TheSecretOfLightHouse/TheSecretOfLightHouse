# 인수인계 — BC4 4a 마무리와 다음 단계 (4a 잔여 검증, 커밋, 4b)

경로: docs/handover/강민서/2026-10-28/BC4-4a-부력교체-시작-인수인계.md
작성일: 2026-10-28 (같은 날 시작용 문서를 마무리 상태로 갱신)
브랜치: feature/minseo / 기준 HEAD 7c48d7a2 (4a 변경은 모두 미커밋)
담당 파트: 강민서 World (Ocean). 플레이어(Boat 프리팹·`BoatController`·`PlayerCamera`)와 Monsters는 읽기 전용이며 수정은 수정 전에 알린다
수신자: BC4 4a 잔여와 4b를 이어서 진행할 다음 세션

## 1. 목표

- 최종 상태: 배가 `BuoyantObject` 부력으로 뜨고(4a), 질량 1 기준으로 외력·표류·Slam이 재튜닝되며 `BuoyancyBody` 껍데기가 삭제된다(4b).
- 이번 문서 범위: 4a는 사용자 결정으로 현재 설정으로 마무리했고 이후 재조정 가능. 남은 4a 검증·커밋·알림과 4b를 다음 세션이 이어간다.
- 근거 문서: [4a 결과·조정표](../../../강민서/2026-10-28/BC4-4a-1단계-코드-교체-결과.md)(마지막 절 "4a 종료 시점 최종 설정과 조정 위치"), [설계 원문 BC4 확정 절](../../../강민서/2026-10-25/범용부력과-자연파도-재설계-검토.md#2026-10-28-bc4-확정-4a부력-교체4b질량-1-전환과-재튜닝-분할), [BC1 2단계 결과](../../../강민서/2026-10-28/BC1-2단계-BuoyantObject-시험물체-결과.md), [담당자 pointer](../../../강민서/POINTER.md)
- 재개 문장: **"강민서 물 재설계 BC4 4a 마무리 이어서 해줘"** (4b는 "강민서 물 재설계 BC4 4b 시작해줘")

## 2. 완료한 것

| 항목 | 증거 | 상태 |
|---|---|---|
| `BuoyantObject`에 `IsSimulating`, 부력점을 수동 점 → `IShipBody` → 콜라이더 순으로 결정 | 컴파일 오류 0, Play | 구현됨, 실행 검증(에디터 1인) |
| `BuoyancyBody`를 `BuoyantObject` 하위 껍데기로 축소(GUID 유지), `BoatBuoyancyAuthority` 전환 | 컴파일 오류 0, 5개 프리팹 동작 | 실행 검증(에디터 1인) |
| `WaveDriftBody`가 `Impulse` 경로 힘을 `AddForce`로 흡수, `ExternalForceApplier` 삭제 | 코드·컴파일 | 코드 확인, **`Impulse` 실측 미실행** |
| `WaveSlamSource` 신설(`BoatShipBody`가 자동 추가) | 6 m 낙하 시 `WaveSlam` 강도 7.01 1회 | 실행 검증(낙하 시험) |
| 프리팹 5개 부력 프로필·부력점 입력(조정표 참조) | Storm 탑승 Play: 침수·수면 위 0%, 루트 피치·롤 최대 0.2° | 실행 검증(에디터 1인, 약 10~70초 창) |
| `WaveTiltVisual`(Model 시각 기울기), `WaveCameraGuard`(카메라 수면 아래 방지·흔들림 상쇄), `HeaveFollow`·`SurfaceFollow` 도입 | 위와 같음, 카메라 높이 표준편차 0.10~0.18 m | 실행 검증(에디터 1인) |
| 원인 규명: 점별 수면 경사 힘이 루트를 최대 13° 기울이고 돌림 → `SurfaceFollow` 0 | 같은 장면 전후 측정 | 실행 검증 |

## 3. 완료하지 못한 것

| 항목 | 어디까지 했나 | 막힌 지점 |
|---|---|---|
| 폭풍벽 `Impulse`가 이동에 반영되는지 | Calm이라 힘 0, 코드만 연결 | 폭풍벽 활성 조건(날씨·위치) 확인 필요 |
| 부력비 0.6(침몰) 확인 | 미실행 | 없음 |
| 도크 판정을 통한 정상 탑승·하선 | `_isOccupied`를 리플렉션으로 켜 탑승 상태만 재현 | 도크에 붙은 배 시작 위치가 필요(`MainGame` 시작 배는 도크 밖) |
| 항해(속도) 중 검증 | W 키 입력을 보냈으나 속력 0.58 m/s 이하 | 입력이 안 먹은 원인 미확인(포커스·입력 경로 추정) |
| Maelstrom, 섬 해안 파도 구간 침수, 4인 | 미실행 | 없음 / 4인은 M2 필요 |
| `WaveSlam` 임계(3) 재측정 | 판 0.2·`HeaveFollow` 0.85 기준으로 안 함 | 4b에서 질량 1과 함께 |
| 플레이어 담당 알림(아래 6장) | 알림 1(프리팹 편집 동의)만 완료 | 사용자가 보낸다 |
| 4b 전체 | 계획만(설계 원문 4b 범위) | 4a 마무리와 알림 2~4 |

## 4. 현재 저장소 상태

- 커밋한 것: BC1 1~2단계 `478f4b75`, `5cf99cf1`, `7c48d7a2`(+문서 `6c3ccfbf`). push 안 함.
- 커밋하지 않은 변경(이번 4a, 커밋 대상): `Ocean/` 코드 — `BoatShipBody`, `BuoyancyBody`, `BuoyantObject`, `Net/BoatBuoyancyAuthority`, `Rules/BuoyancyForceApplier`, `Rules/BuoyancyProfile`, `WaveDriftBody`, 신규 `WaveSlamSource`·`View/WaveTiltVisual`·`View/WaveCameraGuard`(각 `.meta`), 삭제 `ExternalForceApplier`(+`.meta`); 프리팹 5개 `Assets/02.Prefabs/Player/{Boat,captainBoat,Cube,gardenBoat,TankerBoat}.prefab`; 문서 — 4a 결과·이 인수인계·설계 원문·BC1 2단계 결과(폭풍 후속 절)·담당자 pointer·`docs/WORK_POINTER.md`.
- 4a와 무관한 미커밋 변경(커밋에 넣지 않는다): `Ocean/Rules/WaveModel.cs`(`InverseIterations` 6, 이전 파도 정합 작업), `Clock/*`, `Island_*.asset`, `Island_Lighthouse_V12.prefab.meta`, `Lighthouse.EditorTools.asmdef`, `Mirror/Editor/NetworkInformationPreview.cs`, `QualitySettings.asset`, 에셋·씬 미추적 다수. 확인은 `git status`.
- 수정한 씬: 없음(저장한 씬 없음). `WorldSandbox`가 열려 있다. 백업 씬 `Assets/00.Scenes/Minseo/WorldSandbox_BeforeBC4Verify_20261028.unity`(미추적, 커밋하지 않는다)에 이전 `BuoyancyTest` 오브젝트 상태가 들어 있다.
- 신규 ScriptableObject: 없음.

## 5. 검증 현황

- L1: 컴파일 오류 0(에디터 재컴파일).
- L2: `WorldSandbox` 시험 낙하·Calm, `WorldSessionGame` Storm 탑승 Play(1인 에디터). 수치는 결과 문서에 있다.
- L3 / 멀티플레이: M0(에디터 1인)까지. 4인·원격은 미실행.
- 미검증 항목과 이유: `Impulse`, 부력비 0.6, 항해 중, 도크 탑승, Maelstrom, 해안 파도, 체감 멀미(사용자 직접 확인 필요), 4인(인원 필요), `Rules` 순수 검증 코드(`BuoyancyChecks`)에 `HeaveFollow` 케이스 미추가.

## 6. 차단 조건

- [ ] 다른 담당 알림(사용자가 플레이어 담당에게 보낸다, 시점 표는 설계 원문 확정 절):
  - 알림 1: 완료(프리팹 편집 동의). 단 **프리팹 `BoatShipBody._buoyancyPoints` y 변경과 `WaveCameraGuard`(PlayerCamera 뒤에서 카메라 위치 보정)는 동의 범위를 넘으므로 추가 안내 필요**
  - 알림 2: 4a 마무리 후 `BoatController.StopWreckPhysics`(273~277행)의 `BuoyancyBody` 타입을 `BuoyantObject`로 바꿔 달라는 요청(유일한 필수 코드 수정)
  - 알림 3~5: 4b에서(껍데기 삭제 직전, 질량 500 제거 직전 등)
- [ ] 게임 디자인 결정: 폭풍 때 선체 마루 침수와 골짜기 부유를 어느 정도 허용할지(`HeaveFollow`), 표류(질량 500 기준 계수)가 폭풍에서 약 35 m 이동하는 것이 적정한지
- [ ] 4인 테스트(M2): 사용자 일정

## 7. 다음 세션의 첫 행동

1. 위 근거 문서를 읽고 `git branch --show-current`, `git status --short`, `git log --oneline -5`로 상태를 확인한다. 4a 변경이 이미 커밋되었는지 먼저 본다.
2. 4a 커밋이 아직이면 [guided-commit](../../../../.claude/skills/guided-commit/SKILL.md)으로 준비한다. 제안: `feat` 코드·프리팹 한 커밋과 `docs` 한 커밋. 커밋 메시지에 Claude 공동 저자 줄을 넣지 않는다(사용자 요청).
3. 에디터 상태를 `editor_status`로 확인한다. 사용자가 "직접 확인"이라고 하면 `WorldSessionGame` Play → 탑승 상태 → Storm → 실제 `Main Camera` 캡처까지 직접 진행한다(절차는 결과 문서 "플레이어 탑승 상태 재현"과 사용자 메모리 `feedback_direct_check_means_play_to_scene`). `MainGame`은 최신 물이 없어 쓰지 않는다.
4. 4a 잔여를 순서대로 진행한다: ① 폭풍벽 `Impulse`(폭풍벽이 켜지는 조건 확인 후 이동량 측정) ② 부력비 0.6 침몰 확인 ③ 항해 중 측정(W 입력이 안 먹는 원인 조사부터) ④ 도크 판정 탑승 ⑤ `BuoyancyChecks`에 `HeaveFollow` 케이스 추가.
5. 4a를 닫으면 사용자에게 알림 2를 상기시키고, 병합 확인 뒤 4b를 시작한다(질량 1 전환, `BoatShipBody`→`WaterBody` 이름 변경은 에디터에서, 표류·`Impulse` 계수와 Slam 임계 재측정, 미사용 코드 `ShipStabilizer`·`BuoyancyTuning`·`BuoyancyForceSolver`·`BuoyancySampleSession` 정리, `BuoyancyBody` 껍데기 삭제).

## 8. 주의

- 되돌리면 안 되는 변경: 기존 미커밋 변경 전체(특히 `WaveModel.cs`). `.unity`·`.prefab`·`.asset` YAML 직접 편집 금지(에디터 MCP로 필드 입력). 프리팹 값은 `SerializedObject`로 바꾸고 `SaveAsPrefabAsset`.
- 조정 값은 모두 가안이다. 위치와 효과는 결과 문서 마지막 절 표에 있다. 특히 `SurfaceFollow`를 올리면 배와 플레이어가 기울고 돈다.
- 침수 판정은 `WaveModel.HeightAt`만으로 하지 않는다. 섬 해안 파도가 빠져 해안 근처에서 ±3 m 틀어진다. `BuoyantObject.Reading.ReferenceSurface`를 쓴다.
- Play 중 `NetworkServer.Spawn(localConnection)`으로 만든 무인 `Boat`는 `BoatController`가 Kinematic으로 고정한다. 부력만 보려면 `BoatController`를 끄고 Kinematic을 해제하거나, 탑승 상태를 리플렉션으로 만든다.
- 캡처 파일은 `Assets/Temp`에 저장되므로 보낸 뒤 `delete_asset`으로 지운다. 씬을 바꾸면 미저장 변경이 사라지므로 dirty인 씬은 복사본을 저장한다.
- `BoatController`·`PlayerCamera` 등 플레이어 코드는 수정하지 않는다. `MainGame`(김지훈 소유)은 수정하지 않는다.
- 이전에 같은 내용의 파일이 오타 이름으로 있어 중복 컴파일 오류가 난 적이 있다. 새 파일을 만들기 전에 같은 클래스를 검색한다.
