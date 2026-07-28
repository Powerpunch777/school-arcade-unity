using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SoulArcher;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Object = UnityEngine.Object;

public class SoulArcherPresentationTests
{
    [Test]
    public void DirectionalFramesPreserveEightDirectionSheetOrder()
    {
        Assert.AreEqual(0, DirectionalFrames.DirectionIndex(Vector2.down));
        Assert.AreEqual(1, DirectionalFrames.DirectionIndex(new Vector2(1f, -1f)));
        Assert.AreEqual(2, DirectionalFrames.DirectionIndex(Vector2.right));
        Assert.AreEqual(4, DirectionalFrames.DirectionIndex(Vector2.up));
        Assert.AreEqual(6, DirectionalFrames.DirectionIndex(Vector2.left));
        Assert.AreEqual(7, DirectionalFrames.DirectionIndex(new Vector2(-1f, -1f)));
    }

    [Test]
    public void SpriteSheetLayoutWrapsAnimationFramesWithinDirection()
    {
        var layout = new SpriteSheetLayout(64, 64, 6, 8, 64f);
        Assert.AreEqual(0, layout.FrameIndex(0, 0));
        Assert.AreEqual(5, layout.FrameIndex(0, 5));
        Assert.AreEqual(6, layout.FrameIndex(1, 6));
        Assert.AreEqual(47, layout.FrameIndex(7, -1));
    }

    [TestCase(EnemyType.Wolf, "Art/Enemies/Forest/forest_wolf_v2", 64)]
    [TestCase(EnemyType.Mushroom, "Art/Enemies/Forest/poison_mushroom_v2", 64)]
    [TestCase(EnemyType.RockBeetle, "Art/Enemies/Cave/rock_beetle_v2", 64)]
    [TestCase(EnemyType.CrystalSpirit, "Art/Enemies/Cave/crystal_spirit_v2", 64)]
    [TestCase(EnemyType.ShieldSkeleton, "Art/Enemies/Castle/shield_skeleton_v2", 64)]
    [TestCase(EnemyType.DarkMage, "Art/Enemies/Castle/dark_mage_v2", 64)]
    [TestCase(EnemyType.TreeSpirit, "Art/Bosses/Forest/ancient_tree_spirit_v2", 128)]
    [TestCase(EnemyType.GiantBat, "Art/Bosses/Cave/giant_bat_v2", 128)]
    [TestCase(EnemyType.Morkan, "Art/Bosses/Castle/morkan_boss_v2", 128)]
    public void EnemyPresentationPrefersV2PixelArt(EnemyType type, string expectedPath, int expectedCellSize)
    {
        string path = EnemyActor.ResourcePath(type);
        Texture2D texture = Resources.Load<Texture2D>(path);

        Assert.AreEqual(expectedPath, path);
        Assert.IsNotNull(texture);
        Assert.AreEqual(expectedCellSize, texture.width);
        Assert.AreEqual(expectedCellSize, texture.height);
    }

    [Test]
    public void ResponsiveViewportShowsWholeArenaOnNarrowScreens()
    {
        Assert.AreEqual(5.2f, ResponsiveViewport.OrthographicSize(5.2f, 16f / 9f), .001f);
        Assert.Greater(ResponsiveViewport.OrthographicSize(5.2f, 4f / 3f), 5.2f);
        Assert.AreEqual(5.2f, ResponsiveViewport.OrthographicSize(5.2f, 21f / 9f), .001f);
    }

    [Test]
    public void ProceduralPresentationSpritesAreCachedAndPixelFiltered()
    {
        Sprite first = RuntimeSpriteAtlas.Heart();
        Sprite second = RuntimeSpriteAtlas.Heart();
        Assert.AreSame(first, second);
        Assert.AreEqual(FilterMode.Point, first.texture.filterMode);
        Assert.Greater(first.rect.width, 1f);
    }

    [Test]
    public void WolfUsesACloserContactShadowThanOtherRegularEnemies()
    {
        ActorVisualProfile wolf = ActorVisualProfile.Enemy(EnemyType.Wolf);
        ActorVisualProfile mushroom = ActorVisualProfile.Enemy(EnemyType.Mushroom);

        Assert.Greater(wolf.ShadowOffset.y, mushroom.ShadowOffset.y);
        Assert.Less(wolf.ShadowScale.y, mushroom.ShadowScale.y);
    }

    [Test]
    public void EnemyProjectileSpritesAreDistinctAndPixelFiltered()
    {
        Sprite[] sprites =
        {
            RuntimeSpriteAtlas.Spore(), RuntimeSpriteAtlas.Boulder(), RuntimeSpriteAtlas.CrystalShard(),
            RuntimeSpriteAtlas.Lance(), RuntimeSpriteAtlas.Orb(), RuntimeSpriteAtlas.Thorn(),
            RuntimeSpriteAtlas.Crescent(), RuntimeSpriteAtlas.Flame()
        };

        Assert.AreEqual(sprites.Length, new HashSet<Sprite>(sprites).Count);
        foreach (Sprite sprite in sprites) Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
    }

