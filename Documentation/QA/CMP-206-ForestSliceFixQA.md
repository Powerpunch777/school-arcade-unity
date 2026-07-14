# CMP-206 숲 수직 슬라이스 수정 독립 QA 재검증 보고서

- 검증일: 2026-07-15 (KST)
- 대상: `School Arcade Unity Implementation`
- Workspace: `C:\Users\young\Documents\Game Design\school-arcade-unity`
- Unity: `6000.5.3f1`
- 종합 판정: **PASS**
- 다음 검토자: CMP-207 Task Owner, 손영하-CTO

## 기준별 판정

| 기준 | 판정 | 독립 확인 근거 |
|---|---|---|
| F-01 재시작 초기화 | **PASS** | `SoulArcherGame.cs:125-131`의 새 `PlayerStats`·`RoomGraph`·구매 배열 생성, `LoadRoom()`의 `ClearSpawnedActors()` 및 `RestartRunResetsStateAndRemovesSpawnedActors` 통과 |
| F-02 출력 기준 | **PASS** | ProjectSettings 1920×1080/전체화면, `DisplaySettings` 640×360·1920×1080·`FullScreenWindow`, README 일치, Windows 실행 스모크 통과 |
| F-03 보상·상점 경제 | **PASS** | `SoulRewardRules`: 일반 적 1·전투방 보너스 2·숲 보스 5, `RewardEnemy`/방 클리어 코드, 보강 EditMode 테스트 통과 |
| Unity 컴파일 | **PASS** | 새 batchmode 컴파일 종료 코드 0, 컴파일 오류 0개 |
| EditMode 테스트 | **PASS** | 새 XML `total=13, passed=13, failed=0, inconclusive=0, skipped=0` |
| Windows 5초 이상 스모크 | **PASS** | CMP-206 빌드를 D3D11로 6.02초 유지, 초기화 오류/예외 없음 |

## F-01 재시작 초기화

정적 확인 결과 `ResetRunState()`가 새 `PlayerStats`, `RoomGraph`, 구매 상태 배열을 생성하므로 재화, 체력, 공격/공격 간격/이동 속도, 관통·보호막 능력, 사망·무적 상태와 상점 구매 상태가 새 판으로 돌아간다. `RestartRun()`은 기존 플레이어를 제거하고 `LoadRoom()`을 호출한다. `LoadRoom()`은 방 오브젝트를 정리한 뒤 `ClearSpawnedActors()`에서 기존 `EnemyActor`와 `ProjectileActor`를 제거하고 목록을 비운다.

보강 테스트 `RestartRunResetsStateAndRemovesSpawnedActors`는 구매, 영혼 10회 획득, 능력 2개 적용, 피격, 방 방문·클리어, 적·투사체 생성 후 재시작하고 다음을 확인한다.

- 영혼 0, 체력 5, 공격 10, 관통 0
- 구매 상품 미구매
- 현재 방 0, 시작 방 미클리어, 다음 방 미방문
- 활성 적 3, 활성 투사체 0
- 재시작 전 적·투사체 참조가 모두 null
- 재시작 후 다시 영혼을 모아 기본 상품 구매 가능

판정: **PASS**. 실제 키 입력 플레이스루는 수행하지 않았지만, Unity EditMode에서 `DestroyImmediate` 경로까지 포함한 실행 테스트가 통과했다.

## F-02 출력 기준

- `ProjectSettings/ProjectSettings.asset:10-12`: `defaultScreenWidth: 1920`, `defaultScreenHeight: 1080`, `fullscreenMode: 3`
- `Assets/Scripts/SoulArcherModels.cs:100-110`: 내부 640×360, 기본 1920×1080, `FullScreenMode.FullScreenWindow`, `Screen.SetResolution` 적용
- `README.md`: FHD 전체화면과 내부 640×360 정수 3배 기준을 명시
- `DisplayDefaultsMatchIntegerScaledFhdDesign` 통과

기존 `Builds/CMP-206-windows.log`에는 `DisplayProgressNotification: Build Successful`과 batchmode 정상 종료가 있다. 실행 스모크에서는 아래 CMP-206 산출물을 사용했다.

- 실행 파일: `Builds/SoulArcher-CMP-206.exe`
- SHA-256: `5AE0C84993C6BFF7D0F03166CAFD148CDEC61F67EC85B5D97600E04B2ABB26E4`
- 옵션: `-force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0`

`-force-d3d11`은 이 로컬 Windows 스모크의 그래픽 경로를 고정하기 위한 옵션이다. D3D12 실패를 이번 재검증에서 별도로 재현했다고 주장하지 않으며, 플레이어 로그에는 Unity 6000.5.3f1과 Direct3D 11 초기화가 기록되어 있다. 실제 화면의 1920×1080 전체화면 픽셀 출력은 스모크에서 관찰하지 않아 정적 근거와 실행 시작 근거로 판정했다.

## F-03 보상·상점 경제

`SoulRewardRules.For()`는 `Wolf`와 `Mushroom`에 1, `TreeSpirit`에 5를 반환하고, `CombatRoomClearBonus`는 2다. `SoulArcherGame.RewardEnemy()`가 처치 보상을 지급하며, 전투방 클리어 시 `SoulRewardRules.CombatRoomClearBonus`를 추가한다.

상점 전 경로의 최소 경제는 첫 전투 3마리와 두 번째 전투 4마리 기준으로 `(3×1+2)+(4×1+2)=11` 영혼이다. 첫 기본 상품 가격 10보다 크므로 최소 1개 구매가 가능하다. `ForestCombatRewardsAllowFirstShopPurchase`, `EnemyRewardsDifferForRegularEnemiesAndBoss`, `ShopPurchaseSpendsSoulsAndAppliesItemOnce`가 통과했다.

