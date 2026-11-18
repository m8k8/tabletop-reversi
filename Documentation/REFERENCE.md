# デザイン参考と素材

## デザイン参考

- [世界のアソビ大全51 — 任天堂公式サイト](https://www.nintendo.com/jp/switch/as7ta/index.html)
- 参考日: 2026年11月18日

白い余白、明るい水色、読みやすい日本語、親しみやすい大きな操作部品、卓上ゲームらしい盤面の質感をデザインの参考にしています。本プロジェクトでは「あそびのテーブル」という独自のタイトルとUIを使用し、盤面、石、装飾、効果音を独自に実装しています。

参考作品の画像、ロゴ、スクリーンショット、音声、その他の配布素材をゲーム内に転載していません。任天堂との提携・承認関係はありません。「世界のアソビ大全51」および関連する名称・商標は各権利者に帰属します。

## フォント

- フォント: **Noto Sans JP**
- ファイル: `Assets/Fonts/NotoSansJP.ttf`（500 / Medium）、`Assets/Fonts/NotoSansJP-Bold.ttf`（700 / Bold）
- 公式ソース: [Google Fonts / Noto Sans JP](https://fonts.google.com/noto/specimen/Noto+Sans+JP)
- ライセンス: **SIL Open Font License 1.1**
- ライセンス全文: [`Assets/Fonts/OFL.txt`](../Assets/Fonts/OFL.txt)

Google Fontsで配布される可変フォントから、fontToolsを使ってウェイト500と700の静的インスタンスを作成しています。Unityのフォントインポーターで安定して同じ太さを表示するための変換です。フォントの著作権表記と再配布条件は、同梱のOFL.txtを参照してください。

## ソースコードと依存パッケージ

本プロジェクトの独自ソースコードには、ルートの[MIT License](../LICENSE)が適用されます。Unity、Unity公式パッケージ、その他の依存パッケージの権利・ライセンスは、それぞれの配布元とパッケージ内の文書を参照してください。パッケージ一覧は`Packages/manifest.json`にあります。
