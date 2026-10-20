using KadokaShipBattler.AI;

/* { 責務: 敵チーム戦術の実際の本番モデルをCIで検証する入口と、共通の検証補助を提供します。 } */
internal static class TacticsChecks
{
    private static int checks;

    /* { 処理: 推定器、目標選択、役割割り当てのシナリオを実行します。 } */
    private static void Main()
    {
        // 各シナリオは独立した状態を作ります。
        PlannerScenarios.Run();
        RoleScenarios.Run();
        EstimatorScenarios();
        Console.WriteLine($"{checks} tactics checks passed.");
    }

    /* { 処理: 条件が偽なら、説明付きでCIを失敗させます。 } */
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Tactics check failed: " + message);
        checks++;
    }

    /* { 処理: 指定した処理が引数の誤りとして拒否されることを確認します。 } */
    internal static void Rejects(Action action, string message)
    {
        try { action(); }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            checks++;
            return;
        }
        throw new InvalidOperationException("Tactics check failed: " + message);
    }

    /* { 処理: 傾向推定の窓、標本の重複、減衰、時刻の拒否を確認します。 } */
    private static void EstimatorScenarios()
    {
        // 観測がなければ傾向は未知として扱います。
        var estimator = new PlayerTendencyEstimator(100);
        var unknown = estimator.GetTendency(100);
        Check(unknown.ShellsPerMinute == 0 && unknown.BoardingPressure == 0 && unknown.HeavyAmmoRatio == 0, "No observation means unknown tendency");

        // 10秒窓に3発を受けると、毎分18発の傾向になります。
        estimator.RecordShellHit(101);
        estimator.RecordShellHit(104);
        estimator.RecordShellHit(108);
        Check(Math.Abs(estimator.GetTendency(110).ShellsPerMinute - 18) < 0.001f, "Shell pace uses the minimum window");
        Check(estimator.GetTendency(300).ShellsPerMinute == 0, "Old shells leave the window");

        // 同じ弾の再観測は標本を増やさず、標本が足りるまで割合を出しません。
        Check(estimator.RecordObservedAmmo(7, 3), "Heavy ammo sampled");
        Check(!estimator.RecordObservedAmmo(7, 2), "Repeated ammo ignored");
        Check(estimator.RecordObservedAmmo(8, 2) && estimator.GetTendency(300).HeavyAmmoRatio == 0, "Too few samples keep ratio unknown");
        Check(estimator.RecordObservedAmmo(9, 3), "Third sample accepted");
        Check(Math.Abs(estimator.GetTendency(300).HeavyAmmoRatio - 2f / 3f) < 0.001f, "Heavy ratio uses unique samples");

        // 侵入が続くと圧力が上がり、観測がなければ減衰します。
        estimator.RecordIntruderPresence(310, 2);
        estimator.RecordIntruderPresence(330, 2);
        var raised = estimator.GetTendency(330).BoardingPressure;
        Check(raised > 0.8f && raised <= 1, "Sustained intruders raise pressure");
        estimator.RecordIntruderPresence(360, 0);
        Check(estimator.GetTendency(360).BoardingPressure < raised * 0.2f, "Pressure decays without observation");

        // 時刻の逆行と不正値は状態を変えずに拒否します。
        Rejects(() => estimator.RecordShellHit(1), "Backward time rejected");
        Rejects(() => estimator.RecordObservedAmmo(0, 2), "Unspecified ammo id rejected");
        Rejects(() => estimator.RecordObservedAmmo(10, float.NaN), "NaN weight rejected");
        Rejects(() => estimator.RecordIntruderPresence(400, -1), "Negative intruder count rejected");
        Rejects(() => new PlayerTendencyEstimator(float.NaN), "NaN start rejected");
        Check(estimator.GetTendency(360).SampledAmmoCount == 3, "Rejected input leaves samples unchanged");
    }
}
