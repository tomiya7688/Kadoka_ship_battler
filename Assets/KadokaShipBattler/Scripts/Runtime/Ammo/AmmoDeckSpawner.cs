using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    public sealed class AmmoDeckSpawner : MonoBehaviour
    {
        public const float SpawnInterval = 2f;
        public const float GroundLifetime = 60f;
        private readonly List<AmmoPickup> pickups = new();
        private Vector2[] spawnPoints;
        private Sprite sprite;
        private float spawnTimer = SpawnInterval;
        private System.Random random;
        private BattlePrototype arena;
        public AmmoDeckState Deck { get; private set; }
        public TeamSide TeamSide { get; private set; }
        public bool SpawningEnabled { get; set; } = true;
        public IReadOnlyList<AmmoPickup> Pickups
        {
            get
            {
                pickups.RemoveAll(pickup => pickup == null || pickup.IsConsumed);
                return pickups.AsReadOnly();
            }
        }
        public void Initialize(AmmoDeckDefinition definition, TeamSide side, Vector2[] points,
            Sprite pickupSprite, BattlePrototype battle, int seed)
        {
            if (Deck != null) throw new InvalidOperationException("Spawner already initialized.");
            if (definition == null || points == null || points.Length == 0 || pickupSprite == null)
                throw new ArgumentException("Deck, spawn points and sprite are required.");
            foreach (var point in points)
                if (float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y))
                    throw new ArgumentException("Invalid spawn point.");
            Deck = definition.CreateState(seed);
            TeamSide = side;
            spawnPoints = (Vector2[])points.Clone();
            sprite = pickupSprite;
            arena = battle;
            random = new System.Random(seed ^ 0x1a734);
            for (var count = 0; count < points.Length; count++) TrySpawn();
        }
        private void Update()
        {
            if (arena != null && arena.IsFinished) return;
            Tick(Time.deltaTime);
        }
        public void Tick(float deltaTime)
        {
            if (Deck == null || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            // Tick a snapshot because expired pickups leave the active list.
            foreach (var pickup in new List<AmmoPickup>(Pickups)) pickup.TickLifetime(deltaTime);
            if (!SpawningEnabled) return;
            spawnTimer -= deltaTime;
            if (spawnTimer > 0) return;
            spawnTimer = SpawnInterval;
            TrySpawn();
        }
        public AmmoPickup TrySpawn()
        {
            if (Deck == null || spawnPoints == null) return null;
            var occupied = new HashSet<int>();
            foreach (var existingPickup in Pickups) occupied.Add(existingPickup.SpawnPointIndex);
            var free = new List<int>();
            for (var index = 0; index < spawnPoints.Length; index++) if (!occupied.Contains(index)) free.Add(index);
            if (free.Count == 0) return null;
            var round = Deck.TrySpawn();
            if (round == null) return null;
            var pointIndex = free[random.Next(free.Count)];
            var item = new GameObject(TeamSide + " Deck Ammo " + round.Slot);
            item.transform.SetParent(transform);
            item.transform.position = spawnPoints[pointIndex];
            item.transform.localScale = Vector3.one * 0.3f;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 2;
            renderer.color = round.Weight <= 2 ? new Color(1, 0.8f, 0.2f) : new Color(1, 0.45f, 0.1f);
            var pickup = item.AddComponent<AmmoPickup>();
            pickup.Initialize(round, pointIndex, GroundLifetime);
            pickups.Add(pickup);
            return pickup;
        }
        // The fixture's scripted enemy knows its own supply locations. It still transfers a real deck instance.
        public bool TrySupply(CrewAmmoInventory inventory)
        {
            if (inventory == null || inventory.TeamSide != TeamSide) return false;
            foreach (var pickup in Pickups) if (pickup.TryPickup(inventory)) return true;
            return false;
        }
        private void OnDestroy() => Deck?.Close();
    }
}
