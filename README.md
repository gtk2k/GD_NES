# GD_NES
Gdot に移植したファミコンエミュ。
*  マッパー対応は実装していないため、初期のカセットしか遊べません。
* APU は未実装です。なので音はなりません。

# 遊び方
プロジェクトルートフォルダに .nes ファイルを配置し、[romPathに設定しているファイル名を修正](https://github.com/gtk2k/GD_NES/blob/4c4e8624d14ff598a64dca91577e22a82bd05428/NES.cs#L21) して実行すれば遊べます。

# ボタンマッピング
XBOX コントローラー対応
|キーボード|XBOXコントローラー|エミュ|
|:--:|:--:|:--:|
|Z|A|A|
|X|X|B|
|A|Back|Select|
|S|Menu|Start|
|↑|DPad ↑|↑|
|↓|DPad ↓|↓|
|←|DPad ←|←|
|→|DPad →|→|
