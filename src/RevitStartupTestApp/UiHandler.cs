using Newtonsoft.Json.Linq;
using RevitStartup.Base;

public class UiHandler : UiHandlerBase
{
    public void Execute()
    {
        try
        {
            if (_path == "/ping")
            {
                var o = new JObject
                {
                    ["ok"] = true,
                    ["hasActiveDoc"] = true
                };
                _tcs.TrySetResult(o.ToString());
                return;
            }


            _tcs.TrySetResult(@"{""error"":""Unknown path""}");
        }
        catch (Exception ex)
        {
            _tcs.TrySetResult(@"{""error"":""" + ex.Message.Replace("\"", "'") + @"""}");
        }
    }

    public string GetName() => "Claude Revit Bridge";
}
