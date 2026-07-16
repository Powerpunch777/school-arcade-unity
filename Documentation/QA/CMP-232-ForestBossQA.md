# CMP-232 Forest Boss Room E2E Entry and Visual Verification QA Report

환경: Windows 11 x64, 1920x1080, Unity `6000.5.3f1`, `BEE_CACHE_DIRECTORY=C:\Users\young\AppData\Local\Unity\Caches\bee`. 이 문서는 기술 QA 결과이며 제품·배포 승인 또는 사용자 수락이 아니다.

## 1. 기준별 결과

| 기준 | 결과 | 실제 근거 |
|---|---|---|
| Full EditMode | PASS | `Documentation/QA/CMP-232-editmode-results.xml` 74개 테스트 모두 통과. 신규 E2E 진입점 및 타이머 고정 관련 테스트 3개 정상 확인. |
| clean Windows x64 build | PASS | `Documentation/QA/CMP-232-build.log` 에 `Build Finished, Result: Success.` 및 `Exiting batchmode successfully now!` 확인. |
| 보스방 E2E 레이아웃 정렬 | PASS | `ActorView.LateUpdate`에서 `SpriteRenderer`가 루트 GameObject에 붙어있는 경우 `transform.localPosition = Vector3.zero`에 의해 모든 액터가 첫 프레임 이후 `(0,0)`으로 강제 워프/텔레포트되던 중대 버그를 발견하여 수정. 플레이어 Leon이 `(-6, 0)`, 보스 Tree Spirit이 `(4, 0)` 위치를 안정적으로 고수함. |
| 보스 경고 전조(Telegraph)선 검증 | PASS | 플레이어와 보스가 올바른 상대 거리를 유지함에 따라, 3개의 경고선(triple warning line)이 온전한 길이(10.0 유닛)로 그려지고, 펄스 애니메이션이 선명히 드러남. |
| 카메라 프레임 및 HUD 비겹침 보장 | PASS | `ResponsiveCamera2D`와 `GameFeelService`에 의한 midpoint camera (`x = -1.0` 타겟)가 두 액터를 좌우 대칭 구도로 완벽히 화면에 담음. 상단 거대 나무 정령 보스 체력 바 및 컴팩트 HUD 요소들과 비겹침을 보장함. |

---

## 2. 빌드 및 테스트 실행 명령

### Standalone x64 빌드
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.com" -batchmode -nographics -projectPath "C:\Users\young\Documents\Game Design\school-arcade-unity" -buildWindows64Player "C:\Users\young\Documents\Game Design\school-arcade-unity\Builds\CMP-232\SoulArcher-CMP-232.exe" -quit -logFile "C:\Users\young\Documents\Game Design\school-arcade-unity\Documentation\QA\CMP-232-build.log"
```

### EditMode 테스트 실행
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.com" -batchmode -nographics -projectPath "C:\Users\young\Documents\Game Design\school-arcade-unity" -runTests -testPlatform EditMode -testResults "C:\Users\young\Documents\Game Design\school-arcade-unity\Documentation\QA\CMP-232-editmode-results.xml" -quit -logFile "C:\Users\young\Documents\Game Design\school-arcade-unity\Documentation\QA\CMP-232-test.log"
```

### E2E Forest Boss 실행 (640x360 윈도우 모드)
```powershell
Start-Process -FilePath "C:\Users\young\Documents\Game Design\school-arcade-unity\Builds\CMP-232\SoulArcher-CMP-232.exe" -ArgumentList "-e2eForestBoss", "-screen-width", "640", "-screen-height", "360", "-window-mode", "windowed"
```

---

## 3. 산출물 및 증거 확인

### 산출물 경로 및 크기
- 빌드 exe 경로: [SoulArcher-CMP-232.exe](file:///C:/Users/young/Documents/Game%20Design/school-arcade-unity/Builds/CMP-232/SoulArcher-CMP-232.exe)
- 최종 스크린샷 증거: [CMP-232-e2eForestBoss.png](file:///C:/Users/young/Documents/Game%20Design/school-arcade-unity/Documentation/QA/CMP-232-e2eForestBoss.png)

### 시각적 증거 설명 (`CMP-232-e2eForestBoss.png`)
![CMP-232-e2eForestBoss.png](file:///C:/Users/young/Documents/Game%20Design/school-arcade-unity/Documentation/QA/CMP-232-e2eForestBoss.png)
- **왼쪽**: 플레이어 Leon이 통나무 오브젝트 근처 `(-6, 0)` 위치에 차분히 자리 잡고 서 있습니다.
- **오른쪽**: 보스 `거대 나무 정령(Tree Spirit)`이 `(4, 0)` 위치에서 고유의 연두색 타겟 링을 바닥에 두른 채 대치하고 있습니다.
- **경고선**: 보스로부터 플레이어 방향으로 3갈래 of 붉은색 경고 전조선(Telegraph lines)이 선명하게 뻗어 있습니다.
- **UI 및 HUD**: 상단 중앙 보스 체력 바(`거대 나무 정령`), 좌상단 하트 체력 바, 우상단 미니맵, 하단 컨트롤 툴팁 등 HUD가 중앙 구역을 가리지 않고 완벽하게 위치해 있습니다.

---

다음 검토자는 박정훈-CTO 2 (Task Owner) 입니다.
