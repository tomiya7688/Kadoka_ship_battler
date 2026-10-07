using System.Collections;
using System.Linq;
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
    /* {
        責務: 実際のシーンの方針画面、能力制限、AI評価、行動の連携を検証します。
        前提: 各試験は新しい戦闘を使います。実際のマウスやキーボード入力は生成しません。
    } */
    public sealed class AiPolicyTests
    {
        private BattlePrototype arena;
        private PlayerCrewController Actor => arena.PlayerCrew[0];
        private UtilityAiAgent Utility => Actor.GetComponent<UtilityAiAgent>();
        private CrewPolicyDirector Director => arena.GetComponent<CrewPolicyDirector>();
        private AiPolicyPanel Panel => arena.GetComponent<AiPolicyPanel>();

        /* { 処理: 実際の戦闘シーンを読み込み、試験用の観測と進行を固定します。 } */
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 自動進行を停止して、試験ごとに同じ判断条件を作ります。
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.AllyActionsEnabled = false;
            arena.EnemyActionsEnabled = false;
            arena.PlayerAmmo.SpawningEnabled = false;
            arena.EnemyAmmo.SpawningEnabled = false;
            foreach (var pickup in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None))
                pickup.GetComponent<Collider2D>().enabled = false;
            Actor.transform.position = new Vector3(-5, -0.5f);
            Actor.Face(Vector2.down);
            yield return null;
        }

        /* { 処理: 作成した戦闘と方針の状態を破棄します。 } */
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 次の試験へ設定や生成物を残しません。
            Object.Destroy(arena.gameObject);
            yield return null;
        }

        /* { 処理: 全員が一つのチーム設定を共有し、個人設定は独立することを確認します。 } */
        [Test]
        public void TeamAndIndividualSettingsAreIndependent()
        {
            // 画面の編集対象と直接操作する船員も独立しています。
            var controlledCrew = arena.Controls.Current;
            Assert.That(Panel.TrySelectCrew(2), Is.True);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.Supply), Is.True);
            Assert.That(Panel.TrySelectTeamPolicy(AiPolicyId.Defend), Is.True);
            Assert.That(Director.GetIndividualPolicy(arena.PlayerCrew[2]), Is.EqualTo(AiPolicyId.Supply));
            Assert.That(Director.GetIndividualPolicy(Actor), Is.EqualTo(AiPolicyId.Automatic));
            Assert.That(arena.Controls.Current, Is.SameAs(controlledCrew));
            foreach (var crew in arena.PlayerCrew)
                Assert.That(crew.GetComponent<CrewPolicySelection>().Director, Is.SameAs(Director));
            Assert.That(arena.GetComponents<CrewPolicyDirector>(), Has.Length.EqualTo(1));
        }

        /* { 処理: 画面からの禁止方針の指定が拒否され、理由が表示に渡ることを確認します。 } */
        [Test]
        public void CapabilityRestrictionsApplyToPanelCommands()
        {
            // Carrierには攻撃手段がなく、Defenderには運搬能力がありません。
            Panel.TrySelectCrew(2);
            Assert.That(Director.GetUnavailableReason(Panel.SelectedCrew, AiPolicyId.Guard), Is.Not.Empty);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.Guard), Is.False);
            Assert.That(Director.GetIndividualPolicy(Panel.SelectedCrew), Is.EqualTo(AiPolicyId.Automatic));
            Panel.TrySelectCrew(4);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.Supply), Is.False);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.Guard), Is.True);
            Panel.TrySelectCrew(0);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.MobileSupply), Is.False);
            Panel.TrySelectCrew(3);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.MobileSupply), Is.True);
        }

        /* { 処理: 個人方針が同じ観測の行動選択を変更することを確認します。 } */
        [Test]
        public void IndividualPolicyChangesObservedDecisionWithoutChangingLearning()
        {
            // まず警備を選び、世界状態を動かさず補給へ変更します。
            PlaceChoices();
            Panel.TrySelectCrew(0);
            Panel.TrySelectIndividualPolicy(AiPolicyId.Guard);
            Utility.SelectAction();
            Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.DefendShip));
            Panel.TrySelectIndividualPolicy(AiPolicyId.Supply);
            Utility.SelectAction();
            Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.CarryAmmo));
            Assert.That(Utility.Learning.GetWeight(AiActionType.DefendShip), Is.EqualTo(1));
            Assert.That(Utility.Learning.GetWeight(AiActionType.CarryAmmo), Is.EqualTo(1));
        }

        /* { 処理: チーム方針が同じ観測の全体的な行動傾向を変更することを確認します。 } */
        [Test]
        public void TeamPolicyChangesObservedDecision()
        {
            // 十分に近い弾と離れた侵入者を同時に観測します。
            PlaceChoices();
            Panel.TrySelectTeamPolicy(AiPolicyId.Bombard);
            Utility.SelectAction();
            Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.CarryAmmo));
            Panel.TrySelectTeamPolicy(AiPolicyId.Defend);
            Utility.SelectAction();
            Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.DefendShip));
        }

        /* { 処理: 観測した侵入者だけで緊急補正が発生し、視界外になると解除することを確認します。 } */
        [Test]
        public void HiddenInvadersDoNotAffectEmergencyWeight()
        {
            // 対象の隠れた現在状態を方針計算に混入させません。
            PlaceChoices();
            Utility.SelectAction();
            var observedScore = DefenseScore();
            Assert.That(observedScore.PolicyWeight, Is.EqualTo(3));
            arena.EnemyCrew[0].transform.position = new Vector3(-5, 0.5f);
            Utility.SelectAction();
            Assert.That(Utility.Scores.Any(score => score.Candidate.Action == AiActionType.DefendShip), Is.False);
            var carryScore = Utility.Scores.Single(score => score.Candidate.Action == AiActionType.CarryAmmo);
            Assert.That(carryScore.PolicyWeight, Is.EqualTo(1));
            Assert.That(observedScore.PolicyWeight, Is.EqualTo(3), "Prior score remains an immutable snapshot");
        }

        /* { 処理: 自船損傷時の補正と、能力制限の保持を確認します。 } */
        [Test]
        public void HullEmergencyPreservesCapabilityLimitsAndBoundedScores()
        {
            // 学習や個人方針を高くしても重みの上限を超えません。
            PlaceChoices();
            arena.PlayerShip.ApplyDamage(100);
            Panel.TrySelectTeamPolicy(AiPolicyId.Defend);
            Panel.TrySelectCrew(0);
            Panel.TrySelectIndividualPolicy(AiPolicyId.Guard);
            Utility.SelectAction();
            Assert.That(DefenseScore().PolicyWeight, Is.EqualTo(16));
            var carrier = arena.PlayerCrew[2];
            carrier.transform.position = Actor.transform.position;
            carrier.Face(Vector2.down);
            var utility = carrier.GetComponent<UtilityAiAgent>();
            utility.SelectAction();
            Assert.That(utility.Scores.Any(score => score.Candidate.Action == AiActionType.DefendShip), Is.False);
            Assert.That(Director.GetWeight(Actor, AiActionType.DefendShip, 0, false), Is.EqualTo(16));
        }

        /* { 処理: 画面の方針指定から実際のAI移動と攻撃までを実行します。 } */
        [Test]
        public void PanelDefenseCommandReachesActualAiAttack()
        {
            // 画面ボタンと同じ入口で指示し、既存の実行器へ渡します。
            PlaceChoices();
            Panel.TrySelectTeamPolicy(AiPolicyId.Defend);
            Panel.TrySelectCrew(0);
            Panel.TrySelectIndividualPolicy(AiPolicyId.Guard);
            arena.Controls.CycleNext();
            arena.AllyActionsEnabled = true;
            var ai = Actor.GetComponent<CrewAmmoAiController>();
            var enemy = arena.EnemyCrew[0];
            var initialHp = enemy.CurrentHp;
            for (var tick = 0; tick < 160 && enemy.CurrentHp == initialHp; tick++) ai.Tick(0.02f);
            Assert.That(enemy.CurrentHp, Is.LessThan(initialHp));
            Assert.That(Utility.Learning.GetWeight(AiActionType.DefendShip), Is.GreaterThan(1));
            Assert.That(Director.GetIndividualPolicy(Actor), Is.EqualTo(AiPolicyId.Guard));
        }

        /* { 処理: 死亡、戦闘終了、所属違い、不正な選択を拒否することを確認します。 } */
        [Test]
        public void InvalidDeadAndFinishedSelectionsAreRejectedAtomically()
        {
            // 不正な操作後も、以前の有効な方針を保持します。
            Panel.TrySelectCrew(0);
            Panel.TrySelectIndividualPolicy(AiPolicyId.Guard);
            Assert.That(Panel.TrySelectCrew(-1), Is.False);
            Assert.That(Panel.TrySelectCrew(5), Is.False);
            Assert.That(Panel.SelectedCrew, Is.SameAs(Actor));
            Assert.That(Panel.TrySelectTeamPolicy(AiPolicyId.Guard), Is.False);
            Assert.That(Director.Register(arena.EnemyCrew[0].GetComponent<PlayerCrewController>()), Is.False);
            Actor.GetComponent<CrewMember>().ApplyDamage(1000);
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.Automatic), Is.False);
            Assert.That(Director.GetIndividualPolicy(Actor), Is.EqualTo(AiPolicyId.Guard));
            arena.EnemyShip.ApplyDamage(100);
            arena.EnemyShip.TryDamageCore(30);
            Assert.That(Panel.TrySelectTeamPolicy(AiPolicyId.Bombard), Is.False);
            Assert.That(Director.TeamPolicy, Is.EqualTo(AiPolicyId.Balanced));
        }

        /* { 処理: 戦闘再読込が前の方針や画面状態を残さないことを確認します。 } */
        [UnityTest]
        public IEnumerator RestartCreatesFreshPolicyState()
        {
            // セーブ機能未接続の戦闘設定を次の戦闘へ引き継ぎません。
            Panel.TrySelectTeamPolicy(AiPolicyId.Defend);
            Panel.IsOpen = true;
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.AllyActionsEnabled = false;
            arena.EnemyActionsEnabled = false;
            yield return null;
            Assert.That(Director.TeamPolicy, Is.EqualTo(AiPolicyId.Balanced));
            Assert.That(Director.GetIndividualPolicy(Actor), Is.EqualTo(AiPolicyId.Automatic));
            Assert.That(Panel.IsOpen, Is.False);
        }

        /* { 処理: 方針画面を開いた状態でフレームを進め、直接操作と禁止選択の整合性を確認します。 } */
        [UnityTest]
        public IEnumerator OpenPanelPreservesControlAndUnavailableSelection()
        {
            // 実際の描画フレームを進めます。画面の見た目や物理入力を評価する試験ではありません。
            var controlledCrew = arena.Controls.Current;
            Panel.IsOpen = true;
            Panel.TrySelectCrew(2);
            for (var frame = 0; frame < 5; frame++) yield return null;
            Assert.That(Panel.TrySelectIndividualPolicy(AiPolicyId.Guard), Is.False);
            Assert.That(arena.Controls.Current, Is.SameAs(controlledCrew));
            Assert.That(Director.GetIndividualPolicy(Panel.SelectedCrew), Is.EqualTo(AiPolicyId.Automatic));
            LogAssert.NoUnexpectedReceived();
            Panel.IsOpen = false;
        }

        /* { 処理: 比較試験に使う、近い弾と遠い侵入者を同じ視界へ配置します。 } */
        private void PlaceChoices()
        {
            // 船内の壁を無効にせず、実際の視界と移動経路を使います。
            var pickup = arena.PlayerAmmo.Pickups[0];
            pickup.transform.position = new Vector3(-5, -0.9f);
            pickup.GetComponent<Collider2D>().enabled = true;
            arena.EnemyCrew[0].transform.position = new Vector3(-5, -2f);
        }

        /* { 処理: 今回の防衛候補の評価結果を取得します。前提: 防衛候補は一つです。 } */
        private UtilityScore DefenseScore() => Utility.Scores.Single(score => score.Candidate.Action == AiActionType.DefendShip);
    }
}