판정: **PASS**.

## 실행한 검증과 결과

### 1. Unity batchmode 컴파일

실행 명령:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
& $unity -batchmode -nographics -quit -projectPath (Get-Location).Path -logFile 'Documentation/QA/CMP-207-compile.log'
```

결과:

- 종료 코드 0
- `*** Tundra build success`
- `AssetDatabase: script compilation time: 0.330190s`
- `error CS`, `Compilation failed`, `Unhandled`, `Exception` 패턴 없음
- 로그의 라이선스 갱신 오류와 `Curl error 42`는 있었으나 컴파일 실패로 이어지지 않음

증거: `Documentation/QA/CMP-207-compile.log`, 기존 대조 로그 `Tests/CMP-206-compile.log`

### 2. EditMode 재실행

실행 명령:

```powershell
& $unity -batchmode -nographics -projectPath (Get-Location).Path -runTests -testPlatform EditMode `
  -testResults 'Documentation/QA/CMP-207-editmode-results.xml' `
  -logFile 'Documentation/QA/CMP-207-editmode.log'
```

결과 XML: `result=Passed`, `total=13`, `passed=13`, `failed=0`, `inconclusive=0`, `skipped=0`.

주요 보강 테스트:

`RestartRunResetsStateAndRemovesSpawnedActors`, `EnemyRewardsDifferForRegularEnemiesAndBoss`, `ForestCombatRewardsAllowFirstShopPurchase`, `DisplayDefaultsMatchIntegerScaledFhdDesign`, `ShopPurchaseSpendsSoulsAndAppliesItemOnce`.

전체 테스트에는 `DamageStartsInvincibilityAndDeath`, `RoomStateTracksVisitClearAndRevisit`, `AbilitySelectionChangesStats`, `SoulsSupportShopEconomy`, `MovingPlayerCannotFireNewArrow`, `PlayerCanReachBothRoomExits`, `SortingOrderKeepsActorsAboveRoomAndProjectilesAboveActors`, `StoppedPlayerSelectsNearestLivingVisibleEnemy`도 포함되며 모두 통과했다.

증거: `Documentation/QA/CMP-207-editmode-results.xml`, `Documentation/QA/CMP-207-editmode.log`, 기존 대조 결과 `Tests/CMP-206-editmode-results.xml` (`total=13`, `passed=13`, `failed=0`)

### 3. Windows 실행 스모크

실행 명령:

```powershell
$proc = Start-Process -FilePath (Resolve-Path 'Builds/SoulArcher-CMP-206.exe').Path `
  -ArgumentList '-force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0' `
  -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 6
```

결과:

- PID 32920
- 2026-07-15 03:31:06.306 KST 시작, 03:31:12.328 KST 종료
- 6.02초 후 프로세스 생존 확인, 이후 해당 PID만 정리
- 플레이어 로그에 `Initialize engine version: 6000.5.3f1`, `Forcing GfxDevice: Direct3D 11`, NVIDIA GeForce RTX 4070 기록
- 플레이어 로그에서 오류·예외·크래시 패턴 없음

증거: `Documentation/QA/CMP-207-player.log`, `Tests/CMP-206-smoke.log`, 기존 `Tests/CMP-206-player.log`.

첫 번째 스모크 하네스 시도는 PowerShell 예약 변수 `$PID`와의 이름 충돌로 요약 단계가 실패했다. 경로가 일치하는 잔류 PID 7996을 확인 후 종료했고, 변수명을 수정한 재실행은 위 결과로 통과했다. 제품 결함이나 미정리 프로세스는 남기지 않았다.

## 발견 결함 및 재현

이번 독립 재검증에서 CMP-205 F-01~F-03에 해당하는 재현 결함은 발견하지 않았다. 따라서 기능명·재현 단계·기대/실제 결과를 기록할 신규 FAIL 항목은 없다.

## 미검증 영역과 잔여 위험

- 키보드 입력을 이용한 전체 플레이스루, 실제 방 이동·상점 입력·사망 후 `R` 입력은 수행하지 않았다.
- F-02의 실제 렌더링 결과와 전체화면 전환은 픽셀 캡처로 확인하지 않았다. 코드·ProjectSettings·README·EditMode·실행 시작 근거만 확인했다.
- 이번 QA는 새 Windows 빌드를 생성하지 않고 CMP-206 빌드 산출물을 실행했다. 빌드 생성 성공은 `Builds/CMP-206-windows.log`로 대조했다.
- 라이선스 갱신 경고는 환경 메시지로 기록했으며 컴파일·테스트·실행 성공을 막지 않았다.

## 변경 범위 확인

제품 코드, 씬, ProjectSettings, Tests, Assets 및 기존 빌드 산출물은 수정하지 않았다. QA 산출물은 다음 `Documentation/QA` 경로에만 생성했다.

- `Documentation/QA/CMP-206-ForestSliceFixQA.md`
- `Documentation/QA/CMP-207-compile.log`
- `Documentation/QA/CMP-207-editmode-results.xml`
- `Documentation/QA/CMP-207-editmode.log`
- `Documentation/QA/CMP-207-player.log`

QA 전후 `git status --short`의 top-level 상태는 `M .gitignore`, `M README.md`, `?? Assets/`, `?? Documentation/`, `?? Packages/`, `?? ProjectSettings/`, `?? Tests/`로 동일했다. 기존 작업 트리 변경은 소유자 변경으로 보존했으며, 별도 제품 파일을 추가·수정하지 않았다.
