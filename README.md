# ScheduleAdjust

社内外の日程調整を効率化するWebアプリケーションです。Microsoft Graph APIと連携し、Outlookカレンダーの空き時間を自動算出、社外の方にはURLを共有するだけで日程調整が完了します。確定時にはOutlook予定とTeams会議が自動作成されます。

## 主要機能

| 機能 | 説明 |
|------|------|
| **F-01: 調整ページ作成** | タイトル、所要時間、候補期間、同席者を指定して日程調整を作成 |
| **F-02: 空き時間自動算出** | 同席者のOutlookカレンダーからGraph APIで共通の空き時間を自動算出 |
| **F-03: 社外向け予約ページ** | GUID付きURLを共有するだけで、社外の方が認証不要で日時を選択可能 |
| **F-04: 日程確定** | 主催者がワンクリックで確定 → Outlook予定 + Teams会議を自動作成 |
| **F-05: メール通知** | 調整URL送信、確定通知、リマインダーを自動送信 |
| **F-06: ダッシュボード** | 作成した日程調整の一覧表示（ステータス、回答数、期限） |
| **F-07: 候補日時の手動編集** | 自動算出された候補に加え、手動で候補日時を追加・削除可能 |
| **F-08: 期限切れ自動処理** | 回答期限を過ぎた調整を自動検出し、ステータス更新・通知 |

## 技術スタック

- **フレームワーク**: ASP.NET Core 8 MVC
- **ORM**: Entity Framework Core 8 (SQL Server)
- **認証**: Microsoft Entra ID (Azure AD) + MSAL.NET
- **外部API**: Microsoft Graph API (Calendar, OnlineMeetings, Mail)
- **UI**: Razor Views + Bootstrap 5 + Bootstrap Icons
- **ログ**: Serilog
- **テスト**: xUnit + Moq + EF Core InMemory

## プロジェクト構成

```
SchaduleAdjust/
├── ScheduleAdjust.sln                  # ソリューションファイル
├── sql/
│   └── 001_CreateTables.sql          # DBマイグレーションSQL
├── src/ScheduleAdjust/
│   ├── Controllers/
│   │   ├── ScheduleController.cs     # 社内向け（認証必須）
│   │   └── BookingController.cs      # 社外向け（匿名アクセス可）
│   ├── Data/
│   │   └── ScheduleAdjustDbContext.cs   # EF Core DbContext
│   ├── Models/
│   │   ├── SchedulePoll.cs           # 日程調整エンティティ
│   │   ├── PollAttendee.cs           # 同席者
│   │   ├── PollTimeSlot.cs           # 候補日時
│   │   ├── PollResponse.cs           # 回答
│   │   └── PollStatus.cs             # ステータスenum
│   ├── Services/
│   │   ├── IGraphApiService.cs       # Graph APIインターフェース
│   │   ├── GraphApiService.cs        # Graph API実装
│   │   ├── StubGraphApiService.cs    # オフライン開発用スタブ
│   │   ├── IScheduleService.cs       # ビジネスロジックIF
│   │   ├── ScheduleService.cs        # ビジネスロジック実装
│   │   ├── INotificationService.cs   # 通知IF
│   │   ├── NotificationService.cs    # 通知実装
│   │   └── DeadlineHostedService.cs  # 期限切れバックグラウンド処理
│   ├── ViewModels/                   # 各画面用ViewModel
│   ├── Views/                        # Razorビュー
│   ├── wwwroot/                      # 静的ファイル (CSS/JS)
│   ├── Program.cs                    # エントリポイント・DI設定
│   ├── appsettings.json              # 本番設定
│   └── appsettings.Development.json  # 開発設定
└── tests/ScheduleAdjust.Tests/         # ユニットテスト
```

## セットアップ

### 前提条件

- .NET 8 SDK
- SQL Server（ローカルまたはリモート）
- Microsoft Entra ID テナント（Azure AD アプリ登録済み）

### 1. Azure AD アプリ登録

Azure PortalでEntra IDにアプリを登録し、以下のAPI権限を付与します:

- `Calendars.ReadWrite`
- `OnlineMeetings.ReadWrite`
- `User.Read.All`
- `Mail.Send`

リダイレクトURIに `https://localhost:5001/signin-oidc` を設定してください。

### 2. 設定ファイルの編集

`src/ScheduleAdjust/appsettings.json` に Azure AD の情報を設定します:

```json
{
  "AzureAd": {
    "TenantId": "<テナントID>",
    "ClientId": "<クライアントID>",
    "ClientSecret": "<クライアントシークレット>"
  },
  "ConnectionStrings": {
    "DefaultConnection": "<SQL Server接続文字列>"
  }
}
```

### 3. データベースの作成

SQL Serverに接続し、マイグレーションスクリプトを実行します:

```bash
sqlcmd -S . -d ScheduleAdjust -i sql/001_CreateTables.sql
```

### 4. ビルドと実行

