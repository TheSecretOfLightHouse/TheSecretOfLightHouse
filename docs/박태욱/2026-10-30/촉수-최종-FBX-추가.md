# 촉수 최종 FBX 추가
작성자: 박태욱
작성일: 2026-10-30
상태: 에셋 파일 추가 완료, Unity 설정·재생 검증 대기

후속: 같은 날 [등대섬 테스트 씬](촉수-등대섬-테스트씬.md) 작업에서 FBX 액션 슬롯 오류를 수정하고 Generic 임포트·재질·컨트롤러·프리팹 연결과 로컬 Play 움직임 검증을 완료했다. 아래 내용은 최초 추가 시점의 기록이다.

## 요청과 결과
- 사용자 요청대로 추가 하위 폴더 없이 각 Boss 폴더에 배치했다.
- `Assets/04.Models/Boss/`: LighthouseTentacle_Model.fbx, WhipTentacle_Model.fbx.
- `Assets/05.Animations/Boss/`: LighthouseTentacle_Emerge.fbx, LighthouseTentacle_Squeeze.fbx, WhipTentacle_Idle.fbx, WhipTentacle_Attack.fbx.
- `Assets/06.Textures/Boss/`: Tentacle_Stylized_BaseColor.jpg, Tentacle_Stylized_Normal.png, Tentacle_Stylized_Metallic.jpg, Tentacle_Stylized_Roughness.jpg. 두 모델이 공유한다.
- 통합 All FBX와 미리보기 액션은 중복을 줄이기 위해 프로젝트에 넣지 않았다.
- 기존 Tentacle.fbx 등 모델 2개와 meta의 staged 삭제를 발견했고 보존했다. 기존 컨트롤러·프리팹·씬·머티리얼은 수정하지 않았다.

## 검증
- 새 파일 10개의 SHA256이 내보낸 원본과 일치한다.
- Blender에서 통합 FBX 재가져오기 확인: 등대 촉수 80본, 등장 1~200·조이기 1~88프레임; 채찍 촉수 49본, Idle 1~121·공격 1~90프레임. 모두 9545정점, 24fps.
- 추가 파일들의 meta 생성은 확인했다. Unity MCP가 현재 제공되지 않아 ModelImporter 설정, Unity 클립 재생, URP 재질 연결은 검증하지 않았다.

## 다음 작업
- Rig Generic 설정 및 각 모델에 맞는 Avatar·본 경로 확인.
- Whip Idle Loop Time 활성화, 공격·등장 Loop Time 해제.
- Normal 텍스처 타입과 URP 머티리얼 연결, Roughness → Smoothness 변환 확인.
- 별도 프리팹·컨트롤러 구성 후 기존 등대 표현 교체 여부 확인. 기존 런타임의 Climb/Wrapped/Squeeze 계약 보존 필요.
