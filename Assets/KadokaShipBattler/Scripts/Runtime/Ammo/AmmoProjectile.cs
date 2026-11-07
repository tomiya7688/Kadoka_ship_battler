using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    /* {
        責務: 発射された通常砲弾をシーン上で飛ばし、敵対する弾との衝突と敵船への着弾を処理します。
        前提: 飛行・衝突耐久・ダメージは AmmoShotModel と ProjectileClashField が判断し、この部品は座標の反映と後始末だけを行います。
        フィールド: model=砲弾の純粋なモデル。
    } */
    [DisallowMultipleComponent]
    public sealed class AmmoProjectile : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<AmmoProjectile> Live = new();
        private static Sprite shellSprite;
        private AmmoShotModel model;

        public AmmoShotModel Model => model;
        public static int LiveCount => Live.Count;

        /* { 処理: 砲弾を発射位置に生成して飛ばします。引数: ammoは弾、ownerSideは発射側、targetは着弾する船、fromは発射位置、kindは弾道。戻り値: 生成した砲弾。目標がなければnull。 } */
        public static AmmoProjectile Launch(IAmmo ammo, TeamSide ownerSide, ShipController target, Vector2 from, ProjectileTrajectoryKind kind)
        {
            if (ammo == null || target == null) return null;
            var item = new GameObject(ownerSide + " Shell");
            item.transform.position = new Vector3(from.x, from.y, 0f);
            item.transform.localScale = Vector3.one * 0.25f;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = ShellSprite();
            renderer.sortingOrder = 3;
            renderer.color = new Color(1f, 0.55f, 0.1f);
            var projectile = item.AddComponent<AmmoProjectile>();
            var goal = target.transform.position;
            projectile.model = new AmmoShotModel(ammo, ownerSide, target.BattleState, from.x, from.y, goal.x, goal.y, kind);
            ProjectileClashField.Register(projectile.model.Flight, ownerSide);
            Live.Add(projectile);
            return projectile;
        }

        /* { 処理: 飛行を進め、衝突を判定し、着弾または撃墜を反映して自身を片付けます。 } */
        private void Update() => Tick(Time.deltaTime);

        /* { 処理: 飛行中の全砲弾を指定秒数だけ進めます。引数: deltaTimeは有限の正の秒数。補足: 実行時はUpdateと同じ処理で、フレームを待たない検証にも使えます。 } */
        public static void AdvanceAll(float deltaTime)
        {
            foreach (var projectile in Live.ToArray()) projectile.Tick(deltaTime);
        }

        private void Tick(float deltaTime)
        {
            // timeScale 0 などで経過時間が0や非有限のときは、飛行を進めません。
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime)) return;
            if (model == null || model.Outcome != AmmoShotOutcome.Flying) return;
            model.Step(deltaTime);
            transform.position = new Vector3(model.Flight.X, model.Flight.Y, 0f);
            ProjectileClashField.Resolve(model.Flight, model.Side);
            if (model.Settle() == AmmoShotOutcome.Flying) return;
            // 結末が確定した砲弾は登録を外して片付けます。
            ProjectileClashField.Unregister(model.Flight);
            Live.Remove(this);
            Destroy(gameObject);
        }

        /* { 処理: 場面の破棄で飛行中なら取り消し、弾を山札へ返して登録を外します。 } */
        private void OnDestroy()
        {
            Live.Remove(this);
            if (model == null) return;
            model.Cancel();
            ProjectileClashField.Unregister(model.Flight);
        }

        // 外部アセットなしで見えるよう、白い1x1のスプライトを一度だけ作ります。
        private static Sprite ShellSprite()
        {
            if (shellSprite != null) return shellSprite;
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            shellSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return shellSprite;
        }
    }
}
