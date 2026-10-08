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
    /* { 責務: 実際の戦闘を使う予約・共有試験の準備と後始末をまとめます。 } */
    public abstract class CrewCoordinationFixture
    {
        protected BattlePrototype Arena;
        protected CrewTeamCoordinator Coordinator => Arena.GetComponent<CrewTeamCoordinator>();

        /* { 処理: 他のAIや弾出現を停止し、指定する担当だけを進める実際のシーンを準備します。 } */
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 直接操作を試験対象外のDefenderにして、LeaderとGunnerをAIに使います。
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            Arena = Object.FindFirstObjectByType<BattlePrototype>();
            Arena.AllyActionsEnabled = false;
            Arena.EnemyActionsEnabled = false;
            Arena.PlayerAmmo.SpawningEnabled = false;
            Arena.EnemyAmmo.SpawningEnabled = false;
            Arena.Controls.TrySwitch(Arena.PlayerCrew[4]);
            foreach (var actor in Arena.PlayerCrew) actor.GetComponent<CrewAmmoAiController>().Suspend();
            foreach (var pickup in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None))
                pickup.GetComponent<Collider2D>().enabled = false;
            yield return null;
            Coordinator.Observations.Clear();
            Coordinator.Reservations.Clear();
        }

        /* { 処理: 戦闘内の全ての状態を破棄します。 } */
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 予約や生成物を次の試験へ引き継ぎません。
            Object.Destroy(Arena.gameObject);
            yield return null;
        }

        /* { 処理: 船員の位置と向きを固定します。戻り値: 対象の操作コンポーネントです。 } */
        protected PlayerCrewController PlaceCrew(int slot, Vector2 point, Vector2 facing)
        {
            // 壁や能力条件は本番のまま使います。
            var actor = Arena.PlayerCrew[slot];
            actor.transform.position = point;
            actor.Face(facing);
            return actor;
        }

        /* { 処理: 必要な船員のAIだけを再開します。戻り値: 実際の実行コンポーネントです。 } */
        protected CrewAmmoAiController RunCrew(PlayerCrewController actor)
        {
            // 操作状態の切替と同じ入口を使います。
            Arena.AllyActionsEnabled = true;
            actor.SetDirectControl(false);
            return actor.GetComponent<CrewAmmoAiController>();
        }

        /* { 処理: 自船デッキの実在する弾を視界に配置します。 } */
        protected AmmoPickup PlaceAmmo(int slot, Vector2 point)
        {
            // デッキの保持数と返却を含めて実際の弾を使います。
            var pickup = Arena.PlayerAmmo.Pickups[slot];
            pickup.transform.position = point;
            pickup.GetComponent<Collider2D>().enabled = true;
            return pickup;
        }

        /* { 処理: 指定船員の現在の視界をチームへ報告します。 } */
        protected void Report(PlayerCrewController observer)
        {
            // テスト側から共有観測の値を捏造せず、本番センサーを通します。
            observer.GetComponent<VisionSensor>().Scan();
            observer.GetComponent<CrewTaskAssignment>().PublishPerception();
        }
    }
}
