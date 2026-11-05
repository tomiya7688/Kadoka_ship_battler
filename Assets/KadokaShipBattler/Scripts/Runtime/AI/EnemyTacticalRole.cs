namespace KadokaShipBattler.AI
{
    /* {
        責務: 戦術目標を実行するため、敵船員一人ひとりへ割り当てる役割を区別します。
        前提: None=割り当てなし、Gunner=装填済みの砲台を発射する、Supplier=弾を拾って砲台へ装填する、
              Defender=自船内の侵入者へ近づき攻撃する、Boarder=相手船へ乗り込み核を探して攻撃する。
    } */
    public enum EnemyTacticalRole
    {
        None = 0,
        Gunner = 1,
        Supplier = 2,
        Defender = 3,
        Boarder = 4
    }
}
