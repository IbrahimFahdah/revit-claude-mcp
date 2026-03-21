using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitClaudeConnector.ToolLoading;


namespace RevitClaudeConnector.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TooListCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            var toolRegistry = ToolRegistry.LoadForCurrentRevit(commandData.Application);
            new ToolList(toolRegistry.Tools).ShowDialog();

            return Result.Succeeded;
        }
    }
}
