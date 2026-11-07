using KadokaShipBattler.Ammo;
using KadokaShipBattler.Ships;
using UnityEngine;

namespace KadokaShipBattler.Characters
{
    /* {
        責務: 砲台から発射されたキャラクターを、飛行中の弾として動かし、弾同士の衝突と敵船への着地を処理します。
        前提: 発射中の船員は行動不能にし、着地したら船員として行動へ復帰させます。撃ち落とされた船員は戦闘不能になります。
        計算量: 衝突判定は飛行中の弾の組に比例します。同時に飛ぶ弾は少数です。
    } */
    [DisallowMultipleComponent]
    public sealed class CharacterProjectile : MonoBehaviour
    {
        public const float ClashDistance = ProjectileClashField.ClashDistance;
        public const float FlightSpeed = 6f;
        private CharacterProjectileModel model;
        private CrewMember crew;
        private PlayerCrewController controller;

        public CharacterProjectileModel Model => model;

        /* { 処理: 船員を発射します。前提: 呼び出し側が砲台の位置と敵船を渡します。戻り値: 発射できたらtrueです。 } */
        public static CharacterProjectile Launch(CrewMember passenger, Vector2 from, ShipController target, IProjectileBehaviour behaviour = null,
            ProjectileTrajectoryKind trajectoryKind = ProjectileTrajectoryKind.Direct)
        {
            if (passenger == null || target == null || !passenger.IsAlive) return null;
            // 飛行中は通常の行動を止め、発射位置から動かします。
            var projectile = passenger.GetComponent<CharacterProjectile>() ?? passenger.gameObject.AddComponent<CharacterProjectile>();
            projectile.crew = passenger;
            projectile.controller = passenger.GetComponent<PlayerCrewController>();
            projectile.model = new CharacterProjectileModel(from.x, from.y, target.transform.position.x,
                target.transform.position.y, FlightSpeed, passenger.Definition.ProjectileHardness, behaviour, null, trajectoryKind);
            if (projectile.controller != null) projectile.controller.enabled = false;
            // 着地後に無効化された再利用コンポーネントでも、再び飛行を更新します。
            projectile.enabled = true;
            passenger.transform.position = new Vector3(from.x, from.y, 0f);
            ProjectileClashField.Register(projectile.model, passenger.TeamSide);
            return projectile;
        }

        /* { 処理: 飛行を進め、衝突を判定し、着地または撃墜の結果を反映します。 } */
        private void Update()
        {
            if (model == null || !enabled) return;
            // 他の弾に撃ち落とされた場合も、この更新で結果を反映します。
            if (model.Status == ProjectileFlightStatus.Destroyed) { Shootdown(); return; }
            if (!(Time.deltaTime > 0f) || float.IsInfinity(Time.deltaTime)) return;
            model.Advance(Time.deltaTime);
            transform.position = new Vector3(model.X, model.Y, 0f);
            ResolveClashes();
            if (model.Status == ProjectileFlightStatus.Landed) Land();
            else if (model.Status == ProjectileFlightStatus.Destroyed) Shootdown();
        }

        /* { 処理: 飛行中の敵対する弾（通常砲弾・キャラクター弾）との重なりをモデルへ渡します。重なり始めの1回だけ耐久が減ります。 } */
        private void ResolveClashes() => ProjectileClashField.Resolve(model, crew.TeamSide);

        /* { 処理: 敵船内へ着地した船員を行動へ復帰させます。 } */
        private void Land()
        {
            ProjectileClashField.Unregister(model);
            // 着地した船員は、相手船の船体が無傷でもその部屋を移動できます。
            if (controller != null)
            {
                controller.HasBoarded = true;
                controller.enabled = true;
            }
            enabled = false;
        }

        /* { 処理: 撃ち落とされた船員を戦闘不能にします。 } */
        private void Shootdown()
        {
            ProjectileClashField.Unregister(model);
            crew.ApplyDamage(crew.Definition.MaxHp);
            enabled = false;
        }

        private void OnDestroy() => ProjectileClashField.Unregister(model);
    }
}
