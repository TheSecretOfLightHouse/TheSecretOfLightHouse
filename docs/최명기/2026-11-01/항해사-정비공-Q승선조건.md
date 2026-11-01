# 항해사·정비공 Q 승선 조건
작성일: 2026-11-01
갱신일: 2026-11-01
상태: 구현됨(미검증)

## 요청과 근거
- 사용자 요청: 항해사·정비공도 경비병처럼 배에 탔을 때만 Q 능력 창 표시.
- 기존 직군 결정의 승선 Q 안내와 달리 두 능력 CanExecute는 다운 상태만 검사했다. 경비병은 자기 배 탑승·소유·점유·난파 여부까지 검사한다.

## 변경과 이유
- NavigatorRouteAbility.cs, EmergencyRepairAbility.cs의 CanExecute에서 GuardShieldAbility.IsAboardOwnedBoat를 재사용한다. 실패 시 Runner가 Begin을 호출하지 않아 SelectionRequested와 창 열기가 발생하지 않는다.
- 채집가·서버 효과·하루 사용권 계약은 변경하지 않았다. 직군 결정 문서와 pointer를 갱신했다.

## 검증과 남은 일
- 두 능력→Runner.TryExecute→SelectionRequested 호출 경로 및 경비병 조건 정적 대조, 변경 파일 git diff --check 통과.
- dotnet build Lighthouse.Runtime.csproj --no-restore 실패: 기존 Temp/obj/Lighthouse.Runtime/project.assets.json 누락(NETSDK1004). 컴파일 통과로 보고하지 않는다.
- Unity 프로세스 존재 확인. 이번 변경의 Unity 컴파일·실제 Play/M2는 미실행이다.
- 다음 확인: 두 직군 하선 Q 미표시·자기 배 승선 Q 표시, 경비병과 채집가 회귀.
