using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitClaudePlugIn.Startup;

namespace RevitClaudePlugIn.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class RestartClaudeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            _ = App.ActivePanel.RestartClaude();
            return Result.Succeeded;
        }
    }
}
