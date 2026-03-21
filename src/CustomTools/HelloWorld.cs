using Autodesk.Revit.UI;
using System.Text.Json.Nodes;

namespace Tools
{
    public sealed class HelloWorld
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var result = new JsonObject
            {
                ["ok"] = true,
                ["Message"] = "Hello World!",
            };

            return result.ToJsonString();
        }
    }
}


