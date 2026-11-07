using KadokaShipBattler.AI;
using KadokaShipBattler.Characters;

/* { 責務: 実際の方針モデルの設定、拒否、評価をCIで検証します。 } */
internal static class PolicyChecks
{
    private static int checks;

    /* { 処理: 方針の主要な境界と評価シナリオを実行します。 } */
    private static void Main()
    {
        // 設定状態と能力境界を独立したシナリオで確認します。
        CheckSelections();
        CheckAbilities();
        CheckDecisions();
        CheckEmergencies();
        CheckInvalidDefinitions();
        Console.WriteLine($"{checks} policy checks passed.");
    }

    /* { 処理: チーム、個人、別船員の設定が互いに上書きされないことを確認します。 } */
    private static void CheckSelections()
    {
        // 同じ能力でも別の個人設定を保持します。
        var state = CreateState();
        Check(state.Register(2, Abilities()), "Second crew registered");
        Check(state.TrySetIndividualPolicy(1, AiPolicyId.Guard), "Guard selected");
        Check(state.TrySetTeamPolicy(AiPolicyId.Bombard), "Team bombard selected");
        Check(state.GetIndividualPolicy(1) == AiPolicyId.Guard, "Team changes preserve individual selection");
        Check(state.GetIndividualPolicy(2) == AiPolicyId.Automatic, "Other crew selection preserved");
        Check(!state.Register(1, Abilities()), "Duplicate registration rejected");
        Check(state.GetIndividualPolicy(1) == AiPolicyId.Guard, "Duplicate registration preserves selection");
        Check(!state.TrySetTeamPolicy(AiPolicyId.Supply), "Wrong team scope rejected");
        Check(!state.TrySetIndividualPolicy(1, AiPolicyId.Defend), "Wrong individual scope rejected");
        Check(!state.TrySetIndividualPolicy(99, AiPolicyId.Automatic), "Unknown crew rejected");
        Check(!state.TrySetTeamPolicy((AiPolicyId)999), "Unknown policy rejected");
        Check(state.TeamPolicy == AiPolicyId.Bombard, "Rejected team update is atomic");
    }

    /* { 処理: 攻撃、運搬、特殊移動の不足で選択できない方針を確認します。 } */
    private static void CheckAbilities()
    {
        // 能力フラグと実際の攻撃・保持条件を両方確認します。
        var state = new AiPolicyState();
        state.Register(1, new CrewPolicyAbilities(CharacterCapability.CarryAmmo, false, 9, 3, false, false));
        state.Register(2, new CrewPolicyAbilities(CharacterCapability.Combat, true, 0, 0, false, false));
        state.Register(3, new CrewPolicyAbilities(CharacterCapability.Combat | CharacterCapability.CarryAmmo, false, 0, 0, true, false));
        state.Register(4, new CrewPolicyAbilities(CharacterCapability.CarryAmmo, false, 3, 1, false, true));
        Check(!state.TrySetIndividualPolicy(1, AiPolicyId.Guard), "Noncombat carrier cannot guard");
        Check(state.TrySetIndividualPolicy(1, AiPolicyId.Supply), "Carrier can supply");
        Check(!state.TrySetIndividualPolicy(2, AiPolicyId.Supply), "Defender cannot supply");
        Check(state.TrySetIndividualPolicy(2, AiPolicyId.Guard), "Defender can guard");
        Check(!state.TrySetIndividualPolicy(3, AiPolicyId.Guard), "Combat flag without attack is insufficient");
        Check(!state.TrySetIndividualPolicy(3, AiPolicyId.Supply), "Carry flag without capacity is insufficient");
        Check(!state.TrySetIndividualPolicy(1, AiPolicyId.MobileSupply), "Ordinary movement rejects special mobility policy");
        Check(state.TrySetIndividualPolicy(4, AiPolicyId.MobileSupply), "Phase movement enables mobile supply");
        Check(state.GetUnavailableReason(3, AiPolicyId.Guard).Length > 0, "UI receives an unavailable reason");
        Check(state.GetIndividualPolicy(3) == AiPolicyId.Automatic, "Rejected selection preserves previous individual policy");
    }

    /* { 処理: 同じ候補でも方針の変更で行動が変わり、低い運搬適性の評価が下がることを確認します。 } */
    private static void CheckDecisions()
    {
        // 世界状態と学習値を固定して、方針だけを変更します。
        var state = CreateState();
        var learning = new UtilityLearningState();
        var choices = new[]
        {
            new UtilityCandidate(AiActionType.CarryAmmo, ammoDamage: 25, ammoWeight: 2),
            new UtilityCandidate(AiActionType.DefendShip, targetHp: 100)
        };
        var context = Context(5);
        /* { 処理: 固定した候補を現在の方針で評価し、選択行動を取得します。 } */
        AiActionType Choose() => UtilityRules.Evaluate(context, choices, learning,
            action => state.GetWeight(1, action, 1, false)).Selected.Action;
        Check(state.TrySetTeamPolicy(AiPolicyId.Bombard), "Team bombard accepted");
        Check(Choose() == AiActionType.CarryAmmo, "Bombard favors supply over guard");
        Check(state.TrySetTeamPolicy(AiPolicyId.Defend), "Team defense accepted");
        Check(Choose() == AiActionType.DefendShip, "Defense favors guard for identical observations");
        state.TrySetTeamPolicy(AiPolicyId.Balanced);
        Check(state.TrySetIndividualPolicy(1, AiPolicyId.Supply), "Individual supply accepted");
        Check(Choose() == AiActionType.CarryAmmo, "Individual supply changes the balanced decision");
        Check(learning.GetWeight(AiActionType.CarryAmmo) == 1, "Policy changes do not modify learning");

        // 運搬に不向きな船員は、同じ方針でも弾の評価が下がります。
        var strong = UtilityRules.Evaluate(Context(9), choices, learning).Scores[0].Score;
        var weak = UtilityRules.Evaluate(Context(1), choices, learning).Scores[0].Score;
        Check(strong > weak, "Low carrying aptitude lowers carry score");
    }

