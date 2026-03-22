using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitClaudePlugIn.Startup;

namespace RevitClaudePlugIn.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ReloadToolsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            var count = App.ActiveHandler.ReloadTools(commandData.Application);
            TaskDialog.Show("Reload Tools", $"Tools reloaded successfully.\n{count} tool(s) available.");
            return Result.Succeeded;
        }
    }
}
