using Autodesk.Revit.UI;
using RevitClaudeConnector.ToolLoading;
using RevitStartup;

namespace RevitClaudeConnector
{
    public class RevitAppStartup : AppStartup
    {
        private ExternalEvent _extEvent;

        public RevitAppStartup() : base(new UiHandler())
        {
            _extEvent = ExternalEvent.Create((IExternalEventHandler)Handler);
        }

        protected override void RaiseEvent()
        {
            _extEvent.Raise();
        }

        protected override void TaskDialog(string title, string message)
        {
            Autodesk.Revit.UI.TaskDialog.Show(title, message);
        }

        protected override bool MessageBox(string title, string message)
        {
            var res = Autodesk.Revit.UI.TaskDialog.Show(title, message, TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            return res == TaskDialogResult.Yes;
        }
    }
}
