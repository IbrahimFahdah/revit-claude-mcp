using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;

namespace RevitClaudePlugIn.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class OpenGitHubCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/IbrahimFahdah/revit-claude-mcp",
                UseShellExecute = true
            });

            return Result.Succeeded;
        }
    }
}
