/* { 責務: 本番の予約と共有観測の契約をCIで実行します。 } */
internal static class CoordinationChecks
{
    private static int checks;

    /* { 処理: 予約と観測の独立したシナリオを実行し、成功数を出力します。 } */
    private static void Main()
    {
        // Unityなしでも、本番の状態遷移と情報境界を確認します。
        ReservationScenarios.Run(Check);
        SharedObservationScenarios.Run(Check);
        Console.WriteLine($"{checks} coordination checks passed.");
    }

    /* { 処理: 契約を集計し、失敗時はCIを終了します。 } */
    private static void Check(bool condition, string description)
    {
        // 失敗した操作と条件をCIログから特定できるようにします。
        if (!condition) throw new InvalidOperationException(description);
        checks++;
        Console.WriteLine("PASS: " + description);
    }

    /* { 処理: 意図した入力拒否を確認し、無関係な実行例外を成功扱いしません。 } */
    public static void ExpectRejection(Action operation, Action<bool, string> check, string description)
    {
        // 両台帳に共通する入力拒否の判定を一か所で管理します。
        try
        {
            operation();
        }
        catch (ArgumentException)
        {
            check(true, description);
            return;
        }
        throw new InvalidOperationException("Expected rejection: " + description);
    }
}
