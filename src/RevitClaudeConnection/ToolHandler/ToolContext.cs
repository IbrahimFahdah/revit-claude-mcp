using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace RevitClaudeConnector.ToolHandler
{
    public sealed class ToolContext
    {
        public UIApplication UIApp { get; }
        public UIDocument UIDoc => UIApp?.ActiveUIDocument;
        public Document Doc => UIDoc?.Document;

        public ToolContext(UIApplication uiApp) => UIApp = uiApp;
    }
}
