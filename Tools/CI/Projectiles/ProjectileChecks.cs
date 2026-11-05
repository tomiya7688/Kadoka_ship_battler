using KadokaShipBattler.Characters;

/* { 責務: キャラクター弾の飛行、衝突、着地、拡張口の本番モデルをCIで検証します。 } */
internal static class ProjectileChecks
{
    private static int checks;

    /* { 処理: 全シナリオを実行します。 } */
    private static void Main()
    {
        FlightScenarios();
        ClashScenarios();
        InputScenarios();
        Console.WriteLine($"{checks} projectile checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Projectile check failed: " + message);
        checks++;
    }

    private static void Rejects(Action action, string message)
    {
        try { action(); }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            checks++;
            return;
        }
        throw new InvalidOperationException("Projectile check failed: " + message);
    }

    private sealed class Boost : IProjectileBehaviour
    {
        public float GetSpeedMultiplier(float flightSeconds) => 2;
    }

    private sealed class Broken : IProjectileBehaviour
    {
        public float GetSpeedMultiplier(float flightSeconds) => float.NaN;
    }

    /* { 処理: 直進、着地、固有挙動による速度変更を確認します。 } */
    private static void FlightScenarios()
    {
        // 距離10を速度5で飛ぶと、1秒で半分、2秒で着地します。
        var shot = new CharacterProjectileModel(0, 0, 10, 0, 5, 1);
        Check(shot.Advance(1) == ProjectileFlightStatus.Flying && Math.Abs(shot.X - 5) < 0.001f, "Flies straight");
        Check(shot.Advance(1) == ProjectileFlightStatus.Landed && shot.X == 10, "Lands at target");
        Check(shot.Advance(1) == ProjectileFlightStatus.Landed && shot.FlightSeconds == 2, "Landed shot stays put");

        // 固有挙動は速度だけを変えます。不正な倍率は無視します。
        var fast = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, new Boost());
        Check(fast.Advance(1) == ProjectileFlightStatus.Landed, "Behaviour speeds the flight");
        var safe = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, new Broken());
        safe.Advance(1);
        Check(Math.Abs(safe.X - 5) < 0.001f, "Invalid multiplier is ignored");
    }

    /* { 処理: 弾同士と通常弾との衝突を、硬さで裁定することを確認します。 } */
    private static void ClashScenarios()
    {
        var hard = new CharacterProjectileModel(0, 0, 9, 0, 1, 3);
        var soft = new CharacterProjectileModel(0, 0, 9, 0, 1, 1);
        CharacterProjectileModel.Clash(hard, soft);
        Check(hard.Status == ProjectileFlightStatus.Flying && soft.Status == ProjectileFlightStatus.Destroyed, "Harder survives");
        var left = new CharacterProjectileModel(0, 0, 9, 0, 1, 2);
        var right = new CharacterProjectileModel(0, 0, 9, 0, 1, 2);
        CharacterProjectileModel.Clash(left, right);
        Check(left.Status == ProjectileFlightStatus.Destroyed && right.Status == ProjectileFlightStatus.Destroyed, "Equal hardness destroys both");
        Check(!soft.ClashWithShell(0) && soft.Status == ProjectileFlightStatus.Destroyed, "Destroyed shot cannot survive");
        var tough = new CharacterProjectileModel(0, 0, 9, 0, 1, 5);
        Check(tough.ClashWithShell(2) && tough.Status == ProjectileFlightStatus.Flying, "Harder than shell survives");
        var frail = new CharacterProjectileModel(0, 0, 9, 0, 1, 1);
        Check(!frail.ClashWithShell(1) && frail.Status == ProjectileFlightStatus.Destroyed, "Shell shoots down equal hardness");
        var landed = new CharacterProjectileModel(0, 0, 1, 0, 5, 1);
        landed.Advance(1);
        CharacterProjectileModel.Clash(landed, new CharacterProjectileModel(0, 0, 9, 0, 1, 9));
        Check(landed.Status == ProjectileFlightStatus.Landed, "Landed shot ignores clashes");
    }

    /* { 処理: 不正な入力を状態を変えずに拒否することを確認します。 } */
    private static void InputScenarios()
    {
        Rejects(() => new CharacterProjectileModel(float.NaN, 0, 1, 0, 1, 1), "NaN start rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 0, 1), "Zero speed rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 1, -1), "Negative hardness rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 1, 1).Advance(0), "Zero delta rejected");
        Rejects(() => CharacterProjectileModel.Clash(null!, null!), "Null clash rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 1, 1).ClashWithShell(float.NaN), "NaN shell hardness rejected");
    }
}
