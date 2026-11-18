# デザイン参考と素材

## デザイン参考

- [世界のアソビ大全51 — 任天堂公式サイト](https://www.nintendo.com/jp/switch/as7ta/index.html)
- 参考日: 2026年11月18日

白い余白、明るい水色、読みやすい日本語、親しみやすい大きな操作部品、卓上ゲームらしい盤面の質感をデザインの参考にしています。追加で提供された参考画像から、鮮やかな緑の織り目、暗い木枠、マットなコマの質感を取り入れています。本プロジェクトでは「あそび選択」という独自のタイトルとUIを使用し、盤面、石、装飾、効果音を独自に実装しています。

画面は「あそび選択」、「リバーシについて」、対局の順に構成しています。選択・設定画面は白と水色でまとめ、静止画の代わりに独自のプレイ映像を表示します。対局画面では木の床と盤面を大きく見せ、左下と右上にプレイヤー名とコマ色のアイコンを配置し、色名の文字は表示しません。画面上のメニューボタンから、対局の終了・再開やメニューを閉じる操作へ進めます。ルールは対局前の「リバーシについて」の映像で説明します。この画面構築は`ReversiGame.Interface.cs`に分けて実装しています。

参考作品の画像、ロゴ、スクリーンショット、音声、その他の配布素材をゲーム内に転載していません。任天堂との提携・承認関係はありません。「世界のアソビ大全51」および関連する名称・商標は各権利者に帰属します。

## 盤面・木枠・コマ・床の素材

盤面・木枠・コマの素材は`TabletopSurfaceAtlas.cs`で手続き生成し、`TabletopBoardGraphic.cs`で描画しています。参考画像の画素を取り込んだり、配布データへ同梱したりしていません。

- 盤面: エメラルドグリーンを基調に、盤面全体でつながる細かな斜めの織り目を生成。
- 木枠: 深いウォールナット色の木目に、丸い面取りと内側に落ちる影を組み合わせて表現。
- コマ: 黒とアイボリーのマットな面に、浅い側面と接地影を加えて厚みを表現。
- 床: `WoodenFloorGraphic.cs`で、明るい木材の横方向の木目と板の継ぎ目を生成。盤面の下を含む画面全体に連続して描画。

## プレイ映像とルール説明

映像は本プロジェクトのUnity描画から生成したオリジナルのMP4です。任天堂のゲーム映像や外部の録画素材を使っていません。

- 選択用: `Assets/Resources/Video/ReversiIntro.mp4`、10秒の無音ループ。
- 説明用: `Assets/Resources/Video/ReversiGuide.mp4`、32秒の無音ループ。
- 共通仕様: 960×600、24fps、H.264、YUV420p。

`ReversiReplayTimeline.cs`は、同じ合法な60手の対局から序盤・中盤・終盤・終局の要所を選ぶ、再現可能な編集データです。映像は5つの説明場面を短い暗転でつなぎ、途中2か所を省略して終局まで進めます。暗転中は説明文を消し、各場面の簡単なルール説明を動画の再生時刻に同期させています。一時停止・章移動ができ、対戦設定の変更時にも再生位置を引き継ぎます。`ReversiReplayPlayer.cs`がUnityのVideoPlayerを使って再生します。

### 映像を再生成する

Playモードを停止し、EditorのC#実行環境から`TabletopReversi.ReversiVideoBaker.Start()`を実行します。`Status()`が完了を返すまで待つと、実際の盤面と床を描いた768枚の連番PNGが`Captures/ReversiFrames/`に生成されます。このフォルダはGit管理対象外です。

プロジェクトのルートで、ffmpegを使ってMP4を生成します。

```sh
mkdir -p Assets/Resources/Video
ffmpeg -y -framerate 24 -i Captures/ReversiFrames/frame-%05d.png -t 32 -an -c:v libx264 -crf 18 -preset medium -pix_fmt yuv420p -bsf:v h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1 -movflags +faststart Assets/Resources/Video/ReversiGuide.mp4
ffmpeg -y -framerate 24 -i Captures/ReversiFrames/frame-%05d.png -t 10 -vf fade=t=in:st=0:d=0.18 -an -c:v libx264 -crf 18 -preset medium -pix_fmt yuv420p -bsf:v h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1 -movflags +faststart Assets/Resources/Video/ReversiIntro.mp4
```

選択用映像のコマンドでは、ループ開始時を和らげる短いフェードインを加えています。生成後にUnityへインポートします。盤面・床の描画素材と編集データも独自のソースコードから作られます。

## フォント

- フォント: **Noto Sans JP**
- ファイル: `Assets/Fonts/NotoSansJP.ttf`（500 / Medium）、`Assets/Fonts/NotoSansJP-Bold.ttf`（700 / Bold）
- 公式ソース: [Google Fonts / Noto Sans JP](https://fonts.google.com/noto/specimen/Noto+Sans+JP)
- ライセンス: **SIL Open Font License 1.1**
- ライセンス全文: [`Assets/Fonts/OFL.txt`](../Assets/Fonts/OFL.txt)

Google Fontsで配布される可変フォントから、fontToolsを使ってウェイト500と700の静的インスタンスを作成しています。Unityのフォントインポーターで安定して同じ太さを表示するための変換です。フォントの著作権表記と再配布条件は、同梱のOFL.txtを参照してください。

## ソースコードと依存パッケージ

本プロジェクトの独自ソースコードには、ルートの[MIT License](../LICENSE)が適用されます。Unity、Unity公式パッケージ、その他の依存パッケージの権利・ライセンスは、それぞれの配布元とパッケージ内の文書を参照してください。パッケージ一覧は`Packages/manifest.json`にあります。
