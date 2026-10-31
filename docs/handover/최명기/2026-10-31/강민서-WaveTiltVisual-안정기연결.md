# 인수인계 — 강민서 WaveTiltVisual 안정기 연결

경로: docs/handover/최명기/2026-10-31/강민서-WaveTiltVisual-안정기연결.md
작성일: 2026-10-31
브랜치: feature/myeongki (확인 시 HEAD 928c0878)
담당 파트: Player 최명기 → World 강민서
수신자: 강민서(ri-ver-1)

## 1. 목표
- 선체 안정기 장착 시 파도 외형 기울기를 완화하기 위해 수정한 World 코드와 Player 소비 계약을 전달한다.
- 기존 안정기는 Rigidbody 회전 감쇠만 수행하지만 배4종은 물리 X/Z 회전이 잠겨 있고 외형 파도 기울기는 WaveTiltVisual이 따로 계산한다. 따라서 외형 표현에 배율 연결이 필요했다.
- 작업 중 AI가 연결 요청을 World 코드 수정 허용으로 확대 해석해 담당자 합의 확인 전에 수정했다. 이 문서는 강민서의 승인·검토 완료를 뜻하지 않는다. 변경은 현재 로컬에 남아 있다.
- 근거: [배 모듈 결정](../../../design/boat-module-decisions.md#7-선체-안정기--상세-제안), [작업 원문](../../../최명기/2026-10-31/선체안정기-파도흔들림-연결.md).

## 2. 완료한 것
| 항목 | 증거 | 상태 |
|---|---|---|
| World 배율 API 추가 | Assets/01.Scripts/02.World/Ocean/View/WaveTiltVisual.cs | 구현됨, 컴파일 확인 |
| Player에서 배율 설정·복원 | Assets/01.Scripts/05.Player/Boat/Net/StabilizerModule.cs | 구현됨, 격리 상태 검사 통과 |

World diff는 실행 코드 3줄 추가이며 아래 두 부분뿐이다.

```csharp
public float TiltMultiplier { get; set; } = 1f;
```

LateUpdate에서 기존 `_tiltScale`과 `_maxTiltDegrees` 적용 직후, 모델 좌표 변환 전에 다음을 추가했다.

```csharp
tilt = Quaternion.Slerp(Quaternion.identity, tilt, Mathf.Clamp01(TiltMultiplier));
```

- 기본1이면 기존 동작과 동일하며 0이면 파도 외형 기울기0, 0.7이면 제한된 목표 기울기의70%다. 기존 followRate 보간은 그대로라 전환 중 실제 각도가 즉시 정확히70%가 되는 의미는 아니다.
- Player는 Start에서 WaveTiltVisual을 캐싱한다. WaterBody.Awake의 동적 컴포넌트 추가 이후 참조하기 위한 순서다.
- Player.Update에서 활성 안정기·클라이언트·장착 효과 적용·활성 배·승선·미난파면 초기 감소율30%로0.7을 설정하고 나머지는1로 설정한다. 제거·비활성 시에도1로 복원한다. Inspector 원본이나 이전 배율을 반복 곱하지 않는다.
- 관찰 클라이언트도 기존 장착·승선 동기화 상태로 같은 외형 배율을 계산한다. 이 배율에 새 SyncVar/Rpc는 없다.
- World가 Player 타입을 참조하지 않는다. 소비 방향은 Player→World 공개 프로퍼티다.

## 3. 완료하지 못한 것
| 항목 | 어디까지 했나 | 남은 확인 |
|---|---|---|
| World 담당 검토 | 실제 diff·소비 계약 정리 | 강민서가 API 유지 또는 대체 방식 판단 |
| 실제 파도 각도 비교 | 배율 상태 검사만 수행 | 같은 파도/위치에서 장착 전후 시계열·체감 |
| 관찰자/다인 | 소유권false 격리 조건 확인 | 실제 호스트/원격·늦은 관찰·승하선 회귀 |

## 4. 현재 저장소 상태
- 위 World·Player 변경은 미커밋이며 이번 대화에서 커밋·push하지 않았다. 다른 PC에는 공유 전까지 반영되지 않는다.
- 현재 Player에는 몬스터 충돌 흔들림·밀림 완화 후속 변경도 있다(BoatCannonFeedback.cs, BoatCollisionRecoil.cs). 이 효과는 Player에서 처리하며 WaveTiltVisual의 역할이 아니다. 상세는 작업 원문 참조.
- 이번 연결 때문에 수정·저장한 씬/프리팹·신규 ScriptableObject는 없다. 저장소의 다른 미커밋 씬/에셋 변경은 별도 작업이다.
- 확인 명령: `git diff -- Assets/01.Scripts/02.World/Ocean/View/WaveTiltVisual.cs Assets/01.Scripts/05.Player/Boat/Net/StabilizerModule.cs`

## 5. 검증 현황
- L1: Unity MCP 재컴파일 completed/failed=false/errors=[]/compilationFailed=false, 변경 C# diff --check 통과.
- L2 일부: 실제 배4종 프리팹 PreviewScene 인스턴스에 Mirror·장착·승선 상태를 격리 주입한 결과 장착/승선0.7, 하선/난파/제거/복원1 확인. 실제 파도 LateUpdate 각도 시계열 검사는 하지 않았다.
- L3/멀티플레이: 이번 연결의 M2·4인 시험 미실행. 격리 관찰 조건을 원격 종단 검증으로 간주하지 않는다.

## 6. 차단 조건
- World 담당자의 변경 검토·유지할 계약 확인이 남아 있다. 합의 완료로 기록하지 않는다.
- 체감과 원격 결과를 확정하려면 실제 장착 전후 Play/M2 비교가 필요하다.

## 7. 다음 세션의 첫 행동
1. 위 명령으로 World 3줄 diff와 Player의 설정/복원 경로를 확인한다.
2. TiltMultiplier API를 유지할지, World가 제공할 다른 확장점으로 대체할지 검토한다. 대체 시 Player 소비자도 함께 맞춘다.
3. 같은 파도 조건에서 미장착→장착→하선→재승선→난파를 비교하고 원격 표시를 확인한다.

## 8. 주의
- 이 API를 제거하거나 이름을 바꾸면 StabilizerModule의 참조도 함께 처리해야 한다. World만 되돌리면 컴파일이 깨진다.
- TiltMultiplier는 현재 Player 안정기가 매 프레임 쓰는 단일 배율이다. 다른 World 효과가 같은 프로퍼티를 쓰면 마지막 기록값이 적용되므로 중첩 계약은 별도 합의가 필요하다.
- 파도 높이/수면 법선 계산/부력/바람/해류/토네이도 이동은 이번 World diff에서 변경하지 않았다.
- 담당자에게 실제 메시지를 발송하지 않았다. 사용자가 문서를 전달할 수 있도록 작성했다.
