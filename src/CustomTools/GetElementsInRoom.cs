using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace CustomTools
{
    public sealed class GetElementsInRoom
    {
        public string Execute(UIApplication uiapp, UIDocument uidoc, string requestJson)
        {
            var doc = uidoc.Document;

            long roomId;
            try
            {
                var input = JToken.Parse(requestJson);
                roomId = input["roomId"]?.Value<long>()
                    ?? throw new Exception("roomId is required");
            }
            catch (Exception ex)
            {
                return new JObject { ["ok"] = false, ["error"] = ex.Message }.ToString();
            }

            var room = doc.GetElement(new ElementId(roomId)) as Room;
            if (room == null)
                return new JObject { ["ok"] = false, ["error"] = $"Element {roomId} is not a Room" }.ToString();

            if (room.Area == 0)
                return new JObject { ["ok"] = false, ["error"] = "Room has no area (unplaced or unbounded)" }.ToString();

            var bb = room.get_BoundingBox(null);
            if (bb == null)
                return new JObject { ["ok"] = false, ["error"] = "Room has no bounding box" }.ToString();

            // Pre-filter by bounding box, then confirm with IsPointInRoom
            var bbFilter = new BoundingBoxIntersectsFilter(new Outline(bb.Min, bb.Max));

            var candidates = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(bbFilter)
                .ToList();

            var elements = new JArray();

            foreach (var elem in candidates)
            {
                if (elem is SpatialElement) continue; // skip rooms/spaces

                var point = elem.Location switch
                {
                    LocationPoint lp => lp.Point,
                    LocationCurve lc => lc.Curve.Evaluate(0.5, true),
                    _ => null
                };

                if (point != null && room.IsPointInRoom(point))
                {
                    elements.Add(new JObject
                    {
                        ["id"] = elem.Id.Value,
                        ["name"] = elem.Name,
                        ["category"] = elem.Category?.Name ?? "Unknown"
                    });
                }
            }

            return new JObject
            {
                ["ok"] = true,
                ["roomId"] = roomId,
                ["roomName"] = room.Name,
                ["roomNumber"] = room.Number,
                ["count"] = elements.Count,
                ["elements"] = elements
            }.ToString();
        }
    }
}
