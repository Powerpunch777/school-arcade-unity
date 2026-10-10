# Soul Archer development guide

[Game overview](../README.md) · [한국어](Development.ko.md)

This guide covers running the Unity project, building and testing it, and finding the implementation. For the game's premise, gameplay, and development story, start with the main README.

## Run the project

1. Clone or download this repository.
2. Open it in Unity Hub with **Unity 6000.5.3f1**.
3. Open `Assets/Scenes/ForestSlice.unity` and press **Play**.
4. Select **[1] 새 게임** (New Game), then **[1] 쉬움** (Easy) or **[2] 보통** (Normal).

The flow is title → difficulty → three opening scenes → forest, cave, and castle → rescue ending → results and run history. The repository contains source; compiled Windows builds are not included.

## Controls and display

| Action | Input |
| --- | --- |
| Move | WASD or arrow keys |
| Attack | Release movement keys; firing is automatic when a target is available |
| Choose an upgrade, shop item, or menu option | Mouse or 1 / 2 / 3, as shown on screen |
| Advance story scenes | Mouse, Space, or Enter |
| Pause | Esc |
| Results screen | R: restart · H: history · T: title |

The default display is 1920 × 1080 fullscreen, with a 640 × 360 internal UI reference. Settings also offer a 1280 × 720 window. Up to 20 recent run summaries are saved locally using `PlayerPrefs`.

## Build and test

Run these PowerShell commands from the repository root. Adjust the Unity executable path if needed.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
& $unity -batchmode -nographics -quit -projectPath (Get-Location) -logFile build-compile.log
& $unity -batchmode -projectPath (Get-Location) -runTests -testPlatform EditMode -testResults Tests/editmode-results.xml -logFile tests-editmode.log
New-Item -ItemType Directory -Force Builds/CMP-208 | Out-Null
& $unity -batchmode -nographics -quit -projectPath (Get-Location) -buildWindowsPlayer Builds/CMP-208/SoulArcher.exe -logFile Builds/CMP-208/build.log
```

Tests write `Tests/editmode-results.xml`; the build command writes `Builds/CMP-208/SoulArcher.exe`. Some EditMode tests inspect rendering, so the test command omits `-nographics`. `CMP-208` is the output folder used by these build instructions; the README screenshots were captured separately from the supplied CMP-265 build.

After building, launch `Builds/CMP-208/SoulArcher.exe` and inspect the title screen and gameplay. See [Implementation scope](ImplementationScope.md) for the project's recorded validation; those records are separate from the README screenshot capture.

## Source code guide

- [SoulArcherModels.cs](../Assets/Scripts/SoulArcherModels.cs) — stage and room graphs, health, invulnerability, difficulty, targeting, six upgrade types, shop economy, and run history.
- [SoulArcherGame.cs](../Assets/Scripts/SoulArcherGame.cs) — menus, story and campaign flow, combat, bosses, projectiles, rendering, audio, and UI.
- [SoulArcherModelsTests.cs](../Assets/Tests/Editor/SoulArcherModelsTests.cs) — targeting, firing conditions, damage, rooms, rewards, upgrades, difficulty, history, and campaign progression.
- [Runtime assets](../Assets/Resources) — art and audio loaded by the game. See the [asset manifest](AssetManifest.md) for their origins.
- [PixelLab import guide](PixelLabImport.md) — editor tools and validation for generated asset candidates.

## Scope and limitations

The Windows campaign includes three regions, bosses, shops, upgrades, an ending, results, and local history. It uses Unity `SpriteRenderer` and IMGUI, sprite-sheet animation, and WAV music and sound effects.

More detailed obstacle collision and off-screen enemy indicators remain areas for improvement. English localization, WebGL, mobile support, and external distribution are outside the current documented scope.

Source code, final runtime assets, project settings, and license notices are included. Local Unity caches, build output, test result files, the full QA archive, and intermediate generated assets are excluded. Selected README screenshots are stored with their [capture provenance](Media/README.md).

Original project code and custom resources have no separate open-source license. Third-party font, UI, and tile assets retain their own licenses; see [Third-party notices](THIRD_PARTY_NOTICES.md).
