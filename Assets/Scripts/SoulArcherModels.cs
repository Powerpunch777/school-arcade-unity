using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SoulArcher
{
    public enum StageId { Forest, Cave, Castle }
    public enum RoomKind { Combat, Shop, Boss }
    public enum RoomDirection { West, East, North, South }
    public enum Difficulty { Easy, Normal }
    public enum Ability { Attack, Speed, Pierce, Reflect, Heal, Move }
    public enum EnemyType
    {
        Wolf, Mushroom, RockBeetle, CrystalSpirit, ShieldSkeleton, DarkMage,
        TreeSpirit, GiantBat, Morkan
    }

    public static class StageRules
    {
        public static readonly StageId[] Order = { StageId.Forest, StageId.Cave, StageId.Castle };
        public static string Name(StageId stage) { return stage == StageId.Forest ? "숲" : stage == StageId.Cave ? "동굴" : "마왕의 성"; }
        public static string BossName(StageId stage) { return stage == StageId.Forest ? "거대 나무 정령" : stage == StageId.Cave ? "거대 박쥐" : "마왕 기사 모르칸"; }
        public static StageId Next(StageId stage) { return stage == StageId.Forest ? StageId.Cave : stage == StageId.Cave ? StageId.Castle : StageId.Castle; }
        public static bool HasNext(StageId stage) { return stage != StageId.Castle; }
        public static EnemyType[] Enemies(StageId stage)
        {
            if (stage == StageId.Forest) return new[] { EnemyType.Wolf, EnemyType.Mushroom };
            if (stage == StageId.Cave) return new[] { EnemyType.RockBeetle, EnemyType.CrystalSpirit };
            return new[] { EnemyType.ShieldSkeleton, EnemyType.DarkMage };
        }
    }

    public static class RoomExitRules
    {
        public const float PlayerMinX = -8.8f;
        public const float PlayerMaxX = 8.8f;
        public const float PlayerMinY = -4.3f;
        public const float PlayerMaxY = 4.3f;
        public const float TransitionX = 8.65f;
        public const float TransitionY = 4.15f;
        public static bool CanMoveThroughExit(float x, int currentRoomId, int roomCount, bool roomReady)
        {
            if (!roomReady || roomCount <= 0) return false;
            return (x > TransitionX && currentRoomId < roomCount - 1) || (x < -TransitionX && currentRoomId > 0);
        }
        public static int DestinationFor(float x, int currentRoomId) { return x > TransitionX ? currentRoomId + 1 : currentRoomId - 1; }
        public static RoomDirection? ExitDirection(float x, float y)
        {
            if (x > TransitionX) return RoomDirection.East;
            if (x < -TransitionX) return RoomDirection.West;
            if (y > TransitionY) return RoomDirection.North;
            if (y < -TransitionY) return RoomDirection.South;
            return null;
        }
    }

    public static class SpriteSorting
    {
        public const int Background = 0;
        public const int WallAndProps = 10;
        public const int Actor = 20;
        public const int Projectile = 30;
        public const int Ui = 100;
    }

    public static class ArenaRules
    {
        public static Vector3 ClampEnemy(Vector3 position) { return new Vector3(Mathf.Clamp(position.x, -7.8f, 7.8f), Mathf.Clamp(position.y, -3.6f, 3.6f), 0f); }
    }

    [Serializable]
    public sealed class RoomState
    {
        public int id;
        public StageId stage;
        public RoomKind kind;
        public bool visited;
        public bool cleared;
        public string label;
        public int gridX;
        public int gridY;
        public bool offersAbility;
        public bool elite;
        public RoomState(int roomId, StageId roomStage, RoomKind roomKind, string roomLabel, int x = 0, int y = 0, bool grantsAbility = false, bool isElite = false)
        { id = roomId; stage = roomStage; kind = roomKind; label = roomLabel; gridX = x; gridY = y; offersAbility = grantsAbility; elite = isElite; }
    }

    [Serializable]
    public sealed class RoomGraph
    {
        public readonly List<RoomState> Rooms = new List<RoomState>();
        public int CurrentId { get; private set; }
        public StageId Stage { get; private set; }
        public RoomState Current { get { return Rooms[CurrentId]; } }

        public RoomGraph(StageId stage = StageId.Forest)
        {
            Stage = stage;
            string name = StageRules.Name(stage);
            Rooms.Add(new RoomState(0, stage, RoomKind.Combat, name + " 전투 1", 0, 0, true));
            Rooms.Add(new RoomState(1, stage, RoomKind.Combat, name + " 강화 전투", 1, 0, true, true));
            Rooms.Add(new RoomState(2, stage, RoomKind.Shop, name + " 상점", 0, 1));
            Rooms.Add(new RoomState(3, stage, RoomKind.Combat, name + " 전투 3", 1, 1));
            Rooms.Add(new RoomState(4, stage, RoomKind.Boss, StageRules.BossName(stage), 2, 1));
            Rooms[0].visited = true;
        }

        public bool TryMove(int id)
        {
            if (id < 0 || id >= Rooms.Count || !AreConnected(CurrentId, id)) return false;
            if ((Current.kind == RoomKind.Combat || Current.kind == RoomKind.Boss) && !Current.cleared) return false;
            CurrentId = id;
            Rooms[id].visited = true;
            return true;
        }
        public bool TryMove(RoomDirection direction)
        {
            int id = Neighbor(CurrentId, direction);
            return id >= 0 && TryMove(id);
        }
        public int Neighbor(int roomId, RoomDirection direction)
        {
            if (roomId < 0 || roomId >= Rooms.Count) return -1;
            var room = Rooms[roomId];
            int x = room.gridX + (direction == RoomDirection.East ? 1 : direction == RoomDirection.West ? -1 : 0);
            int y = room.gridY + (direction == RoomDirection.North ? 1 : direction == RoomDirection.South ? -1 : 0);
            var neighbor = Rooms.FirstOrDefault(r => r.gridX == x && r.gridY == y);
            return neighbor == null ? -1 : neighbor.id;
        }
        public bool HasNeighbor(int roomId, RoomDirection direction) { return Neighbor(roomId, direction) >= 0; }
        public bool AreConnected(int firstId, int secondId)
        {
            if (firstId < 0 || secondId < 0 || firstId >= Rooms.Count || secondId >= Rooms.Count) return false;
            var first = Rooms[firstId]; var second = Rooms[secondId];
            return Mathf.Abs(first.gridX - second.gridX) + Mathf.Abs(first.gridY - second.gridY) == 1;
        }
        public void ClearCurrent() { Current.cleared = true; }
        public int ClearedCombatCount { get { return Rooms.Count(r => r.kind == RoomKind.Combat && r.cleared); } }
        public bool BossCleared { get { return Rooms.Any(r => r.kind == RoomKind.Boss && r.cleared); } }
    }

    [Serializable]
    public sealed class PlayerStats
    {
        public const float MaxHearts = 5f;
        public const float BaseAttack = 10f;
        public const float BaseAttackInterval = .7f;
        public const float BaseMoveSpeed = 5f;
        public float hearts = MaxHearts;
        public int souls;
        public float attack = BaseAttack;
        public float attackInterval = BaseAttackInterval;
        public float moveSpeed = BaseMoveSpeed;
        public int pierce;
        public int reflect;
        public int shieldCharges;
        public bool stageWhetstone;
        public bool dead;
        public float invincibleUntil;
        public readonly int[] abilityStacks = new int[6];
        public bool CanTakeDamage(float now) { return !dead && now >= invincibleUntil; }
        public bool TakeDamage(float amount, float now, float invincibility = .5f)
        {
            if (!CanTakeDamage(now)) return false;
            if (shieldCharges > 0) { shieldCharges--; invincibleUntil = now + .2f; return false; }
            hearts = Mathf.Max(0f, hearts - Mathf.Max(.5f, amount));
            invincibleUntil = now + invincibility;
            if (hearts <= 0f) dead = true;
            return true;
        }
        public bool TakeDamage(float amount, float now, Difficulty difficulty, float invincibility = .5f)
        { return TakeDamage(DifficultyRules.Damage(amount, difficulty), now, invincibility); }
        public void Heal(float amount) { hearts = Mathf.Min(MaxHearts, hearts + amount); }
        public int StackCount(Ability ability) { return abilityStacks[(int)ability]; }
        public void ApplyStageWhetstone() { stageWhetstone = true; RecalculateDerivedStats(); }
        public void ResetStageEffects() { stageWhetstone = false; RecalculateDerivedStats(); }
        public void RecalculateDerivedStats()
        {
            attack = BaseAttack * (1f + .2f * StackCount(Ability.Attack)) * (stageWhetstone ? 1.2f : 1f);
            attackInterval = Mathf.Max(BaseAttackInterval * .5f, BaseAttackInterval * (1f - .15f * StackCount(Ability.Speed)));
            moveSpeed = BaseMoveSpeed * (1f + .1f * StackCount(Ability.Move));
            pierce = StackCount(Ability.Pierce);
            reflect = StackCount(Ability.Reflect);
        }
    }

    public static class DifficultyRules
    {
        public static float Damage(float amount, Difficulty difficulty) { return difficulty == Difficulty.Easy ? Mathf.Max(.5f, amount - .5f) : amount; }
    }

    public static class SoulRewardRules
    {
        public const int CombatRoomClearBonus = 2;
        public static int For(EnemyType type)
        {
            if (type == EnemyType.TreeSpirit || type == EnemyType.GiantBat) return 5;
            if (type == EnemyType.Morkan) return 0;
            return 1;
        }
    }

    public static class ShopRules
    {
        public static readonly int[] Prices = { 10, 15, 20 };
        public static bool[] CreatePurchaseState() { return new bool[Prices.Length]; }
        public static bool TryPurchase(PlayerStats stats, int itemIndex, bool[] purchased)
        {
            if (stats == null || purchased == null || itemIndex < 0 || itemIndex >= Prices.Length || itemIndex >= purchased.Length || purchased[itemIndex] || stats.souls < Prices[itemIndex]) return false;
            stats.souls -= Prices[itemIndex]; purchased[itemIndex] = true;
            if (itemIndex == 0) stats.Heal(1.5f);
            else if (itemIndex == 1) stats.shieldCharges++;
            else stats.ApplyStageWhetstone();
            return true;
        }
    }

    public static class AbilityRules
    {
        public const int MaxStack = 3;
        public const int ChoicesPerRun = 6;
        public static readonly Ability[] All = { Ability.Attack, Ability.Speed, Ability.Pierce, Ability.Reflect, Ability.Heal, Ability.Move };
        public static void Apply(PlayerStats stats, Ability ability)
        {
            if (stats == null || stats.StackCount(ability) >= MaxStack) return;
            stats.abilityStacks[(int)ability]++;
            switch (ability)
            {
                case Ability.Heal: stats.Heal(1.5f); break;
            }
            stats.RecalculateDerivedStats();
        }
        public static List<Ability> CreateCards(PlayerStats stats, int seed)
        {
            var available = All.Where(a => stats == null || stats.StackCount(a) < MaxStack).ToList();
            var result = new List<Ability>();
            var random = new System.Random(seed);
            while (available.Count > 0 && result.Count < 3)
            {
                int index = random.Next(available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }
            return result;
        }
    }

    public sealed class AutoAimTarget
    {
        public readonly int id; public readonly Vector2 position; public readonly bool alive; public readonly bool visible;
        public AutoAimTarget(int targetId, Vector2 targetPosition, bool isAlive, bool isVisible) { id = targetId; position = targetPosition; alive = isAlive; visible = isVisible; }
    }
    public static class AutoAimRules
    {
        public static bool CanFire(bool isMoving, bool hasTarget, float now, float nextShot) { return !isMoving && hasTarget && now >= nextShot; }
        public static int SelectNearestVisibleLivingTarget(Vector2 origin, IList<AutoAimTarget> targets)
        {
            int selectedIndex = -1; float bestDistance = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.alive || !target.visible) continue;
                float distance = (target.position - origin).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; selectedIndex = i; }
            }
            return selectedIndex;
        }
    }

    [Serializable]
    public sealed class RunRecord
    {
        public bool victory;
        public Difficulty difficulty;
        public float seconds;
        public int reachedStage;
        public int reachedRoom;
        public float remainingHearts;
        public int souls;
        public string abilities;
        public string dateIso;
    }

    [Serializable]
    public sealed class RunRecordCollection { public List<RunRecord> records = new List<RunRecord>(); }

    public static class RunHistory
    {
        private const string Key = "SoulArcher.RunHistory.v1";
        public static void Save(RunRecord record)
        {
            var all = Load(); all.records.Insert(0, record);
            if (all.records.Count > 20) all.records.RemoveRange(20, all.records.Count - 20);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(all)); PlayerPrefs.Save();
        }
        public static RunRecordCollection Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new RunRecordCollection();
            try { return JsonUtility.FromJson<RunRecordCollection>(json) ?? new RunRecordCollection(); }
            catch { return new RunRecordCollection(); }
        }
        public static void Clear() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
        public static int Wins() { return Load().records.Count(r => r.victory); }
        public static float WinRate() { var records = Load().records; return records.Count == 0 ? 0f : Wins() / (float)records.Count; }
        public static float AverageTime() { var records = Load().records; return records.Count == 0 ? 0f : records.Average(r => r.seconds); }
        public static float AverageStage() { var records = Load().records; return records.Count == 0 ? 0f : records.Average(r => (float)r.reachedStage); }
        public static string MostSelectedAbility()
        {
            var counts = new Dictionary<string, int>();
            foreach (var record in Load().records) foreach (string name in (record.abilities ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) counts[name] = counts.ContainsKey(name) ? counts[name] + 1 : 1;
            if (counts.Count == 0) return "없음";
            var favorite = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First();
            return favorite.Key + " (" + favorite.Value + "회)";
        }
    }

    public static class DisplaySettings
    {
        public const int InternalWidth = 640; public const int InternalHeight = 360;
        public const int DefaultOutputWidth = 1920; public const int DefaultOutputHeight = 1080;
        public const int WindowedWidth = 1280; public const int WindowedHeight = 720;
        public const FullScreenMode DefaultFullscreenMode = FullScreenMode.FullScreenWindow;
        public static void ApplyDefaultOutput() { Screen.SetResolution(DefaultOutputWidth, DefaultOutputHeight, DefaultFullscreenMode); }
        public static void Apply(bool fullscreen) { Screen.SetResolution(fullscreen ? DefaultOutputWidth : WindowedWidth, fullscreen ? DefaultOutputHeight : WindowedHeight, fullscreen ? DefaultFullscreenMode : FullScreenMode.Windowed); }
    }
}
