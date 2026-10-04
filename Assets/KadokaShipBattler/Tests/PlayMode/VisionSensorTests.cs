using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KadokaShipBattler.AI;
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
    public sealed class VisionSensorTests
    {
        private BattlePrototype arena;
        private readonly List<ScriptableObject> assets = new();
        private PlayerCrewController Observer => arena.PlayerCrew[0];
        private VisionSensor Sensor => Observer.GetComponent<VisionSensor>();
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.EnemyActionsEnabled = false;
            arena.AllyActionsEnabled = false;
            arena.PlayerAmmo.SpawningEnabled = false;
            arena.EnemyAmmo.SpawningEnabled = false;
            // These fixtures isolate the sensor; navigation tests cover the scene's room walls.
            foreach (var wall in Object.FindObjectsByType<VisionBlocker>(FindObjectsSortMode.None)) wall.GetComponent<Collider2D>().enabled = false;
            foreach (var pickup in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None)) pickup.GetComponent<Collider2D>().enabled = false;
            foreach (var crew in Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None)) crew.GetComponent<Collider2D>().enabled = false;
            foreach (var core in Object.FindObjectsByType<ShipCore>(FindObjectsSortMode.None)) core.GetComponent<Collider2D>().enabled = false;
            Observer.transform.position = new Vector3(-5, 0, 0);
            Observer.Face(Vector2.down);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arena != null) Object.Destroy(arena.gameObject);
            foreach (var asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            yield return null;
        }
        private AmmoPickup Ammo(Vector2 position, Transform parent = null)
        {
            var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
            definition.Initialize("vision-ammo", "Vision Ammo", 25, 2);
            assets.Add(definition);
            var item = new GameObject("Vision Target Ammo");
            item.transform.SetParent(parent != null ? parent : arena.transform);
            item.transform.position = position;
            item.transform.localScale = Vector3.one * 0.2f;
            var pickup = item.AddComponent<AmmoPickup>();
            pickup.Initialize(definition);
            return pickup;
        }
        private CrewMember Crew(Vector2 position)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.Initialize(new CharacterStats { id = "vision-crew", displayName = "Vision Crew", maxHp = 80 });
            assets.Add(definition);
            var item = new GameObject("Vision Target Crew");
            item.transform.SetParent(arena.transform);
            item.transform.position = position;
            item.transform.localScale = Vector3.one * 0.2f;
            var member = item.AddComponent<CrewMember>();
            member.Initialize(definition, TeamSide.Enemy);
            return member;
        }
        private VisionBlocker Wall(Vector2 position, Vector2 size, bool trigger = false)
        {
            var item = new GameObject("Vision Wall");
            item.transform.SetParent(arena.transform);
            item.transform.position = position;
            var collider = item.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = trigger;
            return item.AddComponent<VisionBlocker>();
        }
        private VisionObservation Sample(int id) => Sensor.Observations.Single(sample => sample.TargetId == id);

        [TestCase(0, -1, true)]
        [TestCase(0, 1, false)]
        [TestCase(5, -1, false)]
        [TestCase(0, -6, true)]
        [TestCase(0, -6.01f, false)]
        [TestCase(1.7320508f, -1, true)]
        public void OnlyTargetsInsideForwardConeAndCenterDistanceAreObserved(float x, float y, bool expected)
        {
            var target = Ammo(new Vector2(-5 + x, y));
            Sensor.Scan();
            Assert.That(Sensor.Observations.Any(sample => sample.TargetId == target.GetInstanceID()), Is.EqualTo(expected));
            Assert.That(Sensor.FacingDirection, Is.EqualTo(Vector2.down));
            Assert.That(Sensor.ObservationOrigin, Is.EqualTo(new Vector2(-5, 0)));
        }
        [Test]
        public void ConfigurationChangesAngleAndDistanceAndRejectsInvalidValues()
        {
            var target = Ammo(new Vector2(-4, -1));
            Sensor.Configure(60, 6);
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
            Sensor.Configure(90, 2);
            Sensor.Scan();
            Assert.That(Sensor.GetVisibleAmmo(Sample(target.GetInstanceID())), Is.SameAs(target));
            Sensor.Configure(360, 1);
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => Sensor.Configure(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Sensor.Configure(361, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Sensor.Configure(120, float.NaN));
            Assert.That(Sensor.ViewAngle, Is.EqualTo(360), "Invalid configuration must not partially change the sensor");
        }
        [TestCase(false)]
        [TestCase(true)]
        public void WallOcclusionAndOpeningAClosedDoorChangeObservations(bool trigger)
        {
            var target = Ammo(new Vector2(-5, -1));
            var wall = Wall(new Vector2(-5, -0.5f), new Vector2(1, 0.1f), trigger);
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
            wall.enabled = false;
            Sensor.Scan();
            var sample = Sample(target.GetInstanceID());
            Assert.That(Sensor.GetVisibleAmmo(sample), Is.SameAs(target));
            wall.enabled = true;
            Assert.That(Sensor.GetVisibleAmmo(sample), Is.Null, "A newly closed door must invalidate even the previous scan");
            Assert.That(Sensor.Observations, Is.Empty);
            wall.GetComponent<Collider2D>().enabled = false;
            Sensor.Scan();
            Assert.That(Sensor.Observations.Count, Is.EqualTo(1));
        }
        [Test]
        public void BlockersAndTargetsWorkWhenGlobalTriggerQueriesAreDisabled()
        {
            var previous = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = false;
                var target = Ammo(new Vector2(-5, -1));
                Sensor.Scan();
                Assert.That(Physics2D.queriesHitTriggers, Is.False, "Scan must restore the original global setting");
                Assert.That(Sensor.GetVisibleAmmo(Sample(target.GetInstanceID())), Is.SameAs(target));
                Assert.That(Physics2D.queriesHitTriggers, Is.False, "Action validation must also restore the original setting");
                Wall(new Vector2(-5, -0.5f), new Vector2(1, 0.1f), true);
                Sensor.Scan();
                Assert.That(Sensor.Observations, Is.Empty);
                Assert.That(Physics2D.queriesHitTriggers, Is.False);
            }
            finally { Physics2D.queriesHitTriggers = previous; }
        }
        [Test]
        public void ObserverInsideWallCannotSeeThroughItEvenWhenRayStartsInsideAreDisabled()
        {
            var previous = Physics2D.queriesStartInColliders;
            try
            {
                Physics2D.queriesStartInColliders = false;
                Ammo(new Vector2(-5, -1));
                Wall(new Vector2(-5, 0), new Vector2(0.3f, 0.3f));
                Sensor.Scan();
                Assert.That(Sensor.Observations, Is.Empty);
            }
            finally { Physics2D.queriesStartInColliders = previous; }
        }
        [Test]
        public void OrdinaryCollidersDoNotOccludeAndMultipleChildCollidersDoNotDuplicateTargets()
        {
            var target = Ammo(new Vector2(-5, -1));
            var child = new GameObject("Second Target Collider");
            child.transform.SetParent(target.transform, false);
            child.AddComponent<BoxCollider2D>().isTrigger = true;
            var wall = Wall(new Vector2(-5, -0.5f), new Vector2(1, 0.1f));
            Object.DestroyImmediate(wall);
            Sensor.Scan();
            Assert.That(Sensor.Observations.Count, Is.EqualTo(1));
            Assert.That(Sensor.GetVisibleAmmo(Sample(target.GetInstanceID())), Is.SameAs(target));
        }
        [Test]
        public void OffscreenCrewHasNoLiveStateInTheCacheAndOldSamplesCannotAttack()
        {
            var target = Crew(new Vector2(-5, -0.5f));
            Sensor.Scan();
            var previousList = Sensor.Observations;
            var sample = Sample(target.GetInstanceID());
            Assert.That(sample.Hp, Is.EqualTo(80));
            target.transform.position = new Vector3(-5, 1, 0);
            target.ApplyDamage(10);
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
            Assert.That(sample.Hp, Is.EqualTo(80));
            Assert.That(sample.Y, Is.EqualTo(-0.5f));
            Assert.That(previousList.Single(), Is.SameAs(sample));
            Assert.That(Observer.TryAttackVisible(sample), Is.False);
            target.transform.position = new Vector3(-5, -0.5f, 0);
            Sensor.Scan();
            Assert.That(Sample(target.GetInstanceID()).Hp, Is.EqualTo(70));
            Assert.That(Observer.TryAttackVisible(sample), Is.False, "A previous scan is not current even when the target reappears");
            Assert.That(Observer.TryAttackVisible(Sample(target.GetInstanceID())), Is.True);
        }
        [Test]
        public void MovingTargetBehindObserverInvalidatesAnActionBeforeAnotherScan()
        {
            var target = Ammo(new Vector2(-5, -0.5f));
            Sensor.Scan();
            var sample = Sample(target.GetInstanceID());
            target.transform.position = new Vector3(-5, 0.5f, 0);
            Assert.That(Sensor.TryPickupVisible(sample, Observer.GetComponent<CrewAmmoInventory>()), Is.False);
            Assert.That(Sensor.Observations, Is.Empty);
        }
        [UnityTest]
        public IEnumerator PickupDeathDisableAndDestroyRemoveTargets()
        {
            var target = Ammo(new Vector2(-5, -0.5f));
            Sensor.Scan();
            target.enabled = false;
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
            target.enabled = true;
            Sensor.Scan();
            Assert.That(Sensor.TryPickupVisible(Sample(target.GetInstanceID()), Observer.GetComponent<CrewAmmoInventory>()), Is.True);
            Assert.That(Sensor.Observations, Is.Empty);
            var crew = Crew(new Vector2(-5, -0.5f));
            Sensor.Scan();
            crew.ApplyDamage(100);
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
            target = Ammo(new Vector2(-5, -0.6f));
            Sensor.Scan();
            var sample = Sample(target.GetInstanceID());
            Object.Destroy(target.gameObject);
            yield return null;
            Assert.That(Sensor.CanStillSee(sample), Is.False);
            Assert.That(Sensor.Observations, Is.Empty);
        }
        [Test]
        public void ObserverDisableAndDeathClearItsCache()
        {
            Ammo(new Vector2(-5, -0.5f));
            Sensor.Scan();
            Assert.That(Sensor.Observations.Count, Is.EqualTo(1));
            Sensor.enabled = false;
            Assert.That(Sensor.Observations, Is.Empty);
            Sensor.enabled = true;
            Sensor.Scan();
            Observer.GetComponent<CrewMember>().ApplyDamage(1000);
            Assert.That(Sensor.Observations, Is.Empty);
            Sensor.Scan();
            Assert.That(Sensor.Observations, Is.Empty);
        }
        [Test]
        public void ReleasedAmmoAiUsesTheSensorAndDoesNotInteractThroughWalls()
        {
            var pickup = Ammo(new Vector2(-5, -0.6f));
            var wall = Wall(new Vector2(-5, -0.3f), new Vector2(1, 0.1f));
            arena.Controls.CycleNext();
            var ai = Observer.GetComponent<CrewAmmoAiController>();
            Assert.That(ai.VisibleAmmo, Is.Null);
            Assert.That(ai.CurrentAction, Is.EqualTo(AiActionType.Idle));
            arena.AllyActionsEnabled = true;
            ai.Tick(0.1f);
            Assert.That(Observer.GetComponent<CrewAmmoInventory>().HasAmmo, Is.False);
            wall.enabled = false;
            Observer.Face(Vector2.down);
            ai.ResumeFromCurrentState();
            Assert.That(ai.VisibleAmmo, Is.SameAs(pickup));
            for (var step = 0; step < 5 && !Observer.GetComponent<CrewAmmoInventory>().HasAmmo; step++) ai.Tick(0.1f);
            Assert.That(Observer.GetComponent<CrewAmmoInventory>().HasAmmo, Is.True);
        }
        [Test]
        public void OwnCannonIsKnownMapInformationEvenWhenNoAmmoIsVisible()
        {
            var definition = ScriptableObject.CreateInstance<AmmoDefinition>();
            assets.Add(definition);
            definition.Initialize("held", "Held", 25, 2);
            Observer.GetComponent<CrewAmmoInventory>().TryPickup(definition);
            Wall(new Vector2(-4, 0), new Vector2(0.1f, 2));
            arena.Controls.CycleNext();
            var ai = Observer.GetComponent<CrewAmmoAiController>();
            Assert.That(ai.VisibleAmmo, Is.Null);
            Assert.That(ai.CurrentAction, Is.EqualTo(AiActionType.LoadCannon));
        }
        [Test]
        public void EnemyGuardOnlyAttacksObservedEnemiesAndCannotAttackThroughWalls()
        {
            var actor = arena.EnemyCrew[0].GetComponent<PlayerCrewController>();
            actor.transform.position = new Vector3(-5, 0, 0);
            actor.Face(Vector2.down);
            Observer.transform.position = new Vector3(-5, -0.5f, 0);
            Observer.GetComponent<Collider2D>().enabled = true;
            var wall = Wall(new Vector2(-5, -0.25f), new Vector2(1, 0.1f));
            arena.EnemyActionsEnabled = true;
            var guard = actor.GetComponent<CrewGuardAiController>();
            guard.Tick(1);
            Assert.That(Observer.GetComponent<CrewMember>().CurrentHp, Is.EqualTo(100));
            wall.enabled = false;
            actor.Face(Vector2.down);
            guard.Tick(1);
            Assert.That(Observer.GetComponent<CrewMember>().CurrentHp, Is.EqualTo(75));
        }
        [Test]
        public void EnemySupplyDoesNotReadOrCollectHiddenAmmo()
        {
            var actor = arena.EnemyCrew[0].GetComponent<PlayerCrewController>();
            actor.transform.position = new Vector3(2, 0, 0);
            actor.Face(Vector2.right);
            var pickup = arena.EnemyAmmo.Pickups[0];
            pickup.transform.position = new Vector3(3, 0, 0);
            pickup.GetComponent<Collider2D>().enabled = true;
            var wall = Wall(new Vector2(2.5f, 0), new Vector2(0.1f, 4));
            arena.EnemyActionsEnabled = true;
            var inventory = actor.GetComponent<CrewAmmoInventory>();
            for (var step = 0; step < 10; step++) arena.TickEnemy(0.1f);
            Assert.That(inventory.HasAmmo, Is.False);
            Assert.That(actor.transform.position.x, Is.EqualTo(2));
            wall.enabled = false;
            var carried = false;
            for (var step = 0; step < 100 && arena.PlayerShip.CurrentHull == 100; step++)
            {
                arena.TickEnemy(0.1f);
                carried |= inventory.HasAmmo;
            }
            Assert.That(carried, Is.True);
            Assert.That(arena.PlayerShip.CurrentHull, Is.EqualTo(75));
        }
    }
}
