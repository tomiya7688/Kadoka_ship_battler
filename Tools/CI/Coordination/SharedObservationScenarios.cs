using KadokaShipBattler.AI;
using KadokaShipBattler.Core;

/* { 責務: 共有観測の所属、鮮度、失効、不正報告時の保持を検証します。 } */
internal static class SharedObservationScenarios
{
    /* { 処理: 有効な観測共有と失敗時の情報境界を確認します。 } */
    public static void Run(Action<bool, string> check)
    {
        // 対象のライブ参照なしで値と時刻の契約を確認します。
        CheckReporting(check);
        CheckExpiryAndWithdrawal(check);
        CheckInvalidReports(check);
    }

    /* { 処理: 所属と複数報告の統合が正しいことを確認します。 } */
    private static void CheckReporting(Action<bool, string> check)
    {
        // より新しい報告の位置とHPだけを採用します。
        var board = new SharedObservationBoard(TeamSide.Player);
        check(board.RegisterReporter(1, TeamSide.Player), "Player observer registered");
        check(!board.RegisterReporter(2, TeamSide.Enemy), "Enemy reporter rejected");
        check(!board.RegisterReporter(1, TeamSide.Player), "Duplicate reporter rejected");
        check(board.RegisterReporter(2, TeamSide.Player), "Second player observer registered");
        var old = Observation(10, 0, 4);
        board.Publish(1, new[] { old }, 0);
        board.Publish(2, new[] { Observation(10, 0.5f, 5), Observation(20, 0.5f, 6) }, 0.5f);
        var merged = board.GetCurrent(0.5f);
        check(merged.Count == 2, "Duplicate target reports merge");
        check(merged[0].TargetId == 10 && merged[0].X == 5, "Newest target position selected");
        check(old.X == 4, "Older immutable sample remains unchanged");
        check(ReferenceEquals(merged, board.GetCurrent(0.5f)), "Repeated frame reads reuse merged view");
        board.ForgetTarget(10);
        check(board.GetCurrent(0.5f).All(sample => sample.TargetId != 10), "Consumed target removed from all reporters");
        board.Publish(2, new[] { Observation(30, 0.5f, 8) }, 0.5f);
        board.Publish(1, new[] { Observation(30, 0.5f, 7) }, 0.5f);
        check(board.GetCurrent(0.5f).Single(sample => sample.TargetId == 30).X == 7, "Equal-time samples use deterministic reporter id");
    }

    /* { 処理: 鮮度の境界、報告の撤回、報告者の停止を確認します。 } */
    private static void CheckExpiryAndWithdrawal(Action<bool, string> check)
    {
        // 古い観測の再報告で有効期限を延長しません。
        var board = new SharedObservationBoard(TeamSide.Player);
        board.RegisterReporter(1, TeamSide.Player);
        board.RegisterReporter(2, TeamSide.Player);
        board.Publish(1, new[] { Observation(10, 0) }, 0);
        board.Publish(2, new[] { Observation(20, 1) }, 1);
        check(board.GetCurrent(1.999f).Count == 2, "Samples valid before their deadlines");
        check(board.GetCurrent(2).Count == 1, "Sample expires exactly at two seconds");
        board.Publish(1, new[] { Observation(10, 0) }, 2);
        check(board.GetCurrent(2).Count == 1, "Republishing stale data cannot extend its life");
        board.Publish(2, Array.Empty<VisionObservation>(), 2);
        check(board.GetCurrent(2).Count == 0, "Lost visual contact withdraws current report");
        board.Publish(1, new[] { Observation(10, 2) }, 2);
        board.ClearReporter(1);
        check(board.GetCurrent(2).Count == 0, "Unavailable reporter loses shared information");
        board.Publish(1, new[] { Observation(10, 2) }, 2);
        board.Clear();
        check(board.GetCurrent(2).Count == 0, "Battle cleanup clears all information");
        board.UnregisterReporter(1);
        CoordinationChecks.ExpectRejection(() => board.Publish(1, new[] { Observation(10, 2) }, 2), check, "Destroyed reporter cannot publish");
        check(board.RegisterReporter(1, TeamSide.Player), "Destroyed reporter id can be registered afresh");
    }

    /* { 処理: 不正な観測で既存の有効情報が置き換わらないことを確認します。 } */
    private static void CheckInvalidReports(Action<bool, string> check)
    {
        // 将来や非有限の観測を入れず、更新の原子性を確認します。
        var board = new SharedObservationBoard(TeamSide.Player);
        board.RegisterReporter(1, TeamSide.Player);
        board.Publish(1, new[] { Observation(10, 0) }, 0);
        CoordinationChecks.ExpectRejection(() => board.Publish(99, new[] { Observation(10, 0) }, 0), check, "Unknown reporter rejected");
        CoordinationChecks.ExpectRejection(() => board.Publish(1, null, 0), check, "Null report rejected");
        CoordinationChecks.ExpectRejection(() => board.Publish(1, new VisionObservation[] { null }, 0), check, "Null sample rejected");
        CoordinationChecks.ExpectRejection(() => board.Publish(1, new[] { Observation(10, 1) }, 0), check, "Future observation rejected");
        CoordinationChecks.ExpectRejection(() => board.Publish(1, new[] { Observation(10, 0, float.NaN) }, 0), check, "Nonfinite position rejected");
        CoordinationChecks.ExpectRejection(() => board.Publish(1, new[] { Observation(0, 0) }, 0), check, "Unspecified target rejected");
        CoordinationChecks.ExpectRejection(() => board.GetCurrent(float.PositiveInfinity), check, "Nonfinite lookup clock rejected");
        CoordinationChecks.ExpectRejection(() => board.Publish(1, Enumerable.Repeat(Observation(10, 0), 129).ToArray(), 0), check, "Oversized report rejected");
        check(board.GetCurrent(0).Count == 1 && board.GetCurrent(0)[0].TargetId == 10, "Invalid reports preserve previous complete information");
    }

    /* { 処理: 敵船員の位置と時刻を固定した観測値を作ります。 } */
    private static VisionObservation Observation(int id, float time, float x = 4) =>
        new(id, ObservedTargetKind.Crew, TeamSide.Enemy, x, 0, 100, 0, 0, time);

}
