using UnityEngine;

namespace SoulArcher
{
    [DisallowMultipleComponent]
    public sealed class GameFeelService : MonoBehaviour
    {
        private Camera gameCamera;
        private Vector3 cameraHome = new Vector3(0f, 0f, -10f);
        private float shakeRemaining;
        private float shakeDuration;
        private float shakeStrength;
        private float hitStopRemaining;
        private float resumeTimeScale = 1f;
        private bool ownsTimeScale;
        private static Material spriteMaterial;
        private SoulArcherGame game;

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial != null) return spriteMaterial;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                spriteMaterial = new Material(shader)
                {
                    name = "Soul Archer Runtime Sprite Material",
                    hideFlags = HideFlags.DontSave
                };
                return spriteMaterial;
            }
        }

        public void Initialize(Camera targetCamera)
        {
            gameCamera = targetCamera;
            if (gameCamera != null)
            {
                cameraHome = new Vector3(0f, 0f, gameCamera.transform.position.z <= -1f ? gameCamera.transform.position.z : -10f);
                gameCamera.transform.position = cameraHome;
            }
        }

        public void ProjectileImpact(Vector3 position, Color color, bool heavy = false)
        {
            Burst(position, color, heavy ? 18 : 9, heavy ? .13f : .08f, heavy ? 3.8f : 2.6f);
            RequestShake(heavy ? .13f : .055f, heavy ? .18f : .08f);
            RequestHitStop(heavy ? .055f : .025f);
        }

        public void PlayerHit(Vector3 position)
        {
            Burst(position, new Color(1f, .28f, .2f), 20, .11f, 3.8f);
            RequestShake(.24f, .18f);
            RequestHitStop(.07f);
        }

        public void EnemyDeath(Vector3 position, Color color, int souls)
        {
            Burst(position, color, 26, .12f, 4.3f);
            Burst(position, new Color(1f, .85f, .3f), 12, .07f, 2.9f);
            RequestShake(.16f, .13f);
            RequestHitStop(.045f);
            if (souls > 0) SpawnSoulWisp(position, souls);
        }

        public void ProjectileReflect(Vector3 position)
        {
            Burst(position, new Color(.45f, .9f, 1f), 10, .06f, 2.6f);
            RequestShake(.04f, .045f);
        }

        public void RequestShake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeRemaining = Mathf.Max(shakeRemaining, duration);
        }

        private void RequestHitStop(float duration)
        {
            if (!Application.isPlaying || Time.timeScale <= 0f) return;
            if (!ownsTimeScale)
            {
                resumeTimeScale = Time.timeScale;
                ownsTimeScale = true;
            }
            hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
            Time.timeScale = Mathf.Min(Time.timeScale, .08f);
        }

        private void Update()
        {
            if (game == null) game = GetComponent<SoulArcherGame>();

            Vector3 targetHome = new Vector3(0f, 0f, cameraHome.z);
            if (game != null && game.CurrentGraph != null && game.CurrentGraph.Current != null)
            {
                if ((game.CurrentGraph.Stage == StageId.Forest || game.CurrentGraph.Stage == StageId.Cave || game.CurrentGraph.Stage == StageId.Castle) &&
                    game.CurrentGraph.Current.kind == RoomKind.Boss &&
                    game.ActiveEnemyCount > 0)
                {
                    PlayerActor player = null;
                    EnemyActor boss = null;
                    var players = FindObjectsByType<PlayerActor>();
                    if (players.Length > 0) player = players[0];
                    var enemies = FindObjectsByType<EnemyActor>();
                    foreach (var enemy in enemies)
                    {
                        if (enemy != null && (enemy.Type == EnemyType.TreeSpirit || enemy.Type == EnemyType.GiantBat || enemy.Type == EnemyType.Morkan))
                        {
                            boss = enemy;
                            break;
                        }
                    }

                    if (player != null && boss != null && gameCamera != null)
                    {
                        Vector3 focusPoint = (player.transform.position + boss.transform.position) * 0.5f;
                        focusPoint.y += 0.65f;
                        float aspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
                        float H = gameCamera.orthographicSize;
                        float W = H * aspect;
                        float clampX = Mathf.Max(0f, 9f - W);
                        float clampY = Mathf.Max(0f, 4.5f - H);
                        targetHome.x = Mathf.Clamp(focusPoint.x, -clampX, clampX);
                        targetHome.y = Mathf.Clamp(focusPoint.y, -clampY, clampY);
                    }
                }
            }

            if (Application.isPlaying)
            {
                cameraHome = Vector3.Lerp(cameraHome, targetHome, Time.deltaTime * 3f);
            }
            else
            {
                cameraHome = targetHome;
            }

            if (!ownsTimeScale) return;
            hitStopRemaining -= Time.unscaledDeltaTime;
            if (hitStopRemaining > 0f) return;
            Time.timeScale = resumeTimeScale;
            ownsTimeScale = false;
        }

        private void LateUpdate()
        {
            if (gameCamera == null) return;
            if (shakeRemaining <= 0f)
            {
                gameCamera.transform.position = cameraHome;
                return;
            }
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
            float envelope = shakeDuration <= 0f ? 0f : shakeRemaining / shakeDuration;
            float time = Time.unscaledTime * 67f;
            Vector2 offset = new Vector2(Mathf.Sin(time * 1.13f), Mathf.Cos(time * 1.71f)) * shakeStrength * envelope;
            gameCamera.transform.position = cameraHome + (Vector3)offset;
        }

        private void OnDisable()
        {
            if (ownsTimeScale)
            {
                Time.timeScale = resumeTimeScale;
                ownsTimeScale = false;
            }
            if (gameCamera != null) gameCamera.transform.position = cameraHome;
        }

        private void Burst(Vector3 position, Color color, int count, float size, float speed)
        {
            if (!Application.isPlaying) return;
            GameObject effect = new GameObject("Impact Burst");
            effect.transform.SetParent(transform, true);
            effect.transform.position = position;
            effect.AddComponent<SpriteBurstEffect>().Initialize(color, count, size, speed);
        }

        private void SpawnSoulWisp(Vector3 position, int amount)
        {
            if (!Application.isPlaying) return;
            Vector3 target = new Vector3(-7.35f, 3.75f, 0f);
            if (gameCamera != null)
            {
                Vector3 viewportTarget = gameCamera.ViewportToWorldPoint(new Vector3(.12f, .82f, Mathf.Abs(gameCamera.transform.position.z)));
                target = new Vector3(viewportTarget.x, viewportTarget.y, 0f);
            }
            GameObject wisp = new GameObject("Soul Reward +" + amount);
            wisp.transform.SetParent(transform, true);
            wisp.transform.position = position;
            SpriteRenderer renderer = wisp.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteAtlas.Diamond();
            renderer.color = new Color(.35f, .95f, 1f);
            renderer.sortingOrder = SpriteSorting.Projectile + 8;
            TrailRenderer trail = wisp.AddComponent<TrailRenderer>();
            trail.time = .24f;
            trail.startWidth = .12f;
            trail.endWidth = 0f;
            trail.minVertexDistance = .03f;
            trail.sortingOrder = SpriteSorting.Projectile + 7;
            if (SpriteMaterial != null) trail.sharedMaterial = SpriteMaterial;
            trail.startColor = new Color(.35f, .95f, 1f, .85f);
            trail.endColor = new Color(.35f, .95f, 1f, 0f);
            wisp.AddComponent<SoulWispEffect>().Initialize(position, target, amount);
        }
    }

    public sealed class SpriteBurstEffect : MonoBehaviour
    {
        private Transform[] particles;
        private SpriteRenderer[] renderers;
        private Vector3[] velocities;
        private float[] angularVelocities;
        private Color color;
        private float elapsed;
        private const float Duration = .38f;

        public void Initialize(Color burstColor, int count, float size, float speed)
        {
            color = burstColor;
            count = Mathf.Clamp(count, 1, 40);
            particles = new Transform[count];
            renderers = new SpriteRenderer[count];
            velocities = new Vector3[count];
            angularVelocities = new float[count];
            System.Random random = new System.Random(count * 7919 + Mathf.RoundToInt(Time.unscaledTime * 1000f));
            for (int i = 0; i < count; i++)
            {
                GameObject particle = new GameObject("Pixel " + i);
                particle.transform.SetParent(transform, false);
                float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float velocity = speed * (.45f + (float)random.NextDouble() * .65f);
                velocities[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * velocity;
                angularVelocities[i] = -240f + (float)random.NextDouble() * 480f;
                float scale = size * (.55f + (float)random.NextDouble() * .75f);
                particle.transform.localScale = Vector3.one * scale;
                SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
                renderer.sprite = i % 3 == 0 ? RuntimeSpriteAtlas.SoftSquare() : RuntimeSpriteAtlas.Diamond();
                renderer.color = i % 4 == 0 ? Color.Lerp(color, Color.white, .65f) : color;
                renderer.sortingOrder = SpriteSorting.Projectile + 5 + i % 3;
                particles[i] = particle.transform;
                renderers[i] = renderer;
            }
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < particles.Length; i++)
            {
                velocities[i] *= Mathf.Pow(.04f, delta);
                particles[i].position += velocities[i] * delta;
                particles[i].Rotate(0f, 0f, angularVelocities[i] * delta);
                particles[i].localScale *= Mathf.Pow(.075f, delta);
                Color faded = renderers[i].color;
                faded.a = 1f - t;
                renderers[i].color = faded;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }

    public sealed class SoulWispEffect : MonoBehaviour
    {
        private Vector3 start;
        private Vector3 target;
        private float elapsed;
        private float duration;

        public void Initialize(Vector3 startPosition, Vector3 targetPosition, int amount)
        {
            start = startPosition;
            target = targetPosition;
            duration = .42f + Mathf.Min(amount, 5) * .035f;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.15f;
            transform.position = Vector3.Lerp(start, target, eased) + arc;
            transform.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * .45f);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProjectileView : MonoBehaviour
    {
        private SpriteRenderer mainRenderer;
        private SpriteRenderer glowRenderer;
        private TrailRenderer trail;
        private Color mainColor;
        private bool hostile;
        private EnemyProjectileStyle style;
        private Vector3 mainScale = Vector3.one;
        private Vector3 glowScale = Vector3.one;

        public void Initialize(SpriteRenderer renderer, bool isHostile, EnemyProjectileStyle projectileStyle = EnemyProjectileStyle.EmberBolt)
        {
            mainRenderer = renderer;
            hostile = isHostile;
            style = projectileStyle;
            mainColor = hostile ? EnemyProjectileRules.ColorFor(style) : new Color(1f, .88f, .35f);
            if (hostile)
            {
                mainRenderer.sprite = SpriteFor(style);
                mainScale = Vector3.one * EnemyProjectileRules.ScaleFor(style);
                glowScale = Vector3.one * (EnemyProjectileRules.ScaleFor(style) * 2.05f);
                mainRenderer.transform.localScale = mainScale;
            }
            else if (mainRenderer.sprite == null)
            {
                mainRenderer.sprite = RuntimeSpriteAtlas.Diamond();
                glowScale = new Vector3(1.5f, .7f, 1f);
            }
            mainRenderer.color = mainColor;
            mainRenderer.sortingOrder = SpriteSorting.Projectile;

            GameObject glow = new GameObject("Projectile Glow");
            glow.transform.SetParent(transform, false);
            glow.transform.localScale = hostile ? glowScale : new Vector3(1.5f, .7f, 1f);
            glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = hostile ? RuntimeSpriteAtlas.Orb() : RuntimeSpriteAtlas.Diamond();
            glowRenderer.color = new Color(mainColor.r, mainColor.g, mainColor.b, .28f);
            glowRenderer.sortingOrder = SpriteSorting.Projectile - 1;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = hostile ? .22f : .14f;
            trail.startWidth = hostile ? EnemyProjectileRules.ScaleFor(style) * .52f : .1f;
            trail.endWidth = 0f;
            trail.minVertexDistance = .035f;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.sortingOrder = SpriteSorting.Projectile - 2;
            trail.startColor = new Color(mainColor.r, mainColor.g, mainColor.b, .8f);
            trail.endColor = new Color(mainColor.r, mainColor.g, mainColor.b, 0f);
            if (GameFeelService.SpriteMaterial != null) trail.sharedMaterial = GameFeelService.SpriteMaterial;
        }

        public void Tick()
        {
            if (glowRenderer == null) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * (hostile ? 10f : 18f)) * .15f;
            glowRenderer.transform.localScale = (hostile ? glowScale : new Vector3(1.5f, .7f, 1f)) * pulse;
            if (hostile && mainRenderer != null) mainRenderer.transform.localScale = mainScale * (1f + Mathf.Sin(Time.unscaledTime * 14f) * .06f);
        }

        private static Sprite SpriteFor(EnemyProjectileStyle projectileStyle)
        {
            switch (projectileStyle)
            {
                case EnemyProjectileStyle.PoisonSpore: return RuntimeSpriteAtlas.Spore();
                case EnemyProjectileStyle.Boulder: return RuntimeSpriteAtlas.Boulder();
                case EnemyProjectileStyle.CrystalShard: return RuntimeSpriteAtlas.CrystalShard();
                case EnemyProjectileStyle.BoneLance: return RuntimeSpriteAtlas.Lance();
                case EnemyProjectileStyle.VoidOrb: return RuntimeSpriteAtlas.Orb();
                case EnemyProjectileStyle.ThornSeed: return RuntimeSpriteAtlas.Thorn();
                case EnemyProjectileStyle.SonicCrescent: return RuntimeSpriteAtlas.Crescent();
                case EnemyProjectileStyle.ChaosFlame: return RuntimeSpriteAtlas.Flame();
                default: return RuntimeSpriteAtlas.Diamond();
            }
        }
    }
}