```bash
dotnet restore
dotnet build
dotnet run --project src/ScheduleAdjust
```

ブラウザで `https://localhost:5001` にアクセスしてください。

## 開発モード（オフライン開発）

Graph APIやAzure ADの設定なしで開発・動作確認が可能です。

`appsettings.Development.json` で `UseStubGraphApi: true` が設定されており、開発環境では `StubGraphApiService` が使用されます。このスタブは:

- 空き時間算出時にダミーの候補日時（平日9:00-17:00）を返却
- Outlook予定・Teams会議の作成をログ出力のみで処理
- ユーザー検索にダミーユーザーを返却
- メール送信をスキップ

## テスト

```bash
dotnet test
```

### テスト内容

- **ScheduleServiceTests**: Poll作成、候補枠生成、回答登録、確定処理、期限切れ処理
- **BookingControllerTests**: GUID無効時404、公開中Pollの表示、期限切れ表示、回答送信
- **NotificationServiceTests**: メール送信内容の検証（URL、Teams会議リンク含有）

## ユーザーフロー

```
主催者                                    社外の方
  │                                         │
  ├─ 1. 日程調整を作成                       │
  ├─ 2. 候補日時を自動算出 or 手動追加        │
  ├─ 3. 調整ページを公開                      │
  ├─ 4. 調整URLを共有 ──────────────────────→│
  │                                         ├─ 5. URLにアクセス
  │                                         ├─ 6. 希望日時を選択
  │← 7. 回答を確認 ←────────────────────────┤
  ├─ 8. 日時を確定                           │
  │    → Outlook予定 自動作成                │
  │    → Teams会議 自動作成                  │
  ├─ 9. 確定通知メール ─────────────────────→│
  │                                         │
```

## デプロイ（Azure App Service）

### 構成図

```
┌─────────────────────────────────────────────────┐
│  Microsoft Azure                                │
│                                                 │
│  ┌──────────────────┐   ┌────────────────────┐  │
│  │ Azure App Service │──▶│ Azure SQL Database │  │
│  │ (Linux / .NET 8)  │   │ (S0〜S1)           │  │
│  │                   │   └────────────────────┘  │
│  │ ScheduleAdjust    │                           │
│  │ + DeadlineHosted  │   ┌────────────────────┐  │
│  │   Service         │──▶│ Microsoft Entra ID │  │
│  └──────────────────┘   │ (認証 + Graph API)  │  │
│          │               └────────────────────┘  │
│          ▼                                       │
│  社外: /Booking/{guid}    社内: /Schedule (SSO)   │
└─────────────────────────────────────────────────┘
```

### 1. Azureリソースの作成

Azure Portalで以下のリソースを作成します:

1. **リソースグループ** を作成
2. **Azure SQL Database** を作成（SKU: S0で開始）
   - `sql/001_CreateTables.sql` を実行してテーブル作成
3. **App Service Plan** を作成（B1 Linux）
4. **App Service** を作成（.NET 8, Linux）

### 2. App Serviceのアプリケーション設定

Azure Portal > App Service > 構成 > アプリケーション設定に以下を登録:

| 設定名 | 値 |
|--------|-----|
| `ConnectionStrings__DefaultConnection` | Azure SQL接続文字列 |
| `AzureAd__TenantId` | Entra IDテナントID |
| `AzureAd__ClientId` | アプリ登録のクライアントID |
| `AzureAd__ClientSecret` | アプリ登録のクライアントシークレット |
| `BaseUrl` | `https://<app-name>.azurewebsites.net` |
| `UseStubGraphApi` | `false` |

### 3. Entra IDリダイレクトURI

Azure Portal > Entra ID > アプリの登録 > 認証で、リダイレクトURIを追加:

```
https://<app-name>.azurewebsites.net/signin-oidc
```

### 4. GitHub Actions CI/CD

1. Azure Portal > App Service > デプロイ > 発行プロファイルをダウンロード
2. GitHub > Settings > Secrets > `AZURE_WEBAPP_PUBLISH_PROFILE` に発行プロファイルの内容を登録
3. `.github/workflows/deploy.yml` の `AZURE_WEBAPP_NAME` を実際のApp Service名に変更
4. `main` ブランチへpushすると自動でビルド・テスト・デプロイが実行されます

### 5. Dockerでのデプロイ（オプション）

App ServiceのコンテナデプロイやDocker Composeを利用する場合:

```bash
docker build -t schedule-adjust .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="<接続文字列>" \
  -e AzureAd__TenantId="<テナントID>" \
  -e AzureAd__ClientId="<クライアントID>" \
  -e AzureAd__ClientSecret="<シークレット>" \
  -e BaseUrl="https://your-domain.com" \
  schedule-adjust
```

### ヘルスチェック

App Service > 正常性チェックで以下のパスを設定:

```
/health
```

DB接続を含む正常性を確認できます。

## ライセンス

Private
