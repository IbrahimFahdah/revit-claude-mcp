using Autodesk.Revit.UI;
using RevitClaudePlugIn.Commands;
using RevitClaudePlugIn.ToolHandler;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevitClaudePlugIn.Startup
{
    public class App : IExternalApplication
    {
        AppStartup _appStartup;
        private static readonly Guid PanelGuid =
             new Guid("8A0C52B3-4C67-4F9B-B8C2-7C7E2E8F3123");
        private ClaudePanel _panel;

        /// <summary>The running UiHandler, used by ReloadToolsCommand.</summary>
        public static UiHandler ActiveHandler { get; private set; }

        public Result OnStartup(UIControlledApplication app)
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            ActiveHandler = new UiHandler();
            _appStartup = new AppStartup(ActiveHandler);
            _appStartup.Run(assemblyPath);

            var ribbon = app.CreateRibbonPanel("Revit Claude Connector");
            var asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            var btn = new PushButtonData("ClaudeBtn", "Claude", asmPath, "RevitClaudePlugIn.Commands.ClaudeCommand");
            btn.ToolTip = "Revit Claude Connector from IFADAH";
            btn.LongDescription = "This tool allows you to call many Revit functionalities from Claude.";
            var iconsFolder = Path.GetDirectoryName(assemblyPath);
            btn.LargeImage = new BitmapImage(new Uri(Path.Combine(iconsFolder, "ClaudeBtn32.png")));
            btn.Image = new BitmapImage(new Uri(Path.Combine(iconsFolder, "ClaudeBtn16.png")));
            ribbon.AddItem(btn);

            var toolListbtn = new PushButtonData("toolListbtn", "Tools", asmPath, "RevitClaudePlugIn.Commands.TooListCommand");
            toolListbtn.ToolTip = "Revit Claude Connector Tool List";
            toolListbtn.LongDescription = "Show all tools that are currently available with the connector.";
            toolListbtn.LargeImage = new BitmapImage(new Uri(Path.Combine(iconsFolder, "ToolsBtn32.png")));
            toolListbtn.Image = new BitmapImage(new Uri(Path.Combine(iconsFolder, "ToolsBtn16.png")));
            ribbon.AddItem(toolListbtn);

            var reloadBtn = new PushButtonData("reloadBtn", "Reload Tools", asmPath, "RevitClaudePlugIn.Commands.ReloadToolsCommand");
            reloadBtn.ToolTip = "Reload all tool packages from disk without restarting Revit.";
            reloadBtn.LongDescription = "Use this after adding, updating, or replacing a tool package DLL.";
            ribbon.AddItem(reloadBtn);

            // Register Dockable Panel
            _panel = new ClaudePanel();
            var provider = new ClaudePanelProvider(_panel);
            app.RegisterDockablePane(new DockablePaneId(PanelGuid), "Claude AI", provider);

            app.DockableFrameVisibilityChanged += (sender, args) =>
            {
                if (args.PaneId == new DockablePaneId(PanelGuid))
                {
                    if (args.DockableFrameShown)
                        _panel.AttachClaude();
                    else
                        _panel.ReleaseClaude();
                }
            };


            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            _appStartup.Stop();

            return Result.Succeeded;
        }
    }
}
