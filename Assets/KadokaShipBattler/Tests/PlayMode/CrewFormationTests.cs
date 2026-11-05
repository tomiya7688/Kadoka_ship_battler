using System.Collections;
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
    public sealed class CrewFormationTests
    {
        private BattlePrototype arena;
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
            yield return null;
        }
        private int DirectControlCount()
        {
            var count = 0;
            foreach (var actor in arena.PlayerCrew) if (actor.IsDirectlyControlled) count++;
            return count;
        }

        [Test]
        public void JsonSpawnsFiveMembersPerSideUsingTheSameComponents()
        {
            Assert.That(arena.PlayerCrew.Count, Is.EqualTo(5));
            Assert.That(arena.EnemyCrew.Count, Is.EqualTo(5));
            for (var slot = 0; slot < 5; slot++)
            {
                var player = arena.PlayerCrew[slot].GetComponent<CrewMember>();
                var enemy = arena.EnemyCrew[slot];
                Assert.That(player.Definition.CharacterId, Is.EqualTo(arena.CrewSetup.player.slots[slot]));
                Assert.That(enemy.Definition.CharacterId, Is.EqualTo(arena.CrewSetup.enemy.slots[slot]));
                Assert.That(player.TeamSide, Is.EqualTo(TeamSide.Player));
                Assert.That(enemy.TeamSide, Is.EqualTo(TeamSide.Enemy));
                Assert.That(enemy.GetComponent<PlayerCrewController>(), Is.Not.Null);
                Assert.That(enemy.GetComponent<CrewAmmoInventory>(), Is.Not.Null);
                Assert.That(player.CurrentHp, Is.EqualTo(player.Definition.MaxHp));
                Assert.That(enemy.CurrentHp, Is.EqualTo(enemy.Definition.MaxHp));
                Assert.That(player.Can(CharacterCapability.Repair), Is.False);
                Assert.That(enemy.Can(CharacterCapability.Repair), Is.False);
            }
            Assert.That(DirectControlCount(), Is.EqualTo(1));
            Assert.That(arena.PlayerController, Is.SameAs(arena.PlayerCrew[0]));
        }

        [Test]
        public void TabCycleVisitsEverySlotAndWrapsToLeader()
        {
            for (var slot = 1; slot <= 5; slot++)
            {
                arena.Controls.ApplyInput(new CrewInput(Vector2.zero, switchNext: true), 0.1f);
                Assert.That(arena.PlayerController, Is.SameAs(arena.PlayerCrew[slot % 5]));
                Assert.That(DirectControlCount(), Is.EqualTo(1));
            }
        }

        [Test]
        public void CharacterStatsAndFlagsComeFromJsonAndRepairIsNeverSelected()
        {
            var gunner = arena.PlayerCrew[1].GetComponent<CrewMember>().Definition;
            var carrier = arena.PlayerCrew[2].GetComponent<CrewMember>().Definition;
            var scout = arena.PlayerCrew[3].GetComponent<CrewMember>().Definition;
            var defender = arena.PlayerCrew[4].GetComponent<CrewMember>().Definition;
            Assert.That(gunner.AttackMode, Is.EqualTo(NormalAttackMode.Ranged));
            Assert.That(gunner.AttackDamage, Is.EqualTo(40));
            Assert.That(gunner.AttackRange, Is.EqualTo(2.5f));
            Assert.That(carrier.CarryCapacity, Is.EqualTo(9));
            Assert.That(carrier.MaxCarryCount, Is.EqualTo(3));
            Assert.That(carrier.AttackMode, Is.EqualTo(NormalAttackMode.None));
            Assert.That(scout.CanFly && scout.CanPhase, Is.True);
            Assert.That(defender.MaxHp, Is.EqualTo(180));
            Assert.That(defender.ProjectileHardness, Is.EqualTo(5));
            foreach (var actor in arena.PlayerCrew)
                Assert.That(actor.gameObject.AddComponent<UtilityAiAgent>().SelectAction(), Is.Not.EqualTo(AiActionType.Repair));
        }

        [Test]
        public void NormalAttackDamagesOnlyEnemyAndHealthIsPerInstance()
        {
            var actor = arena.PlayerCrew[0];
            var member = actor.GetComponent<CrewMember>();
            var enemy = arena.EnemyCrew[0];
            enemy.transform.position = actor.transform.position + Vector3.right * 0.6f;
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero, attack: true), 0.1f);
            Assert.That(enemy.CurrentHp, Is.EqualTo(75));
            Assert.That(member.CurrentHp, Is.EqualTo(100));
            Assert.That(member.TryAttack(arena.PlayerCrew[1].GetComponent<CrewMember>()), Is.False);
            Assert.That(enemy.ApplyDamage(float.NaN), Is.False);
        }

        [Test]
        public void RangedAttackUsesItsRangeAndCurrentFacing()
        {
            var contact = arena.PlayerCrew[0];
            var ranged = arena.PlayerCrew[1];
            var enemy = arena.EnemyCrew[0];
            contact.transform.position = ranged.transform.position = new Vector3(-4, 0, 0);
            enemy.transform.position = new Vector3(-2.2f, 0, 0);
            Assert.That(contact.TryAttack(), Is.False);
            ranged.Face(Vector2.left);
            Assert.That(ranged.TryAttack(), Is.False, "Ranged attack must not hit a target behind its facing");
            ranged.Face(Vector2.right);
            arena.Controls.TrySwitch(ranged);
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero, attack: true), 0.1f);
            Assert.That(enemy.CurrentHp, Is.EqualTo(60));
        }

        [Test]
        public void LethalDamageMakesCrewUnavailableAndTransfersControl()
        {
            var leader = arena.PlayerCrew[0];
            var member = leader.GetComponent<CrewMember>();
            Assert.That(member.ApplyDamage(1000), Is.True);
            Assert.That(member.CurrentHp, Is.Zero);
            Assert.That(member.IsAlive, Is.False);
            Assert.That(leader.TryInteract(), Is.False);
            Assert.That(leader.TryAttack(), Is.False);
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero), 0.1f);
            Assert.That(arena.PlayerController, Is.SameAs(arena.PlayerCrew[1]));
            Assert.That(DirectControlCount(), Is.EqualTo(1));
            Assert.That(arena.Controls.TrySwitch(leader), Is.False);
            Assert.That(leader.GetComponent<CrewAmmoAiController>().IsRunning, Is.False);
        }

        [Test]
        public void AdditionalDefinitionCanReplaceAFormationSlot()
        {
            var setup = JsonUtility.FromJson<BattleCrewDefinition>(JsonUtility.ToJson(arena.CrewSetup));
            var extra = new CharacterStats { id = "new-ally", displayName = "New Ally", maxHp = 77,
                moveSpeed = 3, carryCapacity = 7, maxCarryCount = 2, canFly = true,
                capabilities = CharacterCapability.CarryAmmo };
            var entries = new CharacterStats[setup.characters.Length + 1];
            setup.characters.CopyTo(entries, 0);
            entries[entries.Length - 1] = extra;
            setup.characters = entries;
            setup.player.slots[4] = extra.id;
            var changed = JsonUtility.FromJson<BattleCrewDefinition>(JsonUtility.ToJson(setup));
            var selected = changed.GetTeam(TeamSide.Player)[4];
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.Initialize(selected);
            var item = new GameObject("Data Added Crew");
            item.transform.SetParent(arena.transform);
            var member = item.AddComponent<CrewMember>();
            member.Initialize(definition, TeamSide.Player);
            Assert.That(member.CurrentHp, Is.EqualTo(77));
            Assert.That(member.Definition.CanFly, Is.True);
            Assert.That(item.AddComponent<CrewAmmoInventory>().CarryCapacity, Is.EqualTo(7));
            Object.Destroy(definition);
        }

        [Test]
        public void InvalidFormationAndRepairDataAreRejected()
        {
            var setup = JsonUtility.FromJson<BattleCrewDefinition>(JsonUtility.ToJson(arena.CrewSetup));
            setup.player.slots = new[] { "leader", "gunner" };
            Assert.Throws<System.ArgumentException>(() => setup.Validate());
            setup = JsonUtility.FromJson<BattleCrewDefinition>(JsonUtility.ToJson(arena.CrewSetup));
            setup.characters[0].capabilities |= CharacterCapability.Repair;
            Assert.Throws<System.ArgumentException>(() => setup.Validate());
        }

        [Test]
        public void EnemyActorUsesItsOwnDeckAndBridgeRules()
        {
            var actor = arena.EnemyCrew[0].GetComponent<PlayerCrewController>();
            actor.Move(Vector2.left, 0.4f);
            Assert.That(actor.transform.position.x, Is.EqualTo(2), "Intact player hull blocks enemy boarding");
            arena.PlayerShip.ApplyDamage(100);
            for (var step = 0; step < 45; step++) actor.Move(Vector2.left, 0.025f);
            Assert.That(actor.transform.position.x, Is.LessThan(-2));
        }

        [UnityTest]
        public IEnumerator NoLivingCrewLeavesNoDirectControlWithoutEndingCoreBattle()
        {
            foreach (var actor in arena.PlayerCrew) actor.GetComponent<CrewMember>().ApplyDamage(1000);
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero), 0.1f);
            yield return null;
            Assert.That(arena.PlayerController, Is.Null);
            Assert.That(DirectControlCount(), Is.Zero);
            Assert.That(arena.IsFinished, Is.False, "Only core destruction decides the battle");
        }
    }
}
