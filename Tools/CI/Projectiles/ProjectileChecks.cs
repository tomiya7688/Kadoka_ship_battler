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

        // 極端に大きい座標でも、距離計算が溢れず飛行が進みます。
        var huge = new CharacterProjectileModel(-3e38f, 0, 3e38f, 0, 1e38f, 1);
        huge.Advance(1);
        Check(!float.IsNaN(huge.X) && !float.IsInfinity(huge.X) && huge.X > -3e38f && huge.Status == ProjectileFlightStatus.Flying, "Huge distance does not overflow");

        // 固有挙動は速度だけを変えます。不正な倍率は無視します。
        var fast = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, new Boost());
        Check(fast.Advance(1) == ProjectileFlightStatus.Landed, "Behaviour speeds the flight");
        var safe = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, new Broken());
        safe.Advance(1);
        Check(Math.Abs(safe.X - 5) < 0.001f, "Invalid multiplier is ignored");
    }

    private static CharacterProjectileModel Shot(int hardness, IProjectileClashEffect effect = null) =>
        new CharacterProjectileModel(0, 0, 99, 0, 1, hardness, null, effect);

    private sealed class ShieldBreaker : IProjectileClashEffect
    {
        // 相手を耐久に関係なく撃墜し、自分は消耗しません。
        public bool PreClash(CharacterProjectileModel self, CharacterProjectileModel other)
        {
            other.Destroy();
            return true;
        }
    }

    private sealed class Ghost : IProjectileClashEffect
    {
        // 衝突を無視します（通常処理を上書きして何もしない）。
        public bool PreClash(CharacterProjectileModel self, CharacterProjectileModel other) => true;
    }

    /* { 処理: 衝突耐久が衝突ごとに双方1減ること、接触の多重計上防止、特殊効果の上書きを確認します。 } */
    private static void ClashScenarios()
    {
        // 硬さ1 vs 1 は両方消えます。
        var a = Shot(1);
        var b = Shot(1);
        CharacterProjectileModel.UpdateContact(a, b, true);
        Check(a.Status == ProjectileFlightStatus.Destroyed && b.Status == ProjectileFlightStatus.Destroyed, "1 vs 1 destroys both");

        // 硬さ2 vs 1 は、硬さ2側が残り1で継続します。硬い側も必ず1消耗します。
        var two = Shot(2);
        var one = Shot(1);
        CharacterProjectileModel.UpdateContact(two, one, true);
        Check(two.Status == ProjectileFlightStatus.Flying && two.RemainingHardness == 1 && one.Status == ProjectileFlightStatus.Destroyed, "2 vs 1 leaves 1");

        // 硬さ1〜5は、通常衝突をその回数処理した時点で消滅します。
        for (var hardness = 1; hardness <= 5; hardness++)
        {
            var target = Shot(hardness);
            for (var i = 1; i <= hardness; i++)
            {
                var other = Shot(9);
                CharacterProjectileModel.UpdateContact(target, other, true);
                var expectAlive = i < hardness;
                Check((target.Status == ProjectileFlightStatus.Flying) == expectAlive && target.RemainingHardness == Math.Max(0, hardness - i), $"hardness {hardness} after {i} clashes");
            }
        }

        // 同じ2つの接触を複数フレーム加算せず、重なりが外れてから再接触すると再び数えます。
        var x = Shot(5);
        var y = Shot(5);
        CharacterProjectileModel.UpdateContact(x, y, true);
        CharacterProjectileModel.UpdateContact(x, y, true);
        CharacterProjectileModel.UpdateContact(y, x, true);
        Check(x.RemainingHardness == 4 && y.RemainingHardness == 4, "Same contact counted once");
        CharacterProjectileModel.UpdateContact(x, y, false);
        CharacterProjectileModel.UpdateContact(x, y, true);
        Check(x.RemainingHardness == 3 && y.RemainingHardness == 3, "Separated pair clashes again");

        // 重なっていなければ衝突しません。
        var far1 = Shot(1);
        var far2 = Shot(1);
        CharacterProjectileModel.UpdateContact(far1, far2, false);
        Check(far1.Status == ProjectileFlightStatus.Flying && far2.Status == ProjectileFlightStatus.Flying, "No overlap no clash");

        // 特殊効果は通常処理を上書きします。
        var breaker = Shot(1, new ShieldBreaker());
        var tough = Shot(5);
        CharacterProjectileModel.UpdateContact(breaker, tough, true);
        Check(breaker.RemainingHardness == 1 && tough.Status == ProjectileFlightStatus.Destroyed, "PreClash overrides normal clash");
        var ghost = Shot(1, new Ghost());
        var normal = Shot(1);
        CharacterProjectileModel.UpdateContact(normal, ghost, true);
        Check(ghost.Status == ProjectileFlightStatus.Flying && normal.Status == ProjectileFlightStatus.Flying, "Ghost ignores the clash");

        // 通常弾1発は耐久を1減らします。着地済みは衝突を無視します。
        var withShell = Shot(2);
        Check(withShell.HitByShell() && withShell.RemainingHardness == 1, "Shell costs 1 hardness");
        Check(!withShell.HitByShell() && withShell.Status == ProjectileFlightStatus.Destroyed, "Second shell shoots down");
        var landed = new CharacterProjectileModel(0, 0, 1, 0, 5, 1);
        landed.Advance(1);
        CharacterProjectileModel.UpdateContact(landed, Shot(9), true);
        Check(landed.Status == ProjectileFlightStatus.Landed && landed.RemainingHardness == 1, "Landed shot ignores clashes");
    }

    /* { 処理: 不正な入力を状態を変えずに拒否することを確認します。 } */
    private static void InputScenarios()
    {
        Rejects(() => new CharacterProjectileModel(float.NaN, 0, 1, 0, 1, 1), "NaN start rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 0, 1), "Zero speed rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 1, 0), "Zero hardness rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 1, -1), "Negative hardness rejected");
        Rejects(() => new CharacterProjectileModel(0, 0, 1, 0, 1, 1).Advance(0), "Zero delta rejected");
        Rejects(() => CharacterProjectileModel.UpdateContact(null!, null!, true), "Null contact rejected");
    }
}
