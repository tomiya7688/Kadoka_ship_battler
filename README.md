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
- Space: 近くの敵コアを攻撃
- R: 再開始

黄色が弾、灰色が砲台、紫がコア、水色が操作キャラクターです。弾を4発当てて敵船体HPを0にすると中央の橋を通れます。敵船へ渡ってコアを破壊すると勝利します。船体HP0だけでは勝敗は決まりません。敵は10秒ごとに砲撃し、自船体が破壊されると橋から乗り込んでコアを攻撃します。

現在は仮の図形・1対1の船員・単純な敵の定型行動による最小プロトタイプです。5人編成、操作切替、視界、Utility AIの実行は後続Issueです。

## CI and verification

通常のGitHub Actionsは `.NET 10` で本番の `BattleModel.cs` を直接コンパイルし、運搬、装填、誤射防止、船体HP0後の継続、コア破壊など46項目を検査します。Unityのメタデータ・シーン登録も確認します。

```powershell
dotnet run --project Tools/CI/GameplayChecks.csproj --configuration Release
```

UnityのTest RunnerでPlayModeテストを実行すると、実シーンでの弾拾い→装填→砲撃→橋の移動→コア破壊と、敵の乗り込みによる敗北も検査できます。

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
