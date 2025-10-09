valheim_decompiled_source フォルダにあるvalheim の逆コンソースを学習してから修正する

ExtraAttackSystem
GUID Dyju420.ExtraAttackSystem
PluginVersion 1.0.0


✅ 解決済み問題
MOD Desc
Modifier Key押下で即座発動
現在までの仕様
ファイルはすべて処理別に分割
m_animator 経由で実装する
m_animator などの private 変数にアクセスできない問題は、publicilize で解決

memo_changes.txt
- 新しい問題が発生したら、発生状況／原因／修正方法／対応箇所／影響の形式で、同ファイルの「ここから下にメモしていく」以降へ追記します。
- ビルドエラー関連は記録しません（ゲーム内仕様のみ）。

目標仕様

Q/T/GキーでそれぞれStyle1/Style2/Style3の「カスタムアニメーションによるセカンダリ属性攻撃」を発動する
装備中の武器種で判定・ダメージが適用される
ダメージ/ヒット/タイミングなどはYAMLで調整可能
プライマリ攻撃のチェインは現状不要だが、将来使う可能性を考慮し処理は残す

互換必要MOD
https://thunderstore.io/c/valheim/p/Smoothbrain/DualWield/
https://thunderstore.io/c/valheim/p/RustyMods/DualWielder/