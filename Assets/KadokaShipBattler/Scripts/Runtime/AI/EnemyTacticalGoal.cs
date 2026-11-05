namespace KadokaShipBattler.AI
{
    /* {
        責務: 敵チーム全体が同時に共有する戦術目標を区別します。
        前提: 値の意味は次の通りです。Bombard=弾を運び砲撃する、DefendShip=自船内の侵入者を迎撃する、BoardEnemy=開いた橋から相手船へ乗り込む。
    } */
    public enum EnemyTacticalGoal
    {
        Bombard = 0,
        DefendShip = 1,
        BoardEnemy = 2
    }
}
