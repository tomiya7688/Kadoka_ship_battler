using System;
using System.Collections;
using System.Linq;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace KadokaShipBattler.Tests
{
    public sealed class AmmoDeckTests
    {
        private BattlePrototype arena;
        private CrewAmmoInventory Inventory => arena.PlayerCrew[0].GetComponent<CrewAmmoInventory>();
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.AllyActionsEnabled = false;
            arena.EnemyActionsEnabled = false;
            arena.PlayerAmmo.SpawningEnabled = false;
            arena.EnemyAmmo.SpawningEnabled = false;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arena != null) Object.Destroy(arena.gameObject);
            yield return null;
        }
        [Test]
        public void ActualSceneCreatesSeparate25SlotDecksAndShipSpawnLayouts()
        {
            Assert.That(arena.AmmoSetup.player.slots.Length, Is.EqualTo(25));
            Assert.That(arena.AmmoSetup.enemy.slots.Length, Is.EqualTo(25));
            Assert.That(arena.PlayerAmmo.Deck, Is.Not.SameAs(arena.EnemyAmmo.Deck));
            foreach (var spawner in new[] { arena.PlayerAmmo, arena.EnemyAmmo })
            {
                Assert.That(spawner.Pickups.Count, Is.EqualTo(8));
                Assert.That(spawner.Deck.ActiveCount, Is.EqualTo(8));
                Assert.That(spawner.Deck.WaitingCount, Is.EqualTo(17));
                foreach (var pickup in spawner.Pickups)
                {
                    Assert.That(arena.CanCrewStand(spawner.TeamSide, pickup.transform.position), Is.True);
                    Assert.That(pickup.Definition.Hardness, Is.GreaterThan(0));
                    Assert.That(pickup.Round, Is.TypeOf<AmmoDeckRound>());
                }
                Assert.That(spawner.TrySpawn(), Is.Null, "Occupied spawn points cannot receive another pickup");
            }
            Assert.That(arena.AmmoSetup.GetLayout(TeamSide.Player).id, Is.Not.EqualTo(arena.AmmoSetup.GetLayout(TeamSide.Enemy).id));
        }
        [Test]
        public void ScriptableDeckRejectsWrongSizeAndAllowsRepeatedDefinitions()
        {
            var asset = ScriptableObject.CreateInstance<AmmoDeckDefinition>();
            try
            {
                var ammo = arena.PlayerAmmo.Pickups[0].Definition;
                Assert.Throws<ArgumentException>(() => asset.Initialize(Enumerable.Repeat(ammo, 24).ToArray()));
                Assert.Throws<ArgumentException>(() => asset.Initialize(new AmmoDefinition[25]));
                asset.Initialize(Enumerable.Repeat(ammo, 25).ToArray());
                var deck = asset.CreateState(42);
                var rounds = Enumerable.Range(0, 25).Select(_ => deck.TrySpawn()).ToArray();
                Assert.That(rounds.Select(round => round.Slot).Distinct().Count(), Is.EqualTo(25));
                Assert.That(rounds.All(round => ReferenceEquals(round.Definition, ammo)), Is.True);
                Assert.That(deck.TrySpawn(), Is.Null);
            }
            finally { Object.Destroy(asset); }
        }
        [UnityTest]
        public IEnumerator DestroyingConsumedPickupDoesNotReturnCarriedRound()
        {
            var pickup = arena.PlayerAmmo.Pickups[0];
            var round = (AmmoDeckRound)pickup.Round;
            Assert.That(pickup.TryPickup(Inventory), Is.True);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Carried));
            Assert.That(Inventory.CarriedRound, Is.SameAs(round));
            Assert.That(Inventory.CarriedAmmo, Is.SameAs(pickup.Definition));
            Assert.That(pickup.TryPickup(Inventory), Is.False);
            Assert.That(Inventory.TryPickup(round), Is.False, "Another inventory cannot claim an already carried round");
            yield return null;
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(17));
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Carried));
            Assert.That(Inventory.Count, Is.EqualTo(1));
        }
        [Test]
        public void LoadAndFireTransfersOneRoundThenReturnsItAfterImpact()
        {
            var pickup = arena.PlayerAmmo.Pickups[0];
            var round = (AmmoDeckRound)pickup.Round;
            pickup.TryPickup(Inventory);
            Assert.That(arena.PlayerCannon.TryLoadFrom(Inventory), Is.True);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Loaded));
            Assert.That(arena.PlayerCannon.LoadedRound, Is.SameAs(round));
            Assert.That(Inventory.Count, Is.Zero);
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(17));
            Assert.That(arena.PlayerCannon.FireAt(arena.PlayerShip), Is.False);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Loaded));
            Assert.That(arena.PlayerCannon.FireAt(arena.EnemyShip), Is.True);
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(75));
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(18));
            Assert.That(round.ReturnToDeck(), Is.False);
            Assert.That(arena.PlayerCannon.IsLoaded, Is.False);
        }
        [Test]
        public void TimeoutOnlyExpiresGroundRoundsAndFreedPointCanRespawn()
        {
            var pickup = arena.PlayerAmmo.Pickups[0];
            var round = (AmmoDeckRound)pickup.Round;
            pickup.TickLifetime(AmmoDeckSpawner.GroundLifetime - 1);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Ground));
            pickup.TickLifetime(1);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(18));
            Assert.That(pickup.GetComponent<BoxCollider2D>().enabled, Is.False);
            var replacement = arena.PlayerAmmo.TrySpawn();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.Round, Is.Not.SameAs(round));
            Assert.That(replacement.SpawnPointIndex, Is.EqualTo(pickup.SpawnPointIndex));
            Assert.That(arena.PlayerAmmo.Pickups.Count, Is.EqualTo(8));
            replacement.TryPickup(Inventory);
            replacement.TickLifetime(100);
            Assert.That(((AmmoDeckRound)replacement.Round).Stage, Is.EqualTo(AmmoRoundStage.Carried));
        }
        [Test]
        public void TimedSpawnReplenishesOneVacantPointWithoutExceedingGroundLimit()
        {
            var pickup = arena.PlayerAmmo.Pickups[0];
            pickup.TryPickup(Inventory);
            arena.PlayerAmmo.SpawningEnabled = true;
            arena.PlayerAmmo.Tick(AmmoDeckSpawner.SpawnInterval);
            Assert.That(arena.PlayerAmmo.Pickups.Count, Is.EqualTo(8));
            Assert.That(arena.PlayerAmmo.Deck.ActiveCount, Is.EqualTo(9), "Carried rounds still consume deck slots");
            arena.PlayerAmmo.Tick(AmmoDeckSpawner.SpawnInterval);
            Assert.That(arena.PlayerAmmo.Deck.ActiveCount, Is.EqualTo(9));
        }
        [Test]
        public void FailedPickupDoesNotReleaseOrDuplicateDeckInstance()
        {
            var pickup = arena.PlayerAmmo.Pickups[0];
            var round = (AmmoDeckRound)pickup.Round;
            var defender = arena.PlayerCrew[4].GetComponent<CrewAmmoInventory>();
            Assert.That(pickup.TryPickup(defender), Is.False);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Ground));
            Assert.That(arena.PlayerAmmo.Deck.ActiveCount, Is.EqualTo(8));
            Assert.That(pickup.GetComponent<BoxCollider2D>().enabled, Is.True);
        }
        [Test]
        public void DeathReturnsEveryCarriedRoundAndClearsWeight()
        {
            var member = arena.PlayerCrew[2].GetComponent<CrewMember>();
            var inventory = member.GetComponent<CrewAmmoInventory>();
            var first = (AmmoDeckRound)arena.PlayerAmmo.Pickups[0].Round;
            arena.PlayerAmmo.Pickups[0].TryPickup(inventory);
            var second = (AmmoDeckRound)arena.PlayerAmmo.Pickups[0].Round;
            arena.PlayerAmmo.Pickups[0].TryPickup(inventory);
            Assert.That(inventory.Count, Is.EqualTo(2));
            member.ApplyDamage(1000);
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(inventory.CurrentWeight, Is.Zero);
            Assert.That(first.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(second.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(19));
        }
        [UnityTest]
        public IEnumerator DestroyingInventoryAndLoadedCannonReturnsTheirRounds()
        {
            var member = arena.PlayerCrew[2].GetComponent<CrewMember>();
            var inventory = member.GetComponent<CrewAmmoInventory>();
            var carried = (AmmoDeckRound)arena.PlayerAmmo.Pickups[0].Round;
            arena.PlayerAmmo.Pickups[0].TryPickup(inventory);
            var loaded = (AmmoDeckRound)arena.PlayerAmmo.Pickups[0].Round;
            arena.PlayerAmmo.Pickups[0].TryPickup(Inventory);
            var item = new GameObject("Disposable Cannon");
            item.transform.SetParent(arena.transform);
            var cannon = item.AddComponent<CannonController>();
            cannon.Initialize(arena.PlayerShip, arena.EnemyShip);
            cannon.TryLoadFrom(Inventory);
            Object.Destroy(member.gameObject);
            Object.Destroy(item);
            yield return null;
            Assert.That(carried.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(loaded.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(19));
        }
        [UnityTest]
        public IEnumerator DestroyingUnconsumedPickupReturnsIt()
        {
            var pickup = arena.PlayerAmmo.Pickups[0];
            var round = (AmmoDeckRound)pickup.Round;
            Object.Destroy(pickup.gameObject);
            yield return null;
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(18));
        }
        [Test]
        public void All25CarriedRoundsExhaustRealSpawnerUntilTheyAreReturned()
        {
            var member = arena.PlayerCrew[2].GetComponent<CrewMember>();
            member.Definition.Initialize("deck-test", "Deck Test", CharacterCapability.CarryAmmo, 4, 5, 100, 25);
            var inventory = member.GetComponent<CrewAmmoInventory>();
            for (var index = 0; index < 25; index++)
            {
                Assert.That(arena.PlayerAmmo.TrySupply(inventory), Is.True, "Supply slot " + index);
                arena.PlayerAmmo.TrySpawn();
            }
            Assert.That(inventory.Count, Is.EqualTo(25));
            Assert.That(inventory.State.Items.Cast<AmmoDeckRound>().Select(round => round.Slot).Distinct().Count(), Is.EqualTo(25));
            Assert.That(arena.PlayerAmmo.Deck.ActiveCount, Is.EqualTo(25));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.Zero);
            Assert.That(arena.PlayerAmmo.Pickups.Count, Is.Zero);
            Assert.That(arena.PlayerAmmo.TrySpawn(), Is.Null);
            inventory.ReleaseAll();
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(25));
            Assert.That(arena.PlayerAmmo.TrySpawn(), Is.Not.Null);
        }
        [Test]
        public void EnemySupplyUsesItsOwnDeckAndPreservesPlayerDeck()
        {
            var inventory = arena.EnemyCrew[0].GetComponent<CrewAmmoInventory>();
            Assert.That(arena.PlayerAmmo.TrySupply(inventory), Is.False);
            Assert.That(arena.EnemyAmmo.TrySupply(inventory), Is.True);
            var round = (AmmoDeckRound)inventory.CarriedRound;
            var cannon = Object.FindObjectsByType<CannonController>(FindObjectsSortMode.None)
                .Single(item => item.OwnerShip == arena.EnemyShip);
            Assert.That(cannon.TryLoadFrom(inventory), Is.True);
            Assert.That(cannon.FireAt(arena.PlayerShip), Is.True);
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(arena.PlayerAmmo.Deck.WaitingCount, Is.EqualTo(17));
            Assert.That(arena.EnemyAmmo.Deck.WaitingCount, Is.EqualTo(18));
        }
        [UnityTest]
        public IEnumerator SceneTeardownClosesDeckAndReturnsAllLiveHandles()
        {
            var deck = arena.PlayerAmmo.Deck;
            var round = (AmmoDeckRound)arena.PlayerAmmo.Pickups[0].Round;
            Object.Destroy(arena.gameObject);
            yield return null;
            Assert.That(deck.ActiveCount, Is.Zero);
            Assert.That(deck.WaitingCount, Is.EqualTo(25));
            Assert.That(round.Stage, Is.EqualTo(AmmoRoundStage.Returned));
            Assert.That(deck.TrySpawn(), Is.Null, "Closed decks cannot create late scene objects");
        }
    }
}
