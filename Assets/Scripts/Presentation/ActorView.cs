using UnityEngine;

namespace SoulArcher
{
    public readonly struct ActorVisualProfile
    {
        public readonly Color FallbackColor;
        public readonly Color OutlineColor;
        public readonly Color AccentColor;
        public readonly Vector2 ShadowScale;
        public readonly float FramesPerSecond;

        public ActorVisualProfile(Color fallbackColor, Color outlineColor, Color accentColor, Vector2 shadowScale, float framesPerSecond)
        {
            FallbackColor = fallbackColor;
            OutlineColor = outlineColor;
            AccentColor = accentColor;
            ShadowScale = shadowScale;
            FramesPerSecond = framesPerSecond;
        }

        public static ActorVisualProfile Player()
        {
            return new ActorVisualProfile(new Color(.96f, .74f, .28f), new Color(.08f, .12f, .1f, .95f), new Color(1f, .87f, .34f), new Vector2(.92f, .32f), 8f);
        }

        public static ActorVisualProfile Enemy(EnemyType type)
        {
            Color fallback;
            Color accent;
            switch (type)
            {
                case EnemyType.Wolf: fallback = new Color(.61f, .69f, .77f); accent = new Color(.82f, .9f, 1f); break;
                case EnemyType.Mushroom: fallback = new Color(.83f, .24f, .31f); accent = new Color(.78f, .95f, .43f); break;
                case EnemyType.RockBeetle: fallback = new Color(.48f, .39f, .3f); accent = new Color(.88f, .64f, .34f); break;
                case EnemyType.CrystalSpirit: fallback = new Color(.25f, .79f, .96f); accent = new Color(.65f, 1f, 1f); break;
                case EnemyType.ShieldSkeleton: fallback = new Color(.8f, .8f, .73f); accent = new Color(.61f, .72f, .91f); break;
                case EnemyType.DarkMage: fallback = new Color(.54f, .19f, .76f); accent = new Color(.96f, .43f, .91f); break;
                case EnemyType.TreeSpirit: fallback = new Color(.31f, .62f, .2f); accent = new Color(.68f, .94f, .35f); break;
                case EnemyType.GiantBat: fallback = new Color(.63f, .17f, .62f); accent = new Color(.96f, .47f, .9f); break;
                default: fallback = new Color(.9f, .21f, .17f); accent = new Color(1f, .65f, .25f); break;
            }
            bool boss = type == EnemyType.TreeSpirit || type == EnemyType.GiantBat || type == EnemyType.Morkan;
            return new ActorVisualProfile(fallback, new Color(.075f, .07f, .09f, .96f), accent, boss ? new Vector2(1.25f, .38f) : new Vector2(.82f, .28f), boss ? 5f : 6f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ActorView : MonoBehaviour
    {
        private SpriteRenderer mainRenderer;
        private SpriteRenderer outlineRenderer;
        private SpriteRenderer shadowRenderer;
        private SpriteRenderer targetRenderer;
        private SpriteRenderer healthBackground;
        private SpriteRenderer healthFill;
        private Sprite[] idleFrames;
        private Sprite[] movingFrames;
        private SpriteSheetLayout idleLayout;
        private SpriteSheetLayout movingLayout;
        private ActorVisualProfile profile;
        private Color normalColor;
        private Vector3 baseScale;
        private Vector2 facing = Vector2.down;
        private float flashUntil;
        private float pulse;
        private float displayedHealth = 1f;
        private bool healthVisible;
        public bool IsWarning { get; set; }

        public void Initialize(SpriteRenderer renderer, string resourcePath, SpriteSheetLayout sheetLayout, ActorVisualProfile visualProfile, bool hasHealthBar)
        {
            Initialize(renderer, resourcePath, sheetLayout, resourcePath, sheetLayout, visualProfile, hasHealthBar);
        }

        public void Initialize(
            SpriteRenderer renderer,
            string idleResourcePath,
            SpriteSheetLayout idleSheetLayout,
            string movingResourcePath,
            SpriteSheetLayout movingSheetLayout,
            ActorVisualProfile visualProfile,
            bool hasHealthBar)
        {
            mainRenderer = renderer;
            idleLayout = idleSheetLayout;
            movingLayout = movingSheetLayout;
            profile = visualProfile;
            baseScale = transform.localScale;
            idleFrames = ActorFrameSources.Current.Load(idleResourcePath, idleLayout);
            movingFrames = ActorFrameSources.Current.Load(movingResourcePath, movingLayout);
            if (idleFrames.Length > 0)
            {
                mainRenderer.sprite = idleFrames[0];
                normalColor = Color.white;
            }
            else
            {
                normalColor = profile.FallbackColor;
            }
            mainRenderer.color = normalColor;

            outlineRenderer = CreateLayer("Ink Outline", mainRenderer.sprite, profile.OutlineColor, Vector3.zero, Vector3.one * 1.06f, -1);
            shadowRenderer = CreateLayer("Contact Shadow", RuntimeSpriteAtlas.Shadow(), new Color(.01f, .02f, .02f, .48f), new Vector3(0f, -.38f, 0f), new Vector3(profile.ShadowScale.x, profile.ShadowScale.y, 1f), -2);
            targetRenderer = CreateLayer("Target Ring", RuntimeSpriteAtlas.Ring(), profile.AccentColor, new Vector3(0f, -.28f, 0f), Vector3.one * 1.05f, -1);
            targetRenderer.enabled = false;

            if (hasHealthBar) BuildHealthBar();
            RefreshSorting();
        }

        public int IdleFrameCount { get { return idleFrames == null ? 0 : idleFrames.Length; } }
        public int MovingFrameCount { get { return movingFrames == null ? 0 : movingFrames.Length; } }
        public bool UsingMovingFrames { get; private set; }

        public void Tick(Vector2 movementOrFacing, bool moving, float now)
        {
            if (movementOrFacing.sqrMagnitude > .001f) facing = movementOrFacing.normalized;
            Sprite[] activeFrames = moving && movingFrames != null && movingFrames.Length > 0 ? movingFrames : idleFrames;
            SpriteSheetLayout activeLayout = moving && movingFrames != null && movingFrames.Length > 0 ? movingLayout : idleLayout;
            UsingMovingFrames = moving && movingFrames != null && movingFrames.Length > 0;
            if (activeFrames != null && activeFrames.Length > 0)
            {
                int direction = activeLayout.DirectionCount > 1 ? DirectionalFrames.DirectionIndex(facing) : 0;
                int phase = moving ? Mathf.FloorToInt(now * profile.FramesPerSecond) : 0;
                int index = Mathf.Clamp(activeLayout.FrameIndex(direction, phase), 0, activeFrames.Length - 1);
                mainRenderer.sprite = activeFrames[index];
                outlineRenderer.sprite = mainRenderer.sprite;
            }

            float breathe = moving ? Mathf.Sin(now * 13f) * .025f : Mathf.Sin(now * 3.2f) * .012f;
            if (IsWarning) breathe += Mathf.Sin(now * 35f) * 0.06f;
            float punch = 1f + pulse * .11f;
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * (punch + breathe), Mathf.Clamp01(Time.unscaledDeltaTime * 20f));
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 8f);
            RefreshSorting();
        }

        public void PlayAttack()
        {
            pulse = Mathf.Max(pulse, .65f);
        }

        public void PlayHit(float health01)
        {
            flashUntil = Time.unscaledTime + .15f;
            pulse = 1.2f;
            displayedHealth = Mathf.Clamp01(health01);
            healthVisible = true;
            RefreshHealthBar();
        }

        public void SetTargeted(bool targeted)
        {
            if (targetRenderer != null) targetRenderer.enabled = targeted;
        }

        private void LateUpdate()
        {
            if (mainRenderer == null) return;
            bool flashing = Time.unscaledTime < flashUntil;
            Color currentColor = normalColor;
            if (IsWarning)
            {
                float warningPulse = 0.5f + Mathf.Sin(Time.unscaledTime * 20f) * 0.5f;
                currentColor = Color.Lerp(normalColor, new Color(1f, 0.2f, 0.2f), warningPulse);
            }
            mainRenderer.color = flashing ? Color.white : currentColor;
            if (outlineRenderer != null)
            {
                float outlineScale = IsWarning ? (1.08f + Mathf.PingPong(Time.unscaledTime * 5f, 0.08f)) : 1.06f;
                outlineRenderer.transform.localScale = Vector3.one * outlineScale;
                outlineRenderer.color = flashing ? profile.AccentColor : (IsWarning ? new Color(1f, 0.05f, 0.05f) : profile.OutlineColor);
            }
            if (mainRenderer.transform != transform)
            {
                if (IsWarning)
                {
                    float shakeOffset = Mathf.Sin(Time.unscaledTime * 45f) * 0.05f;
                    mainRenderer.transform.localPosition = new Vector3(shakeOffset, 0f, 0f);
                }
                else
                {
                    mainRenderer.transform.localPosition = Vector3.zero;
                }
            }
            if (targetRenderer != null && targetRenderer.enabled)
            {
                float targetPulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * .07f;
                targetRenderer.transform.localScale = Vector3.one * targetPulse;
            }
            if (healthVisible && Time.unscaledTime > flashUntil + 2f)
            {
                healthVisible = false;
                RefreshHealthBar();
            }
        }

        private SpriteRenderer CreateLayer(string layerName, Sprite sprite, Color color, Vector3 localPosition, Vector3 localScale, int orderOffset)
        {
            GameObject layer = new GameObject(layerName);
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = localScale;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = mainRenderer.sortingOrder + orderOffset;
            return renderer;
        }

        private void BuildHealthBar()
        {
            healthBackground = CreateLayer("Health Background", RuntimeSpriteAtlas.Solid(), new Color(.035f, .04f, .045f, .92f), new Vector3(0f, .72f, 0f), new Vector3(.72f, .07f, 1f), 4);
            healthFill = CreateLayer("Health Fill", RuntimeSpriteAtlas.Solid(), new Color(.37f, .9f, .42f), new Vector3(-.35f, .72f, 0f), new Vector3(.68f, .035f, 1f), 5);
            healthFill.transform.localPosition = new Vector3(-.34f, .72f, 0f);
            healthBackground.enabled = false;
            healthFill.enabled = false;
        }

        private void RefreshHealthBar()
        {
            if (healthBackground == null || healthFill == null) return;
            healthBackground.enabled = healthVisible;
            healthFill.enabled = healthVisible;
            float width = .68f * displayedHealth;
            healthFill.transform.localScale = new Vector3(width, .035f, 1f);
            healthFill.transform.localPosition = new Vector3(-.34f + width * .5f, .72f, 0f);
            healthFill.color = Color.Lerp(new Color(.9f, .22f, .2f), new Color(.37f, .9f, .42f), displayedHealth);
        }

        private void RefreshSorting()
        {
            int order = SpriteSorting.Actor + Mathf.RoundToInt((RoomExitRules.PlayerMaxY - transform.position.y) * 10f);
            mainRenderer.sortingOrder = order;
            if (outlineRenderer != null) outlineRenderer.sortingOrder = order - 1;
            if (shadowRenderer != null) shadowRenderer.sortingOrder = order - 2;
            if (targetRenderer != null) targetRenderer.sortingOrder = order - 1;
            if (healthBackground != null) healthBackground.sortingOrder = order + 4;
            if (healthFill != null) healthFill.sortingOrder = order + 5;
        }
    }
}
