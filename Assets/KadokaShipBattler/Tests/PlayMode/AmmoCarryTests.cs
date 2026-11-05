using System.Collections;
using System.Collections.Generic;
using KadokaShipBattler.AI;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KadokaShipBattler.Tests
{
    public sealed class AmmoCarryTests
    {
        private BattlePrototype arena;
        private readonly List<ScriptableObject> assets = new();
        private CrewAmmoInventory Inventory => arena.PlayerCrew[0].GetComponent<CrewAmmoInventory>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.EnemyActionsEnabled = false;
            arena.AllyActionsEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arena != null) Object.Destroy(arena.gameObject);
            foreach (var asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            yield return null;
        }

        private AmmoDefinition Ammo(float weight, float damage = 25)
        {
            var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
            definition.Initialize("test-" + assets.Count, "Test Ammo", damage, weight);
            assets.Add(definition);
            return definition;
        }

        private AmmoPickup Pickup(AmmoDefinition ammo, Vector2 position)
        {
            var item = new GameObject("Weighted Pickup");
            item.transform.SetParent(arena.transform);
            item.transform.position = position;
            var pickup = item.AddComponent<AmmoPickup>();
            pickup.Initialize(ammo);
            return pickup;
        }

        [TestCase(2f, 2f, true)]
        [TestCase(2f, 3f, true)]
        [TestCase(3f, 3f, false)]
        public void IssueExamplesEnforceCombinedWeight(float first, float second, bool expected)
        {
            Assert.That(Inventory.CarryCapacity, Is.EqualTo(5));
            Assert.That(Inventory.MaxCarryCount, Is.EqualTo(2));
            Assert.That(Inventory.TryPickup(Ammo(first)), Is.True);
            var next = Ammo(second);
            Assert.That(Inventory.CanPickup(next), Is.EqualTo(expected));
            Assert.That(Inventory.Count, Is.EqualTo(1), "Preview must not change held items");
            Assert.That(Inventory.TryPickup(next), Is.EqualTo(expected));
            Assert.That(Inventory.Count, Is.EqualTo(expected ? 2 : 1));
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(first + (expected ? second : 0)));
        }

        [Test]
        public void SingleHeavyItemFitsAndCountLimitIsIndependentOfWeight()
        {
            Assert.That(Inventory.TryPickup(Ammo(5)), Is.True);
            Assert.That(Inventory.TryPickup(Ammo(1)), Is.False, "Weight may block before count is full");
            Inventory.TakeAmmo();
            var definition = arena.PlayerCrew[0].GetComponent<CrewMember>().Definition;
            definition.Initialize("test", "Test", CharacterCapability.CarryAmmo, 4, 5, 100, 2);
            Assert.That(Inventory.TryPickup(Ammo(1)), Is.True);
            Assert.That(Inventory.TryPickup(Ammo(1)), Is.True);
            Assert.That(Inventory.TryPickup(Ammo(0)), Is.False, "Count limit applies to zero-weight items too");
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(2));
        }

        [Test]
        public void CannonLoadsOneItemAtATimeInPickupOrder()
        {
            var light = Ammo(2, 25);
            var heavy = Ammo(3, 40);
            Inventory.TryPickup(light);
            Inventory.TryPickup(heavy);
            Assert.That(arena.PlayerCannon.TryLoadFrom(Inventory), Is.True);
            Assert.That(arena.PlayerCannon.LoadedAmmo, Is.SameAs(light));
            Assert.That(Inventory.Count, Is.EqualTo(1));
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(3));
            Assert.That(Inventory.CarriedAmmo, Is.SameAs(heavy));
            Assert.That(arena.PlayerCannon.TryLoadFrom(Inventory), Is.False);
            Assert.That(Inventory.Count, Is.EqualTo(1));
            Assert.That(arena.PlayerCannon.FireAt(arena.EnemyShip), Is.True);
            Assert.That(arena.PlayerCannon.TryLoadFrom(Inventory), Is.True);
            Assert.That(arena.PlayerCannon.LoadedAmmo, Is.SameAs(heavy));
            Assert.That(arena.PlayerCannon.FireAt(arena.EnemyShip), Is.True);
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(35));
            Assert.That(Inventory.Count, Is.Zero);
            Assert.That(Inventory.CurrentWeight, Is.Zero);
        }

        [Test]
        public void RejectedPickupRemainsForStrongerCarrierAndCannotBePickedTwice()
        {
            var pickup = Pickup(Ammo(5), Vector2.zero);
            var gunner = arena.PlayerCrew[1].GetComponent<CrewAmmoInventory>();
            Assert.That(gunner.CarryCapacity, Is.EqualTo(3));
            Assert.That(pickup.TryPickup(gunner), Is.False);
            Assert.That(pickup.GetComponent<BoxCollider2D>().enabled, Is.True);
            Assert.That(gunner.Count, Is.Zero);
            Assert.That(pickup.TryPickup(Inventory), Is.True);
            Assert.That(pickup.GetComponent<BoxCollider2D>().enabled, Is.False);
            Assert.That(pickup.TryPickup(Inventory), Is.False);
            Assert.That(Inventory.Count, Is.EqualTo(1));
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(5));
        }

        [Test]
        public void AiSkipsOverweightPickupAndUsesTheSharedInventoryApi()
        {
            foreach (var pickup in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None))
                pickup.GetComponent<BoxCollider2D>().enabled = false;
            var actor = arena.PlayerCrew[0];
            actor.transform.position = new Vector3(-5, 0, 0);
            actor.Face(Vector2.down);
            var overweight = Pickup(Ammo(6), new Vector2(-5, -0.2f));
            var light = Pickup(Ammo(2), new Vector2(-5, -0.6f));
            arena.Controls.CycleNext();
            var ai = actor.GetComponent<CrewAmmoAiController>();
            Assert.That(Inventory.CanPickup(overweight.Definition), Is.False);
            Assert.That(ai.VisibleAmmo, Is.SameAs(light));
            arena.AllyActionsEnabled = true;
            for (var step = 0; step < 20 && !Inventory.HasAmmo; step++) ai.Tick(0.02f);
            Assert.That(Inventory.CarriedAmmo, Is.SameAs(light.Definition));
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(2));
            Assert.That(overweight.GetComponent<BoxCollider2D>().enabled, Is.True);
        }

        [Test]
        public void SwitchPreservesMultipleItemsAndAiFiresBoth()
        {
            var actor = arena.PlayerCrew[0];
            Inventory.TryPickup(Ammo(2, 25));
            Inventory.TryPickup(Ammo(3, 40));
            actor.transform.position = arena.PlayerCannon.transform.position;
            arena.Controls.CycleNext();
            Assert.That(Inventory.Count, Is.EqualTo(2));
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(5));
            arena.AllyActionsEnabled = true;
            var ai = actor.GetComponent<CrewAmmoAiController>();
            for (var step = 0; step < 100 && arena.EnemyShip.CurrentHull > 35; step++) ai.Tick(0.1f);
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(35));
            Assert.That(Inventory.Count, Is.Zero);
            Assert.That(Inventory.CurrentWeight, Is.Zero);
            Assert.That(arena.PlayerCannon.IsLoaded, Is.False);
        }

        [Test]
        public void ReducedLimitsKeepHeldItemsUntilTheyAreUnloaded()
        {
            var first = Ammo(2);
            var second = Ammo(3);
            Inventory.TryPickup(first);
            Inventory.TryPickup(second);
            var definition = arena.PlayerCrew[0].GetComponent<CrewMember>().Definition;
            definition.Initialize("test", "Test", CharacterCapability.CarryAmmo, 4, 5, 1, 1);
            Assert.That(Inventory.Count, Is.EqualTo(2));
            Assert.That(Inventory.CurrentWeight, Is.EqualTo(5));
            Assert.That(Inventory.CanPickup(Ammo(0)), Is.False);
            Assert.That(Inventory.TakeAmmo(), Is.SameAs(first));
            Assert.That(Inventory.TakeAmmo(), Is.SameAs(second));
            Assert.That(Inventory.CurrentWeight, Is.Zero);
            Assert.That(Inventory.CanPickup(Ammo(1)), Is.True);
        }

        [Test]
        public void CarryCapabilityIsRequiredEvenWhenLimitsAllowTheAmmo()
        {
            var definition = arena.PlayerCrew[0].GetComponent<CrewMember>().Definition;
            definition.Initialize("test", "Test", CharacterCapability.Combat, 4, 5, 100, 10);
            var ammo = Ammo(1);
            Assert.That(Inventory.CanPickup(ammo), Is.False);
            Assert.That(Inventory.TryPickup(ammo), Is.False);
        }

        [Test]
        public void InventoryReadsDefinitionAssignedAfterItsAwake()
        {
            var item = new GameObject("Late Initialized Carrier");
            item.transform.SetParent(arena.transform);
            var inventory = item.AddComponent<CrewAmmoInventory>();
            Assert.That(inventory.CanPickup(Ammo(1)), Is.False);
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            assets.Add(definition);
            definition.Initialize("late", "Late", CharacterCapability.CarryAmmo, 4, 5, 5, 2);
            item.GetComponent<CrewMember>().Initialize(definition, TeamSide.Player);
            Assert.That(inventory.TryPickup(Ammo(2)), Is.True);
            Assert.That(inventory.CarryCapacity, Is.EqualTo(5));
            Assert.That(inventory.MaxCarryCount, Is.EqualTo(2));
        }
    }
}
