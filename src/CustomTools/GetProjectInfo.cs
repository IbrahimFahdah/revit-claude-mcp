using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text.Json.Nodes;

namespace Tools
{
    public sealed class GetProjectInfo
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var doc = uidoc.Document;

            int elementCount = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElementIds().Count;

            var result = new JsonObject
            {
                ["ok"] = true,
                ["title"] = doc.Title,
                ["elementCount"] = elementCount
            };

            return result.ToJsonString();
        }
    }
}


