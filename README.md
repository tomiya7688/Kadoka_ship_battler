# Kadoka ship battler

船内で弾を運び、砲台を動かし、仲間と役割分担しながら敵船と戦う船バトルゲームです。

## Development

- Engine: **Unity 6.3 LTS**
- Language: **C#**
- Editor used for local verification: `6000.3.24f1`

Unity Hub からリポジトリのルートディレクトリを開いてください。

## Playable prototype

`Assets/KadokaShipBattler/Scenes/BattlePrototype.unity` を開いて Play を押してください。

- WASD / 矢印: 移動
- E: 近くの弾を拾う / 砲台へ装填 / 装填済みの砲台を発射
- Space: 通常攻撃（敵船員 / 敵コア）
- Tab: 次の生存している味方へ操作対象を切り替える
- R: 再開始

黄色・オレンジ色が弾、灰色が砲台、紫がコアです。弾を4発当てて敵船体HPを0にすると中央の橋を通れます。敵船へ渡ってコアを破壊すると勝利します。船体HP0だけでは勝敗は決まりません。敵は10秒ごとに砲撃し、自船体が破壊されると橋から乗り込んでコアを攻撃します。

現在は仮の図形・味方5人／敵5人によるプロトタイプです。白いマーカーと画面のControl欄が操作対象を示します。切替時に位置・向き・所持弾を保持し、元の船員は直ちに弾運搬AIへ戻ります。HPが0になった船員は行動・操作できなくなり、操作中なら生存している味方へ切り替わります。船員全滅だけでは勝敗は決まりません。

味方のAIは現在位置と現在向きから前方120度・距離6以内の弾を探し、運搬→装填→発射を実行します。弾が見えない時は向きを変えて探します。砲台の位置は既知情報です。敵には周囲の敵を通常攻撃する簡単な防衛AIがあり、先頭の行動可能な砲撃担当が砲撃・乗り込みを行います。壁による遮蔽、部屋の経路探索、更新型Utility AIは後続Issueです。

### 編成とキャラクターデータ

[`PrototypeCrew.json`](Assets/KadokaShipBattler/Data/PrototypeCrew.json) に共通のキャラクター定義と両陣営の5枠を保存しています。`characters` に定義を追加し、`player.slots` / `enemy.slots` のIDを変更すると次のシーン開始時に反映されます。各陣営の先頭枠がリーダーです。5枠でない編成、未定義・重複定義のID、範囲外の能力値は開始前に拒否します。同じキャラクターを複数枠へ配置することはできます。

| 船員 | HP | 移動速度 | 通常攻撃 | 攻撃力 | 射程 | 弾硬度 | 飛行 / すり抜け |
|---|---:|---:|---|---:|---:|---:|---|
| Leader | 100 | 4 | 接触 | 25 | 0.9 | 2 | false / false |
| Gunner | 70 | 2.8 | 遠距離 | 40 | 2.5 | 1 | false / false |
| Carrier | 90 | 3.5 | なし | 0 | 0 | 2 | false / false |
| Scout | 60 | 5 | 接触 | 15 | 0.9 | 1 | true / true |
| Defender | 180 | 2 | 接触 | 50 | 0.9 | 5 | false / false |

接触攻撃は射程内、遠距離攻撃は現在向きの前方120度・射程内の最も近い敵へ即時にダメージを与えます。弾が飛ぶ処理と硬度判定はIssue #16、飛行・すり抜けによる移動はIssue #10で実装する予定です。現在は硬度・飛行・すり抜けの値をデータとして保持しています。修理能力は使用できません。

`attackMode` は0=なし・1=接触・2=遠距離です。`capabilities` はCombat=1、CarryAmmo=2、OperateCannon=4、BoardEnemyShip=16、Support=32の合計値です。修理用の8は指定できません。`combatSkill` / `carrySkill` は0〜10の適性です。

### 重量と保持数

| 船員 | 許容合計重量 | 最大保持数 |
|---|---:|---:|
| Leader | 5 | 2 |
| Gunner | 3 | 1 |
| Carrier | 9 | 3 |
| Scout | 3 | 1 |
| Defender | 0 | 0 |

黄色の弾は重量2、オレンジ色の弾は重量3です。どちらもダメージ25です。追加時は個数と合計重量の両方を判定します。Leaderは「2+2」「2+3」を持てますが「3+3」は持てません。満杯・重量超過で拾えなかった弾はその場に残ります。

