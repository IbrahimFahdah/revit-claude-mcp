using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text.Json.Nodes;

namespace CustomTools
{
    public sealed class GetLinkedModels
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var doc = uidoc.Document;

            var links = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            var linkArray = new JsonArray();

            foreach (var link in links)
            {
                var linkType = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                var status = linkType?.GetLinkedFileStatus() ?? LinkedFileStatus.NotFound;
                var extRef = linkType?.GetExternalFileReference();
                var path = extRef != null
                    ? ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetAbsolutePath())
                    : "";

                linkArray.Add(new JsonObject
                {
                    ["id"] = link.Id.Value,
                    ["name"] = link.Name,
                    ["path"] = path,
                    ["status"] = status.ToString(),
                    ["isLoaded"] = status == LinkedFileStatus.Loaded
                });
            }

            return new JsonObject
            {
                ["ok"] = true,
                ["count"] = links.Count,
                ["links"] = linkArray
            }.ToJsonString();
        }
    }
}
