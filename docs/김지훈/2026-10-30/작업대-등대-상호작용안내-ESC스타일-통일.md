# 작업대·등대 상호작용 안내를 ESC 메뉴 안내 스타일로 통일

- 작성일: 2026-10-30
- 담당: 김지훈

## 요청
작업대·등대 앞에서 화면 하단에 뜨는 E 안내(배경 판 + "E  작업대 열기"/"E  등대 열기" 텍스트)를 상시 HUD의 `[Esc] 메뉴` 안내처럼 간단하게 통일한다. 문구는 `[E] 작업대 사용` / `[E] 등대 사용`(사용자 결정). 사용자가 생성해 준 "등대 사용 / 유령선 확인" 이미지를 잘라 쓰고, "유령선 확인"은 나중 유령선 UI용으로 저장만 한다.

## 수행
- 사용자 이미지(2094×751, 투명 배경)에서 알파 16 이하 잡티를 지우고 두 줄을 잘라 저장.
  - `Assets/03.Images/InGameHud/hud_label_use_lighthouse.png` (873×245)
  - `Assets/03.Images/InGameHud/hud_label_check_ghostship.png` (1002×234) — 아직 미사용
  - 가져오기 설정은 `hud_label_use_workbench.png`와 같게 복사(Sprite, Single, PPU 100).
- 안내 구조: 배경 없는 `Prompt` 아래 `Key`(`hud_key_e`, 47×46) + `Label`(라벨 이미지, 높이 31~34, preserveAspect). InGameHud의 `MenuKey`(46 높이)·`MenuLabel`(31 높이)과 같은 비율. 위치는 기존 그대로 하단 중앙 +150.
  - 작업대: `hud_label_use_workbench` 134×31, Prompt 폭 190
  - 등대: `hud_label_use_lighthouse` 121×34(글로 여백이 커서 조금 키움), Prompt 폭 177
- 빌더 수정: `TowerScreenSketchSetup`·`WorkbenchScreenSketchSetup`(`HudFolder`, 작업대 쪽 `HudPicture` 헬퍼 추가).
- 쓰지 않던 `WorkbenchScreen._promptText` 필드와 빌더의 대입 제거.
- 기존 프리팹은 재빌드하지 않고 `Prompt` 오브젝트를 유지한 채 Image 컴포넌트·자식만 교체(`WorkbenchScreen_Sketch.prefab`, `TowerScreen_Sketch.prefab`). 재빌드하면 자식 fileID가 바뀌어 씬 오버라이드가 깨질 수 있어서.

## 검증
- Unity 컴파일 후 콘솔 error·warning 0(Workbench 필터).
- 씬 오버라이드: MainGame은 두 Prompt 참조 0, JH04는 작업대 Prompt `m_IsActive: 0` 하나뿐 — 영향 없음.
- 실제 크기 비율 합성 미리보기로 `[Esc] 메뉴`와 나란히 비교해 크기·스타일 일치 확인.
- Play 미실행 — 작업대·등대 앞 실제 표시 위치(운반 모듈 카드와 겹침 여부)는 미확인.

## 남은 일
- Play로 작업대·등대 접근 시 안내 표시 확인.
- 유령선 UI를 붙일 때 `hud_label_check_ghostship` 사용.
