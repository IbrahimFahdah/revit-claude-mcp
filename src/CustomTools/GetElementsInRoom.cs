using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Text.Json.Nodes;

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
                var input = JsonNode.Parse(requestJson);
                roomId = input?["roomId"]?.GetValue<long>()
                    ?? throw new Exception("roomId is required");
            }
            catch (Exception ex)
            {
                return new JsonObject { ["ok"] = false, ["error"] = ex.Message }.ToJsonString();
            }

            var room = doc.GetElement(new ElementId(roomId)) as Room;
            if (room == null)
                return new JsonObject { ["ok"] = false, ["error"] = $"Element {roomId} is not a Room" }.ToJsonString();

            if (room.Area == 0)
                return new JsonObject { ["ok"] = false, ["error"] = "Room has no area (unplaced or unbounded)" }.ToJsonString();

            var bb = room.get_BoundingBox(null);
            if (bb == null)
                return new JsonObject { ["ok"] = false, ["error"] = "Room has no bounding box" }.ToJsonString();

            // Pre-filter by bounding box, then confirm with IsPointInRoom
            var bbFilter = new BoundingBoxIntersectsFilter(new Outline(bb.Min, bb.Max));

            var candidates = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(bbFilter)
                .ToList();

            var elements = new JsonArray();

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
                    elements.Add(new JsonObject
                    {
                        ["id"] = elem.Id.Value,
                        ["name"] = elem.Name,
                        ["category"] = elem.Category?.Name ?? "Unknown"
                    });
                }
            }

            return new JsonObject
            {
                ["ok"] = true,
                ["roomId"] = roomId,
                ["roomName"] = room.Name,
                ["roomNumber"] = room.Number,
                ["count"] = elements.Count,
                ["elements"] = elements
            }.ToJsonString();
        }
    }
}
