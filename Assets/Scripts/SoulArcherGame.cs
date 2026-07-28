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
        private static readonly Dictionary<string, Texture2D> UiTextureCache = new Dictionary<string, Texture2D>();
        private readonly List<EnemyActor> enemies = new List<EnemyActor>();
        private readonly List<ProjectileActor> projectiles = new List<ProjectileActor>();
        private readonly List<GameObject> roomObjects = new List<GameObject>();
        private readonly List<EnemyType> pendingCombatEnemies = new List<EnemyType>();
        private PlayerActor player;
        private RoomGraph graph;
        private PlayerStats stats;
        private Camera cam;
        private AudioSource music;
        private GameHudView hud;
        private GameOverlayView overlay;
        private GameFeelService gameFeel;
        private ResponsiveCamera2D responsiveCamera;
        private float e2eScreenshotTimer = 0f;
        private float nextShot;
        private float hitFlash;
        private float runSeconds;
        private float nextWaveAt;
        private int combatWaveIndex;
        private int combatWaveCount;
        private bool waitingForNextWave;
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
        private ScreenState lastLoggedScreen = ScreenState.Title;
        private int lastLoggedRoom = -1;
        private int lastLoggedEnemies = -1;
        private float lastLoggedHearts = -1f;

        public RoomGraph CurrentGraph { get { return graph; } }
        public PlayerStats CurrentStats { get { return stats; } }
        public int ActiveEnemyCount { get { return enemies.Count; } }
        public int ActiveProjectileCount { get { return projectiles.Count; } }
        public ScreenState CurrentScreen { get { return screen; } }
        public Difficulty CurrentDifficulty { get { return difficulty; } }
        public int AbilityChoices { get { return abilityChoices; } }
        public GameHudView GameplayHud { get { return hud; } }
        public GameOverlayView GameplayOverlay { get { return overlay; } }
        internal GameFeelService GameFeel { get { return gameFeel; } }
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
            cam.allowHDR = false;
            cam.allowMSAA = false;
            responsiveCamera = cam.GetComponent<ResponsiveCamera2D>();
            if (responsiveCamera == null) responsiveCamera = cam.gameObject.AddComponent<ResponsiveCamera2D>();
            responsiveCamera.Initialize(cam);
            music = GetComponent<AudioSource>();
            if (music == null) music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            LoadSettings();
            ResetRunState();
            if (Application.isPlaying)
            {
                gameFeel = GetComponent<GameFeelService>();
                if (gameFeel == null) gameFeel = gameObject.AddComponent<GameFeelService>();
                gameFeel.Initialize(cam);
                hud = GetComponent<GameHudView>();
                if (hud == null) hud = gameObject.AddComponent<GameHudView>();
                hud.Initialize();
                overlay = GetComponent<GameOverlayView>();
                if (overlay == null) overlay = gameObject.AddComponent<GameOverlayView>();
                overlay.Initialize(ExecuteOverlayCommand);
            }
            if (Application.isPlaying)
            {
                if (IsE2EAbilityPreviewMode)
                {
                    BeginRun(Difficulty.Normal);
                    cards = new List<Ability> { Ability.Attack, Ability.Pierce, Ability.Heal };
                    screen = ScreenState.Ability;
                }
                else if (HasCommandLineArgument("-e2eForest") || HasCommandLineArgument("-e2eForestBoss") || HasCommandLineArgument("-e2eCaveBoss") || HasCommandLineArgument("-e2eCastleBoss") || HasCommandLineArgument("-e2eForestBossCombat") || HasCommandLineArgument("-e2eForestCombat")) BeginRun(Difficulty.Normal);
                else PlayMusic("Audio/bgm_title");
            }
            // Direct Start() calls from EditMode fixtures begin a deterministic room for model/integration tests.
            else BeginRun(Difficulty.Normal);
        }

        private static bool HasCommandLineArgument(string expected)
        {
            return Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));
        }
        private bool? e2eForestBossModeOverride;
        public bool IsE2EForestBossMode
        {
            get { return e2eForestBossModeOverride ?? HasCommandLineArgument("-e2eForestBoss"); }
            set { e2eForestBossModeOverride = value; }
        }

        private bool? e2eCaveBossModeOverride;
        public bool IsE2ECaveBossMode
        {
            get { return e2eCaveBossModeOverride ?? HasCommandLineArgument("-e2eCaveBoss"); }
            set { e2eCaveBossModeOverride = value; }
        }

        private bool? e2eCastleBossModeOverride;
        public bool IsE2ECastleBossMode
        {
            get { return e2eCastleBossModeOverride ?? HasCommandLineArgument("-e2eCastleBoss"); }
            set { e2eCastleBossModeOverride = value; }
        }

        private bool IsE2EForestBossCombatMode { get { return HasCommandLineArgument("-e2eForestBossCombat"); } }
        private bool IsE2EForestCombatMode { get { return HasCommandLineArgument("-e2eForestCombat"); } }

        private bool? e2eCampaignModeOverride;
        public bool IsE2ECampaignMode
        {
            get { return e2eCampaignModeOverride ?? HasCommandLineArgument("-e2eCampaign"); }
            set { e2eCampaignModeOverride = value; }
        }
        private bool IsE2EAbilityPreviewMode { get { return HasCommandLineArgument("-e2eAbilityPreview"); } }
        private float e2eActionTimer = 0f;

        private void Update()
        {
            if (IsE2ECampaignMode)
            {
                e2eActionTimer += Time.deltaTime;

                if (screen == ScreenState.Title)
                {
                    if (e2eActionTimer >= 2.0f)
                    {
                        e2eActionTimer = 0f;
                        screen = ScreenState.Difficulty;
                    }
                }
                else if (screen == ScreenState.Difficulty)
                {
                    if (e2eActionTimer >= 1.5f)
                    {
                        e2eActionTimer = 0f;
                        difficulty = Difficulty.Easy;
                        BeginIntro();
                    }
                }
                else if (screen == ScreenState.Intro)
                {
                    if (e2eActionTimer >= 1.5f)
                    {
                        e2eActionTimer = 0f;
                        introIndex++;
                        if (introIndex >= 3)
                        {
                            BeginRun(Difficulty.Easy);
                        }
                    }
                }
                else if (screen == ScreenState.Ability)
                {
                    if (e2eActionTimer >= 1.5f)
                    {
                        e2eActionTimer = 0f;
                        SelectAbility(0);
                    }
                }
                else if (screen == ScreenState.Shop)
                {
                    if (e2eActionTimer >= 1.5f)
                    {
                        e2eActionTimer = 0f;
                        TryPurchaseShopItem(0);
                        screen = ScreenState.Play;
                    }
                }
                else if (screen == ScreenState.StageTransition)
                {
                    if (e2eActionTimer >= 2.0f)
                    {
                        e2eActionTimer = 0f;
                        ContinueStage();
                    }
                }
                else if (screen == ScreenState.Ending)
                {
                    if (e2eActionTimer >= 2.5f)
                    {
                        e2eActionTimer = 0f;
                        endingIndex++;
                        if (endingIndex >= 2)
                        {
                            FinishRun(true);
                        }
                    }
                }
                else if (screen == ScreenState.Result)
                {
                    if (e2eActionTimer >= 5.0f)
                    {
                        e2eActionTimer = -9999f;
                        StartCoroutine(QuitAfterDelay());
                    }
                }
            }
            if (IsE2EAbilityPreviewMode)
            {
                e2eScreenshotTimer += Time.deltaTime;
                if (e2eScreenshotTimer >= 2.0f)
                {
                    e2eScreenshotTimer = -9999f;
                    CaptureAuditScreenshot("CMP-255-e2eAbilityPreview.png");
                    StartCoroutine(QuitAfterDelay());
                }
            }
            if (IsE2EForestBossMode)
            {
                e2eScreenshotTimer += Time.deltaTime;
                if (e2eScreenshotTimer >= 2.0f)
                {
                    e2eScreenshotTimer = -9999f;
                    string targetPath = Application.isEditor ?
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Documentation/QA/CMP-232-e2eForestBoss.png")) :
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../Documentation/QA/CMP-232-e2eForestBoss.png"));
                    UnityEngine.ScreenCapture.CaptureScreenshot(targetPath);
                    StartCoroutine(QuitAfterDelay());
                }
            }
            if (IsE2ECaveBossMode)
            {
                e2eScreenshotTimer += Time.deltaTime;
                if (e2eScreenshotTimer >= 2.0f)
                {
                    e2eScreenshotTimer = -9999f;
                    string targetPath = Application.isEditor ?
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Documentation/QA/CMP-234-e2eCaveBoss.png")) :
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../Documentation/QA/CMP-234-e2eCaveBoss.png"));
                    UnityEngine.ScreenCapture.CaptureScreenshot(targetPath);
                    StartCoroutine(QuitAfterDelay());
                }
            }
            if (IsE2ECastleBossMode)
            {
                e2eScreenshotTimer += Time.deltaTime;
                if (e2eScreenshotTimer >= 2.0f)
                {
                    e2eScreenshotTimer = -9999f;
                    string targetPath = Application.isEditor ?
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Documentation/QA/CMP-234-e2eCastleBoss.png")) :
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../Documentation/QA/CMP-234-e2eCastleBoss.png"));
                    UnityEngine.ScreenCapture.CaptureScreenshot(targetPath);
                    StartCoroutine(QuitAfterDelay());
                }
            }
            if (IsE2EForestBossCombatMode)
            {
                e2eScreenshotTimer += Time.deltaTime;
                if (e2eScreenshotTimer >= 4.5f)
                {
                    e2eScreenshotTimer = -9999f;
                    string targetPath = Application.isEditor ?
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Documentation/QA/CMP-263-e2eForestBossCombat.png")) :
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../Documentation/QA/CMP-263-e2eForestBossCombat.png"));
                    UnityEngine.ScreenCapture.CaptureScreenshot(targetPath);
                    StartCoroutine(QuitAfterDelay());
                }
            }
            if (IsE2EForestCombatMode)
            {
                e2eScreenshotTimer += Time.deltaTime;
                if (e2eScreenshotTimer >= 3.2f)
                {
                    e2eScreenshotTimer = -9999f;
                    string targetPath = Application.isEditor ?
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Documentation/QA/CMP-263-e2eForestCombat.png")) :
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../Documentation/QA/CMP-263-e2eForestCombat.png"));
                    UnityEngine.ScreenCapture.CaptureScreenshot(targetPath);
                    StartCoroutine(QuitAfterDelay());
                }
            }

            if (screen != lastLoggedScreen || (graph != null && graph.CurrentId != lastLoggedRoom) || enemies.Count != lastLoggedEnemies || (stats != null && stats.hearts != lastLoggedHearts))
            {
                ScreenState oldScreen = lastLoggedScreen;
                int oldRoom = lastLoggedRoom;

                lastLoggedScreen = screen;
                lastLoggedRoom = graph != null ? graph.CurrentId : -1;
                lastLoggedEnemies = enemies.Count;
                lastLoggedHearts = stats != null ? stats.hearts : 0f;
                Debug.Log(string.Format("[AUDIT_STATE] Screen={0} Stage={1} Room={2} Enemies={3} Hearts={4}",
                    screen,
                    graph != null ? graph.Stage.ToString() : "none",
                    lastLoggedRoom,
                    lastLoggedEnemies,
                    lastLoggedHearts));

                // Capture screenshots dynamically
                if (screen == ScreenState.Title && oldScreen != ScreenState.Title)
                {
                    CaptureAuditScreenshot("CMP-237-title.png");
                }
                else if (screen == ScreenState.Ability && oldScreen != ScreenState.Ability)
                {
                    CaptureAuditScreenshot("CMP-237-ability-selection.png");
                }
                else if (screen == ScreenState.Shop && oldScreen != ScreenState.Shop)
                {
                    CaptureAuditScreenshot("CMP-237-shop.png");
                }
                else if (screen == ScreenState.Play && graph != null && graph.CurrentId == 4 && oldRoom != 4)
                {
                    if (graph.Stage == StageId.Forest) CaptureAuditScreenshot("CMP-237-forest-boss.png");
                    else if (graph.Stage == StageId.Cave) CaptureAuditScreenshot("CMP-237-cave-boss.png");
                    else if (graph.Stage == StageId.Castle) CaptureAuditScreenshot("CMP-237-castle-boss.png");
                }
                else if ((screen == ScreenState.Ending && oldScreen != ScreenState.Ending) || (screen == ScreenState.Result && oldScreen != ScreenState.Result))
                {
                    CaptureAuditScreenshot("CMP-237-ending.png");
                }
            }

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
            AdvanceCombatWaves();
            UpdateAimTarget();
            TickProjectiles();
            if (graph.Current.kind != RoomKind.Shop && enemies.Count == 0 && pendingCombatEnemies.Count == 0 && !graph.Current.cleared)
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
                    if (StageRules.HasNext(graph.Stage)) { stats.Heal(difficulty == Difficulty.Easy ? 2f : 1f); screen = ScreenState.StageTransition; return; }
                    endingIndex = 0; screen = ScreenState.Ending; PlayMusic("Audio/bgm_boss"); return;
                }
            }
            var exitDirection = RoomExitRules.ExitDirection(player.transform.position.x, player.transform.position.y);
            if (exitDirection.HasValue && (enemies.Count == 0 || graph.Current.cleared)) ChangeRoom(exitDirection.Value);
        }

        private System.Collections.IEnumerator QuitAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(0);
        }

        private void CaptureAuditScreenshot(string filename)
        {
            string folder = Application.isEditor ?
                System.IO.Path.Combine(Application.dataPath, "../Documentation/QA") :
                System.IO.Path.Combine(Application.dataPath, "../../../Documentation/QA");
            string targetPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, filename));
            UnityEngine.ScreenCapture.CaptureScreenshot(targetPath);
            Debug.Log("[AUDIT_SCREENSHOT] Saved to " + filename);
        }

        private void LateUpdate()
        {
            if (hud != null)
            {
                hud.Refresh(stats, graph, selectedAbilities, abilityChoices, enemies, currentTarget, banner, screen, cam);
                var canvasTrans = hud.transform.Find("Gameplay HUD Canvas");
                if (canvasTrans != null)
                {
                    var abilityTextTrans = canvasTrans.Find("Safe Area/Ability Build/Ability Text");
                    if (abilityTextTrans != null)
                    {
                        var textComp = abilityTextTrans.GetComponent<UnityEngine.UI.Text>();
                        if (textComp != null)
                        {
                            textComp.text = "능력 " + Mathf.Clamp(abilityChoices, 0, AbilityRules.ChoicesPerRun) + "/" + AbilityRules.ChoicesPerRun;
                        }
                    }
                }
            }
            if (overlay != null)
                overlay.Refresh(screen, stats, cards, bought, graph);
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
                if (Input.GetKeyDown(KeyCode.Escape)) ExecuteOverlayCommand(GameOverlayCommand.Resume, -1);
                else if (Input.GetKeyDown(KeyCode.R)) ExecuteOverlayCommand(GameOverlayCommand.Restart, -1);
                else if (Input.GetKeyDown(KeyCode.T)) ExecuteOverlayCommand(GameOverlayCommand.Title, -1);
                return;
            }
            if (screen == ScreenState.Ability)
            {
                int choice = ChoiceKey();
                if (choice >= 0) ExecuteOverlayCommand(GameOverlayCommand.Select, choice);
                return;
            }
            int shopChoice = ChoiceKey();
            if (shopChoice >= 0) ExecuteOverlayCommand(GameOverlayCommand.Select, shopChoice);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                ExecuteOverlayCommand(GameOverlayCommand.LeaveShop, -1);
        }

        public static bool IsCanvasOverlayScreen(ScreenState value)
        {
            return value == ScreenState.Ability || value == ScreenState.Shop || value == ScreenState.Pause;
        }

        public void ExecuteOverlayCommand(GameOverlayCommand command, int index)
        {
            if (screen == ScreenState.Ability && command == GameOverlayCommand.Select)
            {
                SelectAbility(index);
                return;
            }
            if (screen == ScreenState.Shop)
            {
                if (command == GameOverlayCommand.Select) TryPurchaseShopItem(index);
                else if (command == GameOverlayCommand.LeaveShop) LeaveShop();
                return;
            }
            if (screen != ScreenState.Pause) return;
            if (command == GameOverlayCommand.Resume) screen = ScreenState.Play;
            else if (command == GameOverlayCommand.Restart) BeginRun(difficulty);
            else if (command == GameOverlayCommand.Title) screen = ScreenState.Title;
            else if (command == GameOverlayCommand.Settings)
            {
                settingsReturnScreen = ScreenState.Pause;
                screen = ScreenState.Settings;
            }
        }

        private void LeaveShop()
        {
            if (screen != ScreenState.Shop || graph == null) return;
            graph.ClearCurrent();
            screen = ScreenState.Play;
            banner = "상점 방을 나왔다";
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
            StageId startStage = StageId.Forest;
            if (IsE2ECaveBossMode) startStage = StageId.Cave;
            else if (IsE2ECastleBossMode) startStage = StageId.Castle;
            graph = new RoomGraph(startStage);
            if (IsE2EForestBossMode || IsE2ECaveBossMode || IsE2ECastleBossMode || IsE2EForestBossCombatMode)
            {
                graph.ForceRoomId(4);
            }
            BuildPlayer();
            LoadRoom();
            if (graph.Stage == StageId.Forest) PlayMusic("Audio/bgm_forest");
            else if (graph.Stage == StageId.Cave) PlayMusic("Audio/bgm_cave");
            else PlayMusic("Audio/bgm_castle");
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
            var go = MakeActor("Leon", new Color(.95f, .75f, .32f), Vector3.zero, 1f, SpriteSorting.Actor, "Art/Player/Leon/leon_idle_south");
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
            else BeginCombatWaves(room);
        }
        private void BeginCombatWaves(RoomState room)
        {
            pendingCombatEnemies.Clear();
            var types = StageRules.Enemies(graph.Stage);
            int total = CampaignPacingRules.EnemiesInRoom(graph.Stage, room.elite);
            for (int index = 0; index < total; index++) pendingCombatEnemies.Add(types[(index + room.id) % types.Length]);
            combatWaveIndex = 0;
            combatWaveCount = CampaignPacingRules.WavesInRoom(graph.Stage, room.elite);
            waitingForNextWave = false;
            SpawnNextCombatWave(room.elite);
        }
        private void AdvanceCombatWaves()
        {
            if (pendingCombatEnemies.Count == 0 || enemies.Count > 0 || graph == null || graph.Current.kind != RoomKind.Combat) return;
            if (!waitingForNextWave)
            {
                waitingForNextWave = true;
                nextWaveAt = Time.time + CampaignPacingRules.InterWaveDelay;
                banner = graph.Current.label + " · 다음 파도 준비";
                if (!Application.isPlaying) SpawnNextCombatWave(graph.Current.elite);
                return;
            }
            if (Time.time >= nextWaveAt) SpawnNextCombatWave(graph.Current.elite);
        }
        private void SpawnNextCombatWave(bool elite)
        {
            if (pendingCombatEnemies.Count == 0) return;
            int count = Mathf.Min(CampaignPacingRules.WaveSize(graph.Stage, elite), pendingCombatEnemies.Count);
            Vector3[] positions =
            {
                new Vector3(3f, 1.45f, 0f),
                new Vector3(5.35f, -1.35f, 0f),
                new Vector3(6.65f, .35f, 0f),
                new Vector3(4.25f, 2.45f, 0f)
            };
            for (int index = 0; index < count; index++)
            {
                EnemyType type = pendingCombatEnemies[0];
                pendingCombatEnemies.RemoveAt(0);
                SpawnEnemy(type, positions[index], elite);
            }
            combatWaveIndex++;
            waitingForNextWave = false;
            banner = graph.Current.label + " · 파도 " + combatWaveIndex + "/" + combatWaveCount;
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
            RoomEnvironmentView.Build(transform, graph.Stage, graph, roomObjects);
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
            foreach (var hazard in FindObjectsByType<BossHazardActor>(FindObjectsSortMode.None)) DestroyRuntimeObject(hazard ? hazard.gameObject : null);
            foreach (var slash in FindObjectsByType<BossSlashEffect>(FindObjectsSortMode.None)) DestroyRuntimeObject(slash ? slash.gameObject : null);
            enemies.Clear(); projectiles.Clear(); pendingCombatEnemies.Clear();
            combatWaveIndex = 0; combatWaveCount = 0; waitingForNextWave = false;
        }
        private static void DestroyRuntimeObject(GameObject actor)
        {
            if (!actor) return; actor.SetActive(false);
            if (Application.isPlaying) Destroy(actor); else DestroyImmediate(actor);
        }
        public void SpawnEnemy(EnemyType type, Vector3 position, bool elite = false)
        {
            var colors = new Dictionary<EnemyType, Color>
            {
                { EnemyType.Wolf, new Color(.65f, .7f, .78f) }, { EnemyType.Mushroom, new Color(.85f, .25f, .32f) },
                { EnemyType.RockBeetle, new Color(.45f, .38f, .32f) }, { EnemyType.CrystalSpirit, new Color(.25f, .8f, .95f) },
                { EnemyType.ShieldSkeleton, new Color(.8f, .8f, .72f) }, { EnemyType.DarkMage, new Color(.55f, .2f, .78f) },
                { EnemyType.TreeSpirit, new Color(.35f, .65f, .22f) }, { EnemyType.GiantBat, new Color(.65f, .18f, .65f) },
                { EnemyType.Morkan, new Color(.9f, .22f, .18f) }
            };
            string resource = EnemyActor.ResourcePath(type);
            float size = type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan ? 1.7f : 1f;
            var go = MakeActor(type.ToString(), colors[type], position, size, SpriteSorting.Actor, resource);
            float health = CampaignPacingRules.EnemyHealth(type, graph == null ? StageId.Forest : graph.Stage, elite);
            var enemy = go.AddComponent<EnemyActor>(); enemy.Initialize(this, player, type, health); enemies.Add(enemy);
        }
        private void TickEnemies() { for (int i = enemies.Count - 1; i >= 0; i--) { if (enemies[i] == null) enemies.RemoveAt(i); else enemies[i].Tick(); } }
        private void TickProjectiles() { for (int i = projectiles.Count - 1; i >= 0; i--) { if (projectiles[i] == null) projectiles.RemoveAt(i); else projectiles[i].Tick(); } }
        private void UpdateAimTarget()
        {
            EnemyActor previous = currentTarget;
            currentTarget = FindNearestVisibleEnemy();
            if (previous != null && previous != currentTarget) previous.SetTargeted(false);
            if (currentTarget != null) currentTarget.SetTargeted(true);
        }
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
            player.PresentAttack();
            nextShot = Time.time + stats.attackInterval;
        }
        public void SpawnEnemyShot(Vector3 origin, Vector3 direction, float damage, EnemyProjectileStyle style = EnemyProjectileStyle.EmberBolt)
        {
            var go = MakeActor("Enemy Shot " + style, EnemyProjectileRules.ColorFor(style), origin, EnemyProjectileRules.ScaleFor(style), SpriteSorting.Projectile);
            var shot = go.AddComponent<ProjectileActor>(); shot.Initialize(this, origin, direction, true, damage, 0, 0, style); projectiles.Add(shot);
        }
        public void SpawnBossHazard(EnemyType boss, Vector3 position, float damage, float radius)
        {
            var go = new GameObject(boss + " Ground Hazard");
            go.transform.SetParent(transform, true);
            go.transform.position = position;
            go.AddComponent<BossHazardActor>().Initialize(this, boss, damage, radius);
        }
        public void SpawnBossSlash(EnemyType boss, Vector3 origin, Vector2 direction, float damage, float range)
        {
            var go = new GameObject(boss == EnemyType.Morkan ? "Morkan Sword Slash" : boss + " Sweep");
            go.transform.SetParent(transform, true);
            go.transform.position = origin;
            go.AddComponent<BossSlashEffect>().Initialize(this, boss, direction, damage, range);
        }
        public void RemoveEnemy(EnemyActor enemy) { enemies.Remove(enemy); }
        public void RewardEnemy(EnemyType type) { RewardEnemy(type, Vector3.zero); }
        internal void RewardEnemy(EnemyType type, Vector3 position)
        {
            int reward = SoulRewardRules.For(type);
            stats.souls += reward;
            PlayEffect("Audio/sfx_enemy_defeat");
            if (reward > 0) PlayEffect("Audio/sfx_soul_gain");
            if (gameFeel != null) gameFeel.EnemyDeath(position, ActorVisualProfile.Enemy(type).AccentColor, reward);
        }
        public void DamagePlayer(float amount)
        {
            if (stats.TakeDamage(amount, Time.time, difficulty))
            {
                hitFlash = Time.time + .15f; PlayEffect("Audio/sfx_player_hurt");
                if (player != null) player.PresentHit();
                if (hud != null) hud.PulseDamage();
                if (gameFeel != null && player != null) gameFeel.PlayerHit(player.transform.position);
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
            var label = new GUIStyle(GUI.skin.label) { font = GameFont.Get(), fontSize = 14, normal = { textColor = Color.white } };
            if (screen == ScreenState.Title) { DrawTitle(label); return; }
            if (screen == ScreenState.Difficulty) { DrawDifficulty(label); return; }
            if (screen == ScreenState.Intro) { DrawCutscene(label, new[] { "우연히 만난 엘리아 공주", "마왕 기사 모르칸이 공주를 납치했다", "레온은 활을 들고 여정을 시작한다" }[introIndex], "도입 컷신"); return; }
            if (screen == ScreenState.Ending) { DrawCutscene(label, endingIndex == 0 ? "엘리아 공주의 봉인이 풀렸다" : "두 사람은 왕국으로 돌아갔다", "공주 구출 엔딩"); return; }
            if (screen == ScreenState.History) { DrawHistory(label); return; }
            if (screen == ScreenState.Controls) { DrawPanel(label, "조작법", "WASD/방향키 이동\n정지 시 화면 안의 가장 가까운 적 자동 공격\n숫자키 또는 마우스로 카드·상점 선택\nSpace/Enter 컷신 진행"); if (PixelButton(new Rect(245, 285, 150, 32), "뒤로")) screen = ScreenState.Title; return; }
            if (screen == ScreenState.Settings) { DrawSettings(label); return; }
            if (screen == ScreenState.Result) { DrawResult(label); return; }
            bool useCanvasHud = hud != null && Application.isPlaying;
            if (!useCanvasHud) DrawHud(label);
            if (IsCanvasOverlayScreen(screen) && overlay != null && Application.isPlaying) return;
            if (screen == ScreenState.StageTransition) { DrawPanel(label, "다음 스테이지", StageRules.Name(StageRules.Next(graph.Stage)) + "으로 이동합니다\nSpace/Enter 계속"); if (PixelButton(new Rect(245, 250, 150, 34), "계속", true)) ContinueStage(); }
            if (!useCanvasHud && hitFlash > Time.time) { GUI.color = new Color(1, .25f, .25f, .5f); GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.whiteTexture); GUI.color = Color.white; }
        }
        private void DrawTitle(GUIStyle label)
        {
            GUI.color = Color.white; var titleTexture = Resources.Load<Texture2D>("Art/Illustrations/title_background_v2") ?? Resources.Load<Texture2D>("Art/Illustrations/title_background"); if (titleTexture != null) GUI.DrawTexture(new Rect(0, 0, 640, 360), titleTexture, ScaleMode.StretchToFill); else { GUI.color = new Color(.04f, .08f, .12f); GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.whiteTexture); } GUI.color = Color.white;
            GUI.Label(new Rect(80, 68, 480, 48), "소울 아처", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, normal = { textColor = new Color(1f, .82f, .3f) } });
            if (PixelButton(new Rect(235, 145, 170, 34), "[1] 새 게임", true)) screen = ScreenState.Difficulty;
            if (PixelButton(new Rect(235, 184, 170, 34), "[2] 전적")) { historyPage = 0; screen = ScreenState.History; }
            if (PixelButton(new Rect(235, 223, 170, 34), "[3] 조작법")) screen = ScreenState.Controls;
            if (PixelButton(new Rect(235, 262, 170, 34), "[4] 설정")) { settingsReturnScreen = ScreenState.Title; screen = ScreenState.Settings; }
            GUI.Label(new Rect(170, 310, 300, 20), "엘리아 공주를 구하러 가자", label);
        }
        private void DrawDifficulty(GUIStyle label)
        {
            DrawOverlayBackground();
            GUI.Label(new Rect(80, 60, 480, 40), "난이도 선택", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = new Color(1f, .82f, .3f) } });
            if (PixelButton(new Rect(105, 130, 200, 100), "[1] 쉬움\n받는 피해 -0.5 하트", true)) { difficulty = Difficulty.Easy; BeginIntro(); }
            if (PixelButton(new Rect(335, 130, 200, 100), "[2] 보통\n기본 피해")) { difficulty = Difficulty.Normal; BeginIntro(); }
            if (PixelButton(new Rect(245, 275, 150, 32), "뒤로")) screen = ScreenState.Title;
        }
        private void DrawHud(GUIStyle label)
        {
            GUI.Label(new Rect(12, 10, 260, 24), HeartText() + "   ◇ 영혼 " + stats.souls, label);
            GUI.Label(new Rect(245, 10, 220, 24), StageRules.Name(graph.Stage) + "  " + graph.ClearedCombatCount + "/3", label);
            GUI.Label(new Rect(12, 32, 350, 22), graph.Current.label + (graph.Current.cleared ? "  ✓" : ""), label);
            GUI.Label(new Rect(12, 330, 570, 20), "WASD/방향키 이동 · 정지 시 자동 조준 발사 · Esc 일시정지", label);
            DrawMiniMap();
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
                if (enemy == null || enemy.Dead || enemy.LastDamagedAt <= 0f || Time.time - enemy.LastDamagedAt > 2f || enemy.IsBoss) continue;
                Vector3 point = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * .65f); if (point.z < 0) continue;
                float x = point.x * DisplaySettings.InternalWidth / Screen.width; float y = DisplaySettings.InternalHeight - point.y * DisplaySettings.InternalHeight / Screen.height;
                GUI.color = new Color(.12f, .12f, .12f); GUI.DrawTexture(new Rect(x - 18, y - 3, 36, 5), Texture2D.whiteTexture); GUI.color = new Color(.35f, .9f, .35f); GUI.DrawTexture(new Rect(x - 17, y - 2, 34 * Mathf.Clamp01(enemy.Health / enemy.MaxHealth), 3), Texture2D.whiteTexture); GUI.color = Color.white;
            }
        }
        private void DrawResult(GUIStyle label)
        {
            string abilities = selectedAbilities.Count == 0 ? "없음" : string.Join(", ", selectedAbilities.Select(AbilityName).ToArray());
            DrawPanel(label, resultMessage, "난이도 " + (difficulty == Difficulty.Easy ? "쉬움" : "보통") + " · " + StageRules.Name(graph.Stage) + " " + (graph.CurrentId + 1) + "번 방\n플레이 " + FormatTime(runSeconds) + " · 하트 " + stats.hearts.ToString("0.0") + " · 영혼 " + stats.souls + "\n능력: " + abilities);
            if (PixelButton(new Rect(150, 285, 105, 32), "다시 시작", true)) BeginRun(difficulty);
            if (PixelButton(new Rect(268, 285, 105, 32), "전적")) { historyPage = 0; screen = ScreenState.History; }
            if (PixelButton(new Rect(386, 285, 105, 32), "타이틀")) screen = ScreenState.Title;
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
            GUI.enabled = historyPage > 0; if (PixelButton(new Rect(175, 292, 80, 30), "이전")) historyPage--; GUI.enabled = true;
            GUI.Label(new Rect(270, 296, 100, 20), (historyPage + 1) + " / " + pageCount, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            GUI.enabled = historyPage < pageCount - 1; if (PixelButton(new Rect(385, 292, 80, 30), "다음")) historyPage++; GUI.enabled = true;
            if (PixelButton(new Rect(30, 326, 110, 28), historyClearArmed ? "정말 초기화" : "기록 초기화")) { if (historyClearArmed) { RunHistory.Clear(); historyPage = 0; historyClearArmed = false; } else historyClearArmed = true; }
            if (historyClearArmed && PixelButton(new Rect(150, 326, 80, 28), "취소")) historyClearArmed = false;
            if (PixelButton(new Rect(500, 326, 110, 28), "뒤로")) { historyClearArmed = false; screen = ScreenState.Title; }
        }
        private void DrawSettings(GUIStyle label)
        {
            DrawOverlayBackground(); GUI.Label(new Rect(80, 55, 480, 40), "설정", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = new Color(1f, .82f, .3f) } });
            GUI.Label(new Rect(130, 125, 150, 24), "전체 음량 " + Mathf.RoundToInt(masterVolume * 100) + "%", label);
            float volume = GUI.HorizontalSlider(new Rect(280, 131, 220, 20), masterVolume, 0f, 1f); if (!Mathf.Approximately(volume, masterVolume)) SetMasterVolume(volume);
            bool nextFullscreen = GUI.Toggle(new Rect(205, 180, 230, 28), fullscreen, " 전체화면 (F)"); if (nextFullscreen != fullscreen) SetFullscreen(nextFullscreen);
            GUI.Label(new Rect(135, 225, 370, 40), "설정과 익명 전적만 로컬에 저장합니다.\n개인정보와 온라인 데이터는 저장하지 않습니다.", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            if (PixelButton(new Rect(245, 285, 150, 32), "뒤로")) screen = settingsReturnScreen;
        }
        private void DrawPanel(GUIStyle label, string title, string body) { DrawOverlayBackground(); GUI.Label(new Rect(80, 65, 480, 40), title, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, normal = { textColor = new Color(1f, .82f, .3f) } }); GUI.Label(new Rect(100, 130, 440, 150), body, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 17 }); }
        private static void DrawOverlayBackground()
        {
            GUI.color = new Color(0, 0, 0, .9f);
            GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.whiteTexture);
            Rect panel = new Rect(22, 18, 596, 324);
            if (!DrawKenneyFrame(panel, "Art/UI/Kenney/space_panel", new Color(.34f, .54f, .76f, .96f)))
                DrawPixelFrame(panel, new Color(.035f, .075f, .12f, .9f), new Color(1f, .72f, .24f, .95f));
            else
                DrawPixelOutline(panel, new Color(1f, .72f, .24f, .95f));
            GUI.color = Color.white;
        }

        private static bool PixelButton(Rect rect, string text, bool primary = false)
        {
            bool enabled = GUI.enabled;
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            Color fill = !enabled ? new Color(.06f, .08f, .1f, .72f) : hovered ? new Color(.18f, .29f, .36f, .98f) : new Color(.07f, .13f, .2f, .96f);
            Color edge = primary ? new Color(1f, .72f, .24f, .98f) : new Color(.48f, .76f, .9f, .94f);
            string framePath = primary ? "Art/UI/Kenney/ancient_brown_panel" : "Art/UI/Kenney/space_panel";
            Color frameTint = primary
                ? (hovered ? new Color(1f, .95f, .72f, .98f) : new Color(.98f, .78f, .48f, .98f))
                : (hovered ? new Color(.56f, .82f, 1f, .98f) : new Color(.36f, .62f, .9f, .96f));
            if (!DrawKenneyFrame(rect, framePath, frameTint)) DrawPixelFrame(rect, fill, edge);
            else DrawPixelOutline(rect, edge);
            var style = new GUIStyle(GUI.skin.button)
            {
                font = GameFont.Get(),
                fontSize = rect.height >= 70f ? 15 : 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { background = null, textColor = enabled ? Color.white : new Color(.55f, .6f, .64f) },
                hover = { background = null, textColor = Color.white },
                active = { background = null, textColor = new Color(1f, .88f, .48f) },
                focused = { background = null, textColor = Color.white }
            };
            return GUI.Button(rect, text, style);
        }

        private static void DrawPixelFrame(Rect rect, Color fill, Color edge)
        {
            Color previous = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            DrawPixelOutline(rect, edge);
        }

        private static void DrawPixelOutline(Rect rect, Color edge)
        {
            Color previous = GUI.color;
            GUI.color = edge;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, .42f);
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.yMax - 5f, rect.width - 6f, 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static bool DrawKenneyFrame(Rect rect, string resourcePath, Color tint)
        {
            Texture2D texture = LoadUiTexture(resourcePath);
            if (texture == null) return false;
            float border = Mathf.Min(8f, Mathf.Min(rect.width, rect.height) * .5f);
            float sourceBorderX = Mathf.Min(8f, texture.width * .5f);
            float sourceBorderY = Mathf.Min(8f, texture.height * .5f);
            float middleWidth = Mathf.Max(0f, rect.width - border * 2f);
            float middleHeight = Mathf.Max(0f, rect.height - border * 2f);
            float u = sourceBorderX / texture.width;
            float v = sourceBorderY / texture.height;
            float centerU = 1f - u * 2f;
            float centerV = 1f - v * 2f;
            Color previous = GUI.color;
            GUI.color = tint;
            DrawNineSlicePatch(texture, new Rect(rect.x, rect.y, border, border), new Rect(0f, 0f, u, v));
            DrawNineSlicePatch(texture, new Rect(rect.x + border, rect.y, middleWidth, border), new Rect(u, 0f, centerU, v));
            DrawNineSlicePatch(texture, new Rect(rect.xMax - border, rect.y, border, border), new Rect(1f - u, 0f, u, v));
            DrawNineSlicePatch(texture, new Rect(rect.x, rect.y + border, border, middleHeight), new Rect(0f, v, u, centerV));
            DrawNineSlicePatch(texture, new Rect(rect.x + border, rect.y + border, middleWidth, middleHeight), new Rect(u, v, centerU, centerV));
            DrawNineSlicePatch(texture, new Rect(rect.xMax - border, rect.y + border, border, middleHeight), new Rect(1f - u, v, u, centerV));
            DrawNineSlicePatch(texture, new Rect(rect.x, rect.yMax - border, border, border), new Rect(0f, 1f - v, u, v));
            DrawNineSlicePatch(texture, new Rect(rect.x + border, rect.yMax - border, middleWidth, border), new Rect(u, 1f - v, centerU, v));
            DrawNineSlicePatch(texture, new Rect(rect.xMax - border, rect.yMax - border, border, border), new Rect(1f - u, 1f - v, u, v));
            GUI.color = previous;
            return true;
        }

        private static Texture2D LoadUiTexture(string resourcePath)
        {
            if (UiTextureCache.TryGetValue(resourcePath, out Texture2D texture)) return texture;
            texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            UiTextureCache[resourcePath] = texture;
            return texture;
        }

        private static void DrawNineSlicePatch(Texture2D texture, Rect destination, Rect source)
        {
            if (destination.width <= 0f || destination.height <= 0f) return;
            GUI.DrawTextureWithTexCoords(destination, texture, source, true);
        }

        private void DrawCutscene(GUIStyle label, string text, string title)
        {
            DrawOverlayBackground();
            string path = screen == ScreenState.Intro
                ? "Art/Illustrations/intro_0" + (introIndex + 1) + "_" + (introIndex == 0 ? "meeting" : introIndex == 1 ? "abduction" : "departure")
                : endingIndex == 0 ? "Art/Illustrations/ending_01_rescue" : "Art/Illustrations/ending_02_return";
            Texture2D texture = Resources.Load<Texture2D>(path + "_v2") ?? Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                GUI.DrawTexture(new Rect(0, 0, 640, 280), texture, ScaleMode.StretchToFill);
            }
            GUI.Label(new Rect(70, 38, 500, 30), title + "  " + ((screen == ScreenState.Intro ? introIndex : endingIndex) + 1) + "/" + (screen == ScreenState.Intro ? 3 : 2), new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            GUI.Label(new Rect(30, 275, 580, 35), text, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            if (PixelButton(new Rect(245, 318, 150, 32), "다음", true))
            {
                if (screen == ScreenState.Intro) { introIndex++; if (introIndex >= 3) BeginRun(difficulty); }
                else { endingIndex++; if (endingIndex >= 2) FinishRun(true); }
            }
        }
        private static string FormatTime(float seconds) { int total = Mathf.Max(0, Mathf.RoundToInt(seconds)); return (total / 60).ToString("00") + ":" + (total % 60).ToString("00"); }
        private static string AbilityName(Ability ability) { switch (ability) { case Ability.Attack: return "공격"; case Ability.Speed: return "공격 속도"; case Ability.Pierce: return "관통"; case Ability.Reflect: return "반사"; case Ability.Heal: return "회복"; default: return "이동 속도"; } }
    }

    public sealed class PlayerActor : MonoBehaviour
    {
        private SoulArcherGame game;
        private PlayerStats stats;
        private ActorView view;
        private Vector2 facing = Vector2.down;
        public bool IsMoving { get; private set; }

        public void Initialize(SoulArcherGame owner, PlayerStats playerStats)
        {
            game = owner;
            stats = playerStats;
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            view = GetComponent<ActorView>();
            if (view == null) view = gameObject.AddComponent<ActorView>();
            view.Initialize(
                renderer,
                "Art/Player/Leon/leon_idle",
                new SpriteSheetLayout(64, 64, 1, 8, 64f),
                "Art/Player/Leon/leon_walk",
                new SpriteSheetLayout(66, 66, 6, 8, 66f),
                ActorVisualProfile.Player(),
                false);
        }

        public void Tick()
        {
            Vector2 input;
            if (game.IsE2ECampaignMode)
            {
                input = Vector2.zero;
                if (game.CurrentScreen == ScreenState.Play && (game.ActiveEnemyCount == 0 || (game.CurrentGraph != null && game.CurrentGraph.Current != null && game.CurrentGraph.Current.cleared)))
                {
                    int currentRoomId = game.CurrentGraph != null ? game.CurrentGraph.CurrentId : -1;
                    if (currentRoomId == 0)
                    {
                        input = Vector2.up; // Move North
                    }
                    else if (currentRoomId == 2)
                    {
                        input = Vector2.right; // Move East
                    }
                    else if (currentRoomId == 3)
                    {
                        input = Vector2.right; // Move East
                    }
                }
            }
            else
            {
                input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            }
            IsMoving = input.sqrMagnitude > .001f;
            if (IsMoving) facing = input;
            if (view != null) view.Tick(facing, IsMoving, Time.time);
            transform.position += (Vector3)(input * stats.moveSpeed * Time.deltaTime);
            float minX = RoomExitRules.PlayerMinX;
            float maxX = RoomExitRules.PlayerMaxX;
            float minY = RoomExitRules.PlayerMinY;
            float maxY = RoomExitRules.PlayerMaxY;
            if (game.CurrentGraph != null && game.CurrentGraph.Current != null && game.CurrentGraph.Current.kind == RoomKind.Boss && (game.CurrentGraph.Stage == StageId.Forest || game.CurrentGraph.Stage == StageId.Cave || game.CurrentGraph.Stage == StageId.Castle))
            {
                minX = -7.5f;
                maxX = 7.5f;
                minY = -2.5f;
                maxY = 2.0f;
            }
            transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), Mathf.Clamp(transform.position.y, minY, maxY), 0);
            if (!IsMoving && !game.IsE2EForestBossMode && !game.IsE2ECaveBossMode && !game.IsE2ECastleBossMode) game.FireArrow();
        }

        public void PresentAttack() { if (view != null) view.PlayAttack(); }
        public void PresentHit() { if (view != null) view.PlayHit(stats == null ? 0f : stats.hearts / PlayerStats.MaxHearts); }
    }

    public sealed class EnemyActor : MonoBehaviour
    {
        private SoulArcherGame game;
        private PlayerActor player;
        private EnemyType type;
        private float health;
        private float maxHealth;
        private float nextAction;
        private float phaseTimer;
        private ActorView view;
        private ActorVisualProfile visualProfile;
        public bool Dead { get; private set; }
        public EnemyType Type { get { return type; } }
        public float Health { get { return health; } }
        public float MaxHealth { get { return maxHealth; } }
        public bool IsBoss { get { return type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan; } }
        public float LastDamagedAt { get; private set; }
        public int BossPhase { get { return type == EnemyType.Morkan && health <= maxHealth * .5f ? 2 : 1; } }
        private readonly List<GameObject> warningLines = new List<GameObject>();
        private Vector2 attackDirection;
        private bool hasLockedDirection;
        private float dashUntil;
        private int activeBossPattern = -1;
        private int lastBossPattern = -1;
        private float bossPatternExecuteAt;
        private Vector2 bossPatternDirection;
        private Vector2 bossPatternTarget;
        private Vector2 bossDashDirection;
        private float bossDashUntil;

        private void ClearWarningLines()
        {
            if (warningLines != null)
            {
                foreach (var line in warningLines)
                {
                    if (line != null) Destroy(line);
                }
                warningLines.Clear();
            }
        }

        private void ShowWarningLines(Vector2 direction, float range, bool triple)
        {
            float dist = Mathf.Min(range, Vector2.Distance(transform.position, player.transform.position));
            int neededLines = triple ? 3 : 1;
            while (warningLines.Count < neededLines)
            {
                var line = new GameObject("BossWarningLine");
                var sr = line.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSpriteAtlas.SoftSquare();
                sr.color = new Color(1f, .58f, .18f, .32f);
                sr.sortingOrder = SpriteSorting.Projectile - 1;
                warningLines.Add(line);
            }
            while (warningLines.Count > neededLines)
            {
                var line = warningLines[warningLines.Count - 1];
                if (line != null) Destroy(line);
                warningLines.RemoveAt(warningLines.Count - 1);
            }

            float alpha = .26f + Mathf.PingPong(Time.unscaledTime * 5f, .22f);
            foreach (var l in warningLines)
            {
                if (l != null)
                {
                    var sr = l.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        float orangeG = .44f + Mathf.PingPong(Time.unscaledTime * 8f, .16f);
                        sr.color = new Color(1f, orangeG, .12f, alpha);
                    }
                }
            }

            if (triple)
            {
                Vector2 dir0 = direction;
                Vector2 dir1 = Quaternion.Euler(0, 0, 30) * direction;
                Vector2 dir2 = Quaternion.Euler(0, 0, -30) * direction;
                SetLinePosition(warningLines[0], dir0, dist);
                SetLinePosition(warningLines[1], dir1, dist);
                SetLinePosition(warningLines[2], dir2, dist);
            }
            else
            {
                SetLinePosition(warningLines[0], direction, dist);
            }
        }

        private void SetLinePosition(GameObject line, Vector2 dir, float length)
        {
            if (line == null) return;
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(dir * length);
            line.transform.position = (start + end) * 0.5f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            float progress = 0.5f;
            if (phaseTimer >= 3.5f && phaseTimer < 4.0f)
            {
                progress = (phaseTimer - 3.5f) / 0.5f;
            }
            else
            {
                float timeToAttack = nextAction - Time.time;
                if (timeToAttack > 0f && timeToAttack <= 0.5f)
                {
                    progress = (0.5f - timeToAttack) / 0.5f;
                }
            }

            float widthPulse = Mathf.Lerp(.045f, .13f, progress) + Mathf.PingPong(Time.unscaledTime * 6f, .018f);
            line.transform.localScale = new Vector3(length, widthPulse, 1f);
        }

        private void OnDestroy()
        {
            ClearWarningLines();
        }

        public void Initialize(SoulArcherGame owner, PlayerActor target, EnemyType enemyType, float healthOverride = -1f)
        {
            game = owner; player = target; type = enemyType;
            maxHealth = healthOverride > 0f ? healthOverride : CampaignPacingRules.EnemyHealth(type, StageId.Forest, false);
            health = maxHealth;
            string resourcePath = ResourcePath(type);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            bool usesV2Art = resourcePath.EndsWith("_v2");
            int cell = usesV2Art ? (IsBoss ? 128 : 64) : (IsBoss ? 64 : 32);
            int frameCount = texture == null ? 1 : Mathf.Max(1, texture.width / cell);
            visualProfile = ActorVisualProfile.Enemy(type);
            view = GetComponent<ActorView>();
            if (view == null) view = gameObject.AddComponent<ActorView>();
            view.Initialize(GetComponent<SpriteRenderer>(), resourcePath, new SpriteSheetLayout(cell, cell, frameCount, 1, cell), visualProfile, !IsBoss);
        }

        public void Tick()
        {
            if (Dead || player == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position); Vector2 direction = (player.transform.position - transform.position).normalized;
            switch (type)
            {
                case EnemyType.Wolf: WolfPattern(direction, distance); break;
                case EnemyType.Mushroom: MoveAway(direction, .28f); FireVolley(direction, .78f, 2.35f, 7.5f, 3, 18f, EnemyProjectileStyle.PoisonSpore); break;
                case EnemyType.RockBeetle: MoveToward(direction, .58f); Shoot(direction, 1.3f, 2.7f, 6.5f, EnemyProjectileStyle.Boulder); break;
                case EnemyType.CrystalSpirit: Strafe(direction, .85f); FireVolley(direction, .85f, 1.9f, 9f, 2, 11f, EnemyProjectileStyle.CrystalShard); break;
                case EnemyType.ShieldSkeleton: MoveToward(direction, .7f); Shoot(direction, 1.15f, 2.15f, 5.8f, EnemyProjectileStyle.BoneLance); break;
                case EnemyType.DarkMage: if (distance < 4.5f) MoveAway(direction, .5f); else Strafe(direction, .38f); FireVolley(direction, .84f, 2.2f, 9.5f, 3, 14f, EnemyProjectileStyle.VoidOrb); break;
                case EnemyType.TreeSpirit: BossPattern(direction, 1f, 1.1f, 7f); break;
                case EnemyType.GiantBat: Strafe(direction, 1.05f); BossPattern(direction, 1.2f, 1.5f, 9f); break;
                case EnemyType.Morkan: BossPattern(direction, BossPhase == 2 ? 1.5f : 1f, BossPhase == 2 ? .75f : 1.25f, 10f); break;
            }
            transform.position = ArenaRules.ClampEnemy(transform.position);
            if (view != null) view.Tick(direction, type != EnemyType.Morkan && type != EnemyType.TreeSpirit, Time.time);
            if (distance < (type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan ? 1.2f : .7f)) game.DamagePlayer(type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan ? 1f : .5f);
        }
        private void WolfPattern(Vector2 direction, float distance)
        {
            if (Time.time >= nextAction && distance < 5.2f)
            {
                dashUntil = Time.time + .45f;
                nextAction = Time.time + 1.85f;
                if (view != null) view.PlayAttack();
                game.PlayEffect("Audio/sfx_arrow_fire");
            }
            MoveToward(direction, Time.time < dashUntil ? 4.35f : 1.1f);
        }

        private void MoveToward(Vector2 direction, float speed)
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        private void MoveAway(Vector2 direction, float speed)
        {
            transform.position -= (Vector3)(direction * speed * Time.deltaTime);
        }

        private void Strafe(Vector2 direction, float speed)
        {
            float side = Mathf.Sin(Time.time * .8f + (int)type) >= 0f ? 1f : -1f;
            transform.position += (Vector3)(new Vector2(-direction.y, direction.x) * side * speed * Time.deltaTime);
        }

        private bool FireVolley(Vector2 direction, float damage, float cooldown, float range, int projectileCount, float spreadDegrees, EnemyProjectileStyle style)
        {
            if (Time.time < nextAction || Vector2.Distance(transform.position, player.transform.position) >= range) return false;
            Vector2 finalDirection = hasLockedDirection ? attackDirection : direction;
            for (int index = 0; index < projectileCount; index++)
            {
                float offset = (index - (projectileCount - 1) * .5f) * spreadDegrees;
                Vector2 shotDirection = Quaternion.Euler(0f, 0f, offset) * finalDirection;
                game.SpawnEnemyShot(transform.position, shotDirection, damage, style);
            }
            if (view != null) view.PlayAttack();
            nextAction = Time.time + cooldown;
            game.PlayEffect("Audio/sfx_arrow_fire");
            hasLockedDirection = false;
            return true;
        }

        private void Shoot(Vector2 direction, float damage, float cooldown, float range, EnemyProjectileStyle style = EnemyProjectileStyle.EmberBolt)
        {
            FireVolley(direction, damage, cooldown, range, 1, 0f, style);
        }

        private void BossPattern(Vector2 direction, float damage, float cooldown, float range)
        {
            EnemyProjectileStyle style = type == EnemyType.GiantBat ? EnemyProjectileStyle.SonicCrescent :
                type == EnemyType.Morkan ? EnemyProjectileStyle.ChaosFlame : EnemyProjectileStyle.ThornSeed;
            float pursuitSpeed = type == EnemyType.GiantBat ? 2.15f :
                type == EnemyType.Morkan ? (BossPhase == 2 ? 2.2f : 1.8f) : 1.7f;
            if (game.IsE2EForestBossMode || game.IsE2ECaveBossMode || game.IsE2ECastleBossMode)
            {
                phaseTimer = 3.8f;
                attackDirection = direction;
                hasLockedDirection = true;
                if (view != null) view.IsWarning = true;
                ShowWarningLines(attackDirection, range, true);
                return;
            }

            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (Time.time < bossDashUntil)
            {
                MoveToward(bossDashDirection, pursuitSpeed * 3.25f);
            }
            else
            {
                float engagementDistance = type == EnemyType.Morkan ? 2.85f : Mathf.Min(4.25f, range * .62f);
                if (distance > engagementDistance) MoveToward(direction, pursuitSpeed);
                else if (distance < 1.9f) MoveAway(direction, pursuitSpeed * .24f);
            }

            if (activeBossPattern >= 0)
            {
                if (Time.time < bossPatternExecuteAt)
                {
                    PresentBossPatternWarning(range);
                    return;
                }
                ExecuteBossPattern(damage, cooldown, style);
                return;
            }

            if (Time.time < nextAction || distance >= range)
            {
                float timeToAttack = nextAction - Time.time;
                if (timeToAttack > 0f && timeToAttack <= .5f && distance < range)
                {
                    attackDirection = direction;
                    hasLockedDirection = true;
                    if (view != null) view.IsWarning = true;
                    ShowWarningLines(attackDirection, range, false);
                }
                else ClearBossWarning();
                return;
            }

            BeginBossPattern(direction, distance, range);
        }

        private void BeginBossPattern(Vector2 direction, float distance, float range)
        {
            int choice = UnityEngine.Random.Range(0, 5);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool isRepeat = choice == lastBossPattern;
                bool meleeTooFar = choice == (int)BossAttackPattern.MeleeSweep && distance > 3.35f;
                if (!isRepeat && !meleeTooFar) break;
                choice = (choice + UnityEngine.Random.Range(1, 5)) % 5;
            }
            activeBossPattern = choice;
            lastBossPattern = choice;
            bossPatternDirection = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.left;
            bossPatternTarget = player.transform.position;
            attackDirection = bossPatternDirection;
            hasLockedDirection = true;
            bossPatternExecuteAt = Time.time + (choice == (int)BossAttackPattern.GroundBurst ? .44f : .56f);
            if (view != null) view.IsWarning = true;
            game.PlayEffect("Audio/sfx_boss_warning");
            PresentBossPatternWarning(range);
        }

        private void PresentBossPatternWarning(float range)
        {
            if (activeBossPattern < 0) return;
            BossAttackPattern pattern = (BossAttackPattern)activeBossPattern;
            bool triple = pattern == BossAttackPattern.FanVolley || pattern == BossAttackPattern.RingBurst;
            float warningRange = pattern == BossAttackPattern.MeleeSweep ? 2.45f :
                pattern == BossAttackPattern.Charge ? Mathf.Min(4.4f, range) : range;
            if (view != null) view.IsWarning = true;
            ShowWarningLines(attackDirection, warningRange, triple);
        }

        private void ExecuteBossPattern(float damage, float cooldown, EnemyProjectileStyle style)
        {
            BossAttackPattern pattern = (BossAttackPattern)activeBossPattern;
            switch (pattern)
            {
                case BossAttackPattern.FanVolley: SpawnBossFan(damage, style); break;
                case BossAttackPattern.GroundBurst: SpawnGroundBursts(damage); break;
                case BossAttackPattern.RingBurst: SpawnBossRing(damage, style); break;
                case BossAttackPattern.Charge: StartBossCharge(); break;
                case BossAttackPattern.MeleeSweep: StartBossSweep(damage); break;
            }
            if (view != null) view.PlayAttack();
            game.PlayEffect("Audio/sfx_arrow_fire");
            nextAction = Time.time + cooldown + (pattern == BossAttackPattern.RingBurst || pattern == BossAttackPattern.GroundBurst ? .55f : .24f);
            activeBossPattern = -1;
            ClearBossWarning();
        }

        private void SpawnBossFan(float damage, EnemyProjectileStyle style)
        {
            int count = type == EnemyType.GiantBat ? 3 : type == EnemyType.Morkan && BossPhase == 2 ? 7 : 5;
            float spread = type == EnemyType.GiantBat ? 24f : 17f;
            for (int index = 0; index < count; index++)
            {
                float offset = (index - (count - 1) * .5f) * spread;
                game.SpawnEnemyShot(transform.position, Quaternion.Euler(0f, 0f, offset) * bossPatternDirection, damage, style);
            }
        }

        private void SpawnGroundBursts(float damage)
        {
            Vector2 side = new Vector2(-bossPatternDirection.y, bossPatternDirection.x);
            int count = type == EnemyType.Morkan ? 4 : 3;
            for (int index = 0; index < count; index++)
            {
                float offset = (index - (count - 1) * .5f) * 1.05f;
                Vector3 point = (Vector3)bossPatternTarget + (Vector3)(side * offset);
                if (type == EnemyType.Morkan) point += (Vector3)(bossPatternDirection * .45f);
                game.SpawnBossHazard(type, ArenaRules.ClampEnemy(point), damage * .9f, type == EnemyType.TreeSpirit ? .78f : .66f);
            }
        }

        private void SpawnBossRing(float damage, EnemyProjectileStyle style)
        {
            int count = type == EnemyType.GiantBat ? 10 : 8;
            float startAngle = Mathf.Atan2(bossPatternDirection.y, bossPatternDirection.x) * Mathf.Rad2Deg;
            for (int index = 0; index < count; index++)
            {
                float angle = startAngle + index * (360f / count);
                Vector2 shotDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                game.SpawnEnemyShot(transform.position, shotDirection, damage * .72f, style);
            }
        }

        private void StartBossCharge()
        {
            bossDashDirection = bossPatternDirection;
            bossDashUntil = Time.time + (type == EnemyType.GiantBat ? .55f : .42f);
        }

        private void StartBossSweep(float damage)
        {
            float range = type == EnemyType.Morkan ? 2.6f : 2.2f;
            game.SpawnBossSlash(type, transform.position + (Vector3)(bossPatternDirection * .85f), bossPatternDirection, damage * 1.25f, range);
        }

        private void ClearBossWarning()
        {
            if (view != null) view.IsWarning = false;
            ClearWarningLines();
            hasLockedDirection = false;
        }

        public void TakeDamage(float amount)
        {
            if (Dead) return;
            if (type == EnemyType.ShieldSkeleton && Time.frameCount % 3 == 0) amount *= .35f;
            LastDamagedAt = Time.time;
            health -= amount;
            if (view != null) view.PlayHit(health / maxHealth);
            if (game.GameFeel != null) game.GameFeel.ProjectileImpact(transform.position, visualProfile.AccentColor, IsBoss);
            if (health <= 0f) { Dead = true; game.RewardEnemy(type, transform.position); game.RemoveEnemy(this); if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject); }
        }

        public void SetTargeted(bool targeted) { if (view != null) view.SetTargeted(targeted); }

        public static string ResourcePath(EnemyType enemyType)
        {
            string basePath;
            if (enemyType == EnemyType.Wolf) basePath = "Art/Enemies/Forest/forest_wolf";
            else if (enemyType == EnemyType.Mushroom) basePath = "Art/Enemies/Forest/poison_mushroom";
            else if (enemyType == EnemyType.RockBeetle) basePath = "Art/Enemies/Cave/rock_beetle";
            else if (enemyType == EnemyType.CrystalSpirit) basePath = "Art/Enemies/Cave/crystal_spirit";
            else if (enemyType == EnemyType.ShieldSkeleton) basePath = "Art/Enemies/Castle/shield_skeleton";
            else if (enemyType == EnemyType.DarkMage) basePath = "Art/Enemies/Castle/dark_mage";
            else if (enemyType == EnemyType.TreeSpirit) basePath = "Art/Bosses/Forest/ancient_tree_spirit";
            else if (enemyType == EnemyType.GiantBat) basePath = "Art/Bosses/Cave/giant_bat";
            else basePath = "Art/Bosses/Castle/morkan_boss";

            return Resources.Load<Texture2D>(basePath + "_v2") != null ? basePath + "_v2" : basePath;
        }
    }

    public sealed class ProjectileActor : MonoBehaviour
    {
        private SoulArcherGame game; private Vector2 direction; private bool hostile; private float damage; private int pierce; private int reflect; private float life;
        private EnemyProjectileStyle style;
        private ProjectileView view;
        private readonly HashSet<EnemyActor> hitEnemies = new HashSet<EnemyActor>();
        public void Initialize(SoulArcherGame owner, Vector3 origin, Vector2 travel, bool isHostile, float amount, int pierceCount, int reflectCount = 0, EnemyProjectileStyle projectileStyle = EnemyProjectileStyle.EmberBolt) { game = owner; transform.position = origin; direction = travel.normalized; hostile = isHostile; damage = amount; pierce = pierceCount; reflect = reflectCount; style = projectileStyle; transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg); view = gameObject.AddComponent<ProjectileView>(); view.Initialize(GetComponent<SpriteRenderer>(), hostile, style); }
        public void Tick()
        {
            life += Time.deltaTime; transform.position += (Vector3)(direction * (hostile ? EnemyProjectileRules.SpeedFor(style) : 9f) * Time.deltaTime);
            if (view != null) view.Tick();
            if (!hostile && reflect > 0 && (Mathf.Abs(transform.position.x) > 8.7f || Mathf.Abs(transform.position.y) > 4.2f))
            {
                bool hitX = Mathf.Abs(transform.position.x) > 8.7f;
                bool hitY = Mathf.Abs(transform.position.y) > 4.2f;
                direction = new Vector2(hitX ? -direction.x : direction.x, hitY ? -direction.y : direction.y).normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                transform.position = new Vector3(Mathf.Clamp(transform.position.x, -8.7f, 8.7f), Mathf.Clamp(transform.position.y, -4.2f, 4.2f), transform.position.z);
                reflect--;
                if (game.GameFeel != null) game.GameFeel.ProjectileReflect(transform.position);
            }
            if (life > 3f || Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 6f) { Destroy(gameObject); return; }
            if (hostile)
            {
                var target = FindAnyObjectByType<PlayerActor>();
                if (target && Vector2.Distance(transform.position, target.transform.position) < EnemyProjectileRules.HitRadiusFor(style)) { game.DamagePlayer(damage); Destroy(gameObject); }
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

    public sealed class BossHazardActor : MonoBehaviour
    {
        private SoulArcherGame game;
        private EnemyType boss;
        private float damage;
        private float radius;
        private float elapsed;
        private bool damaged;
        private SpriteRenderer marker;
        private SpriteRenderer eruption;
        private Vector3 eruptionBaseScale;

        public void Initialize(SoulArcherGame owner, EnemyType bossType, float amount, float hazardRadius)
        {
            game = owner;
            boss = bossType;
            damage = amount;
            radius = hazardRadius;
            Color color = ActorVisualProfile.Enemy(boss).AccentColor;

            GameObject markerObject = new GameObject("Hazard Telegraph");
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localScale = Vector3.one * (radius * 2.15f);
            marker = markerObject.AddComponent<SpriteRenderer>();
            marker.sprite = RuntimeSpriteAtlas.Ring();
            marker.color = new Color(color.r, color.g, color.b, .62f);
            marker.sortingOrder = SpriteSorting.Projectile - 3;

            GameObject eruptionObject = new GameObject("Hazard Eruption");
            eruptionObject.transform.SetParent(transform, false);
            eruption = eruptionObject.AddComponent<SpriteRenderer>();
            eruption.sprite = boss == EnemyType.TreeSpirit ? RuntimeSpriteAtlas.RootPillar() :
                boss == EnemyType.GiantBat ? RuntimeSpriteAtlas.Crescent() : RuntimeSpriteAtlas.Flame();
            eruption.color = Color.Lerp(color, Color.white, .25f);
            eruption.sortingOrder = SpriteSorting.Projectile + 1;
            eruption.transform.localScale = boss == EnemyType.TreeSpirit ? new Vector3(radius * 1.85f, radius * 2.3f, 1f) : Vector3.one * (radius * 1.8f);
            eruptionBaseScale = eruption.transform.localScale;
            eruption.enabled = false;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < .52f)
            {
                float pulse = 1f + Mathf.PingPong(elapsed * 5f, .26f);
                marker.transform.localScale = Vector3.one * (radius * 2.15f * pulse);
                return;
            }

            marker.enabled = false;
            eruption.enabled = true;
            float burst = Mathf.Clamp01((elapsed - .52f) / .12f);
            eruption.color = new Color(eruption.color.r, eruption.color.g, eruption.color.b, 1f - Mathf.Max(0f, elapsed - .72f) * 2.2f);
            eruption.transform.localScale = eruptionBaseScale * (1f + burst * .2f);
            if (!damaged)
            {
                PlayerActor target = FindAnyObjectByType<PlayerActor>();
                if (target != null && Vector2.Distance(transform.position, target.transform.position) <= radius)
                {
                    damaged = true;
                    game.DamagePlayer(damage);
                }
            }
            if (elapsed >= .98f) Destroy(gameObject);
        }
    }

    public sealed class BossSlashEffect : MonoBehaviour
    {
        private SoulArcherGame game;
        private Vector2 direction;
        private float damage;
        private float range;
        private float elapsed;
        private bool damaged;
        private SpriteRenderer slash;
        private SpriteRenderer glow;

        public void Initialize(SoulArcherGame owner, EnemyType boss, Vector2 attackDirection, float amount, float attackRange)
        {
            game = owner;
            direction = attackDirection.sqrMagnitude > .001f ? attackDirection.normalized : Vector2.right;
            damage = amount;
            range = attackRange;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Color color = boss == EnemyType.Morkan ? new Color(1f, .74f, .34f) : ActorVisualProfile.Enemy(boss).AccentColor;
            slash = gameObject.AddComponent<SpriteRenderer>();
            slash.sprite = RuntimeSpriteAtlas.Slash();
            slash.color = color;
            slash.sortingOrder = SpriteSorting.Projectile + 3;
            transform.localScale = new Vector3(range * .82f, 1.35f, 1f);

            GameObject glowObject = new GameObject("Slash Glow");
            glowObject.transform.SetParent(transform, false);
            glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = RuntimeSpriteAtlas.Slash();
            glow.color = new Color(color.r, color.g, color.b, .28f);
            glow.sortingOrder = SpriteSorting.Projectile + 2;
            glow.transform.localScale = Vector3.one * 1.32f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / .4f);
            transform.localScale = new Vector3(range * (.65f + progress * .25f), 1.1f + progress * .42f, 1f);
            slash.color = new Color(slash.color.r, slash.color.g, slash.color.b, 1f - progress);
            if (glow != null) glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, (1f - progress) * .3f);
            if (!damaged && elapsed >= .18f)
            {
                PlayerActor target = FindAnyObjectByType<PlayerActor>();
                if (target != null)
                {
                    Vector2 toTarget = target.transform.position - transform.position;
                    if (toTarget.magnitude <= range && Vector2.Dot(direction, toTarget.normalized) >= .15f)
                    {
                        damaged = true;
                        game.DamagePlayer(damage);
                    }
                }
            }
            if (elapsed >= .4f) Destroy(gameObject);
        }
    }
}
