using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SoulArcher
{
    public enum ScreenState { Title, Difficulty, Intro, Play, Ability, Shop, Pause, Ending, Result, History, Controls, Settings, StageTransition }

    public sealed class SoulArcherGame : MonoBehaviour
    {
        private const float RoomWidth = 18f;
        private const float RoomHeight = 9f;
        private readonly List<EnemyActor> enemies = new List<EnemyActor>();
        private readonly List<ProjectileActor> projectiles = new List<ProjectileActor>();
        private readonly List<GameObject> roomObjects = new List<GameObject>();
        private PlayerActor player;
        private RoomGraph graph;
        private PlayerStats stats;
        private Camera cam;
        private AudioSource music;
        private float nextShot;
        private float hitFlash;
        private float runSeconds;
        private bool[] bought = ShopRules.CreatePurchaseState();
        private ScreenState screen = ScreenState.Title;
        private Difficulty difficulty = Difficulty.Normal;
        private int introIndex;
        private int endingIndex;
        private int abilityChoices;
        private int runSeed;
        private int historyPage;
        private bool historyClearArmed;
        private float masterVolume = 1f;
        private bool fullscreen = true;
        private ScreenState settingsReturnScreen = ScreenState.Title;
        private List<Ability> cards = new List<Ability>();
        private readonly List<Ability> selectedAbilities = new List<Ability>();
        private EnemyActor currentTarget;
        private string banner = "엘리아 공주를 구하러 가자";
        private string resultMessage = string.Empty;

        public RoomGraph CurrentGraph { get { return graph; } }
        public PlayerStats CurrentStats { get { return stats; } }
        public int ActiveEnemyCount { get { return enemies.Count; } }
        public int ActiveProjectileCount { get { return projectiles.Count; } }
        public ScreenState CurrentScreen { get { return screen; } }
        public Difficulty CurrentDifficulty { get { return difficulty; } }
        public int AbilityChoices { get { return abilityChoices; } }
        public bool IsShopItemPurchased(int itemIndex) { return bought != null && itemIndex >= 0 && itemIndex < bought.Length && bought[itemIndex]; }
        public bool TryPurchaseShopItem(int itemIndex)
        {
            bool purchased = ShopRules.TryPurchase(stats, itemIndex, bought);
            if (purchased) PlayEffect("Audio/sfx_purchase");
            return purchased;
        }

        public void Start()
        {
            Application.targetFrameRate = 60;
            DisplaySettings.ApplyDefaultOutput();
            cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cam = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.backgroundColor = new Color(.04f, .06f, .1f);
            music = GetComponent<AudioSource>();
            if (music == null) music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            LoadSettings();
            ResetRunState();
            if (Application.isPlaying) PlayMusic("Audio/bgm_title");
            // Direct Start() calls from EditMode fixtures begin a deterministic room for model/integration tests.
            if (!Application.isPlaying) BeginRun(Difficulty.Normal);
        }

        private void Update()
        {
            if (screen == ScreenState.Title) { HandleTitleInput(); return; }
            if (screen == ScreenState.Difficulty) { HandleDifficultyInput(); return; }
            if (screen == ScreenState.Intro || screen == ScreenState.Ending) { HandleCutsceneInput(); return; }
            if (screen == ScreenState.History || screen == ScreenState.Controls || screen == ScreenState.Settings) { HandleMenuInput(); return; }
            if (screen == ScreenState.Result) { HandleResultInput(); return; }
            if (screen == ScreenState.StageTransition) { if (AdvancePressed()) ContinueStage(); return; }
            if (screen == ScreenState.Ability || screen == ScreenState.Shop || screen == ScreenState.Pause) { HandleOverlayInput(); return; }

            if (Input.GetKeyDown(KeyCode.Escape)) { screen = ScreenState.Pause; return; }
            runSeconds += Time.deltaTime;
            player.Tick();
            TickEnemies();
            UpdateAimTarget();
            TickProjectiles();
            if (graph.Current.kind != RoomKind.Shop && enemies.Count == 0 && !graph.Current.cleared)
            {
                var kind = graph.Current.kind;
                graph.ClearCurrent();
                if (kind == RoomKind.Combat)
                {
                    stats.souls += SoulRewardRules.CombatRoomClearBonus;
                    banner = "방 클리어! 영혼 조각을 얻었다";
                    if (graph.Current.offersAbility && abilityChoices < AbilityRules.ChoicesPerRun) { OpenAbilityCards(); return; }
                }
                else
                {
                    banner = StageRules.BossName(graph.Stage) + " 격파! 공주의 빛이 길을 연다";
                    if (StageRules.HasNext(graph.Stage)) { stats.Heal(1f); screen = ScreenState.StageTransition; return; }
                    endingIndex = 0; screen = ScreenState.Ending; PlayMusic("Audio/bgm_boss"); return;
                }
            }
            var exitDirection = RoomExitRules.ExitDirection(player.transform.position.x, player.transform.position.y);
            if (exitDirection.HasValue && (enemies.Count == 0 || graph.Current.cleared)) ChangeRoom(exitDirection.Value);
            if (cam != null) cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void HandleTitleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Return)) screen = ScreenState.Difficulty;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { historyPage = 0; historyClearArmed = false; screen = ScreenState.History; }
            else if (Input.GetKeyDown(KeyCode.Alpha3)) screen = ScreenState.Controls;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) { settingsReturnScreen = ScreenState.Title; screen = ScreenState.Settings; }
        }
        private void HandleDifficultyInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { difficulty = Difficulty.Easy; BeginIntro(); }
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { difficulty = Difficulty.Normal; BeginIntro(); }
            else if (Input.GetKeyDown(KeyCode.Escape)) screen = ScreenState.Title;
        }
        private void HandleCutsceneInput()
        {
            if (!AdvancePressed()) return;
            if (screen == ScreenState.Intro)
            {
                introIndex++;
                if (introIndex >= 3) BeginRun(difficulty);
            }
            else
            {
                endingIndex++;
                if (endingIndex >= 2) FinishRun(true);
            }
        }
        private void HandleMenuInput()
        {
            if (screen == ScreenState.History)
            {
                int pageCount = Mathf.Max(1, Mathf.CeilToInt(RunHistory.Load().records.Count / 5f));
                if (Input.GetKeyDown(KeyCode.LeftArrow)) historyPage = Mathf.Max(0, historyPage - 1);
                if (Input.GetKeyDown(KeyCode.RightArrow)) historyPage = Mathf.Min(pageCount - 1, historyPage + 1);
                if (Input.GetKeyDown(KeyCode.Delete)) historyClearArmed = true;
                if (historyClearArmed && Input.GetKeyDown(KeyCode.Y)) { RunHistory.Clear(); historyPage = 0; historyClearArmed = false; }
                if (historyClearArmed && Input.GetKeyDown(KeyCode.N)) historyClearArmed = false;
            }
            if (screen == ScreenState.Settings)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) SetMasterVolume(masterVolume - .1f);
                if (Input.GetKeyDown(KeyCode.RightArrow)) SetMasterVolume(masterVolume + .1f);
                if (Input.GetKeyDown(KeyCode.F)) SetFullscreen(!fullscreen);
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) { historyClearArmed = false; screen = screen == ScreenState.Settings ? settingsReturnScreen : ScreenState.Title; }
        }
        private void HandleResultInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) BeginRun(difficulty);
            else if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Escape)) screen = ScreenState.Title;
            else if (Input.GetKeyDown(KeyCode.H)) { historyPage = 0; historyClearArmed = false; screen = ScreenState.History; }
        }
        private void HandleOverlayInput()
        {
            if (screen == ScreenState.Pause)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) screen = ScreenState.Play;
                else if (Input.GetKeyDown(KeyCode.R)) BeginRun(difficulty);
                else if (Input.GetKeyDown(KeyCode.T)) screen = ScreenState.Title;
                return;
            }
            if (screen == ScreenState.Ability)
            {
                int choice = ChoiceKey();
                if (choice >= 0) SelectAbility(choice);
                return;
            }
            int shopChoice = ChoiceKey();
            if (shopChoice >= 0) TryPurchaseShopItem(shopChoice);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                graph.ClearCurrent(); screen = ScreenState.Play; banner = "상점 방을 나왔다";
            }
        }
        private int ChoiceKey()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            return -1;
        }
        private void SelectAbility(int choice)
        {
            if (screen != ScreenState.Ability || choice < 0 || choice >= cards.Count) return;
            var selected = cards[choice];
            selectedAbilities.Add(selected);
            AbilityRules.Apply(stats, selected);
            abilityChoices++;
            PlayEffect("Audio/sfx_ability_select");
            screen = ScreenState.Play;
            banner = AbilityName(selected) + " 선택 완료";
        }
        private static bool AdvancePressed() { return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow); }

        private void BeginIntro() { introIndex = 0; screen = ScreenState.Intro; PlayMusic("Audio/bgm_title"); }
        private void BeginRun(Difficulty selectedDifficulty)
        {
            difficulty = selectedDifficulty;
            if (player != null) DestroyRuntimeObject(player.gameObject);
            foreach (var roomObject in roomObjects) DestroyRuntimeObject(roomObject);
            roomObjects.Clear();
            ClearSpawnedActors();
            ResetRunState();
            screen = ScreenState.Play;
            graph = new RoomGraph(StageId.Forest);
            BuildPlayer();
            LoadRoom();
            PlayMusic("Audio/bgm_forest");
        }
        private void ContinueStage()
        {
            stats.ResetStageEffects();
            bought = ShopRules.CreatePurchaseState();
            graph = new RoomGraph(StageRules.Next(graph.Stage));
            screen = ScreenState.Play;
            LoadRoom();
            PlayMusic(graph.Stage == StageId.Cave ? "Audio/bgm_cave" : "Audio/bgm_castle");
        }
        private void ResetRunState()
        {
            stats = new PlayerStats();
            graph = new RoomGraph(StageId.Forest);
            bought = ShopRules.CreatePurchaseState();
            selectedAbilities.Clear();
            cards.Clear();
            abilityChoices = 0;
            runSeconds = 0f;
            nextShot = 0f;
            hitFlash = 0f;
            runSeed = Guid.NewGuid().GetHashCode();
            historyPage = 0;
            historyClearArmed = false;
            currentTarget = null;
        }
        private void OpenAbilityCards()
        {
            cards = AbilityRules.CreateCards(stats, runSeed + abilityChoices * 31 + (int)graph.Stage * 101 + graph.CurrentId);
            if (cards.Count > 0) screen = ScreenState.Ability;
        }

        private void BuildPlayer()
        {
            var go = MakeActor("Leon", new Color(.95f, .75f, .32f), Vector3.zero, 1f, SpriteSorting.Actor, "Art/Player/Leon/leon_walk");
            player = go.AddComponent<PlayerActor>();
            player.Initialize(this, stats);
        }
        private void LoadRoom(RoomDirection? enteredFrom = null)
        {
            foreach (var o in roomObjects) DestroyRuntimeObject(o);
            roomObjects.Clear();
            ClearSpawnedActors();
            if (player == null) BuildPlayer();
            player.transform.position = EntryPosition(enteredFrom);
            CreateRoomFloor();
            var room = graph.Current;
            banner = room.label;
            if (room.kind == RoomKind.Shop) { screen = ScreenState.Shop; return; }
            if (room.cleared) return;
            if (room.kind == RoomKind.Boss) { SpawnEnemy(BossType(graph.Stage), new Vector3(4f, 0f, 0f)); PlayEffect("Audio/sfx_boss_warning"); PlayMusic("Audio/bgm_boss"); }
            else
            {
                var types = StageRules.Enemies(graph.Stage);
                int count = room.elite ? 6 : 3 + Mathf.Min(2, graph.ClearedCombatCount);
                for (int i = 0; i < count; i++) SpawnEnemy(types[i % types.Length], new Vector3(2f + (i % 4) * 1.8f, i % 2 == 0 ? 1.8f : -1.4f, 0f));
            }
        }
        private static Vector3 EntryPosition(RoomDirection? movementDirection)
        {
            if (!movementDirection.HasValue) return new Vector3(-6f, 0f, 0f);
            switch (movementDirection.Value)
            {
                case RoomDirection.East: return new Vector3(-7.8f, 0f, 0f);
                case RoomDirection.West: return new Vector3(7.8f, 0f, 0f);
                case RoomDirection.North: return new Vector3(0f, -3.3f, 0f);
                default: return new Vector3(0f, 3.3f, 0f);
            }
        }
        private static EnemyType BossType(StageId stage) { return stage == StageId.Forest ? EnemyType.TreeSpirit : stage == StageId.Cave ? EnemyType.GiantBat : EnemyType.Morkan; }
        private void CreateRoomFloor()
        {
            Color floorColor = graph.Stage == StageId.Forest ? new Color(.12f, .27f, .16f) : graph.Stage == StageId.Cave ? new Color(.12f, .14f, .23f) : new Color(.25f, .1f, .12f);
            var floor = MakeActor("" + StageRules.Name(graph.Stage) + " Floor", floorColor, Vector3.zero, 1f, SpriteSorting.Background, graph.Stage == StageId.Forest ? "Art/Environment/Forest/forest_tiles" : graph.Stage == StageId.Cave ? "Art/Environment/Cave/cave_tiles" : "Art/Environment/Castle/castle_tiles");
            floor.transform.localScale = Vector3.one; var floorRenderer = floor.GetComponent<SpriteRenderer>(); floorRenderer.drawMode = SpriteDrawMode.Tiled; floorRenderer.size = new Vector2(RoomWidth, RoomHeight); roomObjects.Add(floor);
            AddBoundary("North", new Vector3(0, RoomHeight / 2f, 0), new Vector3(RoomWidth, .3f, 1), RoomDirection.North);
            AddBoundary("South", new Vector3(0, -RoomHeight / 2f, 0), new Vector3(RoomWidth, .3f, 1), RoomDirection.South);
            AddBoundary("West", new Vector3(-RoomWidth / 2f, 0, 0), new Vector3(.3f, RoomHeight, 1), RoomDirection.West);
            AddBoundary("East", new Vector3(RoomWidth / 2f, 0, 0), new Vector3(.3f, RoomHeight, 1), RoomDirection.East);
        }
        private void AddBoundary(string name, Vector3 position, Vector3 scale, RoomDirection direction)
        {
            bool hasDoor = graph.HasNeighbor(graph.CurrentId, direction);
            var boundary = MakeActor(name + (hasDoor ? "Door" : "Wall"), hasDoor ? new Color(.78f, .61f, .18f) : new Color(.22f, .14f, .09f), position, 1f, SpriteSorting.WallAndProps);
            boundary.transform.localScale = hasDoor && (direction == RoomDirection.North || direction == RoomDirection.South) ? new Vector3(2f, .3f, 1f) : hasDoor ? new Vector3(.3f, 2f, 1f) : scale;
            roomObjects.Add(boundary);
        }
        private GameObject MakeActor(string name, Color color, Vector3 position, float size, int sortingOrder = SpriteSorting.Actor, string resourcePath = null)
        {
            var go = new GameObject(name); go.transform.SetParent(transform); go.transform.position = position; go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            Texture2D texture = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Texture2D>(resourcePath);
            sr.sprite = CreateRuntimeSprite(texture, resourcePath);
            sr.color = texture == null ? color : Color.white; sr.sortingOrder = sortingOrder;
            return go;
        }
        private static Sprite CreateRuntimeSprite(Texture2D texture, string resourcePath)
        {
            if (texture == null) return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f, 0, SpriteMeshType.FullRect);
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Repeat;
            int cell = texture.height;
            if (!string.IsNullOrEmpty(resourcePath) && resourcePath.Contains("/Environment/")) cell = 16;
            else if (!string.IsNullOrEmpty(resourcePath) && resourcePath.Contains("/Player/Leon/") && !resourcePath.EndsWith("arrow")) cell = 32;
            else if (!string.IsNullOrEmpty(resourcePath) && resourcePath.Contains("/Enemies/")) cell = 32;
            else if (!string.IsNullOrEmpty(resourcePath) && resourcePath.Contains("/Bosses/")) cell = 64;
            cell = Mathf.Clamp(cell, 1, Mathf.Min(texture.width, texture.height));
            float y = resourcePath != null && resourcePath.Contains("/Environment/") ? texture.height - cell : 0f;
            return Sprite.Create(texture, new Rect(0, y, cell, cell), Vector2.one * .5f, cell, 0, SpriteMeshType.FullRect);
        }
        private void ClearSpawnedActors()
        {
            foreach (var enemy in enemies) DestroyRuntimeObject(enemy ? enemy.gameObject : null);
            foreach (var projectile in projectiles) DestroyRuntimeObject(projectile ? projectile.gameObject : null);
            enemies.Clear(); projectiles.Clear();
        }
        private static void DestroyRuntimeObject(GameObject actor)
        {
            if (!actor) return; actor.SetActive(false);
            if (Application.isPlaying) Destroy(actor); else DestroyImmediate(actor);
        }
        public void SpawnEnemy(EnemyType type, Vector3 position)
        {
            var colors = new Dictionary<EnemyType, Color>
            {
                { EnemyType.Wolf, new Color(.65f, .7f, .78f) }, { EnemyType.Mushroom, new Color(.85f, .25f, .32f) },
                { EnemyType.RockBeetle, new Color(.45f, .38f, .32f) }, { EnemyType.CrystalSpirit, new Color(.25f, .8f, .95f) },
                { EnemyType.ShieldSkeleton, new Color(.8f, .8f, .72f) }, { EnemyType.DarkMage, new Color(.55f, .2f, .78f) },
                { EnemyType.TreeSpirit, new Color(.35f, .65f, .22f) }, { EnemyType.GiantBat, new Color(.65f, .18f, .65f) },
                { EnemyType.Morkan, new Color(.9f, .22f, .18f) }
            };
            string resource = type == EnemyType.Wolf ? "Art/Enemies/Forest/forest_wolf" : type == EnemyType.Mushroom ? "Art/Enemies/Forest/poison_mushroom" : type == EnemyType.RockBeetle ? "Art/Enemies/Cave/rock_beetle" : type == EnemyType.CrystalSpirit ? "Art/Enemies/Cave/crystal_spirit" : type == EnemyType.ShieldSkeleton ? "Art/Enemies/Castle/shield_skeleton" : type == EnemyType.DarkMage ? "Art/Enemies/Castle/dark_mage" : type == EnemyType.TreeSpirit ? "Art/Bosses/Forest/ancient_tree_spirit" : type == EnemyType.GiantBat ? "Art/Bosses/Cave/giant_bat" : "Art/Bosses/Castle/morkan_boss";
            float size = type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan ? 1.7f : 1f;
            var go = MakeActor(type.ToString(), colors[type], position, size, SpriteSorting.Actor, resource);
            var enemy = go.AddComponent<EnemyActor>(); enemy.Initialize(this, player, type); enemies.Add(enemy);
        }
        private void TickEnemies() { for (int i = enemies.Count - 1; i >= 0; i--) { if (enemies[i] == null) enemies.RemoveAt(i); else enemies[i].Tick(); } }
        private void TickProjectiles() { for (int i = projectiles.Count - 1; i >= 0; i--) { if (projectiles[i] == null) projectiles.RemoveAt(i); else projectiles[i].Tick(); } }
        private void UpdateAimTarget() { currentTarget = FindNearestVisibleEnemy(); }
        private EnemyActor FindNearestVisibleEnemy()
        {
            if (player == null || cam == null) return null;
            var targets = new List<AutoAimTarget>(); var visibleEnemies = new List<EnemyActor>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.Dead) continue;
                Vector3 view = cam.WorldToViewportPoint(enemy.transform.position);
                if (view.z < 0 || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) continue;
                targets.Add(new AutoAimTarget(visibleEnemies.Count, enemy.transform.position, true, true));
                visibleEnemies.Add(enemy);
            }
            int targetIndex = AutoAimRules.SelectNearestVisibleLivingTarget(player.transform.position, targets);
            return targetIndex < 0 ? null : visibleEnemies[targetIndex];
        }
        public void FireArrow()
        {
            currentTarget = FindNearestVisibleEnemy();
            if (!AutoAimRules.CanFire(player.IsMoving, currentTarget != null, Time.time, nextShot)) return;
            var go = MakeActor("Arrow", Color.yellow, player.transform.position, .45f, SpriteSorting.Projectile, "Art/Player/Leon/arrow");
            var arrow = go.AddComponent<ProjectileActor>(); arrow.Initialize(this, player.transform.position, (currentTarget.transform.position - player.transform.position).normalized, false, stats.attack, stats.pierce, stats.reflect); projectiles.Add(arrow); PlayEffect("Audio/sfx_arrow_fire");
            nextShot = Time.time + stats.attackInterval;
        }
        public void SpawnEnemyShot(Vector3 origin, Vector3 direction, float damage)
        {
            var go = MakeActor("Enemy Shot", new Color(.85f, .45f, .15f), origin, .3f, SpriteSorting.Projectile); var shot = go.AddComponent<ProjectileActor>(); shot.Initialize(this, origin, direction, true, damage, 0); projectiles.Add(shot);
        }
        public void RemoveEnemy(EnemyActor enemy) { enemies.Remove(enemy); }
        public void RewardEnemy(EnemyType type) { stats.souls += SoulRewardRules.For(type); PlayEffect("Audio/sfx_enemy_defeat"); }
        public void DamagePlayer(float amount)
        {
            if (stats.TakeDamage(amount, Time.time, difficulty))
            {
                hitFlash = Time.time + .15f; PlayEffect("Audio/sfx_player_hurt");
                if (stats.dead) { resultMessage = "레온이 쓰러졌다"; FinishRun(false); }
            }
        }
        public void ChangeRoom(int id)
        {
            if (enemies.Count > 0 && !graph.Current.cleared) return;
            if (graph.TryMove(id)) { screen = ScreenState.Play; LoadRoom(); PlayEffect("Audio/sfx_door_open"); }
        }
        public void ChangeRoom(RoomDirection direction)
        {
            if (enemies.Count > 0 && !graph.Current.cleared) return;
            if (graph.TryMove(direction)) { screen = ScreenState.Play; LoadRoom(direction); PlayEffect("Audio/sfx_door_open"); }
        }
        public void RestartRun() { BeginRun(difficulty); }
        private void FinishRun(bool victory)
        {
            var names = selectedAbilities.Select(AbilityName).ToArray();
            RunHistory.Save(new RunRecord { victory = victory, difficulty = difficulty, seconds = runSeconds, reachedStage = (int)graph.Stage + 1, reachedRoom = graph.CurrentId + 1, remainingHearts = stats.hearts, souls = stats.souls, abilities = string.Join(",", names), dateIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm") });
            resultMessage = victory ? "엘리아 공주를 구출했다!" : "레온이 쓰러졌다";
            screen = ScreenState.Result;
            PlayMusic(victory ? "Audio/sfx_victory" : "Audio/sfx_defeat", false);
        }
        private void PlayMusic(string resourcePath, bool loop = true)
        {
            if (music == null) return;
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null) return;
            music.clip = clip; music.loop = loop; music.Play();
        }
        public void PlayEffect(string resourcePath)
        {
            if (music == null) return;
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip != null) music.PlayOneShot(clip);
        }
        private void LoadSettings()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SoulArcher.MasterVolume", 1f));
            fullscreen = PlayerPrefs.GetInt("SoulArcher.Fullscreen", 1) != 0;
            AudioListener.volume = masterVolume;
            if (Application.isPlaying) DisplaySettings.Apply(fullscreen);
        }
        private void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value); AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat("SoulArcher.MasterVolume", masterVolume); PlayerPrefs.Save();
        }
        private void SetFullscreen(bool value)
        {
            fullscreen = value;
            if (Application.isPlaying) DisplaySettings.Apply(fullscreen);
            PlayerPrefs.SetInt("SoulArcher.Fullscreen", fullscreen ? 1 : 0); PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / (float)DisplaySettings.InternalWidth, Screen.height / (float)DisplaySettings.InternalHeight, 1));
            GUI.color = Color.white;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            if (screen == ScreenState.Title) { DrawTitle(label); return; }
            if (screen == ScreenState.Difficulty) { DrawDifficulty(label); return; }
            if (screen == ScreenState.Intro) { DrawCutscene(label, new[] { "우연히 만난 엘리아 공주", "마왕 기사 모르칸이 공주를 납치했다", "레온은 활을 들고 여정을 시작한다" }[introIndex], "도입 컷신"); return; }
            if (screen == ScreenState.Ending) { DrawCutscene(label, endingIndex == 0 ? "엘리아 공주의 봉인이 풀렸다" : "두 사람은 왕국으로 돌아갔다", "공주 구출 엔딩"); return; }
            if (screen == ScreenState.History) { DrawHistory(label); return; }
            if (screen == ScreenState.Controls) { DrawPanel(label, "조작법", "WASD/방향키 이동\n정지 시 화면 안의 가장 가까운 적 자동 공격\n숫자키 또는 마우스로 카드·상점 선택\nSpace/Enter 컷신 진행"); if (GUI.Button(new Rect(245, 285, 150, 32), "뒤로")) screen = ScreenState.Title; return; }
            if (screen == ScreenState.Settings) { DrawSettings(label); return; }
            if (screen == ScreenState.Result) { DrawResult(label); return; }
            DrawHud(label);
            if (screen == ScreenState.Ability) DrawAbilityOverlay(label);
            else if (screen == ScreenState.Shop) DrawShopOverlay(label);
            else if (screen == ScreenState.Pause) DrawPause(label);
            else if (screen == ScreenState.StageTransition) { DrawPanel(label, "다음 스테이지", StageRules.Name(StageRules.Next(graph.Stage)) + "으로 이동합니다\nSpace/Enter 계속"); if (GUI.Button(new Rect(245, 250, 150, 34), "계속")) ContinueStage(); }
            if (hitFlash > Time.time) { GUI.color = new Color(1, .25f, .25f, .5f); GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.whiteTexture); GUI.color = Color.white; }
        }
        private void DrawTitle(GUIStyle label)
        {
            GUI.color = Color.white; var titleTexture = Resources.Load<Texture2D>("Art/Illustrations/title_background"); if (titleTexture != null) GUI.DrawTexture(new Rect(0, 0, 640, 360), titleTexture, ScaleMode.StretchToFill); else { GUI.color = new Color(.04f, .08f, .12f); GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.whiteTexture); } GUI.color = Color.white;
            GUI.Label(new Rect(80, 68, 480, 48), "소울 아처", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, normal = { textColor = new Color(1f, .82f, .3f) } });
            if (GUI.Button(new Rect(235, 145, 170, 34), "[1] 새 게임")) screen = ScreenState.Difficulty;
            if (GUI.Button(new Rect(235, 184, 170, 34), "[2] 전적")) { historyPage = 0; screen = ScreenState.History; }
            if (GUI.Button(new Rect(235, 223, 170, 34), "[3] 조작법")) screen = ScreenState.Controls;
            if (GUI.Button(new Rect(235, 262, 170, 34), "[4] 설정")) { settingsReturnScreen = ScreenState.Title; screen = ScreenState.Settings; }
            GUI.Label(new Rect(170, 310, 300, 20), "엘리아 공주를 구하러 가자", label);
        }
        private void DrawDifficulty(GUIStyle label)
        {
            DrawOverlayBackground();
            GUI.Label(new Rect(80, 60, 480, 40), "난이도 선택", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = new Color(1f, .82f, .3f) } });
            if (GUI.Button(new Rect(105, 130, 200, 100), "[1] 쉬움\n받는 피해 -0.5 하트")) { difficulty = Difficulty.Easy; BeginIntro(); }
            if (GUI.Button(new Rect(335, 130, 200, 100), "[2] 보통\n기본 피해")) { difficulty = Difficulty.Normal; BeginIntro(); }
            if (GUI.Button(new Rect(245, 275, 150, 32), "뒤로")) screen = ScreenState.Title;
        }
        private void DrawHud(GUIStyle label)
        {
            GUI.Label(new Rect(12, 10, 260, 24), HeartText() + "   ◇ 영혼 " + stats.souls, label);
            GUI.Label(new Rect(245, 10, 220, 24), StageRules.Name(graph.Stage) + "  " + graph.ClearedCombatCount + "/3", label);
            GUI.Label(new Rect(12, 32, 350, 22), graph.Current.label + (graph.Current.cleared ? "  ✓" : ""), label);
            GUI.Label(new Rect(12, 330, 570, 20), "WASD/방향키 이동 · 정지 시 자동 조준 발사 · Esc 일시정지", label);
            DrawMiniMap();
            DrawTargetIndicator();
            DrawEnemyHealthBars();
            GUI.Label(new Rect(12, 286, 560, 22), "능력 " + abilityChoices + "/6: " + string.Join(" · ", selectedAbilities.Select(AbilityName).ToArray()), label);
            var boss = enemies.FirstOrDefault(e => e != null && (e.Type == EnemyType.TreeSpirit || e.Type == EnemyType.GiantBat || e.Type == EnemyType.Morkan));
            if (boss != null) { GUI.Box(new Rect(215, 42, 210, 18), ""); GUI.color = new Color(.85f, .18f, .22f); GUI.DrawTexture(new Rect(217, 44, 206 * Mathf.Clamp01(boss.Health / boss.MaxHealth), 14), Texture2D.whiteTexture); GUI.color = Color.white; GUI.Label(new Rect(215, 60, 210, 20), boss.Type == EnemyType.Morkan ? "모르칸 · " + boss.BossPhase + "단계" : StageRules.BossName(graph.Stage), new GUIStyle(label) { alignment = TextAnchor.MiddleCenter }); }
            GUI.Label(new Rect(220, 300, 300, 24), banner, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, normal = { textColor = new Color(1f, .85f, .3f) } });
        }
        private string HeartText() { string s = string.Empty; for (int i = 0; i < 5; i++) s += stats.hearts >= i + 1 ? "♥" : stats.hearts >= i + .5f ? "♡" : "·"; return s; }
        private void DrawMiniMap()
        {
            GUI.Box(new Rect(535, 8, 98, 82), "미니맵");
            for (int i = 0; i < graph.Rooms.Count; i++) for (int j = i + 1; j < graph.Rooms.Count; j++)
            {
                var a = graph.Rooms[i]; var b = graph.Rooms[j];
                if (!graph.AreConnected(i, j) || (!a.visited && !b.visited)) continue;
                float ax = 548 + a.gridX * 24; float ay = 64 - a.gridY * 22; float bx = 548 + b.gridX * 24; float by = 64 - b.gridY * 22;
                GUI.color = new Color(.65f, .65f, .65f); GUI.DrawTexture(new Rect(Mathf.Min(ax, bx) + 5, Mathf.Min(ay, by) + 5, Mathf.Max(3, Mathf.Abs(ax - bx)), Mathf.Max(3, Mathf.Abs(ay - by))), Texture2D.whiteTexture);
            }
            for (int i = 0; i < graph.Rooms.Count; i++)
            {
                var room = graph.Rooms[i]; bool discovered = room.visited || graph.Rooms.Any(r => r.visited && graph.AreConnected(r.id, room.id)); if (!discovered) continue;
                GUI.color = i == graph.CurrentId ? Color.yellow : room.cleared ? Color.green : room.visited ? Color.white : Color.gray;
                GUI.DrawTexture(new Rect(548 + room.gridX * 24, 64 - room.gridY * 22, 12, 12), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        private void DrawTargetIndicator()
        {
            if (currentTarget == null || cam == null) return;
            Vector3 point = cam.WorldToScreenPoint(currentTarget.transform.position);
            if (point.z < 0) return;
            float x = point.x * DisplaySettings.InternalWidth / Screen.width;
            float y = DisplaySettings.InternalHeight - point.y * DisplaySettings.InternalHeight / Screen.height;
            GUI.color = new Color(1f, .82f, .15f); GUI.DrawTexture(new Rect(x - 14, y + 12, 28, 2), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(x - 14, y - 14, 28, 2), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(x - 14, y - 14, 2, 28), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(x + 12, y - 14, 2, 28), Texture2D.whiteTexture); GUI.color = Color.white;
        }
        private void DrawEnemyHealthBars()
        {
            if (cam == null) return;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.Dead || enemy.LastDamagedAt <= 0f || Time.time - enemy.LastDamagedAt > 2f || enemy.MaxHealth >= 100f) continue;
                Vector3 point = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * .65f); if (point.z < 0) continue;
                float x = point.x * DisplaySettings.InternalWidth / Screen.width; float y = DisplaySettings.InternalHeight - point.y * DisplaySettings.InternalHeight / Screen.height;
                GUI.color = new Color(.12f, .12f, .12f); GUI.DrawTexture(new Rect(x - 18, y - 3, 36, 5), Texture2D.whiteTexture); GUI.color = new Color(.35f, .9f, .35f); GUI.DrawTexture(new Rect(x - 17, y - 2, 34 * Mathf.Clamp01(enemy.Health / enemy.MaxHealth), 3), Texture2D.whiteTexture); GUI.color = Color.white;
            }
        }
        private void DrawAbilityOverlay(GUIStyle label)
        {
            DrawOverlayBackground(); var abilityTexture = Resources.Load<Texture2D>("Art/UI/ability_icons"); if (abilityTexture != null) GUI.DrawTexture(new Rect(25, 105, 590, 35), abilityTexture, ScaleMode.ScaleToFit); GUI.Label(new Rect(180, 62, 300, 28), "능력 하나를 선택하세요 [1] [2] [3]", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            for (int i = 0; i < cards.Count; i++) if (GUI.Button(new Rect(35 + i * 195, 145, 180, 115), "[" + (i + 1) + "]\n" + AbilityName(cards[i]) + "\n현재 " + stats.StackCount(cards[i]) + "/3중첩\n" + AbilityEffectText(cards[i]))) SelectAbility(i);
        }
        private void DrawShopOverlay(GUIStyle label)
        {
            DrawOverlayBackground(); GUI.Label(new Rect(180, 62, 300, 28), StageRules.Name(graph.Stage) + " 상점 · 영혼 " + stats.souls, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            string[] names = { "회복 하트  (10)", "보호 부적  (15)", "강화 숫돌  (20)" };
            for (int i = 0; i < 3; i++)
            {
                GUI.enabled = !bought[i] && stats.souls >= ShopRules.Prices[i];
                if (GUI.Button(new Rect(55 + i * 180, 125, 160, 125), "[" + (i + 1) + "]\n" + (bought[i] ? "판매 완료" : names[i]))) TryPurchaseShopItem(i);
                GUI.enabled = true;
            }
            if (GUI.Button(new Rect(245, 275, 150, 32), "상점 나가기")) { graph.ClearCurrent(); screen = ScreenState.Play; banner = "상점 방을 나왔다"; }
        }
        private void DrawPause(GUIStyle label)
        {
            DrawPanel(label, "일시정지", "");
            if (GUI.Button(new Rect(235, 130, 170, 34), "계속하기")) screen = ScreenState.Play;
            if (GUI.Button(new Rect(235, 174, 170, 34), "다시 시작")) BeginRun(difficulty);
            if (GUI.Button(new Rect(235, 218, 170, 34), "타이틀로")) screen = ScreenState.Title;
            if (GUI.Button(new Rect(235, 262, 170, 34), "설정")) { settingsReturnScreen = ScreenState.Pause; screen = ScreenState.Settings; }
        }
        private void DrawResult(GUIStyle label)
        {
            string abilities = selectedAbilities.Count == 0 ? "없음" : string.Join(", ", selectedAbilities.Select(AbilityName).ToArray());
            DrawPanel(label, resultMessage, "난이도 " + (difficulty == Difficulty.Easy ? "쉬움" : "보통") + " · " + StageRules.Name(graph.Stage) + " " + (graph.CurrentId + 1) + "번 방\n플레이 " + FormatTime(runSeconds) + " · 하트 " + stats.hearts.ToString("0.0") + " · 영혼 " + stats.souls + "\n능력: " + abilities);
            if (GUI.Button(new Rect(150, 285, 105, 32), "다시 시작")) BeginRun(difficulty);
            if (GUI.Button(new Rect(268, 285, 105, 32), "전적")) { historyPage = 0; screen = ScreenState.History; }
            if (GUI.Button(new Rect(386, 285, 105, 32), "타이틀")) screen = ScreenState.Title;
        }
        private void DrawHistory(GUIStyle label)
        {
            var records = RunHistory.Load().records; DrawOverlayBackground();
            GUI.Label(new Rect(55, 22, 530, 28), "전적 / 통계", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });
            GUI.Label(new Rect(30, 55, 580, 48), "플레이 " + records.Count + "회 · 승리 " + RunHistory.Wins() + "회 · 승률 " + (RunHistory.WinRate() * 100f).ToString("0") + "% · 평균 " + FormatTime(RunHistory.AverageTime()) + "\n평균 도달 " + RunHistory.AverageStage().ToString("0.0") + " 스테이지 · 선호 " + RunHistory.MostSelectedAbility(), label);
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(records.Count / 5f)); historyPage = Mathf.Clamp(historyPage, 0, pageCount - 1); int start = historyPage * 5;
            for (int i = 0; i < 5 && start + i < records.Count; i++)
            {
                var r = records[start + i]; string date = string.IsNullOrEmpty(r.dateIso) ? "이전 기록" : r.dateIso;
                GUI.Label(new Rect(28, 112 + i * 34, 585, 32), (start + i + 1) + ". " + date + " · " + (r.difficulty == Difficulty.Easy ? "쉬움" : "보통") + " · " + (r.victory ? "승리" : "패배") + " · " + StageRules.Name((StageId)Mathf.Clamp(r.reachedStage - 1, 0, 2)) + " " + r.reachedRoom + "방 · " + FormatTime(r.seconds) + "\n    하트 " + r.remainingHearts.ToString("0.0") + " · 능력 " + (string.IsNullOrEmpty(r.abilities) ? "없음" : r.abilities), new GUIStyle(label) { fontSize = 12 });
            }
            GUI.enabled = historyPage > 0; if (GUI.Button(new Rect(175, 292, 80, 30), "이전")) historyPage--; GUI.enabled = true;
            GUI.Label(new Rect(270, 296, 100, 20), (historyPage + 1) + " / " + pageCount, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            GUI.enabled = historyPage < pageCount - 1; if (GUI.Button(new Rect(385, 292, 80, 30), "다음")) historyPage++; GUI.enabled = true;
            if (GUI.Button(new Rect(30, 326, 110, 28), historyClearArmed ? "정말 초기화" : "기록 초기화")) { if (historyClearArmed) { RunHistory.Clear(); historyPage = 0; historyClearArmed = false; } else historyClearArmed = true; }
            if (historyClearArmed && GUI.Button(new Rect(150, 326, 80, 28), "취소")) historyClearArmed = false;
            if (GUI.Button(new Rect(500, 326, 110, 28), "뒤로")) { historyClearArmed = false; screen = ScreenState.Title; }
        }
        private void DrawSettings(GUIStyle label)
        {
            DrawOverlayBackground(); GUI.Label(new Rect(80, 55, 480, 40), "설정", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = new Color(1f, .82f, .3f) } });
            GUI.Label(new Rect(130, 125, 150, 24), "전체 음량 " + Mathf.RoundToInt(masterVolume * 100) + "%", label);
            float volume = GUI.HorizontalSlider(new Rect(280, 131, 220, 20), masterVolume, 0f, 1f); if (!Mathf.Approximately(volume, masterVolume)) SetMasterVolume(volume);
            bool nextFullscreen = GUI.Toggle(new Rect(205, 180, 230, 28), fullscreen, " 전체화면 (F)"); if (nextFullscreen != fullscreen) SetFullscreen(nextFullscreen);
            GUI.Label(new Rect(135, 225, 370, 40), "설정과 익명 전적만 로컬에 저장합니다.\n개인정보와 온라인 데이터는 저장하지 않습니다.", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(245, 285, 150, 32), "뒤로")) screen = settingsReturnScreen;
        }
        private void DrawPanel(GUIStyle label, string title, string body) { DrawOverlayBackground(); GUI.Label(new Rect(80, 65, 480, 40), title, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = new Color(1f, .82f, .3f) } }); GUI.Label(new Rect(100, 130, 440, 150), body, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 17 }); }
        private static void DrawOverlayBackground() { GUI.color = new Color(0, 0, 0, .86f); GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.whiteTexture); GUI.color = Color.white; }
        private void DrawCutscene(GUIStyle label, string text, string title) { DrawOverlayBackground(); string path = screen == ScreenState.Intro ? "Art/Illustrations/intro_0" + (introIndex + 1) + "_" + (introIndex == 0 ? "meeting" : introIndex == 1 ? "abduction" : "departure") : endingIndex == 0 ? "Art/Illustrations/ending_01_rescue" : "Art/Illustrations/ending_02_return"; var texture = Resources.Load<Texture2D>(path); if (texture != null) GUI.DrawTexture(new Rect(0, 0, 640, 280), texture, ScaleMode.StretchToFill); GUI.Label(new Rect(70, 38, 500, 30), title + "  " + ((screen == ScreenState.Intro ? introIndex : endingIndex) + 1) + "/" + (screen == ScreenState.Intro ? 3 : 2), new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 }); GUI.Label(new Rect(30, 275, 580, 35), text, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 }); if (GUI.Button(new Rect(245, 318, 150, 32), "다음")) { if (screen == ScreenState.Intro) { introIndex++; if (introIndex >= 3) BeginRun(difficulty); } else { endingIndex++; if (endingIndex >= 2) FinishRun(true); } } }
        private static string AbilityEffectText(Ability ability) { switch (ability) { case Ability.Attack: return "공격력 +20%"; case Ability.Speed: return "공격 간격 -15%"; case Ability.Pierce: return "적 1명 추가 관통"; case Ability.Reflect: return "벽 1회 추가 반사"; case Ability.Heal: return "하트 1.5 회복"; default: return "이동 속도 +10%"; } }
        private static string FormatTime(float seconds) { int total = Mathf.Max(0, Mathf.RoundToInt(seconds)); return (total / 60).ToString("00") + ":" + (total % 60).ToString("00"); }
        private static string AbilityName(Ability ability) { switch (ability) { case Ability.Attack: return "공격"; case Ability.Speed: return "공격 속도"; case Ability.Pierce: return "관통"; case Ability.Reflect: return "반사"; case Ability.Heal: return "회복"; default: return "이동 속도"; } }
    }

    public sealed class PlayerActor : MonoBehaviour
    {
        private SoulArcherGame game; private PlayerStats stats; private SpriteRenderer spriteRenderer; private Sprite[] walkFrames; private int directionColumn;
        public bool IsMoving { get; private set; }
        public void Initialize(SoulArcherGame owner, PlayerStats playerStats)
        {
            game = owner; stats = playerStats; spriteRenderer = GetComponent<SpriteRenderer>();
            var texture = Resources.Load<Texture2D>("Art/Player/Leon/leon_walk");
            if (texture != null) { texture.filterMode = FilterMode.Point; walkFrames = new Sprite[16]; for (int i = 0; i < walkFrames.Length; i++) walkFrames[i] = Sprite.Create(texture, new Rect(i * 32, 0, 32, 32), Vector2.one * .5f, 32f, 0, SpriteMeshType.FullRect); }
        }
        public void Tick()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            IsMoving = input.sqrMagnitude > .001f;
            if (IsMoving) directionColumn = DirectionColumn(input);
            if (spriteRenderer != null && walkFrames != null) spriteRenderer.sprite = walkFrames[Mathf.Clamp(directionColumn + (IsMoving ? Mathf.FloorToInt(Time.time * 7f) % 2 : 0), 0, walkFrames.Length - 1)];
            transform.position += (Vector3)(input * stats.moveSpeed * Time.deltaTime);
            transform.position = new Vector3(Mathf.Clamp(transform.position.x, RoomExitRules.PlayerMinX, RoomExitRules.PlayerMaxX), Mathf.Clamp(transform.position.y, RoomExitRules.PlayerMinY, RoomExitRules.PlayerMaxY), 0);
            if (!IsMoving) game.FireArrow();
        }
        private static int DirectionColumn(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle >= -22.5f && angle < 22.5f) return 4;
            if (angle >= 22.5f && angle < 67.5f) return 6;
            if (angle >= 67.5f && angle < 112.5f) return 8;
            if (angle >= 112.5f && angle < 157.5f) return 10;
            if (angle >= 157.5f || angle < -157.5f) return 12;
            if (angle >= -157.5f && angle < -112.5f) return 14;
            if (angle >= -112.5f && angle < -67.5f) return 0;
            return 2;
        }
    }

    public sealed class EnemyActor : MonoBehaviour
    {
        private SoulArcherGame game; private PlayerActor player; private EnemyType type; private float health; private float maxHealth; private float nextAction; private float phaseTimer; private SpriteRenderer spriteRenderer; private Sprite[] animationFrames;
        public bool Dead { get; private set; }
        public EnemyType Type { get { return type; } }
        public float Health { get { return health; } }
        public float MaxHealth { get { return maxHealth; } }
        public float LastDamagedAt { get; private set; }
        public int BossPhase { get { return type == EnemyType.Morkan && health <= maxHealth * .5f ? 2 : 1; } }
        public void Initialize(SoulArcherGame owner, PlayerActor target, EnemyType enemyType)
        {
            game = owner; player = target; type = enemyType;
            maxHealth = type == EnemyType.TreeSpirit ? 160f : type == EnemyType.GiantBat ? 190f : type == EnemyType.Morkan ? 260f : 35f;
            health = maxHealth;
            spriteRenderer = GetComponent<SpriteRenderer>(); var texture = spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.texture : null; int cell = maxHealth >= 100f ? 64 : 32;
            if (texture != null && texture.width >= cell) { int count = Mathf.Max(1, texture.width / cell); animationFrames = new Sprite[count]; for (int i = 0; i < count; i++) animationFrames[i] = Sprite.Create(texture, new Rect(i * cell, 0, cell, cell), Vector2.one * .5f, cell, 0, SpriteMeshType.FullRect); }
        }
        public void Tick()
        {
            if (Dead || player == null) return;
            if (spriteRenderer != null && animationFrames != null) spriteRenderer.sprite = animationFrames[Mathf.FloorToInt(Time.time * 4f) % animationFrames.Length];
            float distance = Vector2.Distance(transform.position, player.transform.position); Vector2 direction = (player.transform.position - transform.position).normalized;
            switch (type)
            {
                case EnemyType.Wolf: transform.position += (Vector3)(direction * 1.1f * Time.deltaTime); break;
                case EnemyType.Mushroom: transform.position += (Vector3)(-direction * .25f * Time.deltaTime); Shoot(direction, 1f, 2.2f, 8f); break;
                case EnemyType.RockBeetle: transform.position += (Vector3)(direction * .65f * Time.deltaTime); Shoot(direction, 1.2f, 2.6f, 5f); break;
                case EnemyType.CrystalSpirit: transform.position += (Vector3)(new Vector2(-direction.y, direction.x) * .8f * Time.deltaTime); Shoot(direction, 1.1f, 1.6f, 10f); break;
                case EnemyType.ShieldSkeleton: transform.position += (Vector3)(direction * .75f * Time.deltaTime); Shoot(direction, 1.2f, 2.0f, 6f); break;
                case EnemyType.DarkMage: Shoot(direction, 1.5f, 1.4f, 10f); if (Time.time >= nextAction + .3f) transform.position += (Vector3)(new Vector2(-direction.y, direction.x) * .25f * Time.deltaTime); break;
                case EnemyType.TreeSpirit: BossPattern(direction, 1f, 1.1f, 7f); break;
                case EnemyType.GiantBat: transform.position += (Vector3)(new Vector2(-direction.y, direction.x) * 1.1f * Time.deltaTime); BossPattern(direction, 1.2f, 1.5f, 9f); break;
                case EnemyType.Morkan: BossPattern(direction, BossPhase == 2 ? 1.5f : 1f, BossPhase == 2 ? .75f : 1.25f, 10f); break;
            }
            if (distance < (type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan ? 1.2f : .7f)) game.DamagePlayer(type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan ? 1f : .5f);
        }
        private void Shoot(Vector2 direction, float damage, float cooldown, float range) { if (Time.time >= nextAction && Vector2.Distance(transform.position, player.transform.position) < range) { game.SpawnEnemyShot(transform.position, direction, damage); nextAction = Time.time + cooldown; } }
        private void BossPattern(Vector2 direction, float damage, float cooldown, float range) { phaseTimer += Time.deltaTime; Shoot(direction, damage, cooldown, range); if (phaseTimer > 4f) { game.SpawnEnemyShot(transform.position, Quaternion.Euler(0, 0, 30) * direction, damage); game.SpawnEnemyShot(transform.position, Quaternion.Euler(0, 0, -30) * direction, damage); phaseTimer = 0f; } }
        public void TakeDamage(float amount)
        {
            if (Dead) return;
            if (type == EnemyType.ShieldSkeleton && Time.frameCount % 3 == 0) amount *= .35f;
            LastDamagedAt = Time.time;
            health -= amount;
            if (health <= 0f) { Dead = true; game.RewardEnemy(type); game.RemoveEnemy(this); if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject); }
        }
    }

    public sealed class ProjectileActor : MonoBehaviour
    {
        private SoulArcherGame game; private Vector2 direction; private bool hostile; private float damage; private int pierce; private int reflect; private float life;
        private readonly HashSet<EnemyActor> hitEnemies = new HashSet<EnemyActor>();
        public void Initialize(SoulArcherGame owner, Vector3 origin, Vector2 travel, bool isHostile, float amount, int pierceCount, int reflectCount = 0) { game = owner; transform.position = origin; direction = travel.normalized; hostile = isHostile; damage = amount; pierce = pierceCount; reflect = reflectCount; transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg); }
        public void Tick()
        {
            life += Time.deltaTime; transform.position += (Vector3)(direction * (hostile ? 3.2f : 9f) * Time.deltaTime);
            if (!hostile && reflect > 0 && (Mathf.Abs(transform.position.x) > 8.7f || Mathf.Abs(transform.position.y) > 4.2f))
            {
                bool hitX = Mathf.Abs(transform.position.x) > 8.7f;
                bool hitY = Mathf.Abs(transform.position.y) > 4.2f;
                direction = new Vector2(hitX ? -direction.x : direction.x, hitY ? -direction.y : direction.y).normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                transform.position = new Vector3(Mathf.Clamp(transform.position.x, -8.7f, 8.7f), Mathf.Clamp(transform.position.y, -4.2f, 4.2f), transform.position.z);
                reflect--;
            }
            if (life > 3f || Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 6f) { Destroy(gameObject); return; }
            if (hostile)
            {
                var target = FindAnyObjectByType<PlayerActor>();
                if (target && Vector2.Distance(transform.position, target.transform.position) < .5f) { game.DamagePlayer(damage); Destroy(gameObject); }
            }
            else
            {
                var hit = FindObjectsByType<EnemyActor>();
                foreach (var enemy in hit)
                {
                    if (enemy.Dead || hitEnemies.Contains(enemy) || Vector2.Distance(transform.position, enemy.transform.position) >= .65f) continue;
                    hitEnemies.Add(enemy); enemy.TakeDamage(damage); game.PlayEffect("Audio/sfx_arrow_hit");
                    if (pierce-- <= 0) { Destroy(gameObject); break; }
                }
            }
        }
    }
}
