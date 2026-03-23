using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitClaudePlugIn.Common;

namespace RevitClaudePlugIn.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ClaudeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            var dpId = new DockablePaneId(Constants.PanelGuid);
            var uiapp = commandData.Application;
            var dp = uiapp.GetDockablePane(dpId);
            if (dp.IsShown())
                dp.Hide();
            else
                dp.Show();

            return Result.Succeeded;
        }
    }
}
