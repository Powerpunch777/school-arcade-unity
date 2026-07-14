using NUnit.Framework;
using SoulArcher;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SoulArcherModelsTests
{
    [Test] public void DamageStartsInvincibilityAndDeath() { var s = new PlayerStats(); Assert.IsTrue(s.TakeDamage(1f, 0f)); Assert.IsFalse(s.TakeDamage(1f, .2f)); Assert.AreEqual(4f, s.hearts); Assert.IsTrue(s.TakeDamage(4f, 1f)); Assert.IsTrue(s.dead); }
    [Test] public void RoomStateTracksVisitClearAndRevisit() { var g = new RoomGraph(); Assert.IsTrue(g.TryMove(1)); Assert.IsTrue(g.Rooms[1].visited); g.ClearCurrent(); Assert.IsTrue(g.Current.cleared); Assert.IsTrue(g.TryMove(0)); Assert.IsTrue(g.TryMove(1)); }
    [Test] public void AbilitySelectionChangesStats() { var s = new PlayerStats(); AbilityRules.Apply(s, Ability.Attack); AbilityRules.Apply(s, Ability.Pierce); Assert.AreEqual(12f, s.attack); Assert.AreEqual(1, s.pierce); }
    [Test] public void SoulsSupportShopEconomy() { var s = new PlayerStats { souls = 15 }; Assert.GreaterOrEqual(s.souls, 15); s.souls -= 15; s.shieldCharges++; Assert.AreEqual(0, s.souls); Assert.AreEqual(1, s.shieldCharges); }
    [Test] public void RestartRunResetsStateAndRemovesSpawnedActors() {
        Camera initialCamera = Camera.main;
        var root = new GameObject("CMP-206 Restart Fixture");
        var game = root.AddComponent<SoulArcherGame>();
        try
        {
            game.Start();
            for (int i = 0; i < 10; i++) game.RewardEnemy(EnemyType.Wolf);
            Assert.IsTrue(game.TryPurchaseShopItem(0));
            AbilityRules.Apply(game.CurrentStats, Ability.Attack);
            AbilityRules.Apply(game.CurrentStats, Ability.Pierce);
            game.DamagePlayer(1f);
            game.CurrentGraph.TryMove(1);
            game.CurrentGraph.ClearCurrent();

            EnemyActor[] oldEnemies = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            game.SpawnEnemyShot(Vector3.zero, Vector2.right, 1f);
            ProjectileActor[] oldProjectiles = Object.FindObjectsByType<ProjectileActor>(FindObjectsSortMode.None);
            Assert.Greater(oldEnemies.Length, 0);
            Assert.Greater(oldProjectiles.Length, 0);

            game.RestartRun();

            Assert.AreEqual(0, game.CurrentStats.souls);
            Assert.AreEqual(PlayerStats.MaxHearts, game.CurrentStats.hearts);
            Assert.AreEqual(10f, game.CurrentStats.attack);
            Assert.AreEqual(0, game.CurrentStats.pierce);
            Assert.IsFalse(game.IsShopItemPurchased(0));
            Assert.AreEqual(0, game.CurrentGraph.CurrentId);
            Assert.IsFalse(game.CurrentGraph.Rooms[0].cleared);
            Assert.IsFalse(game.CurrentGraph.Rooms[1].visited);
            Assert.AreEqual(3, game.ActiveEnemyCount);
            Assert.AreEqual(0, game.ActiveProjectileCount);
            Assert.IsTrue(oldEnemies.All(enemy => enemy == null));
            Assert.IsTrue(oldProjectiles.All(projectile => projectile == null));

            for (int i = 0; i < 10; i++) game.RewardEnemy(EnemyType.Wolf);
            Assert.IsTrue(game.TryPurchaseShopItem(0));
            Assert.IsTrue(game.IsShopItemPurchased(0));
        }
        finally
        {
            Object.DestroyImmediate(root);
            if (initialCamera == null && Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);
        }
    }
    [Test] public void MovingPlayerCannotFireNewArrow() { Assert.IsFalse(AutoAimRules.CanFire(true, true, 2f, 1f)); }
    [Test] public void PlayerCanReachBothRoomExits() {
        Assert.IsTrue(RoomExitRules.CanMoveThroughExit(RoomExitRules.PlayerMaxX, 0, 5, true));
        Assert.AreEqual(1, RoomExitRules.DestinationFor(RoomExitRules.PlayerMaxX, 0));
        Assert.IsTrue(RoomExitRules.CanMoveThroughExit(RoomExitRules.PlayerMinX, 1, 5, true));
        Assert.AreEqual(0, RoomExitRules.DestinationFor(RoomExitRules.PlayerMinX, 1));
        Assert.IsFalse(RoomExitRules.CanMoveThroughExit(RoomExitRules.PlayerMaxX, 0, 5, false));
    }
    [Test] public void EnemyRewardsDifferForRegularEnemiesAndBoss() {
        Assert.AreEqual(1, SoulRewardRules.For(EnemyType.Wolf));
        Assert.AreEqual(1, SoulRewardRules.For(EnemyType.Mushroom));
        Assert.AreEqual(2, SoulRewardRules.CombatRoomClearBonus);
        Assert.AreEqual(5, SoulRewardRules.For(EnemyType.TreeSpirit));
        Assert.Greater(SoulRewardRules.For(EnemyType.TreeSpirit), SoulRewardRules.For(EnemyType.Wolf));
    }
    [Test] public void ForestCombatRewardsAllowFirstShopPurchase() {
        int soulsBeforeShop = 3 * SoulRewardRules.For(EnemyType.Wolf) + SoulRewardRules.CombatRoomClearBonus
            + 4 * SoulRewardRules.For(EnemyType.Wolf) + SoulRewardRules.CombatRoomClearBonus;
        Assert.GreaterOrEqual(soulsBeforeShop, ShopRules.Prices[0]);
    }
    [Test] public void DisplayDefaultsMatchIntegerScaledFhdDesign() {
        Assert.AreEqual(640, DisplaySettings.InternalWidth);
        Assert.AreEqual(360, DisplaySettings.InternalHeight);
        Assert.AreEqual(1920, DisplaySettings.DefaultOutputWidth);
        Assert.AreEqual(1080, DisplaySettings.DefaultOutputHeight);
        Assert.AreEqual(FullScreenMode.FullScreenWindow, DisplaySettings.DefaultFullscreenMode);
    }
    [Test] public void ShopPurchaseSpendsSoulsAndAppliesItemOnce() {
        var s = new PlayerStats { souls = 30, hearts = 3f };
        var bought = new bool[3];
        Assert.IsTrue(ShopRules.TryPurchase(s, 0, bought));
        Assert.AreEqual(20, s.souls);
        Assert.AreEqual(4.5f, s.hearts);
        Assert.IsFalse(ShopRules.TryPurchase(s, 0, bought));
        Assert.IsTrue(ShopRules.TryPurchase(s, 1, bought));
        Assert.AreEqual(1, s.shieldCharges);
    }
    [Test] public void SortingOrderKeepsActorsAboveRoomAndProjectilesAboveActors() {
        Assert.Less(SpriteSorting.Background, SpriteSorting.WallAndProps);
        Assert.Less(SpriteSorting.WallAndProps, SpriteSorting.Actor);
        Assert.Less(SpriteSorting.Actor, SpriteSorting.Projectile);
    }
    [Test] public void StoppedPlayerSelectsNearestLivingVisibleEnemy() {
        var targets = new List<AutoAimTarget> {
            new AutoAimTarget(10, new Vector2(4f, 0f), true, true),
            new AutoAimTarget(11, new Vector2(1f, 0f), true, true),
            new AutoAimTarget(12, new Vector2(.5f, 0f), false, true),
            new AutoAimTarget(13, new Vector2(.25f, 0f), true, false)
        };
        int selected = AutoAimRules.SelectNearestVisibleLivingTarget(Vector2.zero, targets);
        Assert.AreEqual(11, targets[selected].id);
        Assert.IsTrue(AutoAimRules.CanFire(false, selected >= 0, 2f, 1f));
    }

    [Test] public void CampaignHasThreeStagesAndFiveRequiredRoomsEach() {
        foreach (StageId stage in StageRules.Order) {
            var graph = new RoomGraph(stage);
            Assert.AreEqual(5, graph.Rooms.Count);
            Assert.AreEqual(3, graph.Rooms.Count(r => r.kind == RoomKind.Combat));
            Assert.AreEqual(1, graph.Rooms.Count(r => r.kind == RoomKind.Shop));
            Assert.AreEqual(1, graph.Rooms.Count(r => r.kind == RoomKind.Boss));
            Assert.AreEqual(stage, graph.Current.stage);
        }
        Assert.AreEqual(6, StageRules.Enemies(StageId.Forest).Length + StageRules.Enemies(StageId.Cave).Length + StageRules.Enemies(StageId.Castle).Length);
    }

    [Test] public void AllSixRegularEnemiesAndThreeBossesAreDistinct() {
        Assert.AreEqual(6, new[] { EnemyType.Wolf, EnemyType.Mushroom, EnemyType.RockBeetle, EnemyType.CrystalSpirit, EnemyType.ShieldSkeleton, EnemyType.DarkMage }.Distinct().Count());
        Assert.AreEqual(0, SoulRewardRules.For(EnemyType.Morkan));
        Assert.AreEqual(5, SoulRewardRules.For(EnemyType.GiantBat));
    }

    [Test] public void EasyDifficultyReducesDamageButNeverBelowHalfHeart() {
        Assert.AreEqual(0.5f, DifficultyRules.Damage(1f, Difficulty.Easy));
        Assert.AreEqual(1.5f, DifficultyRules.Damage(2f, Difficulty.Easy));
        var easy = new PlayerStats();
        Assert.IsTrue(easy.TakeDamage(2f, 0f, Difficulty.Easy));
        Assert.AreEqual(3.5f, easy.hearts);
    }

    [Test] public void AbilityCardsAreUniqueAndExcludeThreeStackAbilities() {
        var stats = new PlayerStats();
        AbilityRules.Apply(stats, Ability.Attack);
        AbilityRules.Apply(stats, Ability.Attack);
        AbilityRules.Apply(stats, Ability.Attack);
        var cards = AbilityRules.CreateCards(stats, 7);
        Assert.IsFalse(cards.Contains(Ability.Attack));
        Assert.AreEqual(cards.Count, cards.Distinct().Count());
        Assert.LessOrEqual(cards.Count, 3);
    }

    [Test] public void RunHistoryKeepsOnlyRecentTwentyNonPersonalRecords() {
        RunHistory.Clear();
        for (int i = 0; i < 21; i++) RunHistory.Save(new RunRecord { victory = i % 2 == 0, seconds = i, reachedStage = 1, reachedRoom = 1, abilities = "공격" });
        var records = RunHistory.Load().records;
        Assert.AreEqual(20, records.Count);
        Assert.AreEqual(10, RunHistory.Wins());
        Assert.AreEqual("공격", RunHistory.MostSelectedAbility());
        RunHistory.Clear();
    }
}
