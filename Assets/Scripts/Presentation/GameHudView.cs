using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SoulArcher
{
    [DisallowMultipleComponent]
    public sealed class GameHudView : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform safeArea;
        private readonly List<Image> hearts = new List<Image>();
        private readonly List<Image> progressPips = new List<Image>();
        private readonly List<Image> abilityIcons = new List<Image>();
        private readonly List<Text> abilityCounts = new List<Text>();
        private Text soulText;
        private Text stageText;
        private Text roomText;
        private Text abilityText;
        private Text hintText;
        private Text bannerText;
        private RectTransform bannerPanel;
        private RectTransform hintPanel;
        private RectTransform bossPanel;
        private Image bossFill;
        private Text bossText;
        private RectTransform minimapContent;
        private RectTransform targetReticle;
        private Image damageFlash;
        private int minimapSignature = int.MinValue;
        private float flashUntil;
        private float bannerUntil;
        private float hintUntil;
        private string activeBanner = string.Empty;
        private string lastRequestedBanner = string.Empty;
        private ScreenState lastScreen;
        private bool hasScreen;
        private bool hasEnteredPlay;
        private Rect lastSafeArea;

        // EditMode tests can replace this seam; production remains tied to unscaled time.
        public static System.Func<float> TimeSource = () => Time.unscaledTime;

        public Canvas Canvas { get { return canvas; } }

        public void Initialize()
        {
            if (canvas != null) return;
            BuildCanvas();
            BuildVitals();
            BuildStageHeader();
            BuildBossBar();
            BuildMinimap();
            BuildFooter();
            BuildDamageFlash();
            ApplySafeArea(true);
        }

        public void Refresh(
            PlayerStats stats,
            RoomGraph graph,
            IReadOnlyList<Ability> selectedAbilities,
            int abilityChoices,
            IReadOnlyList<EnemyActor> enemies,
            EnemyActor currentTarget,
            string banner,
            ScreenState screen,
            Camera gameCamera)
        {
            if (canvas == null) Initialize();
            bool visible = screen == ScreenState.Play || screen == ScreenState.Ability || screen == ScreenState.Shop || screen == ScreenState.Pause || screen == ScreenState.StageTransition;
            canvas.enabled = visible;
            float now = TimeSource();
            bool screenChanged = !hasScreen || screen != lastScreen;
            if (screenChanged)
            {
                hasScreen = true;
                lastScreen = screen;
                activeBanner = string.Empty;
                bannerUntil = 0f;
                bannerPanel.gameObject.SetActive(false);
                if (screen == ScreenState.Play)
                {
                    if (!hasEnteredPlay) hintUntil = now + 3f;
                    hasEnteredPlay = true;
                }
                else hintUntil = 0f;
            }
            if (!visible || stats == null || graph == null) return;

            ApplySafeArea(false);
            for (int i = 0; i < hearts.Count; i++)
            {
                int atlasColumn = stats.hearts >= i + 1f ? 0 : stats.hearts > i ? 1 : 2;
                hearts[i].sprite = RuntimeSpriteAtlas.LoadCell("Art/UI/ui_atlas", atlasColumn, 0);
                hearts[i].color = atlasColumn == 2 ? new Color(.62f, .67f, .72f, .72f) : Color.white;
            }
            soulText.text = stats.souls.ToString("00");
            stageText.text = StageRules.Name(graph.Stage).ToUpperInvariant() + "  ·  " + (graph.ClearedCombatCount + 1) + " / 4";
            roomText.text = graph.Current.label + (graph.Current.cleared ? "  ✓" : string.Empty);
            for (int i = 0; i < progressPips.Count; i++)
                progressPips[i].color = i < graph.ClearedCombatCount ? new Color(.99f, .82f, .29f) : new Color(.25f, .32f, .32f, .92f);

            int[] abilityTotals = new int[6];
            if (selectedAbilities != null)
                foreach (Ability ability in selectedAbilities)
                    abilityTotals[Mathf.Clamp((int)ability, 0, abilityTotals.Length - 1)]++;
            abilityText.text = "능력 " + Mathf.Clamp(abilityChoices, 0, AbilityRules.ChoicesPerRun) + "/" + AbilityRules.ChoicesPerRun;
            for (int i = 0; i < abilityIcons.Count; i++)
            {
                int count = abilityTotals[i];
                abilityIcons[i].color = count > 0 ? Color.white : new Color(.36f, .42f, .42f, .78f);
                abilityCounts[i].text = count > 1 ? Mathf.Min(count, 9).ToString() : string.Empty;
                abilityCounts[i].gameObject.SetActive(count > 1);
            }
            hintText.text = "MOVE WASD/ARROWS   AUTO FIRE STOP   PAUSE ESC";
            bool hintVisible = screen == ScreenState.Ability || screen == ScreenState.Shop || screen == ScreenState.Pause ||
                (screen == ScreenState.Play && now < hintUntil);
            hintPanel.gameObject.SetActive(hintVisible);
            UpdateBanner(banner, screen, now, screenChanged);

            EnemyActor boss = enemies == null ? null : enemies.FirstOrDefault(IsBoss);
            bossPanel.gameObject.SetActive(boss != null);
            if (boss != null)
            {
                float health = Mathf.Clamp01(boss.Health / boss.MaxHealth);
                bossFill.fillAmount = health;
                bossFill.color = Color.Lerp(new Color(.95f, .25f, .18f), new Color(.92f, .57f, .18f), health);
                bossText.text = StageRules.BossName(graph.Stage) + (boss.Type == EnemyType.Morkan ? "  PHASE " + boss.BossPhase : string.Empty);
            }

            int signature = MapSignature(graph);
            if (signature != minimapSignature)
            {
                minimapSignature = signature;
                RebuildMinimap(graph);
            }
            UpdateTarget(currentTarget, gameCamera);
        }

        public void PulseDamage()
        {
            flashUntil = Time.unscaledTime + .18f;
            if (damageFlash != null) damageFlash.enabled = true;
        }

        private void LateUpdate()
        {
            if (bannerPanel != null && lastScreen == ScreenState.Play && !string.IsNullOrEmpty(activeBanner) && TimeSource() >= bannerUntil)
            {
                bannerText.text = string.Empty;
                bannerPanel.gameObject.SetActive(false);
            }
            if (damageFlash == null) return;
            float remaining = flashUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                damageFlash.enabled = false;
                return;
            }
            Color color = damageFlash.color;
            color.a = Mathf.Clamp01(remaining / .18f) * .28f;
            damageFlash.color = color;
        }

        private void UpdateBanner(string banner, ScreenState screen, float now, bool screenChanged)
        {
            string requested = banner ?? string.Empty;
            if (screen == ScreenState.StageTransition)
            {
                activeBanner = requested;
                lastRequestedBanner = requested;
                bannerUntil = float.PositiveInfinity;
                bannerText.text = activeBanner;
                bannerPanel.gameObject.SetActive(!string.IsNullOrEmpty(activeBanner));
                return;
            }
            if (screen == ScreenState.Play)
            {
                if (string.IsNullOrEmpty(requested))
                {
                    activeBanner = string.Empty;
                    bannerUntil = 0f;
                }
                else if (requested != lastRequestedBanner)
                {
                    activeBanner = requested;
                    bannerUntil = now + 2f;
                }
                lastRequestedBanner = requested;
                bool shown = !string.IsNullOrEmpty(activeBanner) && now < bannerUntil;
                bannerText.text = shown ? activeBanner : string.Empty;
                bannerPanel.gameObject.SetActive(shown);
                return;
            }
            activeBanner = requested;
            lastRequestedBanner = requested;
            bannerUntil = float.PositiveInfinity;
            bannerText.text = activeBanner;
            bannerPanel.gameObject.SetActive(!string.IsNullOrEmpty(activeBanner));
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Gameplay HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SpriteSorting.Ui;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DisplaySettings.InternalWidth, DisplaySettings.InternalHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            scaler.referencePixelsPerUnit = 16f;

            GameObject safeObject = UiObject("Safe Area", canvasObject.transform);
            safeArea = safeObject.GetComponent<RectTransform>();
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        private void BuildVitals()
        {
            RectTransform panel = Panel("Vitals", safeArea, new Vector2(8f, -8f), new Vector2(184f, 34f), new Vector2(0f, 1f));
            Accent(panel, new Color(.99f, .82f, .29f), true);
            for (int i = 0; i < 5; i++)
            {
                Image heart = Image("Heart " + (i + 1), panel, RuntimeSpriteAtlas.LoadCell("Art/UI/ui_atlas", 0, 0), Color.white);
                SetRect(heart.rectTransform, new Vector2(13f + i * 22f, -10f), new Vector2(18f, 18f), new Vector2(0f, 1f));
                heart.preserveAspect = true;
                hearts.Add(heart);
            }
            Image soul = Image("Soul", panel, RuntimeSpriteAtlas.LoadCell("Art/UI/ui_atlas", 3, 0), Color.white);
            SetRect(soul.rectTransform, new Vector2(135f, -8f), new Vector2(16f, 16f), new Vector2(0f, 1f));
            soul.preserveAspect = true;
            soulText = Label("Soul Count", panel, "00", 15, TextAnchor.MiddleLeft, new Color(.82f, .97f, 1f));
            SetRect(soulText.rectTransform, new Vector2(151f, -8f), new Vector2(28f, 18f), new Vector2(0f, 1f));
            roomText = Label("Room Name", panel, string.Empty, 10, TextAnchor.MiddleLeft, new Color(.86f, .9f, .85f));
            SetRect(roomText.rectTransform, new Vector2(10f, -26f), new Vector2(166f, 12f), new Vector2(0f, 1f));
        }

        private void BuildStageHeader()
        {
            RectTransform panel = Panel("Stage Header", safeArea, new Vector2(0f, -8f), new Vector2(244f, 28f), new Vector2(.5f, 1f));
            stageText = Label("Stage", panel, string.Empty, 12, TextAnchor.MiddleCenter, new Color(1f, .91f, .64f));
            SetRect(stageText.rectTransform, new Vector2(0f, -3f), new Vector2(224f, 14f), new Vector2(.5f, 1f));
            for (int i = 0; i < 3; i++)
            {
                Image pip = Image("Progress " + (i + 1), panel, RuntimeSpriteAtlas.Diamond(), new Color(.25f, .32f, .32f));
                SetRect(pip.rectTransform, new Vector2(-15f + i * 15f, 2f), new Vector2(6f, 6f), new Vector2(.5f, 0f));
                progressPips.Add(pip);
            }
        }

        private void BuildBossBar()
        {
            // Keep the conditional boss strip in the top HUD band, above the gameplay exclusion.
            bossPanel = Panel("Boss Bar", safeArea, new Vector2(26f, -36f), new Vector2(292f, 18f), new Vector2(.5f, 1f));
            bossText = Label("Boss Name", bossPanel, string.Empty, 10, TextAnchor.MiddleCenter, new Color(1f, .88f, .72f));
            SetRect(bossText.rectTransform, new Vector2(0f, -1f), new Vector2(272f, 12f), new Vector2(.5f, 1f));
            bossText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Image track = Image("Boss Track", bossPanel, RuntimeSpriteAtlas.Solid(), new Color(.08f, .07f, .08f, .95f));
            SetRect(track.rectTransform, new Vector2(0f, 2f), new Vector2(260f, 4f), new Vector2(.5f, 0f));
            bossFill = Image("Boss Fill", track.rectTransform, RuntimeSpriteAtlas.Solid(), new Color(.92f, .3f, .2f));
            SetStretch(bossFill.rectTransform, 1f, 1f, 1f, 1f);
            bossFill.type = UnityEngine.UI.Image.Type.Filled;
            bossFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            bossFill.fillOrigin = 0;
            bossPanel.gameObject.SetActive(false);
        }

        private void BuildMinimap()
        {
            RectTransform panel = Panel("Minimap", safeArea, new Vector2(-8f, -8f), new Vector2(96f, 76f), new Vector2(1f, 1f));
            Accent(panel, new Color(.42f, .78f, .57f), false);
            Text title = Label("Map Label", panel, "AREA MAP", 9, TextAnchor.MiddleCenter, new Color(.62f, .82f, .69f));
            SetRect(title.rectTransform, new Vector2(0f, -9f), new Vector2(80f, 12f), new Vector2(.5f, 1f));
            GameObject content = UiObject("Map Content", panel);
            minimapContent = content.GetComponent<RectTransform>();
            SetRect(minimapContent, new Vector2(0f, -14f), new Vector2(80f, 44f), new Vector2(.5f, .5f));
        }

        private void BuildFooter()
        {
            RectTransform abilityPanel = Panel("Ability Build", safeArea, new Vector2(8f, 8f), new Vector2(230f, 22f), new Vector2(0f, 0f));
            abilityText = Label("Ability Text", abilityPanel, string.Empty, 9, TextAnchor.MiddleLeft, new Color(.8f, .87f, .83f));
            SetRect(abilityText.rectTransform, new Vector2(25f, 0f), new Vector2(60f, 18f), new Vector2(0f, .5f));
            abilityText.horizontalOverflow = HorizontalWrapMode.Wrap;
            for (int i = 0; i < 6; i++)
            {
                Image icon = Image("Ability Slot " + i, abilityPanel, RuntimeSpriteAtlas.AbilityIcon((Ability)i), Color.white);
                SetRect(icon.rectTransform, new Vector2(78f + i * 18f, 0f), new Vector2(16f, 16f), new Vector2(0f, .5f));
                icon.preserveAspect = true;
                abilityIcons.Add(icon);
                Text count = Label("Ability Count " + i, icon.transform, string.Empty, 8, TextAnchor.MiddleCenter, Color.white);
                SetRect(count.rectTransform, new Vector2(5f, -5f), new Vector2(9f, 9f), new Vector2(0f, 1f));
                count.color = new Color(1f, .9f, .3f);
                count.horizontalOverflow = HorizontalWrapMode.Wrap;
                abilityCounts.Add(count);
                count.gameObject.SetActive(false);
            }

            hintPanel = Panel("Control Hint", safeArea, new Vector2(-8f, 8f), new Vector2(265f, 22f), new Vector2(1f, 0f));
            hintText = Label("Hint", hintPanel, string.Empty, 8, TextAnchor.MiddleRight, new Color(.62f, .69f, .68f));
            SetStretch(hintText.rectTransform, 8f, 8f, 2f, 2f);

            bannerPanel = Panel("Banner", safeArea, new Vector2(0f, 40f), new Vector2(290f, 24f), new Vector2(.5f, 0f));
            bannerText = Label("Banner Text", bannerPanel, string.Empty, 11, TextAnchor.MiddleCenter, new Color(1f, .88f, .43f));
            SetStretch(bannerText.rectTransform, 8f, 8f, 2f, 2f);
            bannerPanel.gameObject.SetActive(false);
        }

        private void BuildTargetReticle()
        {
            Image reticle = Image("Auto Aim Reticle", safeArea, RuntimeSpriteAtlas.Ring(), new Color(1f, .85f, .25f));
            targetReticle = reticle.rectTransform;
            SetRect(targetReticle, Vector2.zero, new Vector2(32f, 32f), new Vector2(.5f, .5f));
            reticle.preserveAspect = true;
            targetReticle.gameObject.SetActive(false);
        }

        private void BuildDamageFlash()
        {
            damageFlash = Image("Damage Flash", canvas.transform, RuntimeSpriteAtlas.Solid(), new Color(1f, .12f, .08f, 0f));
            damageFlash.rectTransform.anchorMin = Vector2.zero;
            damageFlash.rectTransform.anchorMax = Vector2.one;
            damageFlash.rectTransform.offsetMin = Vector2.zero;
            damageFlash.rectTransform.offsetMax = Vector2.zero;
            damageFlash.raycastTarget = false;
            damageFlash.enabled = false;
            damageFlash.transform.SetAsLastSibling();
        }

        private void UpdateTarget(EnemyActor target, Camera gameCamera)
        {
            if (targetReticle == null) return;
            if (target == null || gameCamera == null)
            {
                targetReticle.gameObject.SetActive(false);
                return;
            }
            Vector3 screenPoint = gameCamera.WorldToScreenPoint(target.transform.position);
            if (screenPoint.z < 0f)
            {
                targetReticle.gameObject.SetActive(false);
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screenPoint, null, out Vector2 localPoint);
            targetReticle.anchoredPosition = localPoint + Vector2.up * 5f;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 7f) * .08f;
            targetReticle.localScale = Vector3.one * pulse;
            targetReticle.gameObject.SetActive(true);
        }

        private void RebuildMinimap(RoomGraph graph)
        {
            for (int i = minimapContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = minimapContent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            Vector2 origin = new Vector2(-22f, -10f);
            const float spacing = 22f;
            for (int i = 0; i < graph.Rooms.Count; i++)
            for (int j = i + 1; j < graph.Rooms.Count; j++)
            {
                if (!graph.AreConnected(i, j)) continue;
                RoomState first = graph.Rooms[i];
                RoomState second = graph.Rooms[j];
                bool discovered = first.visited || second.visited;
                if (!discovered) continue;
                Vector2 a = origin + new Vector2(first.gridX * spacing, first.gridY * spacing);
                Vector2 b = origin + new Vector2(second.gridX * spacing, second.gridY * spacing);
                CreateLine(minimapContent, a, b, new Color(.28f, .39f, .36f, .92f));
            }
            foreach (RoomState room in graph.Rooms)
            {
                bool discovered = room.visited || graph.Rooms.Any(other => other.visited && graph.AreConnected(other.id, room.id));
                if (!discovered) continue;
                Color color = room.id == graph.CurrentId ? new Color(1f, .84f, .24f) : room.cleared ? new Color(.34f, .86f, .51f) : room.visited ? new Color(.78f, .84f, .8f) : new Color(.37f, .47f, .44f);
                Image node = Image("Room " + room.id, minimapContent, room.id == graph.CurrentId ? RuntimeSpriteAtlas.Diamond() : RuntimeSpriteAtlas.SoftSquare(), color);
                SetRect(node.rectTransform, origin + new Vector2(room.gridX * spacing, room.gridY * spacing), room.kind == RoomKind.Boss ? new Vector2(11f, 11f) : new Vector2(9f, 9f), new Vector2(.5f, .5f));
            }
        }

        private static void CreateLine(RectTransform parent, Vector2 start, Vector2 end, Color color)
        {
            Image line = Image("Path", parent, RuntimeSpriteAtlas.Solid(), color);
            Vector2 delta = end - start;
            line.rectTransform.sizeDelta = new Vector2(delta.magnitude, 3f);
            line.rectTransform.anchoredPosition = (start + end) * .5f;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.transform.SetAsFirstSibling();
        }

        private void ApplySafeArea(bool force)
        {
            Rect area = Screen.safeArea;
            if (!force && area == lastSafeArea) return;
            lastSafeArea = area;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            safeArea.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
            safeArea.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        private static int MapSignature(RoomGraph graph)
        {
            unchecked
            {
                int hash = graph.CurrentId * 397 ^ (int)graph.Stage;
                foreach (RoomState room in graph.Rooms) hash = hash * 31 + (room.visited ? 1 : 0) + (room.cleared ? 2 : 0);
                return hash;
            }
        }

        private static bool IsBoss(EnemyActor enemy)
        {
            return enemy != null && (enemy.Type == EnemyType.TreeSpirit || enemy.Type == EnemyType.GiantBat || enemy.Type == EnemyType.Morkan);
        }

        private static string ShortAbilityName(Ability ability)
        {
            switch (ability)
            {
                case Ability.Attack: return "ATK";
                case Ability.Speed: return "SPD";
                case Ability.Pierce: return "PIERCE";
                case Ability.Reflect: return "BOUNCE";
                case Ability.Heal: return "HEAL";
                default: return "MOVE";
            }
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
        {
            Image image = Image(name, parent, RuntimeSpriteAtlas.SoftSquare(), new Color(.025f, .045f, .045f, .82f));
            image.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(image.rectTransform, anchoredPosition, size, pivot);
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static void Accent(RectTransform panel, Color color, bool left)
        {
            Image accent = Image("Accent", panel, RuntimeSpriteAtlas.Solid(), color);
            if (left) SetRect(accent.rectTransform, new Vector2(2f, 0f), new Vector2(3f, panel.sizeDelta.y - 4f), new Vector2(0f, .5f));
            else SetRect(accent.rectTransform, new Vector2(-2f, 0f), new Vector2(3f, panel.sizeDelta.y - 4f), new Vector2(1f, .5f));
        }

        private static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = UiObject(name, parent);
            UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            GameObject go = UiObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = Font();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Font Font()
        {
            return GameFont.Get();
        }

        private static GameObject UiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = pivot;
            rect.anchorMax = pivot;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
