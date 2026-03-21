using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace RevitClaudePlugIn.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ClaudeCommand : IExternalCommand
    {
        private static readonly Guid PanelGuid =
            new Guid("8A0C52B3-4C67-4F9B-B8C2-7C7E2E8F3123");

        public Result Execute(ExternalCommandData commandData,
                              ref string message, ElementSet elements)
        {
            var dpId = new DockablePaneId(PanelGuid);
            var uiapp = commandData.Application;
            var dp = uiapp.GetDockablePane(dpId);
            dp.Show();

            return Result.Succeeded;
        }
    }
}
