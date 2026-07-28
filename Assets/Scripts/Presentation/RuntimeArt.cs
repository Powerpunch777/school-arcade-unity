using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulArcher
{
    public readonly struct SpriteSheetLayout
    {
        public readonly int CellWidth;
        public readonly int CellHeight;
        public readonly int FramesPerDirection;
        public readonly int DirectionCount;
        public readonly float PixelsPerUnit;

        public SpriteSheetLayout(int cellWidth, int cellHeight, int framesPerDirection, int directionCount, float pixelsPerUnit)
        {
            CellWidth = Mathf.Max(1, cellWidth);
            CellHeight = Mathf.Max(1, cellHeight);
            FramesPerDirection = Mathf.Max(1, framesPerDirection);
            DirectionCount = Mathf.Max(1, directionCount);
            PixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        }

        public int FrameIndex(int direction, int frame)
        {
            int safeDirection = Mathf.Clamp(direction, 0, DirectionCount - 1);
            int safeFrame = ((frame % FramesPerDirection) + FramesPerDirection) % FramesPerDirection;
            return safeDirection * FramesPerDirection + safeFrame;
        }
    }

    public interface IActorFrameSource
    {
        Sprite[] Load(string resourcePath, SpriteSheetLayout layout);
    }

    public static class ActorFrameSources
    {
        private static IActorFrameSource current;

        public static IActorFrameSource Current
        {
            get { return current ?? (current = new ResourcesActorFrameSource()); }
            set { current = value; }
        }
    }

    public sealed class ResourcesActorFrameSource : IActorFrameSource
    {
        private static readonly string[] DirectionSuffixes =
        {
            "south", "south-east", "east", "north-east",
            "north", "north-west", "west", "south-west"
        };
        private readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        public Sprite[] Load(string resourcePath, SpriteSheetLayout layout)
        {
            string key = resourcePath + ":" + layout.CellWidth + ":" + layout.CellHeight + ":" + layout.FramesPerDirection + ":" + layout.DirectionCount;
            if (cache.TryGetValue(key, out Sprite[] cached)) return cached;

            if (layout.DirectionCount > 1)
            {
                Sprite[] directional = LoadDirectional(resourcePath, layout);
                if (directional.Length > 0)
                {
                    cache[key] = directional;
                    return directional;
                }
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Sprite[] directional = LoadDirectional(resourcePath, layout);
                if (directional.Length > 0) cache[key] = directional;
                return directional;
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            int columns = Mathf.Max(1, texture.width / layout.CellWidth);
            int rows = Mathf.Max(1, texture.height / layout.CellHeight);
            int requested = layout.FramesPerDirection * layout.DirectionCount;
            int count = Mathf.Min(requested, columns * rows);
            Sprite[] frames = new Sprite[count];
            for (int index = 0; index < count; index++)
            {
                int column = index % columns;
                int rowFromBottom = index / columns;
                frames[index] = Sprite.Create(
                    texture,
                    new Rect(column * layout.CellWidth, rowFromBottom * layout.CellHeight, layout.CellWidth, layout.CellHeight),
                    new Vector2(.5f, .42f),
                    layout.PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                frames[index].name = resourcePath + "_frame_" + index;
            }

            cache[key] = frames;
            return frames;
        }

        private static Sprite[] LoadDirectional(string resourcePath, SpriteSheetLayout layout)
        {
            if (layout.DirectionCount > DirectionSuffixes.Length) return Array.Empty<Sprite>();
            var textures = new Texture2D[layout.DirectionCount];
            for (int direction = 0; direction < textures.Length; direction++)
            {
                textures[direction] = Resources.Load<Texture2D>(resourcePath + "_" + DirectionSuffixes[direction]);
                if (textures[direction] == null) return Array.Empty<Sprite>();
                textures[direction].filterMode = FilterMode.Point;
                textures[direction].wrapMode = TextureWrapMode.Clamp;
            }

            var frames = new Sprite[layout.DirectionCount * layout.FramesPerDirection];
            for (int direction = 0; direction < textures.Length; direction++)
            {
                Texture2D texture = textures[direction];
                int columns = Mathf.Max(1, texture.width / layout.CellWidth);
                int rows = Mathf.Max(1, texture.height / layout.CellHeight);
                if (columns * rows < layout.FramesPerDirection) return Array.Empty<Sprite>();
                for (int frame = 0; frame < layout.FramesPerDirection; frame++)
                {
                    int column = frame % columns;
                    int rowFromBottom = frame / columns;
                    int index = direction * layout.FramesPerDirection + frame;
                    frames[index] = Sprite.Create(
                        texture,
                        new Rect(column * layout.CellWidth, rowFromBottom * layout.CellHeight, layout.CellWidth, layout.CellHeight),
                        new Vector2(.5f, .42f),
                        layout.PixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                    frames[index].name = resourcePath + "_" + DirectionSuffixes[direction] + "_frame_" + frame;
                }
            }
            return frames;
        }
    }

    public static class RuntimeSpriteAtlas
    {
        private static readonly Dictionary<string, Sprite> Cells = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> Textures = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> Shapes = new Dictionary<string, Sprite>();

        public static Sprite LoadCell(string resourcePath, int column, int rowFromTop, int cellWidth = 16, int cellHeight = 16, float pixelsPerUnit = 16f)
        {
            string key = resourcePath + ":" + column + ":" + rowFromTop + ":" + cellWidth + ":" + cellHeight + ":" + pixelsPerUnit;
            if (Cells.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            int x = Mathf.Clamp(column * cellWidth, 0, Mathf.Max(0, texture.width - cellWidth));
            int y = Mathf.Clamp(texture.height - (rowFromTop + 1) * cellHeight, 0, Mathf.Max(0, texture.height - cellHeight));
            Sprite sprite = Sprite.Create(texture, new Rect(x, y, cellWidth, cellHeight), new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath + "_" + column + "_" + rowFromTop;
            Cells[key] = sprite;
            return sprite;
        }

        public static Sprite AbilityIcon(Ability ability)
        {
            string key = ability == Ability.Attack ? "attack" :
                ability == Ability.Speed ? "speed" :
                ability == Ability.Pierce ? "pierce" :
                ability == Ability.Reflect ? "reflect" :
                ability == Ability.Heal ? "heal" : "move";
            return LoadTextureSprite("Art/UI/AbilityV2/" + key, 96f);
        }

        private static Sprite LoadTextureSprite(string resourcePath, float pixelsPerUnit)
        {
            if (Textures.TryGetValue(resourcePath, out Sprite cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath.Replace("Art/UI/", string.Empty);
            Textures[resourcePath] = sprite;
            return sprite;
        }

        public static Sprite Solid()
        {
            return Shape("solid", 2, 2, (x, y, width, height) => true);
        }

        public static Sprite Shadow()
        {
            return Shape("shadow", 16, 8, (x, y, width, height) =>
            {
                float nx = (x + .5f - width * .5f) / (width * .5f);
                float ny = (y + .5f - height * .5f) / (height * .5f);
                return nx * nx + ny * ny <= 1f;
            });
        }

        public static Sprite Diamond()
        {
            return Shape("diamond", 12, 12, (x, y, width, height) =>
            {
                float dx = Mathf.Abs(x + .5f - width * .5f);
                float dy = Mathf.Abs(y + .5f - height * .5f);
                return dx + dy <= width * .44f;
            });
        }

        public static Sprite Spore()
        {
            return Shape("spore", 12, 12, (x, y, width, height) =>
            {
                float dx = x + .5f - width * .5f;
                float dy = y + .5f - height * .5f;
                bool body = dx * dx + dy * dy <= 25f;
                bool notch = (x == 3 && y == 7) || (x == 8 && y == 4);
                return body && !notch;
            });
        }

        public static Sprite Boulder()
        {
            return Shape("boulder", 14, 14, (x, y, width, height) =>
            {
                float dx = Mathf.Abs(x + .5f - width * .5f);
                float dy = Mathf.Abs(y + .5f - height * .5f);
                return dx * dx + dy * dy <= 36f && !(dx > 4.8f && dy > 3.8f);
            });
        }

        public static Sprite CrystalShard()
        {
            return Shape("crystal-shard", 18, 10, (x, y, width, height) =>
            {
                float nx = x + .5f - width * .5f;
                float ny = Mathf.Abs(y + .5f - height * .5f);
                return nx >= -7.5f && nx <= 8f - ny * 1.35f && ny <= 4.5f;
            });
        }

        public static Sprite Lance()
        {
            return Shape("lance", 18, 8, (x, y, width, height) =>
            {
                float ny = Mathf.Abs(y + .5f - height * .5f);
                return x >= 1 && x <= 16 - ny && ny <= 3.5f;
            });
        }

        public static Sprite Orb()
        {
            return Shape("orb", 14, 14, (x, y, width, height) =>
            {
                float dx = x + .5f - width * .5f;
                float dy = y + .5f - height * .5f;
                return dx * dx + dy * dy <= 36f;
            });
        }

        public static Sprite Thorn()
        {
            return Shape("thorn", 16, 10, (x, y, width, height) =>
            {
                float ny = Mathf.Abs(y + .5f - height * .5f);
                return (x >= 1 && x <= 12 - ny * 1.2f && ny <= 3.5f) ||
                    (x >= 9 && x <= 15 && ny <= 1.5f);
            });
        }

        public static Sprite Crescent()
        {
            return Shape("crescent", 16, 16, (x, y, width, height) =>
            {
                float dx = x + .5f - width * .5f;
                float dy = y + .5f - height * .5f;
                float outer = dx * dx + dy * dy;
                float innerX = dx - 2.7f;
                float inner = innerX * innerX + dy * dy;
                return outer <= 49f && inner >= 25f;
            });
        }

        public static Sprite Flame()
        {
            return Shape("flame", 14, 14, (x, y, width, height) =>
            {
                float nx = x + .5f - width * .5f;
                float ny = y + .5f - height * .5f;
                return (ny >= -5f && ny <= 4f && Mathf.Abs(nx) <= 4f - ny * .2f) ||
                    (ny > 3f && ny <= 6f && Mathf.Abs(nx - 1f) <= 1.5f);
            });
        }

        public static Sprite RootPillar()
        {
            return Shape("root-pillar", 18, 24, (x, y, width, height) =>
            {
                float center = width * .5f;
                if (y < 6) return Mathf.Abs(x + .5f - center) <= 4f - y * .3f;
                if (y < 16) return Mathf.Abs(x + .5f - center + Mathf.Sin(y * 1.7f) * 1.2f) <= 3.2f;
                return Mathf.Abs(x + .5f - center - 1f) <= Mathf.Max(.8f, 3f - (y - 16) * .35f);
            });
        }

        public static Sprite Slash()
        {
            return Shape("slash", 24, 16, (x, y, width, height) =>
            {
                float nx = x + .5f - width * .5f;
                float ny = y + .5f - height * .5f;
                float curve = ny + .035f * (nx + 5f) * (nx + 5f) - 2f;
                return nx >= -9f && nx <= 10f && curve >= -2.2f && curve <= 1.6f;
            });
        }

        public static Sprite Ring()
        {
            return Shape("ring", 24, 24, (x, y, width, height) =>
            {
                float nx = x + .5f - width * .5f;
                float ny = y + .5f - height * .5f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                return radius >= 9.25f && radius <= 10.75f && (Mathf.Abs(nx) > 5f || Mathf.Abs(ny) > 5f);
            });
        }

        public static Sprite Heart()
        {
            return Shape("heart", 16, 16, (x, y, width, height) =>
            {
                int flippedY = height - 1 - y;
                if (flippedY < 3 || flippedY > 13) return false;
                if (flippedY <= 6) return x >= 3 - (flippedY - 3) && x <= 12 + (flippedY - 3);
                int inset = (flippedY - 7) / 2;
                bool leftLobe = x >= 1 + inset && x <= 7;
                bool rightLobe = x >= 8 && x <= 14 - inset;
                return leftLobe || rightLobe;
            });
        }

        public static Sprite SoftSquare()
        {
            return Shape("soft-square", 8, 8, (x, y, width, height) => !((x == 0 || x == width - 1) && (y == 0 || y == height - 1)));
        }

        private static Sprite Shape(string key, int width, int height, Func<int, int, int, int, bool> predicate)
        {
            if (Shapes.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "Runtime " + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = predicate(x, y, width, height) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), Mathf.Max(width, height), 0, SpriteMeshType.FullRect);
            sprite.name = "Runtime " + key;
            sprite.hideFlags = HideFlags.DontSave;
            Shapes[key] = sprite;
            return sprite;
        }
    }

    public static class DirectionalFrames
    {
        // Existing and generated actor sheets use S, SE, E, NE, N, NW, W, SW ordering.
        public static int DirectionIndex(Vector2 direction)
        {
            if (direction.sqrMagnitude < .001f) return 0;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle >= -112.5f && angle < -67.5f) return 0;
            if (angle >= -67.5f && angle < -22.5f) return 1;
            if (angle >= -22.5f && angle < 22.5f) return 2;
            if (angle >= 22.5f && angle < 67.5f) return 3;
            if (angle >= 67.5f && angle < 112.5f) return 4;
            if (angle >= 112.5f && angle < 157.5f) return 5;
            if (angle >= 157.5f || angle < -157.5f) return 6;
            return 7;
        }
    }

    public static class ResponsiveViewport
    {
        public const float ReferenceAspect = 16f / 9f;

        public static float OrthographicSize(float baseSize, float currentAspect)
        {
            if (currentAspect <= .01f) return baseSize;
            return currentAspect >= ReferenceAspect ? baseSize : baseSize * ReferenceAspect / currentAspect;
        }
    }
}
