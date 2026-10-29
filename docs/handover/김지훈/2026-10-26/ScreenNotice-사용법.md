# 공용 알림 띠 `ScreenNotice` 사용법

작성일: 2026-10-26 · 작성: 김지훈 · 대상: 알림을 띄우고 싶은 모든 담당자
상태: 코드·프리팹 있음, `JH04TestScene`에서 확인. `feature/jihun`에 커밋·푸시(2026-10-27), develop 미병합. `MainGame` 배치 전.

화면 위쪽에 한 줄 알림을 띄우는 공용 부품입니다. 등대 연출(장착·층 완성·마지막 부품·모듈 완성)이 첫 사용처입니다.

## 한 줄로 띄우기

```csharp
using Lighthouse.Common.UI;

ScreenNotice.Show(new ScreenNoticeRequest("3일차 아침이 밝았다"));
ScreenNotice.Show(new ScreenNoticeRequest("무고한 시민이 밝혀졌다", ScreenNoticeStyle.Warning, _icon));
ScreenNotice.Show(new ScreenNoticeRequest("등대 부품 장착 (3/8)").WithGauge(0.25f, 0.375f, "+12.5%"));
```

| 항목 | 내용 |
|---|---|
| 문구 | 필수. 비면 무시 |
| 강조 `ScreenNoticeStyle` | `Normal`(기본), `Gold`(좋은 진척), `Warning`(나쁜 소식, 붉은 배경) |
| 아이콘 | 선택. 비우면 기본 아이콘(등대) |
| 게이지 | `WithGauge(시작, 끝, 증가분 문구)` 0~1. 없으면 문구만 가운데 |
| 순서 | 대기열로 하나씩 2.6초. 8개가 넘으면 오래된 것부터 버림 |
| `ScreenNotice.Clear()` | 대기·표시 중인 알림 모두 지움(판 종료 등) |
| 반환값 | 씬에 띠가 없으면 `false`, 아무 일도 없음 |

## 꼭 지킬 것 — 누가 보는가

**띠는 부른 기기 화면에만 그립니다. 네트워크로 보내지 않습니다.**
- 모두에게 보일 공개 소식: 모든 기기에서 받는 신호(SyncVar 값 변화, `[ClientRpc]`, SyncList 변경)에서 부릅니다.
- 본인만 알아야 하는 소식: 본인에게만 오는 `[TargetRpc]`·개인 응답에서만 부릅니다.
- 서버 코드(`[Server]`)에서 부르면 **호스트 화면에만** 뜹니다. 원격 클라이언트에는 안 뜹니다.
- 역할·투표 대상처럼 숨길 정보를 공개 신호에 실어 띠로 띄우지 않습니다([코딩 컨벤션 Mirror 절](../../../coding-convention.md#mirror)).

## 씬에 놓기와 모양 바꾸기

- 메뉴 `Lighthouse/UI/Use Screen Notice In Open Scene` → 씬 저장. 씬마다 하나만 둡니다(둘이면 나중에 켜진 쪽만 동작).
- 프리팹: `Assets/02.Prefabs/UI/Common/ScreenNotice.prefab`. Canvas 순서 90(작업 화면 80 위, 소환 메뉴 100 아래), 위에서 206px(최후 전투 HUD 아래).
- 모양(기본 아이콘·배경색·강조색·표시 시간)은 프리팹의 `ScreenNotice` 인스펙터에서 바꿉니다. 빌더 메뉴 `Build Screen Notice Prefab`을 다시 실행하면 처음 모습으로 덮어쓰므로, 손으로 바꾼 뒤에는 실행하지 않습니다.
- 코드: `Assets/01.Scripts/00.Common/UI/ScreenNotice*.cs`. 공용 계약 변경은 김지훈과 먼저 이야기합니다([코드 소유권](../../../CODE_OWNERSHIP.md)).

## 아직 확인 안 된 것

- 원격 클라이언트(M2)에서의 표시. 등대 연출은 호스트 1인으로만 확인했습니다.
