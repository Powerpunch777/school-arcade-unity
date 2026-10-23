# Soul Archer

**Move to dodge. Stop to shoot.** Guide Leon, a young archer, through a forest, a cave, and a castle to rescue Princess Elia. Soul Archer is a single-player 2D action game about choosing the right moment to stand your ground.

[한국어](README.ko.md)

## Watch the game in action

[![A short gameplay preview: Leon moves through the forest and fights approaching enemies. Click to watch the video.](docs/gameplay-preview.gif)](https://powerpunch777.github.io/school-arcade-unity/)

**[▶ Watch the 70-second gameplay video with sound](https://powerpunch777.github.io/school-arcade-unity/)** · [Download MP4](https://powerpunch777.github.io/school-arcade-unity/gameplay.mp4?v=20261023)

See Leon dodge enemies, stop to fire, choose an upgrade, and enter the next forest room. The clip is actual gameplay at normal speed, recorded with the game's music and sound effects. English captions explain the Korean interface; Korean captions are also available in the player. The animated preview above is silent—open the full video to hear the game.

- **0:00 — Forest combat:** move around approaching enemies and their projectiles, then stop to attack.
- **0:51 — Choose an upgrade:** this run offers piercing arrows, attack power, and healing.
- **0:56 — Prepare to continue:** select piercing arrows and return to the cleared room.
- **1:00 — Enter the next room:** a new encounter begins.

## How it plays

### Find a moment to attack

Use WASD or the arrow keys to move. While Leon is moving, he cannot fire new arrows. Release the movement keys and he automatically aims at the nearest enemy on screen.

I wanted this simple rule to make timing and positioning matter. Standing still gives the player a chance to deal damage, but also leaves them exposed. As enemies approach, the player has to decide whether there is time for another shot or whether to move to safety.

### Choose how to grow

Clear rooms, choose upgrades, visit shops, and find a route to each area's boss. The journey continues from the forest into the cave and finally the castle, with different enemies and boss patterns along the way.

![Three upgrade cards offer movement speed, piercing arrows, or attack speed.](Documentation/Media/ability-selection.jpg)

*In this separate playthrough, the first room offers three choices: move faster, shoot through an additional enemy, or attack more quickly. The choice changes how the player can approach the next encounter.*

<details>
<summary>More screenshots</summary>

![Leon fires an arrow toward enemies in the first forest room.](Documentation/Media/forest-combat.jpg)

*The opening forest encounter, captured in a separate run.*

![Soul Archer's title menu against a moonlit castle.](Documentation/Media/title.jpg)

*The journey begins here. Players can start a new game on Easy or Normal difficulty, view previous runs, or check the controls.*

</details>

*The video and screenshots were captured from the running CMP-265 Windows build. The game interface is currently in Korean. [Capture notes](Documentation/Media/README.md).*

## Why I made it and what I contributed

I began Soul Archer as a school project to explore how generative AI could support game development. I wanted to test that question by making a game I could play and improve.

I handled the planning, core rules, implementation direction, and playtesting. AI helped me turn ideas into working implementations and create visual material. I decided what the game should do, reviewed the results, and identified what needed to change.

## What playtesting changed

**Making encounters last long enough to matter.** Early versions were too easy and ended too quickly. I adjusted monster health and boss patterns to give the player more reason to alternate between moving and attacking, then played again to assess the changes.

**Fixing the visuals in the game itself.** When graphics did not display as intended, I created replacement material with generative image tools and revised it. I checked the results in the game, where characters, enemies, backgrounds, and the interface have to work together.

These experiences taught me that getting an idea to run is only the beginning. Playing it reveals problems that are difficult to see in a plan, and those problems can change what I decide to build next.

I also used [SPS](https://github.com/Powerpunch777/SPS), the AI development workspace I am developing. Making Soul Archer helped me find limitations in the workspace and improve the tools alongside the game.

## Explore further

[Game design and planning](https://github.com/Powerpunch777/school-arcade-design) · [Run the Unity project and inspect the code](Documentation/Development.md) · [Asset credits and licenses](Documentation/THIRD_PARTY_NOTICES.md)

Built with Unity and C# for Windows. Original code and custom resources have no separate open-source license; third-party assets retain their own licenses.
