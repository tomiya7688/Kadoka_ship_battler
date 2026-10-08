using KadokaShipBattler.AI;

/* { 責務: 予約の競合、引継ぎ、期限、失敗時の整合性を検証します。 } */
internal static class ReservationScenarios
{
    /* { 処理: 予約台帳の主要な操作を、固定した時計で確認します。 } */
    public static void Run(Action<bool, string> check)
    {
        // シナリオ間で台帳を共有しません。
        CheckOwnership(check);
        CheckAtomicChanges(check);
        CheckExpiry(check);
        CheckKeysAndInvalidRequests(check);
    }

    /* { 処理: 同点の維持、期限更新、高優先度への引継ぎを確認します。 } */
    private static void CheckOwnership(Action<bool, string> check)
    {
        // 新担当が取得した予約を旧担当の解放で消しません。
        var board = new TaskReservationBoard();
        var target = new TaskTarget(TaskTargetKind.Ammo, 10);
        check(board.TryClaim(target, 1, 0, 0, 2, out var first), "First crew claims ammo");
        check(!board.CanClaim(target, 2, 0, 0), "Equal priority cannot steal ammo");
        check(board.CanClaim(target, 2, 1, 0), "Higher priority may preempt");
        check(board.TryClaim(target, 1, 0, 1, 2, out var renewed), "Owner renews its lease");
        check(first.Generation == renewed.Generation, "Renewal preserves generation");
        check(board.IsCurrent(first, 2), "Renewed ownership survives original expiry");
        check(board.TryClaim(target, 2, 1, 2, 2, out var takeover), "Specialist takes over");
        check(!board.IsCurrent(renewed, 2), "Old ownership invalidated immediately");
        check(takeover.Generation != first.Generation, "Takeover gets a new generation");
        check(!board.ReleaseOwner(1), "Old owner has no remaining claim");
        check(board.IsCurrent(takeover, 2), "Old owner cleanup preserves new claim");
        check(!board.TryClaim(target, 1, 1, 2, 2, out _), "Equal priority cannot bounce a takeover");
        check(board.Count == 1, "One target has exactly one owner");
    }

    /* { 処理: 失敗や別資源への変更が、逆引きと所有権を壊さないことを確認します。 } */
    private static void CheckAtomicChanges(Action<bool, string> check)
    {
        // 取得不可の候補に切り替えようとしても、以前の担当を保持します。
        var board = new TaskReservationBoard();
        var ammo = new TaskTarget(TaskTargetKind.Ammo, 10);
        var cannon = new TaskTarget(TaskTargetKind.Cannon, 20);
        board.TryClaim(ammo, 1, 0, 0, 2, out var carrying);
        board.TryClaim(cannon, 2, 1, 0, 2, out var loading);
        check(!board.TryClaim(cannon, 1, 0, 1, 2, out _), "Failed assignment does not abandon previous work");
        check(board.IsCurrent(carrying, 1), "Previous ammo claim remains valid");
        check(board.IsCurrent(loading, 1), "Other owner's cannon claim remains valid");
        check(board.TryClaim(cannon, 1, 2, 1, 2, out var newWork), "Owner switches to a higher-priority task");
        check(board.Count == 1, "Switch releases previous target and replaced owner");
        check(board.CanClaim(ammo, 3, 0, 1), "Abandoned ammo is available again");
        board.ReleaseTarget(cannon);
        check(!board.IsCurrent(newWork, 1), "Consumed target invalidates ownership");
        check(board.TryClaim(cannon, 1, 2, 1, 2, out var reacquired), "Same owner can reacquire");
        check(reacquired.Generation != newWork.Generation, "Reacquisition rejects old generation");
        board.Clear();
        check(board.Count == 0 && !board.IsCurrent(reacquired, 1), "Battle cleanup clears all claims");
    }

    /* { 処理: 期限の境界と途中回収後の再取得を確認します。 } */
    private static void CheckExpiry(Action<bool, string> check)
    {
        // 期限と同じ時刻には旧担当を有効としません。
        var board = new TaskReservationBoard();
        var ammo = new TaskTarget(TaskTargetKind.Ammo, 10);
        var invader = new TaskTarget(TaskTargetKind.Invader, 30);
        board.TryClaim(ammo, 1, 0, 0, 1, out var shortLease);
        board.TryClaim(invader, 2, 2, 0, 2, out var longLease);
        check(board.IsCurrent(shortLease, 0.999f), "Lease valid immediately before expiry");
        check(!board.IsCurrent(shortLease, 1), "Lease invalid exactly at expiry");
        check(board.CanClaim(ammo, 3, 0, 1), "Expired work can be reassigned without waiting for cleanup");
        board.Expire(1);
        check(board.Count == 1 && board.IsCurrent(longLease, 1), "Expiry removes only elapsed claims");
        board.Expire(2);
        check(board.Count == 0, "All expired work is reclaimed");
        check(board.TryClaim(ammo, 3, 0, 2, 2, out _), "No deadlock after expiry");
    }

    /* { 処理: 同じ砲台の装填と発射を同じキーにし、不正入力を拒否することを確認します。 } */
    private static void CheckKeysAndInvalidRequests(Action<bool, string> check)
    {
        // 別の行動名で同じ砲台予約を迂回しません。
        TaskTarget.TryFromCandidate(new UtilityCandidate(AiActionType.LoadCannon, 20), out var load);
        TaskTarget.TryFromCandidate(new UtilityCandidate(AiActionType.OperateCannon, 20), out var fire);
        check(load.Equals(fire), "Load and fire share one cannon resource");
        check(!TaskTarget.TryFromCandidate(new UtilityCandidate(AiActionType.Idle), out _), "Idle does not reserve anything");
        var board = new TaskReservationBoard();
        board.TryClaim(load, 1, 0, 0, 2, out var valid);
        CoordinationChecks.ExpectRejection(() => board.TryClaim(load, 1, 0, 0, 0, out _), check, "Zero duration rejected");
        CoordinationChecks.ExpectRejection(() => board.TryClaim(load, 1, 0, 0, float.NaN, out _), check, "Nonfinite duration rejected");
        CoordinationChecks.ExpectRejection(() => board.CanClaim(default, 1, 0, 0), check, "Unspecified target rejected");
        CoordinationChecks.ExpectRejection(() => board.CanClaim(load, 0, 0, 0), check, "Unspecified owner rejected");
        CoordinationChecks.ExpectRejection(() => board.CanClaim(load, 1, 4, 0), check, "Out-of-range priority rejected");
        CoordinationChecks.ExpectRejection(() => board.CanClaim(load, 1, 0, float.NaN), check, "Nonfinite clock rejected");
        CoordinationChecks.ExpectRejection(() => board.IsCurrent(valid, -1), check, "Negative clock rejected");
        CoordinationChecks.ExpectRejection(() => board.TryClaim(load, 1, 0, float.MaxValue, 1, out _), check, "Non-advancing finite deadline rejected");
        check(board.IsCurrent(valid, 0), "Invalid requests preserve valid claim");
        var negativeId = new TaskTarget(TaskTargetKind.Ammo, -20);
        check(board.TryClaim(negativeId, -1, 0, 0, 2, out _), "Negative Unity instance ids remain supported");
    }

}