    [Test]
    public void BossTelegraphSpritesAreCachedAndPixelFiltered()
    {
        Sprite root = RuntimeSpriteAtlas.RootPillar();
        Sprite slash = RuntimeSpriteAtlas.Slash();

        Assert.AreSame(root, RuntimeSpriteAtlas.RootPillar());
        Assert.AreSame(slash, RuntimeSpriteAtlas.Slash());
        Assert.AreEqual(FilterMode.Point, root.texture.filterMode);
        Assert.AreEqual(FilterMode.Point, slash.texture.filterMode);
    }

    [Test]
    public void ActorViewBuildsReplaceableLayeredPresentation()
    {
        GameObject actor = new GameObject("Presentation Fixture");
        try
        {
            SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteAtlas.Solid();
            ActorView view = actor.AddComponent<ActorView>();
            view.Initialize(renderer, "Missing/Test/Frames", new SpriteSheetLayout(32, 32, 2, 8, 32f), ActorVisualProfile.Player(), true);
            view.Tick(Vector2.right, true, 1f);
            view.PlayHit(.5f);
            Assert.IsNotNull(actor.transform.Find("Ink Outline"));
            Assert.IsNotNull(actor.transform.Find("Contact Shadow"));
            Assert.IsNotNull(actor.transform.Find("Target Ring"));
            Assert.IsNotNull(actor.transform.Find("Health Fill"));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PlayerPresentationSeparatesIdleAndMovingLeonSources()
    {
        GameObject actor = new GameObject("Leon Presentation Fixture");
        try
        {
            SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
            ActorView view = actor.AddComponent<ActorView>();
            view.Initialize(
                renderer,
                "Art/Player/Leon/leon_idle",
                new SpriteSheetLayout(64, 64, 1, 8, 64f),
                "Art/Player/Leon/leon_walk",
                new SpriteSheetLayout(66, 66, 6, 8, 66f),
                ActorVisualProfile.Player(),
                false);
            Assert.AreEqual(8, view.IdleFrameCount);
            Assert.AreEqual(48, view.MovingFrameCount);
            view.Tick(Vector2.down, false, 0f);
            Assert.IsFalse(view.UsingMovingFrames);
            StringAssert.Contains("leon_idle_south", renderer.sprite.name);
            view.Tick(Vector2.down, true, .25f);
            Assert.IsTrue(view.UsingMovingFrames);
            StringAssert.Contains("leon_walk_south", renderer.sprite.name);
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void GameplayHudUsesCanvasScalerAtInternalResolution()
    {
        GameObject root = new GameObject("HUD Fixture");
        try
        {
            GameHudView hud = root.AddComponent<GameHudView>();
            hud.Initialize();
            CanvasScaler scaler = hud.Canvas.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(DisplaySettings.InternalWidth, DisplaySettings.InternalHeight), scaler.referenceResolution);
            Assert.IsNotNull(hud.Canvas.transform.Find("Safe Area/Vitals"));
            Assert.IsNotNull(hud.Canvas.transform.Find("Safe Area/Minimap"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CompactHudPanelsFitThe640By360ReferenceFrame()
    {
        GameObject root = new GameObject("Compact HUD Bounds Fixture");
        try
        {
            GameHudView hud = root.AddComponent<GameHudView>();
            hud.Initialize();
            RectTransform canvasRect = hud.Canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(640f, 360f);
            hud.Canvas.GetComponent<CanvasScaler>().enabled = false;
            Canvas.ForceUpdateCanvases();
            Transform safe = hud.Canvas.transform.Find("Safe Area");
            Assert.LessOrEqual(safe.Find("Vitals").GetComponent<RectTransform>().sizeDelta.y, 36f);
            Assert.LessOrEqual(safe.Find("Stage Header").GetComponent<RectTransform>().sizeDelta.y, 30f);
            Assert.LessOrEqual(safe.Find("Minimap").GetComponent<RectTransform>().sizeDelta.x, 96f);
            Assert.LessOrEqual(safe.Find("Minimap").GetComponent<RectTransform>().sizeDelta.y, 76f);
            Assert.LessOrEqual(safe.Find("Minimap/Map Content").GetComponent<RectTransform>().sizeDelta.x, 80f);
            Assert.LessOrEqual(safe.Find("Minimap/Map Content").GetComponent<RectTransform>().sizeDelta.y, 64f);
            Rect minimapBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, safe.Find("Minimap").GetComponent<RectTransform>()));
            Rect mapContentBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, safe.Find("Minimap/Map Content").GetComponent<RectTransform>()));
            Assert.GreaterOrEqual(mapContentBounds.xMin, minimapBounds.xMin);
            Assert.LessOrEqual(mapContentBounds.xMax, minimapBounds.xMax);
            Assert.GreaterOrEqual(mapContentBounds.yMin, minimapBounds.yMin);
            Assert.LessOrEqual(mapContentBounds.yMax, minimapBounds.yMax);
            Assert.LessOrEqual(safe.Find("Ability Build").GetComponent<RectTransform>().sizeDelta.x, 230f);
            Assert.LessOrEqual(safe.Find("Ability Build").GetComponent<RectTransform>().sizeDelta.y, 22f);
            Assert.Less(safe.Find("Boss Bar").GetComponent<RectTransform>().sizeDelta.x, 640f);
            RectTransform[] persistent = { safe.Find("Vitals").GetComponent<RectTransform>(), safe.Find("Stage Header").GetComponent<RectTransform>(), safe.Find("Minimap").GetComponent<RectTransform>(), safe.Find("Ability Build").GetComponent<RectTransform>(), safe.Find("Boss Bar").GetComponent<RectTransform>() };
            for (int i = 0; i < persistent.Length; i++)
            {
                Rect bounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, persistent[i]));
                Assert.GreaterOrEqual(bounds.xMin, 0f, persistent[i].name + " crops west " + bounds);
                Assert.LessOrEqual(bounds.xMax, 640f, persistent[i].name + " crops east");
                Assert.GreaterOrEqual(bounds.yMin, 0f, persistent[i].name + " crops south");
                Assert.LessOrEqual(bounds.yMax, 360f, persistent[i].name + " crops north");
                for (int j = i + 1; j < persistent.Length; j++)
                    Assert.IsFalse(bounds.Overlaps(ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, persistent[j]))), persistent[i].name + " overlaps " + persistent[j].name);
            }
            Rect centralGameplay = new Rect(120f, 54f, 400f, 252f);
            foreach (RectTransform panel in persistent)
                Assert.IsFalse(centralGameplay.Overlaps(ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, panel))), panel.name + " enters gameplay exclusion");
            Rect bossBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, safe.Find("Boss Bar").GetComponent<RectTransform>()));
            Assert.LessOrEqual(bossBounds.width, 292f);
            Assert.IsFalse(centralGameplay.Overlaps(bossBounds), "Boss Bar enters gameplay exclusion");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void AbilityBuildUsesSixBoundedIconSlotsAndCompactCountsAtMaximum()
    {
        GameObject root = new GameObject("Ability Build Maximum Fixture");
        try
        {
            GameHudView hud = root.AddComponent<GameHudView>();
            RoomGraph graph = new RoomGraph(StageId.Castle);
            var selected = new List<Ability> { Ability.Attack, Ability.Speed, Ability.Pierce, Ability.Reflect, Ability.Heal, Ability.Move };
            hud.Refresh(new PlayerStats(), graph, selected, 6, new List<EnemyActor>(), null, "", ScreenState.Play, null);
            RectTransform canvasRect = hud.Canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(640f, 360f);
            hud.Canvas.GetComponent<CanvasScaler>().enabled = false;
            Canvas.ForceUpdateCanvases();

            Transform panel = hud.Canvas.transform.Find("Safe Area/Ability Build");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Rect panelBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, panelRect));
            Assert.AreEqual(new Vector2(230f, 22f), panelRect.sizeDelta);
            Rect previous = new Rect();
            for (int i = 0; i < 6; i++)
            {
                RectTransform slot = panel.Find("Ability Slot " + i).GetComponent<RectTransform>();
                Rect slotBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, slot));
                Assert.AreEqual(new Vector2(16f, 16f), slot.sizeDelta);
                Assert.IsTrue(panelBounds.Contains(new Vector2(slotBounds.xMin, slotBounds.yMin)) && panelBounds.Contains(new Vector2(slotBounds.xMax, slotBounds.yMax)), "slot " + i + " escapes Ability Build");
                if (i > 0) Assert.IsFalse(previous.Overlaps(slotBounds), "ability slots overlap");
                Assert.IsNotNull(slot.GetComponent<Image>().sprite, "ability icon " + i + " missing");
                Text badge = slot.Find("Ability Count " + i).GetComponent<Text>();
                Assert.IsFalse(badge.gameObject.activeSelf, "single-choice slot must not show a stack badge " + i);
                Assert.LessOrEqual(badge.preferredWidth, badge.rectTransform.rect.width);
                previous = slotBounds;
            }

            foreach (Text text in panel.GetComponentsInChildren<Text>(true))
            {
                Assert.AreNotEqual(HorizontalWrapMode.Overflow, text.horizontalOverflow, text.name + " may overflow");
                Rect textBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, text.rectTransform));
                Assert.IsTrue(panelBounds.Contains(new Vector2(textBounds.xMin, textBounds.yMin)) && panelBounds.Contains(new Vector2(textBounds.xMax, textBounds.yMax)), text.name + " escapes Ability Build");
                Assert.LessOrEqual(text.preferredWidth, text.rectTransform.rect.width + .01f, text.name + " preferred width escapes rect");
            }
            selected = new List<Ability> { Ability.Attack, Ability.Attack, Ability.Speed, Ability.Pierce, Ability.Heal, Ability.Move };
            hud.Refresh(new PlayerStats(), graph, selected, 6, new List<EnemyActor>(), null, "", ScreenState.Play, null);
            Assert.IsTrue(panel.Find("Ability Slot 0/Ability Count 0").gameObject.activeSelf, "stack badge missing in valid 6-choice build");
            Assert.AreEqual("2", panel.Find("Ability Slot 0/Ability Count 0").GetComponent<Text>().text);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void HudBannerExpiresWithoutRefreshExtensionAndStageTransitionKeepsIt()
    {
        GameObject root = new GameObject("Compact HUD Banner Fixture");
        float now = 0f;
        GameHudView.TimeSource = () => now;
        try
        {
            GameHudView hud = root.AddComponent<GameHudView>();
            hud.Initialize();
            Transform banner = root.transform.Find("Gameplay HUD Canvas/Safe Area/Banner");
            Transform hint = root.transform.Find("Gameplay HUD Canvas/Safe Area/Control Hint");
            RoomGraph graph = new RoomGraph(StageId.Forest);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Play, null);
            Assert.IsFalse(banner.gameObject.activeSelf);
            Assert.IsTrue(hint.gameObject.activeSelf);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "새 방", ScreenState.Play, null);
            Assert.IsTrue(banner.gameObject.activeSelf);
            now = 3.1f;
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "새 방", ScreenState.Play, null);
            Assert.IsFalse(hint.gameObject.activeSelf, "initial Play hint must expire without a screen transition");
            now = 1.9f;
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "새 방", ScreenState.Play, null);
            now = 2.1f;
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "새 방", ScreenState.Play, null);
            Assert.IsFalse(banner.gameObject.activeSelf);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "다른 방", ScreenState.Play, null);
            Assert.IsTrue(banner.gameObject.activeSelf);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.StageTransition, null);
            Assert.IsFalse(banner.gameObject.activeSelf);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "다음 단계", ScreenState.StageTransition, null);
            Assert.IsTrue(banner.gameObject.activeSelf);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "다음 단계", ScreenState.Play, null);
            Assert.IsFalse(banner.gameObject.activeSelf, "StageTransition text must not resurrect on Play");
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Ability, null);
            Assert.IsTrue(hint.gameObject.activeSelf, "Ability must show control hint");
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Shop, null);
            Assert.IsTrue(hint.gameObject.activeSelf, "Shop must show control hint");
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Pause, null);
            Assert.IsTrue(hint.gameObject.activeSelf, "Pause must show control hint");
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Play, null);
            Assert.IsFalse(hint.gameObject.activeSelf, "returning to Play must not restart the first-entry hint");
        }
        finally
        {
            GameHudView.TimeSource = () => Time.unscaledTime;
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CommonHudRefreshPreservesLayoutAndMinimapStateForForestCaveCastle()
    {
        StageId[] stages = { StageId.Forest, StageId.Cave, StageId.Castle };
        foreach (StageId stage in stages)
        {
            GameObject root = new GameObject("HUD " + stage);
            try
            {
                GameHudView hud = root.AddComponent<GameHudView>();
                RoomGraph graph = new RoomGraph(stage);
                graph.Rooms[1].visited = true;
                graph.Rooms[1].cleared = true;
                graph.Rooms[3].visited = true;
                graph.Rooms[3].cleared = true;
                hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Play, null);
                Canvas.ForceUpdateCanvases();
                Transform safe = hud.Canvas.transform.Find("Safe Area");
                Assert.AreEqual(new Vector2(184f, 34f), safe.Find("Vitals").GetComponent<RectTransform>().sizeDelta);
                Assert.AreEqual(new Vector2(244f, 28f), safe.Find("Stage Header").GetComponent<RectTransform>().sizeDelta);
                Transform map = safe.Find("Minimap/Map Content");
                Image current = map.Find("Room 0").GetComponent<Image>();
                Image cleared = map.Find("Room 1").GetComponent<Image>();
                Image discovered = map.Find("Room 2").GetComponent<Image>();
                Assert.AreNotEqual(current.color, cleared.color);
                Assert.AreNotEqual(cleared.color, discovered.color);
                Assert.AreNotEqual(current.sprite, cleared.sprite);
                Assert.AreEqual(RoomKind.Shop, graph.Rooms[2].kind);
                Text header = safe.Find("Stage Header/Stage").GetComponent<Text>();
                StringAssert.StartsWith(StageRules.Name(stage).ToUpperInvariant(), header.text);
                StringAssert.Contains((graph.ClearedCombatCount + 1) + " / 4", header.text);
                for (int i = 0; i < 3; i++)
                    Assert.AreEqual(i < graph.ClearedCombatCount, safe.Find("Stage Header/Progress " + (i + 1)).GetComponent<Image>().color.r > .8f);
                Assert.IsNotNull(map.Find("Room 4"), "boss room must be discovered");
                foreach (RectTransform child in map.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child == map.GetComponent<RectTransform>()) continue;
                    Rect childBounds = ToRect(RectTransformUtility.CalculateRelativeRectTransformBounds(map.GetComponent<RectTransform>(), child));
                    Assert.GreaterOrEqual(childBounds.xMin, -40f, child.name + " crops map west");
                    Assert.LessOrEqual(childBounds.xMax, 40f, child.name + " crops map east");
                    Assert.GreaterOrEqual(childBounds.yMin, -32f, child.name + " crops map south");
                    Assert.LessOrEqual(childBounds.yMax, 32f, child.name + " crops map north");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    [Test]
    public void BossBarIsConditionalAndCompact()
    {
        GameObject root = new GameObject("HUD Boss Fixture");
        GameObject bossObject = new GameObject("Boss", typeof(SpriteRenderer));
        try
        {
            GameHudView hud = root.AddComponent<GameHudView>();
            hud.Initialize();
            RoomGraph graph = new RoomGraph(StageId.Castle);
            Transform bossPanel = root.transform.Find("Gameplay HUD Canvas/Safe Area/Boss Bar");
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor>(), null, "", ScreenState.Play, null);
            Assert.IsFalse(bossPanel.gameObject.activeSelf);
            EnemyActor boss = bossObject.AddComponent<EnemyActor>();
            SetPrivate(boss, "type", EnemyType.Morkan);
            SetPrivate(boss, "maxHealth", 260f);
            SetPrivate(boss, "health", 130f);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor> { boss }, null, "", ScreenState.Play, null);
            Assert.IsTrue(bossPanel.gameObject.activeSelf);
            RectTransform canvasRect = hud.Canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(640f, 360f);
            hud.Canvas.GetComponent<CanvasScaler>().enabled = false;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(new Vector2(292f, 18f), bossPanel.GetComponent<RectTransform>().sizeDelta);
            Rect bossBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.GetComponent<RectTransform>()));
            Assert.AreEqual(new Rect(200f, 306f, 292f, 18f), bossBounds);
            Transform bossName = bossPanel.Find("Boss Name");
            Text bossNameText = bossName.GetComponent<Text>();
            Assert.AreEqual("마왕 기사 모르칸  PHASE 2", bossNameText.text);
            Assert.GreaterOrEqual(bossNameText.fontSize, 10);
            Assert.LessOrEqual(bossNameText.preferredWidth, bossNameText.rectTransform.rect.width + .01f);
            Assert.LessOrEqual(bossNameText.preferredHeight, bossNameText.rectTransform.rect.height + .01f);
            Assert.AreEqual(HorizontalWrapMode.Wrap, bossNameText.horizontalOverflow);
            Assert.AreEqual(VerticalWrapMode.Truncate, bossNameText.verticalOverflow);
            Rect nameBounds = ToRect(RectTransformUtility.CalculateRelativeRectTransformBounds(bossPanel.GetComponent<RectTransform>(), bossName.GetComponent<RectTransform>()));
            Assert.GreaterOrEqual(nameBounds.xMin, -146f);
            Assert.LessOrEqual(nameBounds.xMax, 146f);
            Rect bossPanelLocal = bossPanel.GetComponent<RectTransform>().rect;
            Assert.GreaterOrEqual(nameBounds.yMin, bossPanelLocal.yMin - .01f);
            Assert.LessOrEqual(nameBounds.yMax, bossPanelLocal.yMax + .01f);
            Transform track = bossPanel.Find("Boss Track");
            Rect trackBounds = ToRect(RectTransformUtility.CalculateRelativeRectTransformBounds(bossPanel.GetComponent<RectTransform>(), track.GetComponent<RectTransform>()));
            Assert.AreEqual(new Vector2(260f, 4f), track.GetComponent<RectTransform>().sizeDelta);
            Assert.GreaterOrEqual(trackBounds.xMin, -146f);
            Assert.LessOrEqual(trackBounds.xMax, 146f);
            Assert.GreaterOrEqual(trackBounds.yMin, -18f);
            Assert.LessOrEqual(trackBounds.yMax, 0f);
            Image trackImage = track.GetComponent<Image>();
            Image fillImage = track.Find("Boss Fill").GetComponent<Image>();
            Rect fillBounds = ToRect(RectTransformUtility.CalculateRelativeRectTransformBounds(bossPanel.GetComponent<RectTransform>(), fillImage.rectTransform));
            Assert.IsTrue(trackImage.enabled && fillImage.enabled);
            Assert.AreEqual(.5f, fillImage.fillAmount, .001f);
            Assert.GreaterOrEqual(fillBounds.xMin, trackBounds.xMin - .01f);
            Assert.LessOrEqual(fillBounds.xMax, trackBounds.xMax + .01f);
            Assert.GreaterOrEqual(fillBounds.yMin, trackBounds.yMin - .01f);
            Assert.LessOrEqual(fillBounds.yMax, trackBounds.yMax + .01f);
            Assert.AreEqual(130f, track.GetComponent<RectTransform>().rect.width * fillImage.fillAmount, .01f);
            Color contrast = trackImage.color - fillImage.color;
            Assert.Greater(Mathf.Abs(contrast.r) + Mathf.Abs(contrast.g) + Mathf.Abs(contrast.b), .25f);
            Rect centralGameplay = new Rect(120f, 54f, 400f, 252f);
            Assert.IsFalse(centralGameplay.Overlaps(bossBounds));
            Rect[] otherHud = {
                ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.parent.Find("Vitals").GetComponent<RectTransform>())),
                ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.parent.Find("Stage Header").GetComponent<RectTransform>())),
                ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.parent.Find("Minimap").GetComponent<RectTransform>())),
                ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.parent.Find("Ability Build").GetComponent<RectTransform>()))
            };
            foreach (Rect other in otherHud) Assert.IsFalse(other.Overlaps(bossBounds), "Boss Bar overlaps another HUD panel");
            Rect hintBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.parent.Find("Control Hint").GetComponent<RectTransform>()));
            Rect bannerBounds = ToReferenceRect(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, bossPanel.parent.Find("Banner").GetComponent<RectTransform>()));
            Assert.IsFalse(hintBounds.Overlaps(bossBounds), "Boss Bar overlaps Control Hint");
            Assert.IsFalse(bannerBounds.Overlaps(bossBounds), "Boss Bar overlaps Banner");
            Rect[] gatewayBands = { new Rect(0f, 344f, 640f, 16f), new Rect(624f, 0f, 16f, 360f), new Rect(0f, 0f, 640f, 16f), new Rect(0f, 0f, 16f, 360f) };
            foreach (Rect gatewayBand in gatewayBands) Assert.IsFalse(gatewayBand.Overlaps(bossBounds), "Boss Bar overlaps gateway edge band");
            Assert.Less(bossPanel.GetComponent<Image>().color.a, 1f);
        }
        finally { Object.DestroyImmediate(bossObject); Object.DestroyImmediate(root); }
    }

    [Test]
    public void CastleMorkanBossBarRuntimeEvidenceIsRenderedAtFiftyPercent()
    {
        const int width = 640;
        const int height = 360;
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "SoulArcher-CMP-229-" + Guid.NewGuid().ToString("N"));
        string pngPath = Path.Combine(temporaryDirectory, "bossbar-runtime.png");
        string logPath = Path.Combine(temporaryDirectory, "bossbar-runtime.log");
        GameObject root = new GameObject("CMP-227 Runtime Boss Fixture");
        GameObject bossObject = new GameObject("Morkan Phase 2", typeof(SpriteRenderer));
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Camera camera = null;
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            GameObject cameraObject = new GameObject("CMP-229 Capture Camera");
            cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height / 2f;
            camera.transform.localPosition = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .055f, .07f, 1f);
            camera.targetTexture = renderTexture;
            GameHudView hud = root.AddComponent<GameHudView>();
            RoomGraph graph = new RoomGraph(StageId.Castle);
            EnemyActor boss = bossObject.AddComponent<EnemyActor>();
            SetPrivate(boss, "type", EnemyType.Morkan);
            SetPrivate(boss, "maxHealth", 260f);
            SetPrivate(boss, "health", 130f);
            hud.Refresh(new PlayerStats(), graph, new List<Ability>(), 0, new List<EnemyActor> { boss }, null, "마왕 기사 모르칸 PHASE 2", ScreenState.Play, camera);
            Canvas canvas = hud.Canvas;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(.5f, .5f);
            canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localScale = Vector3.one;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            Canvas.ForceUpdateCanvases();
            Debug.Log("DEBUG: Screen size: " + Screen.width + "x" + Screen.height + ", safeArea: " + Screen.safeArea);
            Debug.Log("DEBUG: Canvas enabled: " + canvas.enabled + ", active: " + canvas.gameObject.activeInHierarchy);
            RectTransform safeAreaRectTrans = canvas.transform.Find("Safe Area") as RectTransform;
            if (safeAreaRectTrans != null) {
                Debug.Log("DEBUG: safeArea anchorMin: " + safeAreaRectTrans.anchorMin + ", anchorMax: " + safeAreaRectTrans.anchorMax + ", sizeDelta: " + safeAreaRectTrans.sizeDelta + ", rect: " + safeAreaRectTrans.rect);
            } else {
                Debug.Log("DEBUG: Safe Area not found!");
            }
            Transform bossBarTrans = canvas.transform.Find("Safe Area/Boss Bar");
            if (bossBarTrans != null) {
                Debug.Log("DEBUG: Boss Bar active: " + bossBarTrans.gameObject.activeSelf + ", activeInHierarchy: " + bossBarTrans.gameObject.activeInHierarchy + ", rect: " + bossBarTrans.GetComponent<RectTransform>().rect + ", pos: " + bossBarTrans.GetComponent<RectTransform>().anchoredPosition);
            } else {
                Debug.Log("DEBUG: Boss Bar not found!");
            }
            camera.Render();
            RenderTexture.active = renderTexture;
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply(false, false);
            byte[] png = screenshot.EncodeToPNG();
            File.WriteAllBytes(pngPath, png);
            Color32[] pixels = screenshot.GetPixels32();
            Color32 background = pixels[(height / 2) * width + width / 2];
            HudPixelMetrics metrics = MeasureHudPixels(pixels, width, height, background);
            using (SHA256 sha = SHA256.Create())
            {
                string hash = System.BitConverter.ToString(sha.ComputeHash(png)).Replace("-", string.Empty).ToLowerInvariant();
                File.WriteAllText(logPath, "CMP-229 isolated Castle runtime fixture\n" +
                    "capture=Unity Camera.Render fixture, 640x360, after Canvas.ForceUpdateCanvases\n" +
                    "boss=마왕 기사 모르칸 PHASE 2\nhp=130/260 (50%)\n" +
                    "nonBackgroundPixels=" + metrics.NonBackgroundPixels + "\n" +
                    "colorBuckets=" + metrics.ColorBuckets + "\n" +
                    "bossRegionPixels=" + metrics.BossRegionPixels + "\n" +
                    "png=" + pngPath + "\ndimensions=640x360\nsha256=" + hash + "\n", Encoding.UTF8);
            }
            Assert.AreEqual(width, screenshot.width);
            Assert.AreEqual(height, screenshot.height);
            Assert.IsTrue(IsMeaningfulHudRender(metrics, width, height),
                "HUD render lacks pixel diversity: non-bg=" + metrics.NonBackgroundPixels +
                ", buckets=" + metrics.ColorBuckets + ", boss=" + metrics.BossRegionPixels);

            var solid = new Color32[width * height];
            var nearSolid = new Color32[width * height];
            for (int i = 0; i < solid.Length; i++)
            {
                solid[i] = background;
                nearSolid[i] = background;
            }
            for (int i = 0; i < nearSolid.Length; i += 4096)
                nearSolid[i] = new Color32((byte)Mathf.Min(255, background.r + 2), background.g, background.b, 255);
            Assert.IsFalse(IsMeaningfulHudRender(MeasureHudPixels(solid, width, height, background), width, height));
            Assert.IsFalse(IsMeaningfulHudRender(MeasureHudPixels(nearSolid, width, height, background), width, height));
        }
        finally
        {
            RenderTexture.active = null;
            if (camera != null) camera.targetTexture = null;
            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(bossObject);
            Object.DestroyImmediate(root);
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            Assert.IsFalse(Directory.Exists(temporaryDirectory), "CMP-229 temporary capture was not cleaned");
        }
    }

    [Test]
    public void ForestEnvironmentUsesFullPlateAndActiveGateways()
    {
        var directions = new[] { RoomDirection.North, RoomDirection.East, RoomDirection.South, RoomDirection.West };
        try
        {
            for (int roomId = 0; roomId < 5; roomId++)
            {
                GameObject root = new GameObject("Forest Environment Fixture " + roomId);
                var spawned = new List<GameObject>();
                try
                {
                    var graph = ForestGraphAt(roomId);
                    RoomEnvironmentView.Build(root.transform, StageId.Forest, graph, spawned);
                    int activeCount = 0;
                    foreach (RoomDirection direction in directions)
                    {
                        bool active = graph.HasNeighbor(roomId, direction);
                        GameObject gateway = root.transform.Find("Forest Gateway " + direction)?.gameObject;
                        GameObject marker = root.transform.Find("Forest Exit Marker " + direction)?.gameObject;
                        Assert.AreEqual(active, gateway != null, "gateway parity room " + roomId + " " + direction);
                        Assert.AreEqual(active, marker != null, "marker parity room " + roomId + " " + direction);
                        if (!active) continue;
                        activeCount++;
                        Assert.AreEqual(1.5f, GatewayPassAxis(gateway.GetComponent<SpriteRenderer>(), direction), .001f,
                            "gateway width room " + roomId + " " + direction);
                        if (direction == RoomDirection.North || direction == RoomDirection.South)
                            Assert.AreEqual(direction == RoomDirection.North ? RoomExitRules.TransitionY : -RoomExitRules.TransitionY, gateway.transform.position.y, .001f);
                        else
                            Assert.AreEqual(direction == RoomDirection.East ? RoomExitRules.TransitionX : -RoomExitRules.TransitionX, gateway.transform.position.x, .001f);
                    }
                    Assert.AreEqual(activeCount, spawned.FindAll(item => item.name.StartsWith("Forest Gateway ")).Count);
                    Assert.AreEqual(activeCount, spawned.FindAll(item => item.name.StartsWith("Forest Exit Marker ")).Count);
                    Assert.AreEqual(1, spawned.FindAll(item => item.name == "Forest Full Plate").Count);
                    Assert.AreEqual(0, spawned.FindAll(item => item.name.StartsWith("Floor ")).Count);
                    Assert.AreEqual(0, spawned.FindAll(item => item.name.StartsWith("Ground Detail ")).Count);
                    Assert.AreEqual(0, spawned.FindAll(item => item.name == "Atmospheric Ground Wash").Count);
                    Assert.AreEqual(0, spawned.FindAll(item => item.name.StartsWith("Boundary ")).Count);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
        finally
        {
        }
    }

    [Test]
    public void ForestPlateImporterIsPixelExact()
    {
        const string path = "Assets/Resources/Art/Environment/Forest/forest_room_style_anchor.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.IsNotNull(importer);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
        Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType);
        Assert.AreEqual(32f, importer.spritePixelsPerUnit);
        Assert.AreEqual(FilterMode.Point, importer.filterMode);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        Assert.AreEqual(TextureImporterAlphaSource.None, importer.alphaSource);
        Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.AreEqual(640, texture.width);
        Assert.AreEqual(360, texture.height);
    }

    [Test]
    public void ForestPlateFlipIsDeterministicAndEveryStageUsesItsApprovedFullPlate()
    {
        var combinations = new HashSet<string>();
        GameObject caveRoot = new GameObject("Cave Regression");
        GameObject castleRoot = new GameObject("Castle Regression");
        try
        {
            for (int roomId = 0; roomId < 5; roomId++)
            {
                GameObject firstRoot = new GameObject("Forest Flip First " + roomId);
                GameObject secondRoot = new GameObject("Forest Flip Second " + roomId);
                try
                {
                    var firstObjects = new List<GameObject>();
                    var secondObjects = new List<GameObject>();
                    RoomEnvironmentView.Build(firstRoot.transform, StageId.Forest, ForestGraphAt(roomId), firstObjects);
                    RoomEnvironmentView.Build(secondRoot.transform, StageId.Forest, ForestGraphAt(roomId), secondObjects);
                    SpriteRenderer firstRenderer = firstRoot.transform.Find("Forest Full Plate").GetComponent<SpriteRenderer>();
                    SpriteRenderer secondRenderer = secondRoot.transform.Find("Forest Full Plate").GetComponent<SpriteRenderer>();
                    Assert.AreEqual(firstRenderer.flipX, secondRenderer.flipX);
                    Assert.AreEqual(firstRenderer.flipY, secondRenderer.flipY);
                    combinations.Add(firstRenderer.flipX + ":" + firstRenderer.flipY);
                }
                finally
                {
                    Object.DestroyImmediate(firstRoot);
                    Object.DestroyImmediate(secondRoot);
                }
            }
            Assert.GreaterOrEqual(combinations.Count, 2);

            var caveObjects = new List<GameObject>();
            var castleObjects = new List<GameObject>();
            RoomEnvironmentView.Build(caveRoot.transform, StageId.Cave, new RoomGraph(StageId.Cave), caveObjects);
            RoomEnvironmentView.Build(castleRoot.transform, StageId.Castle, new RoomGraph(StageId.Castle), castleObjects);
            SpriteRenderer cavePlate = caveRoot.transform.Find("Cave Full Plate").GetComponent<SpriteRenderer>();
            SpriteRenderer castlePlate = castleRoot.transform.Find("Castle Full Plate").GetComponent<SpriteRenderer>();
            Assert.AreEqual("cave_room_style_anchor", cavePlate.sprite.name);
            Assert.AreEqual("castle_room_style_anchor", castlePlate.sprite.name);
            Assert.AreEqual(FilterMode.Point, cavePlate.sprite.texture.filterMode);
            Assert.AreEqual(FilterMode.Point, castlePlate.sprite.texture.filterMode);
            Assert.AreEqual(0, caveObjects.FindAll(item => item.name.StartsWith("Floor ")).Count);
            Assert.AreEqual(0, castleObjects.FindAll(item => item.name.StartsWith("Floor ")).Count);
        }
        finally
        {
            Object.DestroyImmediate(caveRoot);
            Object.DestroyImmediate(castleRoot);
        }
    }

    private static RoomGraph ForestGraphAt(int roomId)
    {
        var graph = new RoomGraph(StageId.Forest);
        graph.Rooms[0].cleared = true;
        if (roomId == 0) return graph;
        Assert.IsTrue(graph.TryMove(RoomDirection.East));
        graph.Rooms[1].cleared = true;
        if (roomId == 1) return graph;
        if (roomId == 2) { graph = new RoomGraph(StageId.Forest); graph.Rooms[0].cleared = true; Assert.IsTrue(graph.TryMove(RoomDirection.North)); return graph; }
        Assert.IsTrue(graph.TryMove(RoomDirection.North));
        graph.Rooms[3].cleared = true;
        if (roomId == 3) return graph;
        Assert.IsTrue(graph.TryMove(RoomDirection.East));
        return graph;
    }

    private static Rect ToRect(Bounds bounds)
    {
        return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
    }

    private static Rect ToReferenceRect(Bounds bounds)
    {
        Rect rect = ToRect(bounds);
        return new Rect(rect.x + 320f, rect.y + 180f, rect.width, rect.height);
    }

    private static void SetPrivate<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }

    private struct HudPixelMetrics
    {
        public int NonBackgroundPixels;
        public int ColorBuckets;
        public int BossRegionPixels;
        public int VitalsRegionPixels;
        public int FooterRegionPixels;
    }

    private static HudPixelMetrics MeasureHudPixels(Color32[] pixels, int width, int height, Color32 background)
    {
        var buckets = new HashSet<int>();
        var metrics = new HudPixelMetrics();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 pixel = pixels[y * width + x];
            int difference = Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) + Mathf.Abs(pixel.b - background.b);
            if (difference <= 8) continue;
            metrics.NonBackgroundPixels++;
            buckets.Add((pixel.r >> 4) << 8 | (pixel.g >> 4) << 4 | (pixel.b >> 4));
            if (x >= 174 && x < 466 && y >= 300 && y < 330) metrics.BossRegionPixels++;
            if (x < 205 && y >= 312) metrics.VitalsRegionPixels++;
            if (y < 48) metrics.FooterRegionPixels++;
        }
        metrics.ColorBuckets = buckets.Count;
        return metrics;
    }

    private static bool IsMeaningfulHudRender(HudPixelMetrics metrics, int width, int height)
    {
        return metrics.NonBackgroundPixels >= width * height / 100 &&
            metrics.ColorBuckets >= 8 &&
            metrics.BossRegionPixels >= 120 &&
            metrics.VitalsRegionPixels >= 80 &&
            metrics.FooterRegionPixels >= 80;
    }

    private static float GatewayPassAxis(SpriteRenderer renderer, RoomDirection direction)
    {
        return direction == RoomDirection.North || direction == RoomDirection.South ? renderer.bounds.size.x : renderer.bounds.size.y;
    }

}
