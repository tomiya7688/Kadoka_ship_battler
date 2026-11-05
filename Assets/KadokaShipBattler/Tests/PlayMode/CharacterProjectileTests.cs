using System.Collections;
using System.Collections.Generic;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KadokaShipBattler.Tests
{
    public sealed class CharacterProjectileTests
    {
        private readonly List<Object> objects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear();
            yield return null;
        }

        private ShipController Ship(TeamSide side, float x)
        {
            var item = new GameObject(side + " Ship"); objects.Add(item);
            item.transform.position = new Vector3(x, 0, 0);
            var ship = item.AddComponent<ShipController>(); ship.Initialize(side); return ship;
        }

        private CrewMember Passenger(TeamSide side, float hardness)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); objects.Add(definition);
            definition.Initialize(new CharacterStats
            {
                id = "p", displayName = "P", capabilities = CharacterCapability.Combat, moveSpeed = 4, combatSkill = 5,
                carrySkill = 5, carryCapacity = 5, maxCarryCount = 1, maxHp = 50, attackMode = NormalAttackMode.Contact,
                attackDamage = 10, attackRange = 0.9f, projectileHardness = hardness
            });
            var item = new GameObject("Passenger"); objects.Add(item);
            var crew = item.AddComponent<CrewMember>(); crew.Initialize(definition, side);
            item.AddComponent<CrewAmmoInventory>(); item.AddComponent<PlayerCrewController>();
            return crew;
        }

        private CannonController Cannon(ShipController owner, ShipController target)
        {
            var item = new GameObject("Cannon"); objects.Add(item);
            item.transform.position = owner.transform.position;
            var cannon = item.AddComponent<CannonController>(); cannon.Initialize(owner, target); return cannon;
        }

        [Test]
        public void EnemyCharacterCannotBeLoaded()
        {
            var player = Ship(TeamSide.Player, 0);
            var cannon = Cannon(player, Ship(TeamSide.Enemy, 3));
            Assert.IsFalse(cannon.TryLoadCharacter(Passenger(TeamSide.Enemy, 1)));
            Assert.IsFalse(cannon.IsLoaded);
        }

        [Test]
        public void DirectlyControlledEmptyHandedCrewLoadsItselfByInteracting()
        {
            var player = Ship(TeamSide.Player, 0);
            var cannon = Cannon(player, Ship(TeamSide.Enemy, 3));
            var passenger = Passenger(TeamSide.Player, 2);
            var controller = passenger.GetComponent<PlayerCrewController>();
            var inventory = passenger.GetComponent<CrewAmmoInventory>();
            // 自動操作のままでは装填しません。
            Assert.IsFalse(cannon.TryInteract(passenger, inventory));
            Assert.IsFalse(cannon.IsLoaded);
            controller.SetDirectControl(true);
            Assert.IsTrue(cannon.TryInteract(passenger, inventory));
            Assert.AreSame(passenger, cannon.LoadedCharacter);
        }

        [UnityTest]
        public IEnumerator LoadedCharacterFliesAndLandsThenResumesActing()
        {
            var player = Ship(TeamSide.Player, 0);
            var enemy = Ship(TeamSide.Enemy, 3);
            var cannon = Cannon(player, enemy);
            var passenger = Passenger(TeamSide.Player, 2);
            Assert.IsTrue(cannon.TryLoadCharacter(passenger));
            Assert.IsFalse(passenger.GetComponent<PlayerCrewController>().enabled);
            Assert.IsTrue(cannon.FireAt(enemy));
            Assert.IsFalse(cannon.IsLoaded);
            var projectile = passenger.GetComponent<CharacterProjectile>();
            Assert.AreEqual(ProjectileFlightStatus.Flying, projectile.Model.Status);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(ProjectileFlightStatus.Landed, projectile.Model.Status);
            Assert.IsTrue(passenger.IsAlive);
            Assert.IsTrue(passenger.GetComponent<PlayerCrewController>().enabled);
            Assert.IsTrue(enemy.ContainsDeckPoint(passenger.transform.position) || Vector2.Distance(passenger.transform.position, enemy.transform.position) < 0.01f);
        }

        [UnityTest]
        public IEnumerator HarderCharacterShootsDownSofterOne()
        {
            var player = Ship(TeamSide.Player, 0);
            var enemy = Ship(TeamSide.Enemy, 6);
            var hard = Passenger(TeamSide.Player, 5);
            var soft = Passenger(TeamSide.Player, 1);
            var first = CharacterProjectile.Launch(hard, Vector2.zero, enemy);
            var second = CharacterProjectile.Launch(soft, Vector2.zero, enemy);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(ProjectileFlightStatus.Destroyed, second.Model.Status);
            Assert.IsFalse(soft.IsAlive);
            Assert.AreNotEqual(ProjectileFlightStatus.Destroyed, first.Model.Status);
            Assert.IsTrue(hard.IsAlive);
        }
    }
}
