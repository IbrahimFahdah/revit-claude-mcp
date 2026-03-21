using RevitStartup;
using RevitStartup.Base;

public class AppStartupTest: AppStartup
{
    public AppStartupTest(IUIHandler UiHandler):base(UiHandler)
    {
        
    }

    protected override void RaiseEvent()
    {
        ((UiHandler)Handler).Execute();
    }
}
