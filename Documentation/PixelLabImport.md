# PixelLab 산출물 임포트

SPS의 Image Worker가 기록하는 PixelLab 산출물은 아래 후보 경로에서 검토한다.

```text
Assets/Art/Generated/PixelLab/<asset>/
  asset-manifest.json
  *.png
  *.zip                 # 선택 사항인 캐릭터 묶음
```

`Generated`는 승인 전 후보 영역이다. 최종 런타임 아트 경로는 `Assets/Resources/Art/...`이며, 이 도구는 두 영역 사이에서 파일을 복사하거나 이동하지 않는다. 승인과 런타임 배치는 사람이 별도 변경으로 수행해야 한다.

## PNG 자동 임포트

`Assets/Art/Generated/PixelLab` 아래의 모든 PNG에 다음 설정을 적용한다. 명시적으로 ZIP을 해제해 만들어진 하위 방향/애니메이션 PNG도 같은 경로 아래에 있으므로 동일한 규칙을 거친다.

- Texture Type: `Sprite (2D and UI)`
- Filter Mode: `Point`
- Compression: `None`
- 기존 Standalone/Android/iOS/WebGL/UWP/Server 플랫폼 override도 `Automatic + Uncompressed`로 정규화
- Generate Mip Maps: 끔
- Alpha Source: 입력 이미지, Alpha Is Transparency: 켬
- NPOT Scale: `None`
- Wrap Mode: `Clamp`
- sRGB: 켬
- 새 스프라이트는 `Single`; 이미 수동으로 `Multiple` 슬라이싱한 시트는 해당 모드를 보존

PPU는 다음 우선순위로 선택한다.

1. 파일명 표식: `hero_ppu16.png`, `walk-ppu_32.png`, `idle.ppu-24.png`
2. 해당 파일을 함께 참조하는 manifest 객체의 `ppu`, `pixelsPerUnit`, `spritePixelsPerUnit`
3. manifest 루트 또는 `defaultPpu`, `defaultPixelsPerUnit` 전역 값
4. 기본값 `16`

허용 PPU 범위는 1~4096이다. JSON 파싱 실패, 범위 밖 값, 동일 우선순위의 충돌 값은 기본값 16으로 보수적으로 돌아가며 Console에 원인을 표시한다.

필요하면 `Tools > PixelLab > Reimport All Generated PNGs`로 후보 PNG 전체에 규칙을 다시 적용할 수 있다. 이 메뉴도 런타임 경로에는 영향을 주지 않는다.

## Manifest 보고서

다음 메뉴에서 보고서를 연다.

- 전체 검사: `Tools > PixelLab > Open Manifest Report`
- 선택한 후보 검사: Project 창에서 파일/폴더를 선택한 뒤 `Assets > PixelLab > Validate Selected Asset Manifest`

보고서는 영구 `.asset` 파일을 만들지 않는 EditorWindow다. manifest, 후보 폴더, 존재하는 선언 파일의 `Select` 버튼으로 해당 Unity 오브젝트를 Project 창에서 바로 선택할 수 있다.

검사 항목은 다음과 같다.

- 유효한 JSON 객체이며 4 MiB 이하인지
- asset, operation, resource ID가 비어 있지 않고 중복/제어 문자가 없는지
- `assetId`와 `asset: { id: ... }`, snake_case, 중첩 resource 배열 등 일반적인 구조 변형
- 파일 목록의 상대 경로가 asset 폴더 안에 남는지
- 선언 파일이 실제로 존재하는지와 동일 파일이 중복 선언됐는지
- manifest에 없는 PNG/ZIP이 존재하는지(정보 수준)
- 원격 URL은 로컬 파일로 오인하지 않고 경고로 표시하며, URL만 있는 파일 목록은 오류 처리

알 수 없는 JSON 필드는 무시한다. 필수 ID나 로컬 PNG/ZIP 파일 목록을 발견하지 못하면 오류로 보고하지만 PNG 임포트 자체를 차단하지는 않는다.

## 캐릭터 ZIP 해제

ZIP은 임포트 시 자동으로 해제되지 않는다. Project 창에서 `Generated/PixelLab/<asset>` 아래 ZIP 하나를 선택하고 다음 메뉴 중 하나를 실행한다.

- `Assets > PixelLab > Extract Selected Character ZIP...`
- `Tools > PixelLab > Extract Selected Character ZIP...`

확인 후 `<zip-name>_extracted` 형제 폴더에만 해제한다. 예를 들어 `hero/character.zip`은 `hero/character_extracted/`로 간다. 기존 대상 폴더는 덮어쓰거나 지우지 않는다.

쓰기 전에 전체 archive를 검사한다.

- 절대 경로, 드라이브 경로, `.`/`..`, 빈 경로 segment, 대상 폴더 밖으로 정규화되는 경로 거부
- symlink/reparse point와 대소문자만 다른 중복 대상 거부
- `.png`, `.json`, `.txt`, `.md`만 허용; `.cs`, `.meta`, 중첩 ZIP 등 실행/Unity 제어 파일 거부
- 최대 4096 entries, 파일당 128 MiB, 전체 512 MiB, 경로 깊이 64 제한
- 사전 검사가 끝나기 전에는 대상 폴더를 만들지 않음
- 쓰기 실패 시 이번 동작이 새로 만든 부분 대상만 정리

성공 후 동기식 `AssetDatabase.Refresh`를 실행하므로 해제된 PNG에는 자동 픽셀아트 임포트 규칙이 적용된다. 해제 결과 역시 승인 전 후보이며 자동 승격되지 않는다.

## 검증

PixelLab 전용 EditMode 테스트는 `SchoolArcade.PixelLab.Editor.Tests` 어셈블리에 있다. Unity 6000.5.3f1 Windows 예시:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\Users\young\Documents\Game Design\school-arcade-unity' `
  -runTests -testPlatform EditMode `
  -assemblyNames SchoolArcade.PixelLab.Editor.Tests `
  -testResults "$env:TEMP\PixelLab-editmode-results.xml" `
  -logFile "$env:TEMP\PixelLab-editmode.log"
```

## 알려진 제약

- 실제 PixelLab 토큰과 샘플 manifest가 아직 없으므로 ID/파일 키 탐색은 의미 기반의 보수적 호환 규칙이다. 향후 스키마가 ID 의미나 로컬 파일 확장자를 전혀 드러내지 않으면 키 매핑을 추가해야 한다.
- sprite rect 슬라이싱, 방향 이름 해석, AnimationClip/Animator 생성은 자동화하지 않는다.
- ZIP 재해제는 기존 `<zip-name>_extracted`를 검토하거나 제거한 뒤 다시 실행해야 한다.
- 승인, 런타임 파일명 결정, `Assets/Resources/Art` 배치는 의도적으로 수동 절차다.
