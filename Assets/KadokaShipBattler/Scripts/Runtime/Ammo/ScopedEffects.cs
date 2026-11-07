using System;
using System.Collections.Generic;

namespace KadokaShipBattler.Ammo
{
    /* { 責務: 所持中だけ所持者に作用する効果（CarryEffect）の拡張口。弾の飛翔効果とは独立しています。 } */
    public interface ICarryEffect
    {
        /* { 処理: 所持を始めた時に呼ばれます。引数: holderIdは所持者のID。 } */
        void OnHeld(string holderId);

        /* { 処理: 所持をやめた時に呼ばれます。引数: holderIdは所持者のID。 } */
        void OnReleased(string holderId);
    }

    /* { 責務: デッキに入っている間だけ作用する効果（DeckPassiveEffect）の拡張口。CarryEffectや飛翔効果とは独立しています。 } */
    public interface IDeckPassiveEffect
    {
        /* { 処理: デッキに入った時に呼ばれます。引数: deckIdはデッキのID。 } */
        void OnEnterDeck(string deckId);

        /* { 処理: デッキから出た時に呼ばれます。引数: deckIdはデッキのID。 } */
        void OnLeaveDeck(string deckId);
    }

    /* {
        責務: 所有者ごとに有効な効果を管理し、有効化・無効化の通知を1回ずつ行う、型に依存しない純粋な台帳です。
        前提: 同じ所有者に同じ効果を重複して有効化できません。
        フィールド: active=所有者IDから有効な効果の一覧への表。
        計算量: 有効化・無効化は所有者の有効数に比例します。
    } */
    public sealed class ScopedEffectLedger<TEffect> where TEffect : class
    {
        private readonly Dictionary<string, List<TEffect>> active = new();
        private readonly Action<TEffect, string> onActivate;
        private readonly Action<TEffect, string> onDeactivate;

        /* { 処理: 台帳を作ります。引数: onActivate/onDeactivateは有効化・無効化時の通知（効果の型ごとに渡す）。 } */
        public ScopedEffectLedger(Action<TEffect, string> onActivate, Action<TEffect, string> onDeactivate)
        {
            this.onActivate = onActivate ?? throw new ArgumentNullException(nameof(onActivate));
            this.onDeactivate = onDeactivate ?? throw new ArgumentNullException(nameof(onDeactivate));
        }

        /* { 処理: 効果を有効にして通知します。戻り値: 新たに有効になったらtrue、既に有効ならfalseです。例外: null/空IDを拒否します。 } */
        public bool Activate(string ownerId, TEffect effect)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("Owner id is required.", nameof(ownerId));
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (!active.TryGetValue(ownerId, out var list)) active[ownerId] = list = new List<TEffect>();
            if (list.Contains(effect)) return false;
            list.Add(effect);
            try { onActivate(effect, ownerId); }
            catch
            {
                // 通知に失敗した効果は有効のまま残さず、台帳と通知の状態を一致させます。
                list.Remove(effect);
                if (list.Count == 0) active.Remove(ownerId);
                throw;
            }
            return true;
        }

        /* { 処理: 効果を無効にして通知します。戻り値: 有効だったらtrueです。 } */
        public bool Deactivate(string ownerId, TEffect effect)
        {
            if (ownerId == null || effect == null || !active.TryGetValue(ownerId, out var list) || !list.Remove(effect)) return false;
            if (list.Count == 0) active.Remove(ownerId);
            onDeactivate(effect, ownerId);
            return true;
        }

        /* { 処理: 所有者の有効な効果の数を返します。 } */
        public int CountFor(string ownerId) => ownerId != null && active.TryGetValue(ownerId, out var list) ? list.Count : 0;

        /* { 処理: 所有者の効果をすべて無効にして通知します（所持者の離脱やデッキの破棄用）。戻り値: 無効にした数。 } */
        public int Clear(string ownerId)
        {
            if (ownerId == null || !active.TryGetValue(ownerId, out var list)) return 0;
            var count = list.Count;
            // 1件ずつ外して通知するため、途中で例外が出ても未通知分は台帳に残り、後から再実行できます。
            while (list.Count > 0)
            {
                var effect = list[list.Count - 1];
                onDeactivate(effect, ownerId);
                list.RemoveAt(list.Count - 1);
            }
            active.Remove(ownerId);
            return count;
        }
    }
}
