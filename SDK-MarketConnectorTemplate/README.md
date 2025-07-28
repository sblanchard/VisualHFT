# MarketConnector Template

This directory contains a skeleton project that can be used as a starting
point for building new market‑data connectors for **VisualHFT**.  The
structure and file names mirror the existing crypto connectors (e.g.
`MarketConnectors.Binance`, `MarketConnectors.Kraken`) found in the
`VisualHFT.Plugins` solution folder.  To create your own connector:

1. **Rename the project**: Replace `MarketConnectorTemplate` and the
   namespace `MarketConnectorTemplate` with a name that reflects your
   exchange (e.g. `MarketConnectors.MyExchange`).  Update the
   `<AssemblyName>` and `<RootNamespace>` in the `.csproj` if desired.
2. **Reference an exchange client**: Add an appropriate
   `<PackageReference>` to the `.csproj` so you can talk to the target
   venue.  For example, Binance connectors use `Binance.Net` and Kraken
   connectors use `KrakenExchange.Net`.
3. **Implement connection logic**: Fill in the TODO sections of
   `TemplatePlugin.cs` to instantiate your client, subscribe to
   orderbook and trade streams, and convert the received messages into
   `VisualHFT.Commons.Model.OrderBook` and `VisualHFT.Commons.Model.Trade`
   objects.  Use `RaiseOnDataReceived()` to publish these objects back
   into VisualHFT.
4. **Expose settings**: Customize `TemplateSettings.cs` with any
   configuration values you need (API credentials, symbol lists,
   endpoints).  These settings will appear in the VisualHFT settings UI.
5. **Implement a settings page**: A simple WPF user control is provided
   in `UserControls/PluginSettingsView.xaml`.  The control is bound to
   the properties on `TemplateSettings` and includes basic text boxes for
   API keys, symbol lists, depth levels and aggregation level.  You can
   edit this XAML to add additional fields or instructions.  The
   corresponding code‑behind (`PluginSettingsView.xaml.cs`) handles
   hyperlink navigation.  When VisualHFT loads your plug‑in it will
   automatically display this control when the user edits the plug‑in
   settings.
6. **Build and deploy**: Compile the project.  The resulting DLL will be
   auto‑discovered by the VisualHFT plug‑in loader when placed in
   the `plugins` folder.  If you include additional XAML files or
   resources, ensure that the `.csproj` file lists them in an
   `<ItemGroup>` so they are compiled correctly.

For detailed guidance on how to extend VisualHFT, see the
`MarketConnectorSDK_Guidelines.md` document in the repository root.  It
explains the life‑cycle of a plug‑in, how settings are managed and how
data flows through the system.