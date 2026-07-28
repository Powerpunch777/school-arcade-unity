using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SoulArcher;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SoulArcherOverlayPresentationTests
{
    [Test]
    public void AbilityOverlayUsesThreeApprovedPixelCardsInsideReferenceFrame()
    {
        GameObject root = new GameObject("Ability Overlay Fixture");
        try
        {
            GameOverlayCommand received = GameOverlayCommand.Title;
            int receivedIndex = -1;
            GameOverlayView view = root.AddComponent<GameOverlayView>();
            view.Initialize((command, index) => { received = command; receivedIndex = index; });
            var stats = new PlayerStats();
            stats.abilityStacks[(int)Ability.Attack] = 1;
            view.Refresh(ScreenState.Ability, stats, new[] { Ability.Attack, Ability.Pierce, Ability.Heal }, ShopRules.CreatePurchaseState(), new RoomGraph());
            Canvas.ForceUpdateCanvases();

            Assert.IsTrue(view.Canvas.enabled);
            Assert.IsTrue(view.Canvas.pixelPerfect);
            Assert.AreEqual(SpriteSorting.Ui + 10, view.Canvas.sortingOrder);
            CanvasScaler scaler = view.Canvas.GetComponent<CanvasScaler>();
            Assert.AreEqual(new Vector2(640f, 360f), scaler.referenceResolution);
            Assert.AreEqual(3, view.CardButtons.Count);
            Assert.AreEqual(3, view.CardRects.Count);

            for (int i = 0; i < view.CardRects.Count; i++)
            {
                RectTransform card = view.CardRects[i];
                Assert.AreEqual(new Vector2(144f, 192f), card.sizeDelta);
                AssertInteger(card.anchoredPosition.x);
                AssertInteger(card.anchoredPosition.y);
                AssertInsideReference(view.ReferenceFrame, card);
                AssertChildrenInsideCard(card);
                Assert.AreEqual(FilterMode.Point, card.GetComponent<Image>().sprite.texture.filterMode);
                Assert.AreEqual("ancient_brown_panel", card.GetComponent<Image>().sprite.name);
                Assert.AreEqual("ancient_brown_panel_pressed", view.CardButtons[i].spriteState.highlightedSprite.name);
                Image icon = card.Find("Icon").GetComponent<Image>();
                string[] expectedIcons = { "AbilityV2/attack", "AbilityV2/pierce", "AbilityV2/heal" };
                Assert.AreEqual(expectedIcons[i], icon.sprite.name);
                Assert.AreEqual(FilterMode.Point, icon.sprite.texture.filterMode);
            }
            AssertNoOverlap(view.ReferenceFrame, view.CardRects);
            Assert.AreEqual("현재 1/3  →  2/3", view.CardRects[0].Find("Stack").GetComponent<Text>().text);

            view.CardButtons[1].onClick.Invoke();
            Assert.AreEqual(GameOverlayCommand.Select, received);
            Assert.AreEqual(1, receivedIndex);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ShopOverlayDistinguishesAffordableDisabledAndConfirmedCards()
    {
        GameObject root = new GameObject("Shop Overlay Fixture");
        try
        {
            var received = new List<string>();
            GameOverlayView view = root.AddComponent<GameOverlayView>();
            view.Initialize((command, index) => received.Add(command + ":" + index));
            var stats = new PlayerStats { souls = 12 };
            view.Refresh(ScreenState.Shop, stats, null, new[] { false, false, true }, new RoomGraph(StageId.Cave));
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(3, view.CardButtons.Count);
            Assert.IsTrue(view.CardButtons[0].interactable);
            Assert.IsFalse(view.CardButtons[1].interactable);
            Assert.IsFalse(view.CardButtons[2].interactable);
            Assert.AreEqual("space_panel_inlay", view.CardButtons[1].spriteState.disabledSprite.name);
            Assert.AreEqual(Selectable.Transition.None, view.CardButtons[2].transition);
            Assert.AreEqual("ancient_brown_panel_pressed", ((Image)view.CardButtons[2].targetGraphic).sprite.name);
            Assert.AreEqual("구매 가능", view.CardRects[0].Find("State").GetComponent<Text>().text);
            Assert.AreEqual("영혼 부족", view.CardRects[1].Find("State").GetComponent<Text>().text);
            Assert.AreEqual("판매 완료", view.CardRects[2].Find("State").GetComponent<Text>().text);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("보유 12", view.CardRects[i].Find("Owned Souls").GetComponent<Text>().text);
                AssertChildrenInsideCard(view.CardRects[i]);
            }
            AssertNoOverlap(view.ReferenceFrame, view.CardRects);

            view.CardButtons[0].onClick.Invoke();
            Button leave = view.ReferenceFrame.Find("Leave Shop").GetComponent<Button>();
            leave.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { "Select:0", "LeaveShop:-1" }, received);
            Assert.IsFalse(CardRect(view.ReferenceFrame, view.CardRects[2]).Overlaps(RectInReference(view.ReferenceFrame, leave.GetComponent<RectTransform>())));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PauseOverlayExposesFourCommandsWithoutCardOverlap()
    {
        GameObject root = new GameObject("Pause Overlay Fixture");
        try
        {
            var received = new List<GameOverlayCommand>();
            GameOverlayView view = root.AddComponent<GameOverlayView>();
            view.Initialize((command, index) => received.Add(command));
            view.Refresh(ScreenState.Pause, new PlayerStats(), null, ShopRules.CreatePurchaseState(), new RoomGraph());
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(4, view.PauseButtons.Count);
            Assert.AreEqual(4, view.CardRects.Count);
            AssertNoOverlap(view.ReferenceFrame, view.CardRects);
            foreach (RectTransform card in view.CardRects)
            {
                Assert.AreEqual(new Vector2(96f, 128f), card.sizeDelta);
                AssertInsideReference(view.ReferenceFrame, card);
                AssertChildrenInsideCard(card);
            }

            foreach (Button button in view.PauseButtons) button.onClick.Invoke();
            CollectionAssert.AreEqual(new[] {
                GameOverlayCommand.Resume,
                GameOverlayCommand.Restart,
                GameOverlayCommand.Title,
                GameOverlayCommand.Settings
            }, received);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ReferenceLayoutScalesToOneTwoAndThreePixelMultiples()
    {
        Assert.AreEqual(1, GameOverlayView.PixelScaleForResolution(640, 360));
        Assert.AreEqual(2, GameOverlayView.PixelScaleForResolution(1280, 720));
        Assert.AreEqual(3, GameOverlayView.PixelScaleForResolution(1920, 1080));

        GameObject root = new GameObject("Overlay Scale Fixture");
        try
        {
            GameOverlayView view = root.AddComponent<GameOverlayView>();
            view.Initialize(null);
            view.Refresh(ScreenState.Ability, new PlayerStats(), new[] { Ability.Attack, Ability.Speed, Ability.Move }, ShopRules.CreatePurchaseState(), new RoomGraph());
            Canvas.ForceUpdateCanvases();
            int[,] resolutions = { { 640, 360 }, { 1280, 720 }, { 1920, 1080 } };
            for (int r = 0; r < resolutions.GetLength(0); r++)
            {
                int width = resolutions[r, 0];
                int height = resolutions[r, 1];
                int scale = GameOverlayView.PixelScaleForResolution(width, height);
                foreach (RectTransform card in view.CardRects)
                {
                    Rect reference = CardRect(view.ReferenceFrame, card);
                    Rect scaled = new Rect(reference.x * scale, reference.y * scale, reference.width * scale, reference.height * scale);
                    Assert.GreaterOrEqual(scaled.xMin, 0f);
                    Assert.GreaterOrEqual(scaled.yMin, 0f);
                    Assert.LessOrEqual(scaled.xMax, width);
                    Assert.LessOrEqual(scaled.yMax, height);
                    AssertInteger(scaled.xMin);
                    AssertInteger(scaled.yMin);
                    AssertInteger(scaled.width);
                    AssertInteger(scaled.height);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CanvasCommandsUseExistingAbilityShopAndScreenRules()
    {
        GameObject root = new GameObject("Overlay Command Fixture");
        try
        {
            SoulArcherGame game = root.AddComponent<SoulArcherGame>();
            var stats = new PlayerStats { souls = 20 };
            var graph = new RoomGraph(StageId.Forest);
            var purchased = ShopRules.CreatePurchaseState();
            SetPrivate(game, "stats", stats);
            SetPrivate(game, "graph", graph);
            SetPrivate(game, "bought", purchased);
            SetPrivate(game, "cards", new List<Ability> { Ability.Attack, Ability.Pierce, Ability.Heal });

            SetPrivate(game, "screen", ScreenState.Ability);
            game.ExecuteOverlayCommand(GameOverlayCommand.Select, 0);
            Assert.AreEqual(ScreenState.Play, game.CurrentScreen);
            Assert.AreEqual(1, stats.StackCount(Ability.Attack));
            Assert.AreEqual(1, game.AbilityChoices);

            SetPrivate(game, "screen", ScreenState.Shop);
            game.ExecuteOverlayCommand(GameOverlayCommand.Select, 1);
            Assert.AreEqual(5, stats.souls);
            Assert.AreEqual(1, stats.shieldCharges);
            Assert.IsTrue(game.IsShopItemPurchased(1));
            game.ExecuteOverlayCommand(GameOverlayCommand.LeaveShop, -1);
            Assert.AreEqual(ScreenState.Play, game.CurrentScreen);
            Assert.IsTrue(graph.Current.cleared);

            SetPrivate(game, "screen", ScreenState.Pause);
            game.ExecuteOverlayCommand(GameOverlayCommand.Settings, -1);
            Assert.AreEqual(ScreenState.Settings, game.CurrentScreen);
            SetPrivate(game, "screen", ScreenState.Pause);
            game.ExecuteOverlayCommand(GameOverlayCommand.Resume, -1);
            Assert.AreEqual(ScreenState.Play, game.CurrentScreen);
            SetPrivate(game, "screen", ScreenState.Pause);
            game.ExecuteOverlayCommand(GameOverlayCommand.Title, -1);
            Assert.AreEqual(ScreenState.Title, game.CurrentScreen);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void AbilityShopAndPauseNoLongerHaveImguiProductMethods()
    {
        string source = File.ReadAllText("Assets/Scripts/SoulArcherGame.cs");
        Assert.IsFalse(source.Contains("DrawAbilityOverlay("));
        Assert.IsFalse(source.Contains("DrawShopOverlay("));
        Assert.IsFalse(source.Contains("DrawPause("));
        Assert.IsTrue(source.Contains("ExecuteOverlayCommand(GameOverlayCommand.Select"));
        Assert.IsTrue(SoulArcherGame.IsCanvasOverlayScreen(ScreenState.Ability));
        Assert.IsTrue(SoulArcherGame.IsCanvasOverlayScreen(ScreenState.Shop));
        Assert.IsTrue(SoulArcherGame.IsCanvasOverlayScreen(ScreenState.Pause));
        Assert.IsFalse(SoulArcherGame.IsCanvasOverlayScreen(ScreenState.Settings));
    }

    private static void AssertChildrenInsideCard(RectTransform card)
    {
        Rect cardRect = card.rect;
        foreach (RectTransform child in card.GetComponentsInChildren<RectTransform>(true))
        {
            if (child == card) continue;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(card, child);
            Assert.GreaterOrEqual(bounds.min.x, cardRect.xMin - .01f, child.name + " cropped left");
            Assert.LessOrEqual(bounds.max.x, cardRect.xMax + .01f, child.name + " cropped right");
            Assert.GreaterOrEqual(bounds.min.y, cardRect.yMin - .01f, child.name + " cropped bottom");
            Assert.LessOrEqual(bounds.max.y, cardRect.yMax + .01f, child.name + " cropped top");
            Text text = child.GetComponent<Text>();
            if (text != null)
                Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.name + " text truncates");
        }
    }

    private static void AssertInsideReference(RectTransform reference, RectTransform child)
    {
        Rect rect = RectInReference(reference, child);
        Assert.GreaterOrEqual(rect.xMin, 0f);
        Assert.GreaterOrEqual(rect.yMin, 0f);
        Assert.LessOrEqual(rect.xMax, 640f);
        Assert.LessOrEqual(rect.yMax, 360f);
    }

    private static void AssertNoOverlap(RectTransform reference, IReadOnlyList<RectTransform> rects)
    {
        for (int i = 0; i < rects.Count; i++)
        for (int j = i + 1; j < rects.Count; j++)
            Assert.IsFalse(RectInReference(reference, rects[i]).Overlaps(RectInReference(reference, rects[j])), rects[i].name + " overlaps " + rects[j].name);
    }

    private static Rect CardRect(RectTransform reference, RectTransform child)
    {
        return RectInReference(reference, child);
    }

    private static Rect RectInReference(RectTransform reference, RectTransform child)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(reference, child);
        return new Rect(bounds.min.x + 320f, bounds.min.y + 180f, bounds.size.x, bounds.size.y);
    }

    private static void AssertInteger(float value)
    {
        Assert.AreEqual(Mathf.Round(value), value, .001f);
    }

    private static void SetPrivate<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }
}
