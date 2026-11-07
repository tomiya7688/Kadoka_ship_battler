using KadokaShipBattler.Ammo;
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
        TrajectoryScenarios();
        EffectScenarios();
        ReflectAndAreaClearScenarios();
        ScopedScenarios();
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

    private static CharacterProjectileModel Tagged(int hardness, string tag, IProjectileClashEffect effect = null)
    {
        var shot = new CharacterProjectileModel(0, 0, 99, 0, 1, hardness, null, effect);
        return tag == null ? shot : shot.AddTag(tag);
    }

    /* { 処理: 効果が弾名でなくタグで働くこと、評価順、再利用、素材規則の独立登録を確認します。 } */
    private static void EffectScenarios()
    {
        // 同じ盾割り効果を、別々の弾が共有して使えます。
        var breaker = new BreakTaggedProjectileEffect("shield");
        var a = Tagged(1, null, breaker);
        var b = Tagged(1, null, breaker);
        var shieldA = Tagged(5, "shield");
        var shieldB = Tagged(5, "shield");
        CharacterProjectileModel.UpdateContact(a, shieldA, true);
        CharacterProjectileModel.UpdateContact(b, shieldB, true);
        Check(shieldA.Status == ProjectileFlightStatus.Destroyed && shieldB.Status == ProjectileFlightStatus.Destroyed && a.RemainingHardness == 1 && b.RemainingHardness == 1, "Breaker is reusable and not consumed");

        // タグが違う相手には働かず、通常の耐久処理になります。
        var plain = Tagged(2, "arrow");
        var breaker2 = Tagged(1, null, breaker);
        CharacterProjectileModel.UpdateContact(breaker2, plain, true);
        Check(breaker2.Status == ProjectileFlightStatus.Destroyed && plain.RemainingHardness == 1, "Untagged target falls back to normal clash");

        // チェーンは登録順で評価し、最初に上書きした効果で止まります。
        var chain = new ClashEffectChain().Add(new IgnoreTaggedProjectileEffect("ghost")).Add(new BreakTaggedProjectileEffect("ghost"));
        var owner = Tagged(1, null, chain);
        var ghost = Tagged(1, "ghost");
        CharacterProjectileModel.UpdateContact(owner, ghost, true);
        Check(ghost.Status == ProjectileFlightStatus.Flying && owner.Status == ProjectileFlightStatus.Flying, "First registered effect wins");
        var reversed = new ClashEffectChain().Add(new BreakTaggedProjectileEffect("ghost")).Add(new IgnoreTaggedProjectileEffect("ghost"));
        var owner2 = Tagged(1, null, reversed);
        var ghost2 = Tagged(1, "ghost");
        CharacterProjectileModel.UpdateContact(owner2, ghost2, true);
        Check(ghost2.Status == ProjectileFlightStatus.Destroyed, "Order decides the result");

        // 素材規則は弾定義の外で登録でき、順序に依存しません。
        var rules = new AmmoCombinationRules();
        rules.Register(new[] { "fire", "arrow" }, "fire-arrow");
        Check(rules.TryResolve(new[] { "arrow", "fire" }, out var result) && result == "fire-arrow", "Combination ignores order");
        Check(!rules.TryResolve(new[] { "fire", "stone" }, out _), "Unknown combination not resolved");
        var duplicateRejected = false;
        try { rules.Register(new[] { "arrow", "fire" }, "other"); } catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "Duplicate combination rejected");
        Rejects(() => rules.Register(new[] { "only" }, "x"), "Single ingredient rejected");
        Rejects(() => new BreakTaggedProjectileEffect(" "), "Blank tag rejected");
        Check(!breaker.PreClash(a, null!), "Null target is ignored");
        var tricky = new AmmoCombinationRules();
        tricky.Register(new[] { "a001fb", "c" }, "x");
        Check(!tricky.TryResolve(new[] { "a", "b001fc" }, out _), "Separator inside ids does not collide");
        tricky.Register(new[] { "a", "a", "b" }, "y");
        Check(tricky.TryResolve(new[] { "b", "a", "a" }, out var multi) && multi == "y" && !tricky.TryResolve(new[] { "a", "b" }, out _), "Quantities are distinguished");
        Rejects(() => new ClashEffectChain().Add(null!), "Null effect rejected");
    }

    private sealed class Counter : ICarryEffect, IDeckPassiveEffect
    {
        public int Held, Released, Entered, Left;
        public void OnHeld(string holderId) => Held++;
        public void OnReleased(string holderId) => Released++;
        public void OnEnterDeck(string deckId) => Entered++;
        public void OnLeaveDeck(string deckId) => Left++;
    }

    /* { 処理: 反射（発射元へ撃ち返す・上限・非対象）と範囲消去（半径・保護・不正値）を確認します。 } */
    private static void ReflectAndAreaClearScenarios()
    {
        // 反射: 相手を発射元へ向け直し、耐久とタグは保ち、自分は消耗しません。
        var reflector = Tagged(1, null, new ReflectTaggedProjectileEffect("arrow"));
        var arrow = new CharacterProjectileModel(-10, 0, 90, 0, 10, 2).AddTag("arrow");
        arrow.Advance(1);
        Check(arrow.X > -1f, "Arrow moved before reflection");
        CharacterProjectileModel.UpdateContact(reflector, arrow, true);
        Check(arrow.Status == ProjectileFlightStatus.Flying && arrow.RemainingHardness == 2 && arrow.ReflectCount == 1 && reflector.RemainingHardness == 1, "Reflect keeps durability and does not consume reflector");
        var before = arrow.X;
        arrow.Advance(0.5f);
        Check(arrow.X < before, "Reflected arrow flies back toward origin");
        // 発射元に届くと着地します。
        for (var i = 0; i < 10 && arrow.Status == ProjectileFlightStatus.Flying; i++) arrow.Advance(1);
        Check(arrow.Status == ProjectileFlightStatus.Landed && arrow.X == -10f, "Reflected arrow lands at origin");

        // 反射上限に達した弾は反射されず、通常処理（耐久-1）になります。
        var again = new CharacterProjectileModel(-10, 0, 90, 0, 10, 3).AddTag("arrow");
        var once = Tagged(1, null, new ReflectTaggedProjectileEffect("arrow", 1));
        CharacterProjectileModel.UpdateContact(once, again, true);
        CharacterProjectileModel.UpdateContact(once, again, false);
        CharacterProjectileModel.UpdateContact(once, again, true);
        Check(again.ReflectCount == 1 && again.RemainingHardness == 2 && once.Status == ProjectileFlightStatus.Destroyed, "Reflection cap falls back to normal clash");

        // タグが違う弾は反射されません。
        var other = Tagged(2, "stone");
        var reflector2 = Tagged(1, null, new ReflectTaggedProjectileEffect("arrow"));
        CharacterProjectileModel.UpdateContact(reflector2, other, true);
        Check(other.ReflectCount == 0 && other.RemainingHardness == 1, "Untagged projectile is not reflected");
        Rejects(() => new ReflectTaggedProjectileEffect(" "), "Blank reflect tag rejected");
        Rejects(() => new ReflectTaggedProjectileEffect("arrow", 0), "Zero reflection cap rejected");
        Check(Tagged(1, null).Reflect(1, 1), "Flying projectile can be reflected");
        var landed = new CharacterProjectileModel(0, 0, 1, 0, 10, 1);
        landed.Advance(1);
        Check(!landed.Reflect(5, 5), "Landed projectile cannot be reflected");
        Rejects(() => Tagged(1, null).Reflect(float.NaN, 0), "Reflect rejects NaN");

        // 範囲消去: 半径内の飛行中の弾だけを消し、境界を含み、保護対象は消しません。
        var center = Tagged(1, null);
        var near = new CharacterProjectileModel(3, 4, 99, 4, 1, 5);
        var far = new CharacterProjectileModel(6, 8, 99, 8, 1, 5);
        var safe = new CharacterProjectileModel(1, 1, 99, 1, 1, 5).AddTag("safe");
        var all = new List<CharacterProjectileModel> { center, near, far, safe, null };
        Check(ProjectileAreaClear.Clear(0, 0, 5, all, center, "safe") == 1 && near.Status == ProjectileFlightStatus.Destroyed, "Area clear removes only in-range unprotected projectile (boundary inclusive)");
        Check(far.Status == ProjectileFlightStatus.Flying && safe.Status == ProjectileFlightStatus.Flying && center.Status == ProjectileFlightStatus.Flying, "Out of range, protected and excluded projectiles survive");
        Check(ProjectileAreaClear.Clear(0, 0, 5, all, center, "safe") == 0, "Already destroyed projectile is not counted twice");
        Check(ProjectileAreaClear.Clear(0, 0, 0, new[] { Tagged(1, null) }) == 1, "Zero radius clears co-located projectile");
        Rejects(() => ProjectileAreaClear.Clear(0, 0, -1, all), "Negative radius rejected");
        Rejects(() => ProjectileAreaClear.Clear(float.NaN, 0, 1, all), "NaN center rejected");
        Rejects(() => ProjectileAreaClear.Clear(0, 0, 1, null), "Null list rejected");
    }

    /* { 処理: 所持効果とデッキ効果が独立して有効化・無効化され、通知が1回ずつであることを確認します。 } */
    private static void ScopedScenarios()
    {
        var effect = new Counter();
        var carry = new ScopedEffectLedger<ICarryEffect>((e, id) => e.OnHeld(id), (e, id) => e.OnReleased(id));
        var deck = new ScopedEffectLedger<IDeckPassiveEffect>((e, id) => e.OnEnterDeck(id), (e, id) => e.OnLeaveDeck(id));
        Check(carry.Activate("crew-1", effect) && !carry.Activate("crew-1", effect) && effect.Held == 1, "Carry notified once");
        Check(deck.Activate("deck-1", effect) && effect.Entered == 1 && effect.Held == 1, "Deck passive is independent of carry");
        Check(carry.Deactivate("crew-1", effect) && !carry.Deactivate("crew-1", effect) && effect.Released == 1 && effect.Left == 0, "Releasing carry leaves deck passive");
        Check(deck.CountFor("deck-1") == 1 && deck.Clear("deck-1") == 1 && effect.Left == 1 && deck.CountFor("deck-1") == 0, "Clear deactivates everything");
        Rejects(() => carry.Activate(" ", effect), "Blank owner rejected");
        Rejects(() => carry.Activate("crew-2", null!), "Null effect rejected by ledger");
        var failing = new ScopedEffectLedger<ICarryEffect>((e, id) => throw new ArgumentException("boom"), (e, id) => { });
        Rejects(() => failing.Activate("crew-3", effect), "Failing notification propagates");
        Check(failing.CountFor("crew-3") == 0, "Failed activation is rolled back");
    }
}