Eで砲台へ装填する時は、先に拾った弾を1発だけ渡します。残りの弾と重量は保持します。Tabで操作を外しても複数の所持弾は失われず、復帰したAIも同じ運搬判定を使って順番に装填・発射します。AIは運べない弾を探索候補から除外します。

`CharacterDefinition` の `CarryCapacity` / `MaxCarryCount` と `AmmoDefinition` の `Weight` が定義データです。実行中に上限を下げた場合、すでに持っている弾は保持し、追加取得を制限します。重量は取得時の値を保持して集計します。

## CI and verification

通常のGitHub Actionsは `.NET 10` で本番の `BattleModel.cs`、`CrewControlState.cs`、`CrewDefinitions.cs` を直接コンパイルし、実際の編成JSONを読み込んで戦闘・操作切替・重量と保持数・5人編成とHPの133項目を検査します。Unityのメタデータ・シーン登録も確認します。

```powershell
dotnet run --project Tools/CI/GameplayChecks.csproj --configuration Release
```

UnityのTest RunnerのPlayModeテストは36件です。実シーンの戦闘・勝敗・描画に加えて、操作切替、状態保持、AI復帰と運搬・砲撃、船員別の移動・攻撃、重量と保持数の上限、複数弾の順次装填、両陣営5人の生成、HP・死亡時の操作切替、データ追加も検査します。

GitHub上のUnity PlayModeジョブには別途ライセンス設定が必要です。変数 `RUN_UNITY_TESTS=true` と、GameCIに適合する `UNITY_LICENSE` または `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` をsecretsへ設定してください。ローカルUnityのライセンスはGitHubへ自動転送されません。未設定時はUnityジョブがskippedになり、通常の戦闘ロジックCIは実行されます。

## Architecture

ゲーム定義と実行ロジックを分離します。

- `ScriptableObject`: キャラクター、弾、AI方針などの定義データ
- `MonoBehaviour`: 船、船員、戦闘中の実体
- Utility AI: 行動候補を評価値で比較して選択
- Team policy / Character policy: Utility AI の評価値へ補正を加える
- Capability: キャラクターが実行可能な行動そのものを制限

キャラクター固有モーションや見た目は判断ロジックから分離し、同じ行動でもキャラクターごとに表現を差し替えられる構造を目指します。

## Directory

```text
Assets/KadokaShipBattler/
├─ Scripts/
│  └─ Runtime/
│     ├─ Core/
│     ├─ Ships/
│     ├─ Characters/
│     ├─ Ammo/
│     └─ AI/
├─ Data/
├─ Prefabs/
├─ Scenes/
└─ Tests/
```

## Initial gameplay target

1. プレイヤーと敵の船を1隻ずつ配置する
2. プレイヤーが船内を移動する
3. 弾を拾って砲台へ運ぶ
4. 砲台へ装填して発射する
5. 敵船にダメージを与える
6. 味方4人・敵5人をAIで動かす
7. AI方針・弾デッキ・キャラクター適性によって行動傾向を変える

## AI principles

- 味方AIは更新型評価関数を持ち、弾デッキや戦闘傾向へ徐々に適応する
- チーム方針とキャラクター方針は別々に設定する
- キャラクターごとに選択可能な方針が異なる
- 戦闘が強いキャラクターの多くは運搬を不得意とするなど、役割間にトレードオフを持たせる
- 敵AIは賢くするが、未観測の隠し情報を直接読むチートAIにはしない

## Characters

`Obake_Lisense` の「かどか」「まる」も登場予定です。両キャラクターは性能を大きく優遇するのではなく、専用モーションやリアクションなど演出面を厚くする方針です。

## License

現在はゲーム本体と開発者ツールの分離途中のため、暫定的な混合ライセンスを採用しています。

- **Developer Tool Components:** MIT License
- **Game Components:** Kadoka Ship Battler Game License (Provisional)
- **Developer Tools から生成したゲームや成果物:** 改変・再配布・商用販売可。Kadoka Ship Battler と構造やゲームメカニクスが近く、主にキャラクターやマップ等が異なるゲームも許可対象です。
- **元ゲーム固有の素材・未指定コード:** 生成物の自由利用許可には含まれません。

詳細は [`LICENSE.md`](LICENSE.md) を参照してください。
