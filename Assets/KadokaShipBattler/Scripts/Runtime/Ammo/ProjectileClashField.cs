using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Ammo
{
    /* {
        責務: 飛行中の通常砲弾とキャラクター弾を同じ場に登録し、敵対する弾同士の重なりを衝突耐久モデルへ渡します。
        前提: 味方同士は衝突しません。軌道の種類は問わず、2D上の当たり判定が重なったときだけ衝突します。
        計算量: 1回の解決は登録数に比例します。同時に飛ぶ弾は少数です。
    } */
    public static class ProjectileClashField
    {
        public const float ClashDistance = 0.4f;
        private static readonly List<(CharacterProjectileModel Model, TeamSide Side)> Entries = new();

        public static int Count => Entries.Count;

        /* { 処理: 飛行中の弾を場へ登録します。引数: modelは弾の飛行モデル、sideは陣営。例外: nullを拒否します。 } */
        public static void Register(CharacterProjectileModel model, TeamSide side)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            foreach (var entry in Entries) if (ReferenceEquals(entry.Model, model)) return;
            Entries.Add((model, side));
        }

        /* { 処理: 弾を場から外します。登録がなければ何もしません。 } */
        public static void Unregister(CharacterProjectileModel model)
        {
            Entries.RemoveAll(entry => ReferenceEquals(entry.Model, model));
        }

        /* { 処理: 指定した弾と、敵対する他の飛行中の弾との重なりを渡します。重なりが外れた場合も渡して次の接触を数え直します。 } */
        public static void Resolve(CharacterProjectileModel model, TeamSide side)
        {
            if (model == null || model.Status != ProjectileFlightStatus.Flying) return;
            // 解決中に撃墜された弾が外されても安全なよう、複製を走査します。
            foreach (var other in Entries.ToArray())
            {
                if (ReferenceEquals(other.Model, model) || other.Side == side || other.Model.Status != ProjectileFlightStatus.Flying) continue;
                var dx = other.Model.X - model.X;
                var dy = other.Model.Y - model.Y;
                CharacterProjectileModel.UpdateContact(model, other.Model, dx * dx + dy * dy <= ClashDistance * ClashDistance);
            }
        }

        /* { 処理: 登録をすべて消します。テストの後始末に使います。 } */
        public static void Clear() => Entries.Clear();
    }
}
