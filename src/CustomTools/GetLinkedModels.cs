using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

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

            var linkArray = new JArray();

            foreach (var link in links)
            {
                var linkType = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                var status = linkType?.GetLinkedFileStatus() ?? LinkedFileStatus.NotFound;
                var extRef = linkType?.GetExternalFileReference();
                var path = extRef != null
                    ? ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetAbsolutePath())
                    : "";

                linkArray.Add(new JObject
                {
                    ["id"] = link.Id.Value,
                    ["name"] = link.Name,
                    ["path"] = path,
                    ["status"] = status.ToString(),
                    ["isLoaded"] = status == LinkedFileStatus.Loaded
                });
            }

            return new JObject
            {
                ["ok"] = true,
                ["count"] = links.Count,
                ["links"] = linkArray
            }.ToString();
        }
    }
}
