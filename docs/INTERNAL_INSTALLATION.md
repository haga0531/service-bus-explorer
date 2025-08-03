# Service Bus Explorer - 社内インストールガイド

## 🍺 Homebrew でのインストール（推奨）

### 前提条件
- macOS または Linux
- Homebrew がインストール済み
- 社内GitHubへのアクセス権限

### インストール手順

1. **Tap を追加**（初回のみ）
```bash
brew tap yourusername/service-bus-explorer https://github.com/yourusername/homebrew-service-bus-explorer.git
```

2. **インストール**
```bash
brew install service-bus-explorer
```

3. **実行**
```bash
service-bus-explorer
```

### アップデート

```bash
brew update
brew upgrade service-bus-explorer
```

### アンインストール

```bash
brew uninstall service-bus-explorer
```

## 💻 Windows での利用

### オプション1: スタンドアロン版

1. [Releases](https://github.com/yourusername/service-bus-explorer-crossplat/releases) から最新の `ServiceBusExplorer-win-x64.zip` をダウンロード
2. 任意のフォルダに展開
3. `ServiceBusExplorer.UI.exe` を実行

### オプション2: Scoop でのインストール（設定済みの場合）

```powershell
scoop bucket add company-tools https://github.com/yourusername/scoop-bucket.git
scoop install service-bus-explorer
```

## 🔧 トラブルシューティング

### macOS で「開発元が未確認」エラーが出る場合

```bash
# Homebrew でインストールした場合
xattr -cr $(brew --prefix)/Cellar/service-bus-explorer/
```

### .NET ランタイムが必要な場合

```bash
# macOS/Linux
brew install dotnet

# Windows
winget install Microsoft.DotNet.Runtime.8
```

## 📞 サポート

- 社内Slack: #service-bus-support
- 問題報告: [社内GitHubリポジトリ](https://github.com/yourusername/service-bus-explorer-crossplat/issues)

## 🔒 セキュリティ

- Service Bus の接続文字列は暗号化されて保存されます
- 認証情報はキーチェーン（macOS）またはCredential Manager（Windows）に保存されます
- 社内ネットワーク外からの利用時はVPN接続が必要です