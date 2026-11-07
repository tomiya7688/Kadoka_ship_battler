using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;

/* { 責務: キャラクター弾の飛行、衝突、着地、拡張口の本番モデルをCIで検証します。 } */
internal static class ProjectileChecks
{
    private static int checks;

    /* { 処理: 全シナリオを実行します。 } */
    private static void Main()
    {
        FlightScenarios();
        ClashScenarios();
        TrajectoryScenarios();
        AmmoShotScenarios();
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

    private sealed class FakeShell : ITrackedAmmo, IHardAmmo
    {
        public FakeShell(float damage, float hardness) { Damage = damage; Hardness = hardness; }
        public float Damage { get; }
        public float Weight => 1;
        public float Hardness { get; }
        public bool CanCarry => true;
        public int Returns;
        public bool TryCarry() => true;
        public bool TryLoad() => true;
        public bool ReturnToDeck() { Returns++; return true; }
    }

    private static AmmoShotModel Shell(FakeShell ammo, TeamSide side, ShipBattleState target, float sx, float sy, float gx, float gy,
        ProjectileTrajectoryKind kind = ProjectileTrajectoryKind.Direct) => new(ammo, side, target, sx, sy, gx, gy, kind);

    /* { 処理: 通常砲弾の飛翔体化（発射時は無傷、着弾でダメージ、敵対弾との衝突耐久、味方は衝突しない、軌道を問わない重なり衝突）を確認します。 } */
    private static void AmmoShotScenarios()
    {
        ProjectileClashField.Clear();
        // 発射時にはダメージを与えず、着弾でだけ船体を減らし、山札へ1回だけ返します。
        var enemyShip = new ShipBattleState(TeamSide.Enemy, 100, 30);
        var cannon = new CannonState();
        var carry = new AmmoCarryState();
        var plain = new FakeShell(25, 1);
        Check(carry.TryPickup(plain, true) && cannon.TryLoadFrom(carry, new ShipBattleState(TeamSide.Player, 100, 30), TeamSide.Player), "Shell loads");
        var owner = new ShipBattleState(TeamSide.Player, 100, 30);
        Check(cannon.Detach(owner, owner) == null && cannon.LoadedAmmo == plain, "Detach rejects own ship and keeps the shell loaded");
        var launched = cannon.Detach(owner, enemyShip);
        Check(ReferenceEquals(launched, plain) && cannon.LoadedAmmo == null, "Detach hands the shell over");
        Check(enemyShip.CurrentHull == 100 && plain.Returns == 0, "Launch itself deals no damage and keeps the round out");
        var flight = Shell(plain, TeamSide.Player, enemyShip, 0, 0, 10, 0);
        flight.Step(0.5f);
        Check(flight.Settle() == AmmoShotOutcome.Flying && enemyShip.CurrentHull == 100, "Shell in flight deals no damage");
        flight.Step(0.6f);
        Check(flight.Settle() == AmmoShotOutcome.Impacted && enemyShip.CurrentHull == 75 && plain.Returns == 1, "Impact damages hull and returns the round");
        Check(flight.Settle() == AmmoShotOutcome.Impacted && enemyShip.CurrentHull == 75 && plain.Returns == 1, "Outcome is settled only once");
        flight.Cancel();
        Check(plain.Returns == 1, "Cancel after impact does nothing");

        // 敵対する硬さ1同士は両方消え、どちらも着弾しません。
        var targetA = new ShipBattleState(TeamSide.Player, 100, 30);
        var targetB = new ShipBattleState(TeamSide.Enemy, 100, 30);
        var left = new FakeShell(10, 1);
        var right = new FakeShell(10, 1);
        var a = Shell(left, TeamSide.Player, targetB, 0, 0, 10, 0);
        var b = Shell(right, TeamSide.Enemy, targetA, 10, 0, 0, 0);
        ProjectileClashField.Register(a.Flight, a.Side);
        ProjectileClashField.Register(b.Flight, b.Side);
        for (var i = 0; i < 40 && a.Outcome == AmmoShotOutcome.Flying; i++)
        {
            a.Step(0.05f); b.Step(0.05f);
            ProjectileClashField.Resolve(a.Flight, a.Side); ProjectileClashField.Resolve(b.Flight, b.Side);
            a.Settle(); b.Settle();
        }
        Check(a.Outcome == AmmoShotOutcome.Intercepted && b.Outcome == AmmoShotOutcome.Intercepted, "Equal hardness shells destroy each other");
        Check(targetA.CurrentHull == 100 && targetB.CurrentHull == 100 && left.Returns == 1 && right.Returns == 1, "Intercepted shells deal no damage and return to the deck");

        // 硬さ3は硬さ1を迎撃し、残り耐久2で飛び続けて着弾します。
        ProjectileClashField.Clear();
        var heavyTarget = new ShipBattleState(TeamSide.Enemy, 100, 30);
        var heavy = Shell(new FakeShell(40, 3), TeamSide.Player, heavyTarget, 0, 0, 10, 0);
        var light = Shell(new FakeShell(10, 1), TeamSide.Enemy, targetA, 10, 0, 0, 0);
        ProjectileClashField.Register(heavy.Flight, heavy.Side);
        ProjectileClashField.Register(light.Flight, light.Side);
        for (var i = 0; i < 60 && heavy.Outcome == AmmoShotOutcome.Flying; i++)
        {
            heavy.Step(0.05f); light.Step(0.05f);
            ProjectileClashField.Resolve(heavy.Flight, heavy.Side); ProjectileClashField.Resolve(light.Flight, light.Side);
            heavy.Settle(); light.Settle();
        }
        Check(light.Outcome == AmmoShotOutcome.Intercepted && heavy.Outcome == AmmoShotOutcome.Impacted && heavyTarget.CurrentHull == 60, "Hardness 3 survives a hardness 1 clash and still lands");
        Check(heavy.Flight.RemainingHardness == 2, "A clash costs the harder shell one point");

        // 味方同士の砲弾は、重なっても衝突しません。
        ProjectileClashField.Clear();
        var friend1 = Shell(new FakeShell(10, 1), TeamSide.Player, targetB, 0, 0, 10, 0);
        var friend2 = Shell(new FakeShell(10, 1), TeamSide.Player, targetB, 0, 0, 10, 0);
        ProjectileClashField.Register(friend1.Flight, friend1.Side);
        ProjectileClashField.Register(friend2.Flight, friend2.Side);
        friend1.Step(0.2f); friend2.Step(0.2f);
        ProjectileClashField.Resolve(friend1.Flight, friend1.Side);
        Check(friend1.Flight.RemainingHardness == 1 && friend2.Flight.RemainingHardness == 1, "Allied shells never clash");

        // Direct と Arc は、軌道の種類に関係なく、同時刻に重なれば衝突します。通る時刻が違えば衝突しません。
        ProjectileClashField.Clear();
        var overlapA = Shell(new FakeShell(10, 1), TeamSide.Player, targetB, 0, 0, 10, 0, ProjectileTrajectoryKind.Arc);
        var overlapB = Shell(new FakeShell(10, 1), TeamSide.Enemy, targetA, 0.1f, 0, 10, 0);
        ProjectileClashField.Clear();
        ProjectileClashField.Register(overlapA.Flight, overlapA.Side);
        ProjectileClashField.Register(overlapB.Flight, overlapB.Side);
        ProjectileClashField.Resolve(overlapA.Flight, overlapA.Side);
        Check(overlapA.Flight.Status == ProjectileFlightStatus.Destroyed && overlapB.Flight.Status == ProjectileFlightStatus.Destroyed, "Overlapping Arc and Direct shells clash");

        // 硬さの換算: 硬さのない弾・不正値は1、小数は四捨五入。
        Check(AmmoShotModel.HardnessOf(new FakeShell(1, 2.4f)) == 2 && AmmoShotModel.HardnessOf(new FakeShell(1, 2.5f)) == 3, "Hardness rounds to an integer");
        Check(AmmoShotModel.HardnessOf(new FakeShell(1, 0)) == 1 && AmmoShotModel.HardnessOf(new FakeShell(1, float.NaN)) == 1 &&
            AmmoShotModel.HardnessOf(new FakeShell(1, float.PositiveInfinity)) == 1000, "Invalid hardness is clamped");
        Rejects(() => Shell(new FakeShell(1, 1), TeamSide.Player, null!, 0, 0, 1, 0), "Null target is rejected");
        ProjectileClashField.Clear();
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

    /* { 処理: DirectとArcが別の2D軌道を通り、交差しても通過時刻が違えば重ならないことを確認します。 } */
    private static void TrajectoryScenarios()
    {
        var direct = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, null, null, ProjectileTrajectoryKind.Direct);
        var arc = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, null, null, ProjectileTrajectoryKind.Arc);
        direct.Advance(1);
        arc.Advance(1);
        // Directは直線、Arcは同じ進行度でも頂点側へ持ち上がります。
        Check(Math.Abs(direct.Y) < 0.001f && arc.Y > 1f && Math.Abs(direct.X - arc.X) < 0.001f, "Arc rises above the straight line");
        Check(direct.Advance(1) == ProjectileFlightStatus.Landed && arc.Advance(1) == ProjectileFlightStatus.Landed, "Both kinds land at the target");
        Check(arc.X == 10 && arc.Y == 0, "Arc ends exactly on the target");

        // 軌道が交差しても、同じ時刻に当たり判定が重ならなければ衝突しません。
        var a = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, null, null, ProjectileTrajectoryKind.Direct);
        var b = new CharacterProjectileModel(5, -5, 5, 5, 1, 1, null, null, ProjectileTrajectoryKind.Direct);
        var clashed = false;
        for (var i = 0; i < 40 && a.Status == ProjectileFlightStatus.Flying; i++)
        {
            a.Advance(0.05f);
            b.Advance(0.05f);
            var overlap = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) <= 0.4;
            CharacterProjectileModel.UpdateContact(a, b, overlap);
            clashed |= overlap;
        }
        Check(!clashed && a.RemainingHardness == 1 && b.RemainingHardness == 1, "Crossing paths at different times do not clash");

        // 同じ時刻に実際に重なれば、軌道が違っても衝突します。
        var c = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, null, null, ProjectileTrajectoryKind.Direct);
        var d = new CharacterProjectileModel(0, 0, 10, 0, 5, 1, null, null, ProjectileTrajectoryKind.Arc);
        c.Advance(0.01f);
        d.Advance(0.01f);
        CharacterProjectileModel.UpdateContact(c, d, Math.Sqrt((c.X - d.X) * (c.X - d.X) + (c.Y - d.Y) * (c.Y - d.Y)) <= 0.4);
        Check(c.Status == ProjectileFlightStatus.Destroyed && d.Status == ProjectileFlightStatus.Destroyed, "Overlapping projectiles clash regardless of kind");

        Rejects(() => new ProjectileTrajectory((ProjectileTrajectoryKind)99, 0, 0, 1, 1), "Undefined kind rejected");
        Rejects(() => new ProjectileTrajectory(ProjectileTrajectoryKind.Arc, float.NaN, 0, 1, 1), "NaN trajectory rejected");
        Rejects(() => new ProjectileTrajectory(ProjectileTrajectoryKind.Arc, -3e38f, 0, 3e38f, 0), "Arc beyond the float range rejected");
        var zero = new CharacterProjectileModel(1, 1, 1, 1, 5, 1, null, null, ProjectileTrajectoryKind.Arc);
        Check(zero.Advance(0.1f) == ProjectileFlightStatus.Landed && !float.IsNaN(zero.X), "Zero-length flight lands without NaN");
    }
}
