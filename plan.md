## Plan: Qudi Visualizer（Runtime Console + 図表出力）

Qudiは既に登録メタデータ（`TypeRegistrationInfo`）を生成・集約しており、実行時に `QudiConfiguration.Registrations` として受け取れます（[src/Qudi.Core/QudiConfigurationBuilder.cs](src/Qudi.Core/QudiConfigurationBuilder.cs)、[src/Qudi.Core/Internal/QudiConfigurationExecutor.cs](src/Qudi.Core/Internal/QudiConfigurationExecutor.cs)、[src/Qudi.Core/TypeRegistrationInfo.cs](src/Qudi.Core/TypeRegistrationInfo.cs)）。これを使って「実行時に」「Console（Spectre.Console等）で見やすく」「必要ならDOT/Mermaid/DGML/JSONへ書き出し」する Visualizer を追加します。  
またREADME上の想定API（`conf.EnableVisualizationOutput("qudi-registrations.svg");`）は未実装なので、まずは“図の定義ファイルを生成”を確実にし、SVG直生成はオプション（`dot`存在時のみ等）として段階導入します（[README.md](README.md#L631)）。

**Steps**
1. 可視化の“中間モデル”を定義（サービス/実装/デコレータ/キー/条件/ライフタイム/依存辺）
   - 入力: `QudiConfiguration.Registrations`（[src/Qudi.Core/QudiConfigurationBuilder.cs](src/Qudi.Core/QudiConfigurationBuilder.cs#L194)）
   - 参照: `TypeRegistrationInfo.RequiredTypes` を依存辺（Impl → RequiredTypes）として扱う（[src/Qudi.Core/TypeRegistrationInfo.cs](src/Qudi.Core/TypeRegistrationInfo.cs)）
2. 診断ロジック（Console “トレース”優先）
   - “Missing registrations”: `RequiredTypes` のうち、登録（AsTypes/自己登録/Key/Condition適用後）に存在しない型を列挙
   - “解決トレース（最優先）”: 指定した Service 型（+Key/Condition）から、候補実装→依存→…の探索木を作成し、分岐（多重登録）や欠落点を表示
   - “循環検出”: 依存グラフでサイクル検出し、ループ経路を表示
   - “Lifetime 警告”: Singleton → Scoped など、よくある破綻パターンを静的に警告（確定ではなく“可能性”として）
3. 出力（美しく・手軽）
   - Console（開発時）: Spectre.Consoleで
     - ① サマリ（登録数/欠落数/循環数/多重登録数）
     - ② サービス別テーブル（Service, Impl, Lifetime, Key, When, Order, Decorator）
     - ③ 失敗時の解決トレース（ツリー表示）
   - ファイル出力（任意）:
     - JSON: 将来のTUI/GUI/拡張の土台（安定スキーマ）
     - Graphviz DOT: `.dot`（レンダリングはユーザーが `dot` でSVG/PNG化できる）
     - Mermaid: Markdown埋め込み用（GitHubで閲覧しやすい）
     - DGML: Visual Studioで開ける依存グラフ
     - draw.io: 将来対応
4. API設計（READMEの想定に寄せる）
   - `QudiConfigurationRootBuilder` 拡張として `EnableVisualizationOutput(...)` を提供（`AddQudiServices(conf => conf....)` に合う）
     - 引数で出力形式やファイル名を指定可能にする。
     - 実装は `QudiConfigurationRootBuilder.AddService(...)` で “Visualizer用 `QudiConfigurationBuilder`” を追加し、`Execute(...)` 内で診断/出力
     - Release時(Qudi.VisualizerはDebug時のみのインストールを推奨している)でもコードが壊れないように、もし`Qudi.Visualizer`関連のMetatagがSG(GetTypeByMetadataName)で利用できなければ、空の関数定義を吐き出す。
5. “SGでの追加生成”は最小に（必要ならハイブリッド）
   - 基本は runtime で完結（下の「SG/実行時」参照）
   - もし高速化/安定化が必要なら、SGが “登録マニフェスト（JSON相当の文字列）” を `AddSource` で埋め込み、runtimeはそれを読む（外部ファイル書き込みはしない）
6. ドキュメント更新
   - READMEの「(TODO) Visualize Registration」を、実際に提供する出力（Console + DOT/JSON等）に合わせて更新（[README.md](README.md#L631)）
   - 「SVG直出しは `dot` がある場合のみ」等の注意書き

**Verification**
- `dotnet test`（[README.md](README.md#L893)）
- 手動: examples の Worker などで `services.AddQudiServices(conf => conf.EnableVisualizationOutput(...))` を呼び、Console表示と生成ファイル（JSON/DOT/DGML/Mermaid）が期待どおりか確認
- 追加テスト（推奨）: 既存テスト資産に合わせ、欠落/循環/多重登録/デコレータチェーンのスナップショット相当を検証

**Decisions**
- 可視化アイデア（候補）
  - Console: サマリ/登録一覧/差分（条件別）/解決トレース/循環/多重登録/キー別/デコレータチェーン
  - 図表: DOT・Mermaid・DGML・JSON（将来TUI/GUI/IDE拡張の入力にする）
  - 将来: 専用TUI（JSONを読んでフィルタ/検索）やVS拡張（DGML/JSONを可視化）  
- 既存実装の方向性（“調査結果”の結論）
  - Microsoft.Extensions.DependencyInjection（ME.DI）は “登録可視化” を公式に強く提供しておらず、主に `ValidateOnBuild` / `ValidateScopes` と例外メッセージで原因追跡する思想（ただし図は出ない）
  - 一方、他DIコンテナ（例: SimpleInjector/StructureMap系/Lamar等）は診断・登録ダンプ・“What do I have?” 的なテキスト出力を持つことが多く、QudiのConsoleトレース方針と相性が良い  
  - ME.DI内部のCallSite等を反射で覗いて図化する手もあるが、内部実装依存で壊れやすいので、Qudiのメタデータ（`RequiredTypes` 等）から組み立てる方が堅い  
