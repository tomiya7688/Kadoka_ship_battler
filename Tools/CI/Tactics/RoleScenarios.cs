using KadokaShipBattler.AI;
using KadokaShipBattler.Characters;

/* { 責務: 実際のプロトタイプ編成を模した船員への役割割り当てをCIで検証します。 } */
internal static class RoleScenarios
{
    private const CharacterCapability FighterCarrier = (CharacterCapability)23;
    private const CharacterCapability Hauler = (CharacterCapability)22;

    /* { 処理: 全てのシナリオを順に実行します。 } */
    internal static void Run()
    {
        CheckBombard();
        CheckDefense();
        CheckBoarding();
        CheckEdgeCases();
    }

    /* { 処理: Leader、Gunner、Carrier、Scout、Defenderに相当する5人を作ります。 } */
    private static List<EnemyCrewProfile> Crew() => new()
    {
        new(10, 0, FighterCarrier, true, 5),
        new(11, 1, FighterCarrier, true, 3),
        new(12, 2, Hauler, false, 9),
        new(13, 3, (CharacterCapability)19, true, 3),
        new(14, 4, (CharacterCapability)17, true, 0)
    };

    /* { 処理: 指定した戦況で割り当てを実行し、船員IDごとの役割を返します。 } */
    private static Dictionary<int, EnemyTacticalRole> Assign(List<EnemyCrewProfile> crew, EnemyTacticalGoal goal,
        int intruders = 0, float pressure = 0)
    {
        var situation = new EnemyTacticalSituation(1, false, goal == EnemyTacticalGoal.BoardEnemy, intruders, 0, false, crew.Count);
        var roles = new Dictionary<int, EnemyTacticalRole>();
        EnemyRoleAssigner.Assign(crew, goal, situation, new PlayerTendency(0, pressure, 0, 0), roles);
        return roles;
    }

    /* { 処理: 砲撃では先頭の戦闘兼運搬員が砲手、運搬力の高い2人が補給役になることを確認します。 } */
    private static void CheckBombard()
    {
        var roles = Assign(Crew(), EnemyTacticalGoal.Bombard);
        TacticsChecks.Check(roles[10] == EnemyTacticalRole.Gunner, "Lowest full slot is gunner");
        TacticsChecks.Check(roles[12] == EnemyTacticalRole.Supplier && roles[11] == EnemyTacticalRole.Supplier, "Highest carry capacity supplies");
        TacticsChecks.Check(roles[13] == EnemyTacticalRole.Defender && roles[14] == EnemyTacticalRole.Defender, "Remaining fighters defend");
        TacticsChecks.Check(roles.Count == 5, "Every living crew member has one role");
        var again = Assign(Crew(), EnemyTacticalGoal.Bombard);
        foreach (var pair in roles) TacticsChecks.Check(again[pair.Key] == pair.Value, "Assignment is deterministic");
    }

    /* { 処理: 迎撃では戦闘員が迎撃し、侵入者が複数なら砲手も迎撃に回ることを確認します。 } */
    private static void CheckDefense()
    {
        var single = Assign(Crew(), EnemyTacticalGoal.DefendShip, intruders: 1);
        TacticsChecks.Check(single[10] == EnemyTacticalRole.Gunner, "Gunner stays with one intruder");
        TacticsChecks.Check(single[12] == EnemyTacticalRole.Supplier && single[11] == EnemyTacticalRole.Defender, "Fighters do not supply while defending");
        var overrun = Assign(Crew(), EnemyTacticalGoal.DefendShip, intruders: 2);
        TacticsChecks.Check(overrun[10] == EnemyTacticalRole.Defender, "Gunner defends against several intruders");
    }

    /* { 処理: 乗り込みでは砲手も乗り込み、侵入者や圧力に応じて後方が防衛に残ることを確認します。 } */
    private static void CheckBoarding()
    {
        var open = Assign(Crew(), EnemyTacticalGoal.BoardEnemy);
        TacticsChecks.Check(open[10] == EnemyTacticalRole.Boarder && open[14] == EnemyTacticalRole.Boarder, "All boarders go when the ship is quiet");
        TacticsChecks.Check(open[12] == EnemyTacticalRole.None, "Noncombat carrier does not board");
        var pressured = Assign(Crew(), EnemyTacticalGoal.BoardEnemy, pressure: 0.6f);
        TacticsChecks.Check(pressured[14] == EnemyTacticalRole.Defender && pressured[13] == EnemyTacticalRole.Boarder, "Boarding pressure keeps one defender");
        var raided = Assign(Crew(), EnemyTacticalGoal.BoardEnemy, intruders: 1);
        TacticsChecks.Check(raided[14] == EnemyTacticalRole.Defender && raided[13] == EnemyTacticalRole.Defender && raided[11] == EnemyTacticalRole.Boarder, "Intruders keep two defenders");
    }

    /* { 処理: 砲手がいない、船員がいない、ID重複、未指定の出力先を確認します。 } */
    private static void CheckEdgeCases()
    {
        var noGunner = new List<EnemyCrewProfile> { new(1, 0, CharacterCapability.Combat, true, 0) };
        TacticsChecks.Check(Assign(noGunner, EnemyTacticalGoal.Bombard)[1] == EnemyTacticalRole.Defender, "No cannon operator leaves a defender");
        TacticsChecks.Check(Assign(new List<EnemyCrewProfile>(), EnemyTacticalGoal.Bombard).Count == 0, "No crew yields no roles");
        var duplicate = new List<EnemyCrewProfile> { new(1, 0, FighterCarrier, true, 5), new(1, 1, FighterCarrier, true, 5) };
        var kept = Assign(Crew(), EnemyTacticalGoal.Bombard);
        var situation = new EnemyTacticalSituation(1, false, false, 0, 0, false, 2);
        TacticsChecks.Rejects(() => EnemyRoleAssigner.Assign(duplicate, EnemyTacticalGoal.Bombard, situation, default, kept), "Duplicate ids rejected");
        TacticsChecks.Check(kept.Count == 5, "Rejected assignment leaves the output unchanged");
        TacticsChecks.Rejects(() => EnemyRoleAssigner.Assign(Crew(), (EnemyTacticalGoal)9, situation, default, kept), "Unknown goal rejected");
        TacticsChecks.Rejects(() => new EnemyCrewProfile(0, 0, FighterCarrier, true, 1), "Unspecified id rejected");
    }
}