    /* { 処理: 自船の危機による防衛補正と、重みの上限を確認します。 } */
    private static void CheckEmergencies()
    {
        // 正常時、船体損傷、観測侵入を区別します。
        var state = CreateState();
        Check(state.GetWeight(1, AiActionType.DefendShip, 1, false) == 1, "Normal state has neutral defense weight");
        Check(state.GetWeight(1, AiActionType.DefendShip, 0, false) == 3, "Breached own hull increases defense");
        Check(state.GetWeight(1, AiActionType.DefendShip, 1, true) == 3, "Observed invasion increases defense");
        Check(state.GetWeight(1, AiActionType.DefendShip, 0.25f, false) == 1, "Emergency threshold boundary is continuous");
        Check(state.GetWeight(1, AiActionType.DefendShip, 0.125f, false) == 2, "Hull emergency interpolates continuously");
        Check(state.GetWeight(1, AiActionType.CarryAmmo, 0, true) == 1, "Emergency does not boost unrelated actions");
        state.TrySetTeamPolicy(AiPolicyId.Defend);
        state.TrySetIndividualPolicy(1, AiPolicyId.Guard);
        Check(state.GetWeight(1, AiActionType.DefendShip, 0, true) == 16, "Combined emergency weight remains bounded");
        Reject(() => state.GetWeight(1, AiActionType.DefendShip, float.NaN, false), "Nonfinite own hull rejected");
        Reject(() => state.GetWeight(1, AiActionType.DefendShip, 1.01f, false), "Out-of-range hull rejected");
        Reject(() => state.GetWeight(99, AiActionType.Idle, 1, false), "Unregistered evaluation rejected");
        Reject(() => state.GetWeight(1, (AiActionType)999, 1, false), "Unknown action rejected");
    }

    /* { 処理: 不正な定義と能力値が評価へ入らないことを確認します。 } */
    private static void CheckInvalidDefinitions()
    {
        // 例外後も、既存の状態を変更しません。
        foreach (var weight in new[] { -1f, 4.01f, float.NaN, float.PositiveInfinity })
            Reject(() => new AiPolicyProfile(AiPolicyId.Supply, AiPolicyScope.Individual, "Supply", carry: weight), "Invalid profile weight rejected");
        Reject(() => new AiPolicyProfile((AiPolicyId)99, AiPolicyScope.Team, "Unknown"), "Invalid profile id rejected");
        Reject(() => new AiPolicyProfile(AiPolicyId.Balanced, (AiPolicyScope)99, "Unknown"), "Invalid profile scope rejected");
        Reject(() => new AiPolicyProfile(AiPolicyId.Balanced, AiPolicyScope.Team, " "), "Empty display name rejected");
        Reject(() => new CrewPolicyAbilities(0, false, float.NaN, 1, false, false), "Nonfinite carry limit rejected");
        Reject(() => new CrewPolicyAbilities(0, false, 1, -1, false, false), "Negative carry count rejected");
        Reject(() => new CrewPolicyAbilities((CharacterCapability)128, false, 1, 1, false, false), "Unknown capability rejected");
        Reject(() => new AiPolicyProfile(AiPolicyId.Guard, AiPolicyScope.Individual, "Guard", CharacterCapability.Repair), "Unsupported policy capability rejected");
        Check(AiPolicyCatalog.Find((AiPolicyScope)99, AiPolicyId.Automatic) == null, "Catalog rejects invalid scope");
    }

    /* { 処理: 全ての現行方針を選べる試験船員の能力を作ります。 } */
    private static CrewPolicyAbilities Abilities() => new(CharacterCapability.Combat | CharacterCapability.CarryAmmo |
        CharacterCapability.OperateCannon, true, 5, 2, true, false);

    /* { 処理: 試験船員を登録した中立方針状態を作ります。 } */
    private static AiPolicyState CreateState()
    {
        // シナリオ間で設定を共有しません。
        var state = new AiPolicyState();
        state.Register(1, Abilities());
        return state;
    }

    /* { 処理: 運搬適性だけが異なる同条件の評価文脈を作ります。 } */
    private static UtilityContext Context(float carrySkill) => new(CharacterCapability.Combat | CharacterCapability.CarryAmmo,
        5, carrySkill, 1, 1, 1, false, false, true);

    /* { 処理: 必須の拒否がArgumentExceptionとして発生することを確認します。 } */
    private static void Reject(Action operation, string description)
    {
        // 処理の別の失敗を、意図した入力拒否として扱いません。
        try { operation(); }
        catch (ArgumentException) { Check(true, description); return; }
        throw new InvalidOperationException("Expected rejection: " + description);
    }

    /* { 処理: 条件を集計し、失敗時はCIを終了させます。 } */
    private static void Check(bool condition, string description)
    {
        // メッセージから失敗した契約を特定できるようにします。
        if (!condition) throw new InvalidOperationException(description);
        checks++;
        Console.WriteLine("PASS: " + description);
    }
}
