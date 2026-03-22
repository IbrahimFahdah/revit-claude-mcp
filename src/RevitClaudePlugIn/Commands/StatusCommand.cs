using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitClaudePlugIn.Startup;

namespace RevitClaudePlugIn.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class StatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            var startup = App.ActiveStartup;
            var handler = App.ActiveHandler;

            var bridgeState = startup.IsRunning ? "Running" : "Stopped";
            var toolCount = handler.ToolCount;

            TaskDialog.Show("Claude Connector Status",
                $"Bridge:  {bridgeState}\n" +
                $"URL:     {startup.BridgeUrl}\n" +
                $"Tools:   {toolCount} loaded");

            return Result.Succeeded;
        }
    }
}
