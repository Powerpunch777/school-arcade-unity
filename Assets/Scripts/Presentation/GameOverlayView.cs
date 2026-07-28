using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulArcher
{
    public enum GameOverlayCommand
    {
        Select,
        LeaveShop,
        Resume,
        Restart,
        Title,
        Settings
    }

    [DisallowMultipleComponent]
    public sealed class GameOverlayView : MonoBehaviour
    {
        private const float ReferenceWidth = 640f;
        private const float ReferenceHeight = 360f;

        private readonly List<Button> cardButtons = new List<Button>();
        private readonly List<RectTransform> cardRects = new List<RectTransform>();
        private readonly List<Button> pauseButtons = new List<Button>();
        private Canvas canvas;
        private RectTransform referenceFrame;
        private Action<GameOverlayCommand, int> commandSink;
        private ScreenState currentScreen;
        private int contentSignature = int.MinValue;

        public Canvas Canvas { get { return canvas; } }
        public RectTransform ReferenceFrame { get { return referenceFrame; } }
        public IReadOnlyList<Button> CardButtons { get { return cardButtons; } }
        public IReadOnlyList<RectTransform> CardRects { get { return cardRects; } }
        public IReadOnlyList<Button> PauseButtons { get { return pauseButtons; } }
        public ScreenState CurrentScreen { get { return currentScreen; } }

        public void Initialize(Action<GameOverlayCommand, int> sink)
        {
            commandSink = sink;
            if (canvas != null) return;
            BuildCanvas();
            canvas.enabled = false;
        }

        public void Refresh(
            ScreenState screen,
            PlayerStats stats,
            IReadOnlyList<Ability> abilityCards,
            bool[] purchased,
            RoomGraph graph)
        {
            if (canvas == null) Initialize(commandSink);
            currentScreen = screen;
            bool visible = SoulArcherGame.IsCanvasOverlayScreen(screen);
            canvas.enabled = visible;
            if (!visible) return;

            int signature = ContentSignature(screen, stats, abilityCards, purchased, graph);
            if (signature == contentSignature) return;
            contentSignature = signature;
            Rebuild(screen, stats, abilityCards, purchased, graph);
        }

        public static int PixelScaleForResolution(int width, int height)
        {
            if (width <= 0 || height <= 0) return 1;
            return Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(width / ReferenceWidth, height / ReferenceHeight)));
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Game Overlay Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SpriteSorting.Ui + 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            scaler.referencePixelsPerUnit = 16f;

            referenceFrame = RectObject("Reference Frame", canvasObject.transform);
            referenceFrame.anchorMin = new Vector2(.5f, .5f);
            referenceFrame.anchorMax = new Vector2(.5f, .5f);
            referenceFrame.pivot = new Vector2(.5f, .5f);
            referenceFrame.anchoredPosition = Vector2.zero;
            referenceFrame.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

            if (Application.isPlaying && EventSystem.current == null)
                new GameObject("Overlay Event System", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void Rebuild(
            ScreenState screen,
            PlayerStats stats,
            IReadOnlyList<Ability> abilityCards,
            bool[] purchased,
            RoomGraph graph)
        {
            ClearReferenceFrame();
            BuildBackdrop();
            if (screen == ScreenState.Ability) BuildAbility(stats, abilityCards);
            else if (screen == ScreenState.Shop) BuildShop(stats, purchased, graph);
            else BuildPause();
            Canvas.ForceUpdateCanvases();
        }

        private void ClearReferenceFrame()
        {
            cardButtons.Clear();
            cardRects.Clear();
            pauseButtons.Clear();
            for (int i = referenceFrame.childCount - 1; i >= 0; i--)
            {
                GameObject child = referenceFrame.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        private void BuildBackdrop()
        {
            Image backdrop = Image("Overlay Backdrop", referenceFrame, OverlayFrameSprite(), new Color(.16f, .22f, .3f, .97f));
            Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);
            backdrop.type = UnityEngine.UI.Image.Type.Sliced;
            backdrop.raycastTarget = true;
        }

        private void BuildAbility(PlayerStats stats, IReadOnlyList<Ability> abilityCards)
        {
            Text title = Label("Overlay Title", referenceFrame, "능력 선택", 22, TextAnchor.MiddleCenter, new Color(1f, .88f, .42f));
            SetRect(title.rectTransform, new Vector2(0f, 151f), new Vector2(400f, 28f));
            Text subtitle = Label("Overlay Subtitle", referenceFrame, "하나를 선택하세요  ·  숫자키 1 / 2 / 3", 11, TextAnchor.MiddleCenter, new Color(.78f, .86f, .84f));
            SetRect(subtitle.rectTransform, new Vector2(0f, 130f), new Vector2(460f, 18f));

            int count = abilityCards == null ? 0 : Mathf.Min(3, abilityCards.Count);
            for (int i = 0; i < count; i++)
            {
                Ability ability = abilityCards[i];
                RectTransform card = CreateCard("Ability Card " + (i + 1), new Vector2(-164f + i * 164f, -3f), new Vector2(144f, 192f), true, false, GameOverlayCommand.Select, i);
                int current = stats == null ? 0 : stats.StackCount(ability);
                int next = Mathf.Min(AbilityRules.MaxStack, current + 1);
                AddCardLabel(card, "Key", "[" + (i + 1) + "]", new Vector2(0f, 79f), new Vector2(38f, 16f), 11, new Color(1f, .87f, .35f));
                Image icon = Image("Icon", card, RuntimeSpriteAtlas.AbilityIcon(ability), Color.white);
                SetRect(icon.rectTransform, new Vector2(0f, 52f), new Vector2(52f, 52f));
                icon.preserveAspect = true;
                AddCardLabel(card, "Name", AbilityName(ability), new Vector2(0f, 18f), new Vector2(122f, 20f), 14, Color.white);
                AddCardLabel(card, "Stack", "현재 " + current + "/3  →  " + next + "/3", new Vector2(0f, -7f), new Vector2(122f, 18f), 11, new Color(.69f, .91f, .8f));
                AddCardLabel(card, "Effect", AbilityEffect(ability), new Vector2(0f, -39f), new Vector2(122f, 34f), 11, new Color(.84f, .87f, .88f));
                AddCardLabel(card, "State", "선택", new Vector2(0f, -78f), new Vector2(110f, 16f), 10, new Color(1f, .86f, .38f));
            }
        }

        private void BuildShop(PlayerStats stats, bool[] purchased, RoomGraph graph)
        {
            int souls = stats == null ? 0 : stats.souls;
            string stage = graph == null ? string.Empty : StageRules.Name(graph.Stage) + "  ·  ";
            Text title = Label("Overlay Title", referenceFrame, stage + "상점", 22, TextAnchor.MiddleCenter, new Color(1f, .88f, .42f));
            SetRect(title.rectTransform, new Vector2(0f, 151f), new Vector2(400f, 28f));
            Text subtitle = Label("Overlay Subtitle", referenceFrame, "보유 영혼 " + souls + "  ·  숫자키로 구매  ·  Esc/Space로 나가기", 11, TextAnchor.MiddleCenter, new Color(.78f, .86f, .84f));
            SetRect(subtitle.rectTransform, new Vector2(0f, 130f), new Vector2(500f, 18f));

            for (int i = 0; i < ShopRules.Prices.Length; i++)
            {
                bool sold = purchased != null && i < purchased.Length && purchased[i];
                bool affordable = !sold && souls >= ShopRules.Prices[i];
                RectTransform card = CreateCard("Shop Card " + (i + 1), new Vector2(-164f + i * 164f, -3f), new Vector2(144f, 192f), affordable, sold, GameOverlayCommand.Select, i);
                AddCardLabel(card, "Key", "[" + (i + 1) + "]", new Vector2(0f, 79f), new Vector2(38f, 16f), 11, sold ? new Color(.54f, .75f, 1f) : new Color(1f, .87f, .35f));
                Image icon = Image("Icon", card, RuntimeSpriteAtlas.LoadCell("Art/UI/shop_icons", i, 0), sold ? new Color(.72f, .82f, .94f) : Color.white);
                SetRect(icon.rectTransform, new Vector2(0f, 53f), new Vector2(48f, 48f));
                icon.preserveAspect = true;
                AddCardLabel(card, "Name", ShopName(i), new Vector2(0f, 18f), new Vector2(122f, 20f), 14, Color.white);
                AddCardLabel(card, "Effect", ShopEffect(i), new Vector2(0f, -8f), new Vector2(122f, 28f), 10, new Color(.84f, .87f, .88f));
                AddCardLabel(card, "Price", "가격 " + ShopRules.Prices[i] + " 영혼", new Vector2(0f, -38f), new Vector2(122f, 18f), 11, affordable ? new Color(.52f, 1f, .66f) : new Color(.68f, .7f, .72f));
                AddCardLabel(card, "State", sold ? "판매 완료" : affordable ? "구매 가능" : "영혼 부족", new Vector2(0f, -62f), new Vector2(116f, 18f), 10, sold ? new Color(.54f, .75f, 1f) : affordable ? new Color(1f, .86f, .38f) : new Color(.68f, .7f, .72f));
                AddCardLabel(card, "Owned Souls", "보유 " + souls, new Vector2(0f, -80f), new Vector2(116f, 14f), 9, new Color(.7f, .79f, .79f));
            }

            Button leave = CreateFooterButton("Leave Shop", "상점 나가기", "ESC / SPACE / ENTER", GameOverlayCommand.LeaveShop);
            leave.gameObject.name = "Leave Shop";
        }

        private void BuildPause()
        {
            Text title = Label("Overlay Title", referenceFrame, "일시정지", 24, TextAnchor.MiddleCenter, new Color(1f, .88f, .42f));
            SetRect(title.rectTransform, new Vector2(0f, 139f), new Vector2(400f, 32f));
            string[] names = { "계속하기", "다시 시작", "타이틀", "설정" };
            string[] keys = { "ESC", "R", "T", "CLICK" };
            GameOverlayCommand[] commands = { GameOverlayCommand.Resume, GameOverlayCommand.Restart, GameOverlayCommand.Title, GameOverlayCommand.Settings };
            for (int i = 0; i < commands.Length; i++)
            {
                RectTransform card = CreateCard("Pause Action " + (i + 1), new Vector2(-174f + i * 116f, -7f), new Vector2(96f, 128f), true, false, commands[i], -1);
                Image icon = Image("Icon", card, RuntimeSpriteAtlas.LoadCell("Art/UI/ui_atlas", 6 + i, 0), Color.white);
                SetRect(icon.rectTransform, new Vector2(0f, 34f), new Vector2(32f, 32f));
                icon.preserveAspect = true;
                AddCardLabel(card, "Name", names[i], new Vector2(0f, 2f), new Vector2(82f, 34f), 12, Color.white);
                AddCardLabel(card, "Key", keys[i], new Vector2(0f, -39f), new Vector2(80f, 18f), 9, new Color(1f, .86f, .38f));
                pauseButtons.Add(card.GetComponent<Button>());
            }
            Text hint = Label("Pause Hint", referenceFrame, "클릭 또는 표시된 키로 선택", 10, TextAnchor.MiddleCenter, new Color(.72f, .8f, .79f));
            SetRect(hint.rectTransform, new Vector2(0f, -108f), new Vector2(400f, 18f));
        }

        private RectTransform CreateCard(
            string name,
            Vector2 position,
            Vector2 size,
            bool interactable,
            bool confirmed,
            GameOverlayCommand command,
            int index)
        {
            Image image = Image(name, referenceFrame, CardFrameSprite(confirmed), Color.white);
            SetRect(image.rectTransform, position, size);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            if (confirmed)
            {
                button.transition = Selectable.Transition.None;
            }
            else
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = button.spriteState;
                state.highlightedSprite = CardFrameSprite(true);
                state.selectedSprite = CardFrameSprite(true);
                state.pressedSprite = CardFrameSprite(true);
                state.disabledSprite = DisabledCardFrameSprite();
                button.spriteState = state;
            }
            button.onClick.AddListener(() => Dispatch(command, index));
            cardButtons.Add(button);
            cardRects.Add(image.rectTransform);
            return image.rectTransform;
        }

        private Button CreateFooterButton(string name, string value, string hint, GameOverlayCommand command)
        {
            Image image = Image(name, referenceFrame, RuntimeSpriteAtlas.LoadCell("Art/UI/ui_atlas", 10, 0), new Color(1f, .86f, .38f));
            SetRect(image.rectTransform, new Vector2(286f, -161f), new Vector2(32f, 32f));
            image.preserveAspect = true;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.55f, 1f, .72f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.45f, .72f, 1f);
            colors.fadeDuration = 0f;
            button.colors = colors;
            button.onClick.AddListener(() => Dispatch(command, -1));
            Text label = Label("Leave Label", referenceFrame, value, 10, TextAnchor.MiddleRight, Color.white);
            SetRect(label.rectTransform, new Vector2(226f, -155f), new Vector2(94f, 16f));
            Text key = Label("Leave Key", referenceFrame, hint, 7, TextAnchor.MiddleRight, new Color(.68f, .77f, .77f));
            SetRect(key.rectTransform, new Vector2(226f, -169f), new Vector2(112f, 12f));
            return button;
        }

        private void Dispatch(GameOverlayCommand command, int index)
        {
            if (commandSink != null) commandSink(command, index);
        }

        private static int ContentSignature(ScreenState screen, PlayerStats stats, IReadOnlyList<Ability> cards, bool[] purchased, RoomGraph graph)
        {
            unchecked
            {
                int value = (int)screen * 397;
                value = value * 31 + (stats == null ? 0 : stats.souls);
                value = value * 31 + (stats == null ? 0 : Mathf.RoundToInt(stats.hearts * 2f));
                value = value * 31 + (graph == null ? -1 : (int)graph.Stage);
                if (stats != null)
                    for (int i = 0; i < stats.abilityStacks.Length; i++) value = value * 31 + stats.abilityStacks[i];
                if (cards != null)
                    for (int i = 0; i < cards.Count; i++) value = value * 31 + (int)cards[i] + 1;
                if (purchased != null)
                    for (int i = 0; i < purchased.Length; i++) value = value * 31 + (purchased[i] ? 1 : 0);
                return value;
            }
        }

        private static Sprite PanelSprite(int state)
        {
            return RuntimeSpriteAtlas.LoadCell("Art/UI/panel_states", Mathf.Clamp(state, 0, 3), 0, 48, 64, 16f);
        }

        private static Sprite OverlayFrameSprite()
        {
            return Resources.Load<Sprite>("Art/UI/Kenney/space_panel") ?? PanelSprite(0);
        }

        private static Sprite CardFrameSprite(bool pressed)
        {
            string path = pressed ? "Art/UI/Kenney/ancient_brown_panel_pressed" : "Art/UI/Kenney/ancient_brown_panel";
            return Resources.Load<Sprite>(path) ?? PanelSprite(pressed ? 3 : 0);
        }

        private static Sprite DisabledCardFrameSprite()
        {
            return Resources.Load<Sprite>("Art/UI/Kenney/space_panel_inlay") ?? PanelSprite(2);
        }

        private static void AddCardLabel(RectTransform card, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            Text text = Label(name, card, value, fontSize, TextAnchor.MiddleCenter, color);
            SetRect(text.rectTransform, position, size);
        }

        private static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Font();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static Font Font()
        {
            return GameFont.Get();
        }

        private static RectTransform RectObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static string AbilityName(Ability ability)
        {
            switch (ability)
            {
                case Ability.Attack: return "공격";
                case Ability.Speed: return "공격 속도";
                case Ability.Pierce: return "관통";
                case Ability.Reflect: return "반사";
                case Ability.Heal: return "회복";
                default: return "이동 속도";
            }
        }

        private static string AbilityEffect(Ability ability)
        {
            switch (ability)
            {
                case Ability.Attack: return "공격력 +20%";
                case Ability.Speed: return "공격 간격 -15%";
                case Ability.Pierce: return "적 1명 추가 관통";
                case Ability.Reflect: return "벽 1회 추가 반사";
                case Ability.Heal: return "하트 1.5 회복";
                default: return "이동 속도 +10%";
            }
        }

        private static string ShopName(int index)
        {
            if (index == 0) return "회복 하트";
            if (index == 1) return "보호 부적";
            return "강화 숫돌";
        }

        private static string ShopEffect(int index)
        {
            if (index == 0) return "하트 1.5 회복";
            if (index == 1) return "피해 1회 방어";
            return "이번 스테이지 공격력 +20%";
        }
    }
}
