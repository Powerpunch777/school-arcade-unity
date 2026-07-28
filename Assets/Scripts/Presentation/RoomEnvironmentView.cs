using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulArcher
{
    public static class RoomEnvironmentView
    {
        private const int Columns = 18;
        private const int Rows = 9;

        public static void Build(Transform parent, StageId stage, RoomGraph graph, List<GameObject> roomObjects)
        {
            if (parent == null || graph == null || roomObjects == null) return;
            if (stage == StageId.Forest)
            {
                BuildForestPlate(parent, graph, roomObjects);
                if (Application.isPlaying) BuildAmbientMotes(parent, stage, roomObjects);
                return;
            }
            if (stage == StageId.Cave)
            {
                BuildCavePlate(parent, graph, roomObjects);
                if (Application.isPlaying) BuildAmbientMotes(parent, stage, roomObjects);
                return;
            }
            if (stage == StageId.Castle)
            {
                BuildCastlePlate(parent, graph, roomObjects);
                if (Application.isPlaying) BuildAmbientMotes(parent, stage, roomObjects);
                return;
            }
            BuildFloor(parent, stage, graph, roomObjects);
            BuildBoundaries(parent, stage, graph, roomObjects);
            BuildDecoration(parent, stage, graph, roomObjects);
            if (stage == StageId.Cave) BuildCaveSetPieces(parent, graph, roomObjects);
            if (Application.isPlaying) BuildAmbientMotes(parent, stage, roomObjects);
        }

        private static void BuildForestPlate(Transform parent, RoomGraph graph, List<GameObject> roomObjects)
        {
            Sprite plate = Resources.Load<Sprite>("Art/Environment/Forest/forest_room_style_anchor");
            GameObject plateObject = CreateSprite("Forest Full Plate", parent, plate, Color.white, Vector3.zero, Vector3.one, SpriteSorting.Background, roomObjects);
            SpriteRenderer plateRenderer = plateObject.GetComponent<SpriteRenderer>();
            plateRenderer.flipX = (graph.CurrentId & 1) != 0;
            plateRenderer.flipY = (graph.CurrentId & 2) != 0;

            BuildForestGateway(parent, graph, RoomDirection.North, roomObjects);
            BuildForestGateway(parent, graph, RoomDirection.East, roomObjects);
            BuildForestGateway(parent, graph, RoomDirection.South, roomObjects);
            BuildForestGateway(parent, graph, RoomDirection.West, roomObjects);
        }

        private static void BuildForestGateway(Transform parent, RoomGraph graph, RoomDirection direction, List<GameObject> roomObjects)
        {
            if (!graph.HasNeighbor(graph.CurrentId, direction)) return;
            string atlas = AtlasPath(StageId.Forest);
            int doorColumn = direction == RoomDirection.North ? 8 : direction == RoomDirection.South ? 9 : direction == RoomDirection.West ? 10 : 11;
            Vector3 position = direction == RoomDirection.North ? new Vector3(0f, RoomExitRules.TransitionY, 0f)
                : direction == RoomDirection.South ? new Vector3(0f, -RoomExitRules.TransitionY, 0f)
                : direction == RoomDirection.East ? new Vector3(RoomExitRules.TransitionX, 0f, 0f)
                : new Vector3(-RoomExitRules.TransitionX, 0f, 0f);
            Vector3 scale = direction == RoomDirection.North || direction == RoomDirection.South
                ? new Vector3(3f, 1f, 1f) : new Vector3(1f, 3f, 1f);
            CreateSprite("Forest Gateway " + direction, parent, RuntimeSpriteAtlas.LoadCell(atlas, doorColumn, 2, 16, 16, 32f), Color.white, position, scale, SpriteSorting.WallAndProps, roomObjects);
            CreateSprite("Forest Exit Marker " + direction, parent, RuntimeSpriteAtlas.Diamond(), new Color(1f, .88f, .25f, .95f), position, Vector3.one * .42f, SpriteSorting.WallAndProps + 1, roomObjects);
        }

        private static void BuildCavePlate(Transform parent, RoomGraph graph, List<GameObject> roomObjects)
        {
            Sprite plate = Resources.Load<Sprite>("Art/Environment/Cave/cave_room_style_anchor");
            GameObject plateObject = CreateSprite("Cave Full Plate", parent, plate, Color.white, Vector3.zero, Vector3.one, SpriteSorting.Background, roomObjects);
            plateObject.GetComponent<SpriteRenderer>().flipX = (graph.CurrentId & 1) != 0;

            BuildCaveGateway(parent, graph, RoomDirection.North, roomObjects);
            BuildCaveGateway(parent, graph, RoomDirection.East, roomObjects);
            BuildCaveGateway(parent, graph, RoomDirection.South, roomObjects);
            BuildCaveGateway(parent, graph, RoomDirection.West, roomObjects);
        }

        private static void BuildCaveGateway(Transform parent, RoomGraph graph, RoomDirection direction, List<GameObject> roomObjects)
        {
            if (!graph.HasNeighbor(graph.CurrentId, direction)) return;
            Vector3 position = direction == RoomDirection.North ? new Vector3(0f, RoomExitRules.TransitionY, 0f)
                : direction == RoomDirection.South ? new Vector3(0f, -RoomExitRules.TransitionY, 0f)
                : direction == RoomDirection.East ? new Vector3(RoomExitRules.TransitionX, 0f, 0f)
                : new Vector3(-RoomExitRules.TransitionX, 0f, 0f);
            Vector3 scale = direction == RoomDirection.North || direction == RoomDirection.South
                ? new Vector3(1.7f, .25f, 1f) : new Vector3(.25f, 1.7f, 1f);
            CreateSprite("Cave Passage " + direction, parent, RuntimeSpriteAtlas.SoftSquare(), new Color(.03f, .12f, .16f, .72f), position, scale, SpriteSorting.WallAndProps, roomObjects);
            CreateSprite("Cave Exit Rune " + direction, parent, RuntimeSpriteAtlas.Diamond(), new Color(.35f, .94f, 1f, .95f), position, Vector3.one * .34f, SpriteSorting.WallAndProps + 1, roomObjects);
        }

        private static void BuildCastlePlate(Transform parent, RoomGraph graph, List<GameObject> roomObjects)
        {
            Sprite plate = Resources.Load<Sprite>("Art/Environment/Castle/castle_room_style_anchor");
            GameObject plateObject = CreateSprite("Castle Full Plate", parent, plate, Color.white, Vector3.zero, Vector3.one, SpriteSorting.Background, roomObjects);
            plateObject.GetComponent<SpriteRenderer>().flipX = (graph.CurrentId & 1) != 0;

            BuildCastleGateway(parent, graph, RoomDirection.North, roomObjects);
            BuildCastleGateway(parent, graph, RoomDirection.East, roomObjects);
            BuildCastleGateway(parent, graph, RoomDirection.South, roomObjects);
            BuildCastleGateway(parent, graph, RoomDirection.West, roomObjects);
        }

        private static void BuildCastleGateway(Transform parent, RoomGraph graph, RoomDirection direction, List<GameObject> roomObjects)
        {
            if (!graph.HasNeighbor(graph.CurrentId, direction)) return;
            Vector3 position = direction == RoomDirection.North ? new Vector3(0f, RoomExitRules.TransitionY, 0f)
                : direction == RoomDirection.South ? new Vector3(0f, -RoomExitRules.TransitionY, 0f)
                : direction == RoomDirection.East ? new Vector3(RoomExitRules.TransitionX, 0f, 0f)
                : new Vector3(-RoomExitRules.TransitionX, 0f, 0f);
            Vector3 scale = direction == RoomDirection.North || direction == RoomDirection.South
                ? new Vector3(1.55f, .22f, 1f) : new Vector3(.22f, 1.55f, 1f);
            CreateSprite("Castle Passage " + direction, parent, RuntimeSpriteAtlas.SoftSquare(), new Color(.16f, .02f, .025f, .78f), position, scale, SpriteSorting.WallAndProps, roomObjects);
            CreateSprite("Castle Exit Rune " + direction, parent, RuntimeSpriteAtlas.Diamond(), new Color(1f, .46f, .16f, .95f), position, Vector3.one * .3f, SpriteSorting.WallAndProps + 1, roomObjects);
        }

        private static void BuildFloor(Transform parent, StageId stage, RoomGraph graph, List<GameObject> roomObjects)
        {
            string atlas = AtlasPath(stage);
            System.Random random = new System.Random(1729 + (int)stage * 337 + graph.CurrentId * 97);
            bool horizontalPath = graph.HasNeighbor(graph.CurrentId, RoomDirection.West) || graph.HasNeighbor(graph.CurrentId, RoomDirection.East);
            bool verticalPath = graph.HasNeighbor(graph.CurrentId, RoomDirection.North) || graph.HasNeighbor(graph.CurrentId, RoomDirection.South);

            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                bool onHorizontalPath = horizontalPath && (row == 4 || row == 5);
                bool onVerticalPath = verticalPath && (column == 8 || column == 9);
                int tileColumn;
                if (stage == StageId.Forest && (onHorizontalPath || onVerticalPath)) tileColumn = random.NextDouble() < .72 ? 3 : 4 + random.Next(0, 2);
                else tileColumn = random.NextDouble() < .7 ? 0 : 1 + random.Next(0, 2);
                Sprite sprite = RuntimeSpriteAtlas.LoadCell(atlas, tileColumn, 0);
                Color tint = stage == StageId.Forest ? Color.white : stage == StageId.Cave ? new Color(.62f, .58f, .72f) : new Color(1f, .84f, .82f);
                Vector3 position = new Vector3(-8.5f + column, -4f + row, 0f);
                CreateSprite("Floor " + column + "," + row, parent, sprite, tint, position, Vector3.one, SpriteSorting.Background, roomObjects);
            }

            GameObject underlay = CreateSprite(
                "Room Underlay",
                parent,
                RuntimeSpriteAtlas.Solid(),
                stage == StageId.Forest ? new Color(.025f, .1f, .055f) : stage == StageId.Cave ? new Color(.025f, .035f, .09f) : new Color(.12f, .025f, .035f),
                new Vector3(0f, 0f, .1f),
                new Vector3(19.2f, 10.2f, 1f),
                SpriteSorting.Background - 1,
                roomObjects);
            underlay.transform.SetAsFirstSibling();
        }

        private static void BuildBoundaries(Transform parent, StageId stage, RoomGraph graph, List<GameObject> roomObjects)
        {
            BuildBoundary(parent, stage, graph, RoomDirection.North, roomObjects);
            BuildBoundary(parent, stage, graph, RoomDirection.South, roomObjects);
            BuildBoundary(parent, stage, graph, RoomDirection.West, roomObjects);
            BuildBoundary(parent, stage, graph, RoomDirection.East, roomObjects);
        }

        private static void BuildBoundary(Transform parent, StageId stage, RoomGraph graph, RoomDirection direction, List<GameObject> roomObjects)
        {
            bool horizontal = direction == RoomDirection.North || direction == RoomDirection.South;
            int length = horizontal ? Columns : Rows;
            bool hasDoor = graph.HasNeighbor(graph.CurrentId, direction);
            string atlas = AtlasPath(stage);
            int forestWallColumn = direction == RoomDirection.North ? 0 : direction == RoomDirection.South ? 1 : direction == RoomDirection.West ? 2 : 3;
            int forestDoorColumn = direction == RoomDirection.North ? 8 : direction == RoomDirection.South ? 9 : direction == RoomDirection.West ? 10 : 11;
            Color wallColor = stage == StageId.Forest ? Color.white : stage == StageId.Cave ? new Color(.16f, .13f, .24f) : new Color(.52f, .22f, .2f);

            for (int index = 0; index < length; index++)
            {
                bool doorTile = hasDoor && Mathf.Abs(index - (length - 1) * .5f) <= 1.1f;
                Sprite sprite = stage == StageId.Forest
                    ? RuntimeSpriteAtlas.LoadCell(atlas, doorTile ? forestDoorColumn : forestWallColumn, doorTile ? 2 : 1)
                    : RuntimeSpriteAtlas.SoftSquare();
                Vector3 position;
                if (horizontal)
                    position = new Vector3(-8.5f + index, direction == RoomDirection.North ? 4.48f : -4.48f, 0f);
                else
                    position = new Vector3(direction == RoomDirection.East ? 8.96f : -8.96f, -4f + index, 0f);
                Vector3 scale = stage == StageId.Forest ? Vector3.one : horizontal ? new Vector3(1.04f, .5f, 1f) : new Vector3(.5f, 1.04f, 1f);
                CreateSprite((doorTile ? "Open Door " : "Boundary ") + direction + " " + index, parent, sprite, doorTile ? Color.white : wallColor, position, scale, SpriteSorting.WallAndProps, roomObjects);
            }

            Color rimColor = stage == StageId.Forest ? new Color(.04f, .14f, .07f, .9f) : stage == StageId.Cave ? new Color(.04f, .055f, .12f, .9f) : new Color(.17f, .035f, .045f, .9f);
            Vector3 rimPosition = horizontal
                ? new Vector3(0f, direction == RoomDirection.North ? 4.84f : -4.84f, 0f)
                : new Vector3(direction == RoomDirection.East ? 9.3f : -9.3f, 0f, 0f);
            Vector3 rimScale = horizontal ? new Vector3(19f, .35f, 1f) : new Vector3(.35f, 10f, 1f);
            CreateSprite("Boundary Rim " + direction, parent, RuntimeSpriteAtlas.Solid(), rimColor, rimPosition, rimScale, SpriteSorting.WallAndProps - 1, roomObjects);
        }

        private static void BuildDecoration(Transform parent, StageId stage, RoomGraph graph, List<GameObject> roomObjects)
        {
            System.Random random = new System.Random(9109 + (int)stage * 521 + graph.CurrentId * 181);
            string atlas = AtlasPath(stage);
            int decorationCount = stage == StageId.Forest ? 28 : stage == StageId.Cave ? 0 : 18;
            for (int i = 0; i < decorationCount; i++)
            {
                float x = -7.8f + (float)random.NextDouble() * 15.6f;
                float y = -3.45f + (float)random.NextDouble() * 6.9f;
                if (Mathf.Abs(x) < 1.3f || Mathf.Abs(y) < .65f) continue;
                Sprite sprite = stage == StageId.Forest
                    ? RuntimeSpriteAtlas.LoadCell(atlas, random.Next(0, 8), 3)
                    : RuntimeSpriteAtlas.LoadCell(atlas, 1 + random.Next(0, 3), 0);
                float alpha = .48f + (float)random.NextDouble() * .42f;
                Color color = stage == StageId.Forest ? new Color(1f, 1f, 1f, alpha) : new Color(1f, 1f, 1f, alpha * .55f);
                CreateSprite("Ground Detail " + i, parent, sprite, color, new Vector3(x, y, 0f), Vector3.one * (.75f + (float)random.NextDouble() * .35f), SpriteSorting.Background + 2, roomObjects);
            }

            if (stage == StageId.Forest)
            {
                StampForestProp(parent, atlas, 12, 2, new Vector2(-7.45f, 3.15f), "Canopy NW", roomObjects);
                StampForestProp(parent, atlas, 12, 2, new Vector2(7.4f, 3.05f), "Canopy NE", roomObjects);
                StampForestProp(parent, atlas, 14, 2, new Vector2(-7.35f, -3.25f), "Rock SW", roomObjects);
                StampForestProp(parent, atlas, 2, 4, new Vector2(7.45f, -3.25f), "Bush SE", roomObjects);
            }

            Color haze = stage == StageId.Forest ? new Color(.18f, .48f, .22f, .08f) : stage == StageId.Cave ? new Color(.23f, .35f, .72f, .08f) : new Color(.72f, .16f, .18f, .07f);
            CreateSprite("Atmospheric Ground Wash", parent, RuntimeSpriteAtlas.SoftSquare(), haze, Vector3.zero, new Vector3(16.5f, 7.3f, 1f), SpriteSorting.Background + 1, roomObjects);
        }

        private static void BuildCaveSetPieces(Transform parent, RoomGraph graph, List<GameObject> roomObjects)
        {
            const string atlas = "Art/ThirdParty/EmanQuest/cave_lava_tiles";
            const int cellSize = 64;
            const float pixelsPerUnit = 64f;

            // The play field stays clear; these CC0 tiles only establish a cavern edge and depth.
            StampCaveTile(parent, atlas, 5, 4, new Vector2(-7.65f, 3.45f), "Northwest Boulder", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 6, 4, new Vector2(-6.65f, 3.43f), "Northwest Boulder", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 5, 5, new Vector2(-7.72f, 2.48f), "Northwest Rubble", roomObjects, cellSize, pixelsPerUnit);

            StampCaveTile(parent, atlas, 9, 4, new Vector2(7.55f, 3.42f), "Northeast Boulder", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 10, 4, new Vector2(6.55f, 3.42f), "Northeast Boulder", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 10, 5, new Vector2(7.62f, 2.45f), "Northeast Rubble", roomObjects, cellSize, pixelsPerUnit);

            StampCaveTile(parent, atlas, 5, 6, new Vector2(-7.72f, -3.36f), "Southwest Rubble", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 6, 6, new Vector2(-6.72f, -3.35f), "Southwest Rubble", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 11, 6, new Vector2(7.65f, -3.35f), "Southeast Rubble", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 12, 6, new Vector2(6.65f, -3.35f), "Southeast Rubble", roomObjects, cellSize, pixelsPerUnit);

            // Blue crystal seams echo the cave enemies without pretending to be interactive hazards.
            StampCaveTile(parent, atlas, 5, 7, new Vector2(-4.8f, 3.72f), "Crystal Seam West", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 6, 7, new Vector2(-3.8f, 3.72f), "Crystal Seam West", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 7, 7, new Vector2(3.8f, -3.7f), "Crystal Seam East", roomObjects, cellSize, pixelsPerUnit);
            StampCaveTile(parent, atlas, 8, 7, new Vector2(4.8f, -3.7f), "Crystal Seam East", roomObjects, cellSize, pixelsPerUnit);

            if ((graph.CurrentId & 1) == 1)
            {
                StampCaveTile(parent, atlas, 6, 0, new Vector2(-7.85f, .65f), "Collapsed Pillar", roomObjects, cellSize, pixelsPerUnit);
                StampCaveTile(parent, atlas, 6, 1, new Vector2(-7.85f, -.35f), "Collapsed Pillar", roomObjects, cellSize, pixelsPerUnit);
            }
            else
            {
                StampCaveTile(parent, atlas, 7, 0, new Vector2(7.85f, .65f), "Collapsed Pillar", roomObjects, cellSize, pixelsPerUnit);
                StampCaveTile(parent, atlas, 7, 1, new Vector2(7.85f, -.35f), "Collapsed Pillar", roomObjects, cellSize, pixelsPerUnit);
            }
        }

        private static void StampCaveTile(Transform parent, string atlas, int atlasColumn, int atlasRow, Vector2 position, string name, List<GameObject> roomObjects, int cellSize, float pixelsPerUnit)
        {
            Sprite sprite = RuntimeSpriteAtlas.LoadCell(atlas, atlasColumn, atlasRow, cellSize, cellSize, pixelsPerUnit);
            int order = SpriteSorting.WallAndProps + Mathf.RoundToInt((RoomExitRules.PlayerMaxY - position.y) * 10f);
            CreateSprite(name, parent, sprite, Color.white, position, Vector3.one, order, roomObjects);
        }

        private static void StampForestProp(Transform parent, string atlas, int atlasColumn, int atlasRow, Vector2 center, string name, List<GameObject> roomObjects)
        {
            for (int row = 0; row < 2; row++)
            for (int column = 0; column < 2; column++)
            {
                Sprite sprite = RuntimeSpriteAtlas.LoadCell(atlas, atlasColumn + column, atlasRow + row);
                Vector3 position = new Vector3(center.x + column - .5f, center.y + .5f - row, 0f);
                int order = SpriteSorting.WallAndProps + Mathf.RoundToInt((RoomExitRules.PlayerMaxY - position.y) * 10f);
                CreateSprite(name + " " + column + "," + row, parent, sprite, Color.white, position, Vector3.one, order, roomObjects);
            }
        }

        private static void BuildAmbientMotes(Transform parent, StageId stage, List<GameObject> roomObjects)
        {
            GameObject motes = new GameObject("Ambient Motes");
            motes.transform.SetParent(parent, false);
            motes.AddComponent<AmbientMotesEffect>().Initialize(stage);
            roomObjects.Add(motes);
        }

        private static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Color color, Vector3 position, Vector3 scale, int sortingOrder, List<GameObject> roomObjects)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite == null ? RuntimeSpriteAtlas.Solid() : sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            roomObjects.Add(go);
            return go;
        }

        private static string AtlasPath(StageId stage)
        {
            if (stage == StageId.Forest) return "Art/Environment/Forest/forest_tiles";
            if (stage == StageId.Cave) return "Art/Environment/Cave/cave_tiles";
            return "Art/Environment/Castle/castle_tiles";
        }
    }

    public sealed class AmbientMotesEffect : MonoBehaviour
    {
        private readonly List<Transform> motes = new List<Transform>();
        private readonly List<float> speeds = new List<float>();
        private readonly List<float> phases = new List<float>();

        public void Initialize(StageId stage)
        {
            Color color = stage == StageId.Forest ? new Color(.82f, 1f, .62f, .26f) : stage == StageId.Cave ? new Color(.52f, .72f, 1f, .24f) : new Color(1f, .52f, .42f, .22f);
            System.Random random = new System.Random(4703 + (int)stage * 101);
            for (int i = 0; i < 22; i++)
            {
                GameObject mote = new GameObject("Mote " + i);
                mote.transform.SetParent(transform, false);
                mote.transform.localPosition = new Vector3(-8f + (float)random.NextDouble() * 16f, -3.6f + (float)random.NextDouble() * 7.2f, 0f);
                mote.transform.localScale = Vector3.one * (.025f + (float)random.NextDouble() * .055f);
                SpriteRenderer renderer = mote.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeSpriteAtlas.Diamond();
                renderer.color = color;
                renderer.sortingOrder = SpriteSorting.WallAndProps + 95;
                motes.Add(mote.transform);
                speeds.Add(.035f + (float)random.NextDouble() * .085f);
                phases.Add((float)random.NextDouble() * Mathf.PI * 2f);
            }
        }

        private void Update()
        {
            for (int i = 0; i < motes.Count; i++)
            {
                Transform mote = motes[i];
                Vector3 position = mote.localPosition;
                position.y += speeds[i] * Time.unscaledDeltaTime;
                position.x += Mathf.Sin(Time.unscaledTime * .45f + phases[i]) * .018f * Time.unscaledDeltaTime;
                if (position.y > 3.7f) position.y = -3.7f;
                mote.localPosition = position;
            }
        }
    }
}
