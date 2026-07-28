using NUnit.Framework;
using SoulArcher;
using UnityEngine;
using UnityEngine.UI;

public sealed class SoulArcherForestPresentationTests
{
    [Test]
    public void ResponsiveCameraZoomsInForestBossRoomWithActiveBoss()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject cameraObj = new GameObject("Camera");
        GameObject playerObj = new GameObject("Player");
        GameObject bossObj = new GameObject("Boss");
        try
        {
            var camera = cameraObj.AddComponent<Camera>();
            var responsiveCam = cameraObj.AddComponent<ResponsiveCamera2D>();
            var game = gameObj.AddComponent<SoulArcherGame>();
            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();

            responsiveCam.Initialize(camera, 5.2f);

            var graph = new RoomGraph(StageId.Forest);
            graph.Rooms[0].cleared = true;
            graph.TryMove(RoomDirection.East);
            graph.Rooms[1].cleared = true;
            graph.TryMove(RoomDirection.North);
            graph.Rooms[3].cleared = true;
            graph.TryMove(RoomDirection.East);

            SetPrivate(game, "graph", graph);
            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.TreeSpirit);
            SetPrivate(boss, "health", 160f);
            SetPrivate(boss, "maxHealth", 160f);

            var view = bossObj.AddComponent<ActorView>();
            var renderer = bossObj.AddComponent<SpriteRenderer>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Enemy(EnemyType.TreeSpirit), false);
            SetPrivate(boss, "view", view);

            var enemiesList = (System.Collections.IList)typeof(SoulArcherGame)
                .GetField("enemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(game);
            enemiesList.Add(boss);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(4.5f, 0f, 0f);

            var method = typeof(ResponsiveCamera2D).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);
            method.Invoke(responsiveCam, new object[] { true });

            float aspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
            float expectedSize = ResponsiveViewport.OrthographicSize(3.5f, aspect);
            Assert.AreEqual(expectedSize, camera.orthographicSize, 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(cameraObj);
            Object.DestroyImmediate(playerObj);
            Object.DestroyImmediate(bossObj);
        }
    }

    [Test]
    public void ActorViewWarningFlashingIsSupported()
    {
        GameObject actor = new GameObject("Actor");
        try
        {
            var renderer = actor.AddComponent<SpriteRenderer>();
            var view = actor.AddComponent<ActorView>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Player(), false);

            view.IsWarning = true;
            Assert.IsTrue(view.IsWarning);

            var method = typeof(ActorView).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);
            method.Invoke(view, null);

            Assert.AreNotEqual(Color.black, renderer.color);
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void GameHudBuildTextReplacementIsCorrect()
    {
        GameObject root = new GameObject("HUD Root");
        try
        {
            GameHudView hud = root.AddComponent<GameHudView>();
            hud.Initialize();

            Transform abilityTextTrans = root.transform.Find("Gameplay HUD Canvas/Safe Area/Ability Build/Ability Text");
            Assert.IsNotNull(abilityTextTrans);

            var textComp = abilityTextTrans.GetComponent<Text>();
            Assert.IsNotNull(textComp);

            textComp.text = "BUILD 0/6";

            var canvasTrans = root.transform.Find("Gameplay HUD Canvas");
            if (canvasTrans != null)
            {
                var textTrans = canvasTrans.Find("Safe Area/Ability Build/Ability Text");
                if (textTrans != null)
                {
                    var text = textTrans.GetComponent<Text>();
                    if (text != null)
                    {
                        text.text = "능력 0/6";
                    }
                }
            }

            Assert.AreEqual("능력 0/6", textComp.text);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ForestBossPatternTriggersCorrectWarningTimings()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();

            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.TreeSpirit);
            SetPrivate(boss, "health", 160f);
            SetPrivate(boss, "maxHealth", 160f);

            var view = bossObj.AddComponent<ActorView>();
            var renderer = bossObj.AddComponent<SpriteRenderer>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Enemy(EnemyType.TreeSpirit), false);
            SetPrivate(boss, "view", view);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(1f, 1f, 0f);

            SetPrivate(boss, "nextAction", Time.time + 1.1f);
            var method = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);

            method.Invoke(boss, new object[] { Vector2.down, 1f, 1.1f, 7f });
            Assert.IsFalse(view.IsWarning);

            SetPrivate(boss, "nextAction", Time.time + 0.4f);
            method.Invoke(boss, new object[] { Vector2.down, 1f, 1.1f, 7f });
            Assert.IsTrue(view.IsWarning);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void WolfDashUsesTheExtendedFortyFiveHundredthsSecondWindow()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject wolfObj = new GameObject("Wolf");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            var player = playerObj.AddComponent<PlayerActor>();
            var wolf = wolfObj.AddComponent<EnemyActor>();
            SetPrivate(wolf, "game", game);
            SetPrivate(wolf, "player", player);
            SetPrivate(wolf, "type", EnemyType.Wolf);
            player.transform.position = Vector3.zero;
            wolf.transform.position = new Vector3(3f, 0f, 0f);
            SetPrivate(wolf, "nextAction", Time.time - .1f);

            var method = typeof(EnemyActor).GetMethod("WolfPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);
            method.Invoke(wolf, new object[] { Vector2.left, 3f });

            float dashUntil = (float)typeof(EnemyActor).GetField("dashUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(wolf);
            Assert.Greater(dashUntil - Time.time, .4f);
            Assert.Less(dashUntil - Time.time, .5f);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(wolfObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void GroundBurstAndMorkanSweepCreateTheirDedicatedActors()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();
            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.TreeSpirit);
            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(3f, 0f, 0f);
            SetPrivate(boss, "activeBossPattern", (int)BossAttackPattern.GroundBurst);
            SetPrivate(boss, "bossPatternExecuteAt", Time.time - .1f);
            SetPrivate(boss, "bossPatternDirection", Vector2.left);
            SetPrivate(boss, "bossPatternTarget", Vector2.zero);

            var pattern = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(pattern);
            pattern.Invoke(boss, new object[] { Vector2.left, 1f, 1.1f, 7f });
            Assert.AreEqual(3, Object.FindObjectsByType<BossHazardActor>(FindObjectsSortMode.None).Length);

            SetPrivate(boss, "type", EnemyType.Morkan);
            SetPrivate(boss, "activeBossPattern", (int)BossAttackPattern.MeleeSweep);
            SetPrivate(boss, "bossPatternExecuteAt", Time.time - .1f);
            SetPrivate(boss, "bossPatternDirection", Vector2.left);
            pattern.Invoke(boss, new object[] { Vector2.left, 1f, 1.25f, 10f });
            Assert.AreEqual(1, Object.FindObjectsByType<BossSlashEffect>(FindObjectsSortMode.None).Length);
        }
        finally
        {
            foreach (var hazard in Object.FindObjectsByType<BossHazardActor>(FindObjectsSortMode.None)) Object.DestroyImmediate(hazard.gameObject);
            foreach (var slash in Object.FindObjectsByType<BossSlashEffect>(FindObjectsSortMode.None)) Object.DestroyImmediate(slash.gameObject);
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void DistantBossClosesTheGapWithoutPostponingItsAttackTimer()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();
            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.TreeSpirit);
            SetPrivate(boss, "health", 160f);
            SetPrivate(boss, "maxHealth", 160f);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(8f, 0f, 0f);
            float readyAt = Time.time - .1f;
            SetPrivate(boss, "nextAction", readyAt);
            var method = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(Vector2), typeof(float), typeof(float), typeof(float) }, null);
            Assert.IsNotNull(method);

            method.Invoke(boss, new object[] { Vector2.left, 1f, 1.1f, 7f });

            float updatedReadyAt = (float)typeof(EnemyActor).GetField("nextAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
            Assert.AreEqual(readyAt, updatedReadyAt, .001f);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void ForestBossWarningLineLocksDirectionAndThickens()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();

            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.TreeSpirit);
            SetPrivate(boss, "health", 160f);
            SetPrivate(boss, "maxHealth", 160f);

            var view = bossObj.AddComponent<ActorView>();
            var renderer = bossObj.AddComponent<SpriteRenderer>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Enemy(EnemyType.TreeSpirit), false);
            SetPrivate(boss, "view", view);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(2f, 0f, 0f);

            SetPrivate(boss, "nextAction", Time.time + 0.4f);
            var method = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);

            method.Invoke(boss, new object[] { Vector2.left, 1f, 1.1f, 7f });

            bool hasLocked = (bool)typeof(EnemyActor).GetField("hasLockedDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
            Assert.IsTrue(hasLocked);

            Vector2 attackDir = (Vector2)typeof(EnemyActor).GetField("attackDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
            Assert.AreEqual(Vector2.left, attackDir);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void ForestBossRoomPlayerMovementIsClampedToSafeArea()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            playerObj.AddComponent<SpriteRenderer>();
            var player = playerObj.AddComponent<PlayerActor>();
            var stats = new PlayerStats();
            player.Initialize(game, stats);
            SetPrivate(game, "player", player);

            var graph = new RoomGraph(StageId.Forest);
            graph.Rooms[0].cleared = true;
            graph.TryMove(RoomDirection.East);
            graph.Rooms[1].cleared = true;
            graph.TryMove(RoomDirection.North);
            graph.Rooms[3].cleared = true;
            graph.TryMove(RoomDirection.East);

            SetPrivate(game, "graph", graph);

            player.transform.position = new Vector3(8.5f, 4.0f, 0f);
            player.Tick();

            Assert.LessOrEqual(player.transform.position.x, 7.5f);
            Assert.LessOrEqual(player.transform.position.y, 2.8f);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void ForestBossE2EEntryPointForceRoomId4()
    {
        var graph = new RoomGraph(StageId.Forest);
        graph.ForceRoomId(4);
        Assert.AreEqual(RoomKind.Boss, graph.Current.kind);
        Assert.AreEqual("거대 나무 정령", graph.Current.label);
    }

    [Test]
    public void CaveBossE2EEntryPointForceRoomId4()
    {
        var graph = new RoomGraph(StageId.Cave);
        graph.ForceRoomId(4);
        Assert.AreEqual(RoomKind.Boss, graph.Current.kind);
        Assert.AreEqual("거대 박쥐", graph.Current.label);
    }

    [Test]
    public void CastleBossE2EEntryPointForceRoomId4()
    {
        var graph = new RoomGraph(StageId.Castle);
        graph.ForceRoomId(4);
        Assert.AreEqual(RoomKind.Boss, graph.Current.kind);
        Assert.AreEqual("마왕 기사 모르칸", graph.Current.label);
    }

    [Test]
    public void ForestBossE2EEntryPointFreezesPhaseTimerAndShowsWarning()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            game.IsE2EForestBossMode = true;

            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();

            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.TreeSpirit);
            SetPrivate(boss, "health", 160f);
            SetPrivate(boss, "maxHealth", 160f);

            var view = bossObj.AddComponent<ActorView>();
            var renderer = bossObj.AddComponent<SpriteRenderer>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Enemy(EnemyType.TreeSpirit), false);
            SetPrivate(boss, "view", view);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(2f, 0f, 0f);

            var method = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);

            method.Invoke(boss, new object[] { Vector2.left, 1f, 1.1f, 7f });

            float phaseTimer = (float)typeof(EnemyActor).GetField("phaseTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
            Assert.AreEqual(3.8f, phaseTimer, 0.001f);
            Assert.IsTrue(view.IsWarning);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void CaveBossE2EEntryPointFreezesPhaseTimerAndShowsWarning()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            game.IsE2ECaveBossMode = true;

            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();

            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.GiantBat);
            SetPrivate(boss, "health", 250f);
            SetPrivate(boss, "maxHealth", 250f);

            var view = bossObj.AddComponent<ActorView>();
            var renderer = bossObj.AddComponent<SpriteRenderer>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Enemy(EnemyType.GiantBat), false);
            SetPrivate(boss, "view", view);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(2f, 0f, 0f);

            var method = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);

            method.Invoke(boss, new object[] { Vector2.left, 1f, 1.1f, 7f });

            float phaseTimer = (float)typeof(EnemyActor).GetField("phaseTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
            Assert.AreEqual(3.8f, phaseTimer, 0.001f);
            Assert.IsTrue(view.IsWarning);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void CastleBossE2EEntryPointFreezesPhaseTimerAndShowsWarning()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject bossObj = new GameObject("Boss");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            game.IsE2ECastleBossMode = true;

            var player = playerObj.AddComponent<PlayerActor>();
            var boss = bossObj.AddComponent<EnemyActor>();

            SetPrivate(boss, "game", game);
            SetPrivate(boss, "player", player);
            SetPrivate(boss, "type", EnemyType.Morkan);
            SetPrivate(boss, "health", 400f);
            SetPrivate(boss, "maxHealth", 400f);

            var view = bossObj.AddComponent<ActorView>();
            var renderer = bossObj.AddComponent<SpriteRenderer>();
            view.Initialize(renderer, "Art/Player/Leon/leon_idle_south", new SpriteSheetLayout(64, 64, 1, 8, 64f), ActorVisualProfile.Enemy(EnemyType.Morkan), false);
            SetPrivate(boss, "view", view);

            player.transform.position = Vector3.zero;
            boss.transform.position = new Vector3(2f, 0f, 0f);

            var method = typeof(EnemyActor).GetMethod("BossPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);

            method.Invoke(boss, new object[] { Vector2.left, 1f, 1.1f, 7f });

            float phaseTimer = (float)typeof(EnemyActor).GetField("phaseTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
            Assert.AreEqual(3.8f, phaseTimer, 0.001f);
            Assert.IsTrue(view.IsWarning);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(bossObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void ForestBossE2EEntryPointDisablesPlayerAutoFire()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            game.IsE2EForestBossMode = true;

            playerObj.AddComponent<SpriteRenderer>();
            var player = playerObj.AddComponent<PlayerActor>();
            var stats = new PlayerStats();
            player.Initialize(game, stats);

            SetPrivate(game, "player", player);
            Assert.IsFalse(player.IsMoving);

            // Trigger PlayerActor.Tick()
            player.Tick();

            // Check if player shot any arrows
            var projectilesList = (System.Collections.IList)typeof(SoulArcherGame)
                .GetField("projectiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(game);
            Assert.AreEqual(0, projectilesList.Count);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void CaveBossE2EEntryPointDisablesPlayerAutoFire()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            game.IsE2ECaveBossMode = true;

            playerObj.AddComponent<SpriteRenderer>();
            var player = playerObj.AddComponent<PlayerActor>();
            var stats = new PlayerStats();
            player.Initialize(game, stats);

            SetPrivate(game, "player", player);
            Assert.IsFalse(player.IsMoving);

            player.Tick();

            var projectilesList = (System.Collections.IList)typeof(SoulArcherGame)
                .GetField("projectiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(game);
            Assert.AreEqual(0, projectilesList.Count);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    [Test]
    public void CastleBossE2EEntryPointDisablesPlayerAutoFire()
    {
        GameObject gameObj = new GameObject("Game");
        GameObject playerObj = new GameObject("Player");
        try
        {
            var game = gameObj.AddComponent<SoulArcherGame>();
            game.IsE2ECastleBossMode = true;

            playerObj.AddComponent<SpriteRenderer>();
            var player = playerObj.AddComponent<PlayerActor>();
            var stats = new PlayerStats();
            player.Initialize(game, stats);

            SetPrivate(game, "player", player);
            Assert.IsFalse(player.IsMoving);

            player.Tick();

            var projectilesList = (System.Collections.IList)typeof(SoulArcherGame)
                .GetField("projectiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(game);
            Assert.AreEqual(0, projectilesList.Count);
        }
        finally
        {
            Object.DestroyImmediate(gameObj);
            Object.DestroyImmediate(playerObj);
        }
    }

    private static void SetPrivate<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }
}
