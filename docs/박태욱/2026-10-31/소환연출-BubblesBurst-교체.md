# 소환 연출 물보라를 Bubbles_Burst로 교체
작성자: 박태욱
작성일·갱신일: 2026-10-31
상태: 프리팹 교체·테스트 씬 재생/렌더 확인, 본편·원격 시각 확인 대기

## 요청과 범위
- 사용자 요청: 촉수 등장 시 기존 물 튀는 효과를 새 Bubbles_Burst로 교체하고 새 VFX 묶음에서 관련 에셋만 남긴다.
- 새로 추가된 `Assets/NamuFX`만 정리 대상으로 한정했다. 기존 구매 원본·다른 기능·이전 연출 씬 일괄 정리로 확대하지 않았다.

## 변경과 결정 이유
- 후속 사용자 지적에 따라 외부 에셋 보관 규칙을 적용해 폴더를 Unity 에디터에서 `Assets/ThirdParty/1조 Unity Asset/ThirdParty/NamuFX/`로 이동했다. 현재 Bubbles_Burst 위치는 그 아래 `StylizedWaterEffects/Prefabs/Bubbles_Burst.prefab`이다. 아래 Assets/NamuFX 표기는 교체 당시 위치 이력이다.
- `Assets/02.Prefabs/Boss/MainSceneSummonRig.prefab`의 WaterBurst_1~5를 `Assets/NamuFX/StylizedWaterEffects/Prefabs/Bubbles_Burst.prefab`의 중첩 인스턴스로 교체했다.
- 기존 위치·크기·다섯 순차 등장 시점(0.85/1.4/1.95/2.5/3.05초)·ControlTrack을 유지하고 Director의 노출 참조를 새 객체로 교체했다. 새 프리팹의 원래 회전을 사용했다. 원본 Bubbles_Burst와 재질·셰이더는 수정하지 않았다.
- AssetDatabase.GetDependencies의 재귀 결과로 필요한 NamuFX 에셋9개를 보존했다. 프리팹·재질·ShaderGraph·SubGraph·텍스처와 PSD 의존성을 포함한다. Packages 의존성은 삭제 대상으로 삼지 않았다.
- 새 묶음의 GUID를 Assets/ProjectSettings/Packages에서 검색해 외부 참조가 새 Rig뿐임을 확인했다. NamuFX 미사용 에셋48개(다른 효과·데모·뷰어·미사용 재질/텍스처/모델 등)와 빈 폴더8개를 Unity manage_asset delete 도구로 삭제했다. meta도 함께 처리했다.
- 처음 execute_code의 DeleteAsset 호출은 도구 패턴 검사에 차단됐고, 검사 해제 대신 전용 에셋 삭제 도구를 사용했다.
- 열려 있던 DemoScene은 dirty=false 확인 후 TentacleIslandTest로 전환했다. 씬은 저장하지 않았다. 이전 SummonCinematic 씬이 참조하는 기존 Summon_Foam/Summon_FoamParticle 등의 공유 에셋은 유지했다.

## 검증
- ThirdParty 이동 후 폴더 GUID와 실제 에셋9개의 GUID가 모두 이전과 동일하고, Director의 효과 참조5개가 새 경로의 중첩 프리팹을 가리키며 shader.isSupported=true임을 재조회했다. 이동 도구가 실패 응답을 반환했지만 실제 폴더 이동은 완료됐음을 별도 조회로 확인했다. MCP WebSocket 초기화 경고1개가 있었으며 후속 조회는 성공했다. 이동 후 Play 재검증은 하지 않았다.
- SVN 체크아웃 루트의 .svn 존재와 해당 경로의 Git 제외를 확인했다. svn CLI는 PATH에서 찾지 못해 SVN 추가/커밋·원격 공유는 실행하지 않았다.
- 삭제 뒤 새 효과5개가 모두 Bubbles_Burst 중첩 원본을 참조하고 재질 shader.isSupported=true 확인.
- 각 효과0.2초 파티클 시뮬레이션에서30개 생성 확인.
- TentacleIslandTest 단독 Play 및 Timeline3.2초 평가: 4번2개·5번30개 파티클 확인. 실제 연출 카메라 렌더에서 버블 표시 확인. 캡처: `Captures/Bubbles_Burst_Cinematic.png`.
- Timeline12초 평가에서 효과5개 모두 inactive 확인. 검사 시 콘솔 오류/경고0. Play 종료 후 Edit 모드 복귀.
- NamuFX의 남은 실제 에셋9개 확인. 기존 World meta 변경3개는 보존했다.
- 실제 본편 조명·파도 환경의 효과 크기/색·원격 동기화는 이번에 검증하지 않았다. 현재 테스트 씬의 별도 최상위 Rig와 기존 섬 하위 Rig 구성은 임의 정리하지 않았다.

## 다음 행동
- 본편 소환에서 버블의 크기·밝기·수면 위치를 확인한다.
- 이번 변경 공유 시 MainSceneSummonRig와 기록은 Git, 정리된 ThirdParty/NamuFX 폴더와 meta는 SVN으로 전달한다. SVN 추가/커밋·팀원 업데이트는 별도 필요하다.
