using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace CustomTools
{
    public sealed class GetProjectInfo
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var doc = uidoc.Document;

            var elementCount = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElementIds().Count;

            var result = new JObject
            {
                ["ok"] = true,
                ["title"] = doc.Title,
                ["elementCount"] = elementCount
            };

            return result.ToString();
        }
    }
}
