using KadokaShipBattler.AI;

/* { 責務: 戦況に応じた共有戦術目標の選択、保持、切替、拒否をCIで検証します。 } */
internal static class PlannerScenarios
{
    /* { 処理: 全てのシナリオを順に実行します。 } */
    internal static void Run()
    {
        CheckInitialAndHold();
        CheckSituationChanges();
        CheckTendencyInfluence();
        CheckInvalidInput();
    }

    /* { 処理: 戦況値を作ります。引数は既定で平常時の戦況です。 } */
    private static EnemyTacticalSituation Situation(float hull = 1, bool opponentBreached = false, int intruders = 0,
        int ammo = 0, bool loaded = false, int alive = 5) =>
        new(hull, hull <= 0, opponentBreached, intruders, ammo, loaded, alive);

    /* { 処理: 開始時は砲撃で、最小保持時間の前は点差があっても切り替えないことを確認します。 } */
    private static void CheckInitialAndHold()
    {
        var planner = new EnemyTacticalPlanner(10);
        TacticsChecks.Check(planner.Goal == EnemyTacticalGoal.Bombard, "Start with bombard");
        var raid = Situation(intruders: 2);
        TacticsChecks.Check(planner.Update(raid, default, 11) == EnemyTacticalGoal.Bombard, "Hold time prevents immediate switch");
        TacticsChecks.Check(planner.Update(raid, default, 12) == EnemyTacticalGoal.DefendShip, "Defend after hold time");
        var calm = Situation();
        TacticsChecks.Check(planner.Update(calm, default, 12.5f) == EnemyTacticalGoal.DefendShip, "Recent switch is held");
        TacticsChecks.Check(planner.Update(calm, default, 14.5f) == EnemyTacticalGoal.Bombard, "Return to bombard when calm");
    }

    /* { 処理: 相手の橋が開く、侵入者が増える、船員がいない場合の目標を確認します。 } */
    private static void CheckSituationChanges()
    {
        var planner = new EnemyTacticalPlanner(0);
        var opened = Situation(opponentBreached: true);
        TacticsChecks.Check(EnemyTacticalPlanner.Score(EnemyTacticalGoal.Bombard, opened, default) == 0, "Bombard is invalid after the hull breaks");
        TacticsChecks.Check(planner.Update(opened, default, 0.1f) == EnemyTacticalGoal.BoardEnemy, "Invalid goal switches immediately");
        var oneIntruder = Situation(opponentBreached: true, intruders: 1);
        TacticsChecks.Check(planner.Update(oneIntruder, default, 5) == EnemyTacticalGoal.BoardEnemy, "One intruder does not stop boarding");
        var overrun = Situation(opponentBreached: true, intruders: 3);
        TacticsChecks.Check(planner.Update(overrun, default, 8) == EnemyTacticalGoal.DefendShip, "Overrun ship defends first");
        TacticsChecks.Check(planner.Update(Situation(alive: 0, opponentBreached: true), default, 20) == EnemyTacticalGoal.DefendShip, "No crew keeps the goal");
    }

    /* { 処理: 推定した傾向が、同じ戦況でも点数に反映されることを確認します。 } */
    private static void CheckTendencyInfluence()
    {
        var situation = Situation(hull: 0.4f);
        var heavy = new PlayerTendency(6, 0, 10, 1);
        var plain = new PlayerTendency(0, 0, 10, 0);
        var heavyScore = EnemyTacticalPlanner.Score(EnemyTacticalGoal.Bombard, situation, heavy);
        var plainScore = EnemyTacticalPlanner.Score(EnemyTacticalGoal.Bombard, situation, plain);
        TacticsChecks.Check(heavyScore > plainScore, "Heavy ammo and fast shelling raise bombard urgency");
        var calm = Situation();
        var pressure = new PlayerTendency(0, 1, 0, 0);
        TacticsChecks.Check(EnemyTacticalPlanner.Score(EnemyTacticalGoal.DefendShip, calm, pressure) > 0, "Boarding pressure raises defense");
        TacticsChecks.Check(EnemyTacticalPlanner.Score(EnemyTacticalGoal.Bombard, calm, pressure) > EnemyTacticalPlanner.Score(EnemyTacticalGoal.DefendShip, calm, pressure), "Pressure alone does not abandon bombardment");
    }

    /* { 処理: 範囲外の戦況、時刻の逆行、不明な目標を拒否することを確認します。 } */
    private static void CheckInvalidInput()
    {
        TacticsChecks.Rejects(() => Situation(hull: 1.5f), "Hull ratio above one rejected");
        TacticsChecks.Rejects(() => new EnemyTacticalSituation(0.5f, true, false, 0, 0, false, 1), "Breach flag must match hull ratio");
        TacticsChecks.Rejects(() => Situation(intruders: -1), "Negative intruders rejected");
        TacticsChecks.Rejects(() => new PlayerTendency(0, 2, 0, 0), "Pressure above one rejected");
        TacticsChecks.Rejects(() => EnemyTacticalPlanner.Score((EnemyTacticalGoal)99, Situation(), default), "Unknown goal rejected");
        var planner = new EnemyTacticalPlanner(5);
        TacticsChecks.Rejects(() => planner.Update(Situation(), default, 4), "Time before start rejected");
        TacticsChecks.Check(planner.Goal == EnemyTacticalGoal.Bombard, "Rejected update keeps the goal");
    }
}
