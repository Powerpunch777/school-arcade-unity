using UnityEngine;

namespace SoulArcher
{
    [DisallowMultipleComponent]
    public sealed class ResponsiveCamera2D : MonoBehaviour
    {
        private Camera targetCamera;
        private float baseSize = 5.2f;
        private float currentBaseSize = 5.2f;
        private int lastWidth;
        private int lastHeight;
        private SoulArcherGame game;

        public void Initialize(Camera cameraToControl, float referenceSize = 5.2f)
        {
            targetCamera = cameraToControl;
            baseSize = referenceSize;
            currentBaseSize = referenceSize;
            Refresh(true);
        }

        private void Update()
        {
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (targetCamera == null) return;

            float targetBaseSize = 5.2f;
            if (game == null) game = FindAnyObjectByType<SoulArcherGame>();
            if (game != null && game.CurrentGraph != null && game.CurrentGraph.Current != null)
            {
                if ((game.CurrentGraph.Stage == StageId.Forest || game.CurrentGraph.Stage == StageId.Cave || game.CurrentGraph.Stage == StageId.Castle) &&
                    game.CurrentGraph.Current.kind == RoomKind.Boss &&
                    game.ActiveEnemyCount > 0)
                {
                    PlayerActor player = FindAnyObjectByType<PlayerActor>();
                    EnemyActor boss = null;
                    var enemies = FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
                    foreach (var enemy in enemies)
                    {
                        if (enemy != null && (enemy.Type == EnemyType.TreeSpirit || enemy.Type == EnemyType.GiantBat || enemy.Type == EnemyType.Morkan))
                        {
                            boss = enemy;
                            break;
                        }
                    }
                    if (player != null && boss != null)
                    {
                        float distance = Vector2.Distance(player.transform.position, boss.transform.position);
                        if (distance <= 4.5f)
                        {
                            targetBaseSize = Mathf.Lerp(2.8f, 3.5f, Mathf.InverseLerp(2f, 4.5f, distance));
                        }
                        else
                        {
                            targetBaseSize = Mathf.Lerp(3.5f, 5.2f, Mathf.InverseLerp(4.5f, 9.5f, distance));
                        }
                    }
                    else
                    {
                        targetBaseSize = 3.6f;
                    }
                }
            }

            if (Application.isPlaying)
            {
                currentBaseSize = Mathf.MoveTowards(currentBaseSize, targetBaseSize, Time.deltaTime * 2.5f);
            }
            else
            {
                currentBaseSize = targetBaseSize;
            }

            if (!force && lastWidth == Screen.width && lastHeight == Screen.height && Mathf.Approximately(currentBaseSize, baseSize)) return;

            baseSize = currentBaseSize;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            float aspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
            float rawSize = ResponsiveViewport.OrthographicSize(baseSize, aspect);
            if (Application.isPlaying)
            {
                // Align orthographicSize to integer scaling factors to keep pixel scale clean and avoid shimmering
                float ppPpu = 32f;
                float pixelScale = Screen.height / (2f * rawSize * ppPpu);
                float roundedScale = Mathf.Max(1f, Mathf.Round(pixelScale));
                targetCamera.orthographicSize = Screen.height / (2f * roundedScale * ppPpu);
            }
            else
            {
                targetCamera.orthographicSize = rawSize;
            }
        }
    }
}
