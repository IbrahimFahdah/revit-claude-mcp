using Autodesk.Revit.UI;

namespace RevitClaudePlugIn.Commands
{
    public class ClaudePanelProvider : IDockablePaneProvider
    {
        private ClaudePanel _host;

        public ClaudePanelProvider(ClaudePanel host)
        {
            _host = host;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            // The visual content of the panel
            data.FrameworkElement = _host;

            // Initial docking state
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };

            // Whether visible on Revit startup
            data.VisibleByDefault = false;
        }
    }
}
