using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace CustomTools
{
    public sealed class HelloWorld
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var result = new JObject
            {
                ["ok"] = true,
                ["Message"] = "Hello World!",
            };

            return result.ToString();
        }
    }
}
