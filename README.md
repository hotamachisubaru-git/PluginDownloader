# PluginDownloader

Windows x64 向けの Bukkit/Paper プラグイン更新アプリです。  
左ペインに更新対象 Jar を入れると、Plugin は Modrinth/Spiget、Paper本体は PaperMC API から最新版を取得して右ペインに結果を表示します。

## 主な機能

- 更新対象 Jar の追加
  - ファイル選択
  - Paper本体専用追加
  - フォルダ選択（直下の `*.jar`）
  - ドラッグ&ドロップ
- Jar 内の `plugin.yml` 解析（`name` / `version` / `website`）
- Paper本体Jar名解析（`paper-<mcVersion>-<build>.jar`）
- 最新版検索
  - `PaperMC API (fill v3)`（Paper本体）
  - `Modrinth API`
  - `Spiget API`
- ダウンロード保存
  - 保存先を固定
  - 実行時に毎回指定
- 更新結果表示
  - 更新済み / 最新 / 失敗
  - 保存先パス、失敗理由

## 要件

- Windows 10/11 x64
- .NET 8 Runtime（self-contained で配布する場合は不要）

## 開発ビルド

```powershell
dotnet build -c Release
```

## 実行

```powershell
dotnet run -c Release
```

## 配布用 publish (win-x64)

Framework-dependent:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

成果物:

- `bin/Release/net8.0-windows/win-x64/publish/PluginDownloader.exe`

## 使い方

1. 保存先フォルダを設定
2. 左ペインへ更新対象の `*.jar` を追加（Plugin / Paper本体）
3. `更新実行` を押す
4. 右ペインで結果を確認

## 注意点

- API で直接配布ファイルに到達できない外部配布プラグインは自動取得できません。
- プラグイン名の曖昧一致に依存するため、同名/類似名プラグインは誤マッチの可能性があります。
- 一部プラグインは配布元の利用規約上、自動ダウンロード対象外です。
- Paper本体は安全性のため、同一Minecraftバージョン内で最新buildに更新します（例: `1.21.8-20` -> `1.21.8-60`）。
