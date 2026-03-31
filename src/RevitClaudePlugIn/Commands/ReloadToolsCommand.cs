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
#if NET48
            TaskDialog.Show("Reload Tools — Not Supported",
                "Tool hot-reload requires Revit 2025 or above.\n\n" +
                "On Revit 2024 and below, .NET Framework does not support unloading assemblies. " +
                "Tool metadata (new tools, schema changes) will be refreshed, but any changes " +
                "to existing tool DLLs will not take effect until Revit is restarted.");
            return Result.Succeeded;
#endif
            var count = App.ActiveHandler.ReloadTools(commandData.Application);
            TaskDialog.Show("Reload Tools", $"Tools reloaded successfully.\n{count} tool(s) available.");
            return Result.Succeeded;
        }
    }
}
