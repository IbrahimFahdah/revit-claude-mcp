using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Tools
{
    // =====================================================
    // 1. GetCategoryByKeyword
    // =====================================================
    public sealed class GetCategoryByKeyword
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;

            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var keyword = args.Value<string>("keyword")?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(keyword))
                return new JObject { ["error"] = "Missing 'keyword' parameter." }.ToString();

            // Search all categories whose name contains the keyword
            var matches = doc.Settings.Categories
                .Cast<Category>()
                .Where(c => c.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(c => new JObject
                {
                    ["id"] = c.Id.Value,
                    ["name"] = c.Name,
                    ["type"] = c.CategoryType.ToString()
                })
                .ToList();

            var result = new JObject
            {
                ["keyword"] = keyword,
                ["match_count"] = matches.Count,
                ["categories"] = JArray.FromObject(matches)
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 2. SetCopyViewFilters
    // =====================================================
    public sealed class SetCopyViewFilters
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var sourceViewId = args.Value<int>("source_view_id");
            var targetViewIds = args["target_view_ids"]?.ToObject<List<int>>() ?? new List<int>();

            var sourceView = doc.GetElement(new ElementId(sourceViewId)) as View;
            if (sourceView == null)
                return new JObject { ["error"] = $"Source view {sourceViewId} not found." }.ToString();

            var targetViews = targetViewIds
                .Select(id => doc.GetElement(new ElementId(id)) as View)
                .Where(v => v != null)
                .ToList();

            using (var tx = new Transaction(doc, "Copy View Filters"))
            {
                tx.Start();

                var filterIds = sourceView.GetFilters();
                foreach (var targetView in targetViews)
                {
                    foreach (var filterId in filterIds)
                    {
                        if (!targetView.GetFilters().Contains(filterId))
                            targetView.AddFilter(filterId);

                        var ogs = sourceView.GetFilterOverrides(filterId);
                        targetView.SetFilterOverrides(filterId, ogs);
                    }
                }

                tx.Commit();
            }

            var result = new JObject
            {
                ["source_view_id"] = sourceViewId,
                ["target_view_count"] = targetViews.Count,
                ["filters_copied"] = sourceView.GetFilters().Count
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 3. GetSchedulesInfoAndColumns
    // =====================================================
    public sealed class GetSchedulesInfoAndColumns
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var scheduleIds = args["schedule_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (scheduleIds.Count == 0)
                return new JObject { ["error"] = "No schedule IDs provided." }.ToString();

            var schedules = scheduleIds
                .Select(id => doc.GetElement(new ElementId(id)) as ViewSchedule)
                .Where(vs => vs != null)
                .ToList();

            var scheduleArray = new JArray();

            foreach (var sched in schedules)
            {
                var def = sched.Definition;
                var fields = def.GetFieldOrder()
                    .Select(fid => def.GetField(fid))
                    .Select(field => new JObject
                    {
                        ["column_index"] = field.ColumnHeading,
                        ["parameter_id"] = field.GetName(),
                        ["heading"] = field.ColumnHeading
                    });

                scheduleArray.Add(new JObject
                {
                    ["id"] = sched.Id.Value,
                    ["name"] = sched.Name,
                    ["columns"] = JArray.FromObject(fields)
                });
            }

            var result = new JObject
            {
                ["schedule_count"] = schedules.Count,
                ["schedules"] = scheduleArray
            };

            return result.ToString();
        }
    }


    // =====================================================
    // 4. GetAllAdditionalPropertiesFromElementId
    // =====================================================
    public sealed class GetAllAdditionalPropertiesFromElementId
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var elementIdValue = args.Value<int>("element_id");
            if (elementIdValue == 0)
                return new JObject { ["error"] = "Missing or invalid element_id." }.ToString();

            var element = doc.GetElement(new ElementId(elementIdValue));
            if (element == null)
                return new JObject { ["error"] = $"Element with id {elementIdValue} not found." }.ToString();

            var props = new JArray();

            // General element info
            props.Add(new JObject { ["name"] = "Id", ["value"] = element.Id.Value });
            props.Add(new JObject { ["name"] = "Category", ["value"] = element.Category?.Name ?? "None" });
            props.Add(new JObject { ["name"] = "Class", ["value"] = element.GetType().FullName });

            // Try reflection-based exploration of public readable properties
            var type = element.GetType();
            var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            foreach (var prop in type.GetProperties(bindingFlags))
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                try
                {
                    var val = prop.GetValue(element);
                    string strVal = val == null ? "null" : val.ToString();
                    props.Add(new JObject { ["name"] = prop.Name, ["value"] = strVal });
                }
                catch
                {
                    // skip properties that throw
                }
            }

            var result = new JObject
            {
                ["element_id"] = element.Id.Value,
                ["property_count"] = props.Count,
                ["properties"] = props
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 5. GetGraphicOverridesForElementIdsInView
    // =====================================================
    public sealed class GetGraphicOverridesForElementIdsInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var viewId = args.Value<int>("view_id");
            var elementIds = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (viewId == 0 || elementIds.Count == 0)
                return new JObject { ["error"] = "Missing view_id or element_ids." }.ToString();

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            var graphicsInfo = new JArray();

            foreach (var eid in elementIds)
            {
                var el = doc.GetElement(new ElementId(eid));
                if (el == null) continue;

                try
                {
                    var ogs = view.GetElementOverrides(el.Id);
                    var color = ogs.ProjectionLineColor;
                    var patternId = ogs.ProjectionLinePatternId;
                    var lineWeight = ogs.ProjectionLineWeight;

                    graphicsInfo.Add(new JObject
                    {
                        ["element_id"] = el.Id.Value,
                        ["category"] = el.Category?.Name ?? "None",
                        ["line_color"] = color != null
                            ? new JObject { ["R"] = color.Red, ["G"] = color.Green, ["B"] = color.Blue }
                            : null,
                        ["line_pattern_id"] = patternId.Value,
                        ["line_weight"] = lineWeight
                    });
                }
                catch (Exception ex)
                {
                    graphicsInfo.Add(new JObject
                    {
                        ["element_id"] = eid,
                        ["error"] = ex.Message
                    });
                }
            }

            var result = new JObject
            {
                ["view_id"] = view.Id.Value,
                ["count"] = graphicsInfo.Count,
                ["graphics"] = graphicsInfo
            };

            return result.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. GetAllElementsOfSpecificFamilies
    // =====================================================
    public sealed class GetAllElementsOfSpecificFamilies
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var familyNames = args["family_names"]?.ToObject<List<string>>() ?? new List<string>();
            if (familyNames.Count == 0)
                return new JObject { ["error"] = "No family names provided." }.ToString();

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => familyNames.Any(fn =>
                    fi.Symbol?.Family?.Name?.Equals(fn, StringComparison.OrdinalIgnoreCase) == true))
                .Select(fi => fi.Id.Value)
                .ToList();

            var result = new JObject
            {
                ["family_names"] = JArray.FromObject(familyNames),
                ["element_count"] = collector.Count,
                ["element_ids"] = JArray.FromObject(collector)
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 2. GetMaterialLayersFromTypes
    // =====================================================
    public sealed class GetMaterialLayersFromTypes
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var typeIds = args["type_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (typeIds.Count == 0)
                return new JObject { ["error"] = "No type ids provided." }.ToString();

            var infoArray = new JArray();

            foreach (var id in typeIds)
            {
                var type = doc.GetElement(new ElementId(id)) as HostObjAttributes;
                if (type == null) continue;

                var compound = type.GetCompoundStructure();
                if (compound == null) continue;

                var layers = new JArray();
                foreach (var layer in compound.GetLayers())
                {
                    var mat = doc.GetElement(layer.MaterialId) as Material;
                    layers.Add(new JObject
                    {
                        ["function"] = layer.Function.ToString(),
                        ["material_id"] = layer.MaterialId.Value,
                        ["material_name"] = mat?.Name ?? "None",
                        ["width_ft"] = layer.Width
                    });
                }

                infoArray.Add(new JObject
                {
                    ["type_id"] = id,
                    ["type_name"] = type.Name,
                    ["layer_count"] = layers.Count,
                    ["layers"] = layers
                });
            }

            return new JObject
            {
                ["count"] = infoArray.Count,
                ["types"] = infoArray
            }.ToString();
        }
    }

    // =====================================================
    // 3. SetUserSelectionInRevit
    // =====================================================
    public sealed class SetUserSelectionInRevit
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var uiSel = uidoc.Selection;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var elIds = ids.Select(i => new ElementId(i)).ToList();
            uiSel.SetElementIds(elIds);

            return new JObject
            {
                ["count"] = elIds.Count,
                ["selected_ids"] = JArray.FromObject(ids)
            }.ToString();
        }
    }

    // =====================================================
    // 4. SetGraphicOverridesForElementsInView
    // =====================================================
    public sealed class SetGraphicOverridesForElementsInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var viewId = args.Value<int>("view_id");
            var elementIds = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            var colorArr = args["color_rgb"]?.ToObject<List<int>>();
            if (viewId == 0 || elementIds.Count == 0)
                return new JObject { ["error"] = "Missing view_id or element_ids." }.ToString();

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            var color = (colorArr != null && colorArr.Count == 3)
                ? new Color((byte)colorArr[0], (byte)colorArr[1], (byte)colorArr[2])
                : new Color(255, 0, 0);

            using (var tx = new Transaction(doc, "Set Graphic Overrides"))
            {
                tx.Start();

                var ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(color);
                ogs.SetSurfaceForegroundPatternColor(color);

                foreach (var eid in elementIds)
                {
                    var el = doc.GetElement(new ElementId(eid));
                    if (el == null) continue;
                    view.SetElementOverrides(el.Id, ogs);
                }

                tx.Commit();
            }

            var result = new JObject
            {
                ["view_id"] = view.Id.Value,
                ["element_count"] = elementIds.Count,
                ["color"] = new JObject
                {
                    ["R"] = color.Red,
                    ["G"] = color.Green,
                    ["B"] = color.Blue
                }
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 5. GetViewportsAndSchedulesOnSheets
    // =====================================================
    public sealed class GetViewportsAndSchedulesOnSheets
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var sheetIds = args["sheet_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (sheetIds.Count == 0)
                return new JObject { ["error"] = "No sheet ids provided." }.ToString();

            var results = new JArray();

            foreach (var id in sheetIds)
            {
                var sheet = doc.GetElement(new ElementId(id)) as ViewSheet;
                if (sheet == null) continue;

                var viewports = new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Where(vp => vp.SheetId == sheet.Id)
                    .ToList();

                var schedInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .Where(s => s.OwnerViewId == sheet.Id)
                    .ToList();

                var vpArr = new JArray(viewports.Select(vp => new JObject
                {
                    ["viewport_id"] = vp.Id.Value,
                    ["view_id"] = vp.ViewId.Value,
                    ["view_name"] = doc.GetElement(vp.ViewId)?.Name ?? "Unknown"
                }));

                var schedArr = new JArray(schedInstances.Select(s => new JObject
                {
                    ["schedule_id"] = s.ScheduleId.Value,
                    ["schedule_name"] = doc.GetElement(s.ScheduleId)?.Name ?? "Unknown"
                }));

                results.Add(new JObject
                {
                    ["sheet_id"] = sheet.Id.Value,
                    ["sheet_name"] = sheet.Name,
                    ["viewports"] = vpArr,
                    ["schedules"] = schedArr
                });
            }

            var result = new JObject
            {
                ["sheet_count"] = results.Count,
                ["sheets"] = results
            };

            return result.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. GetAdditionalPropertyForAllElementIds
    // =====================================================
    public sealed class GetAdditionalPropertyForAllElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var propertyName = args.Value<string>("property_name");
            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (string.IsNullOrEmpty(propertyName) || ids.Count == 0)
                return new JObject { ["error"] = "Missing property_name or element_ids." }.ToString();

            var results = new JArray();
            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var type = el.GetType();
                var prop = type.GetProperty(propertyName);
                if (prop == null) continue;

                try
                {
                    var value = prop.GetValue(el);
                    results.Add(new JObject
                    {
                        ["element_id"] = id,
                        ["property_name"] = propertyName,
                        ["value"] = value?.ToString() ?? "null"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new JObject
                    {
                        ["element_id"] = id,
                        ["property_name"] = propertyName,
                        ["error"] = ex.Message
                    });
                }
            }

            return new JObject
            {
                ["property_name"] = propertyName,
                ["count"] = results.Count,
                ["results"] = results
            }.ToString();
        }
    }

    // =====================================================
    // 2. GetElementsByCategory
    // =====================================================
    public sealed class GetElementsByCategory
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var catId = args.Value<int>("category_id");
            if (catId == 0)
                return new JObject { ["error"] = "Missing category_id." }.ToString();

            var bic = (BuiltInCategory)catId;
            var elements = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .Select(e => e.Id.Value)
                .ToList();

            return new JObject
            {
                ["category_id"] = catId,
                ["element_count"] = elements.Count,
                ["element_ids"] = JArray.FromObject(elements)
            }.ToString();
        }
    }

    // =====================================================
    // 3. GetCategoriesFromElementIds
    // =====================================================
    public sealed class GetCategoriesFromElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var catList = ids
                .Select(i => doc.GetElement(new ElementId(i)))
                .Where(e => e != null)
                .GroupBy(e => e.Category?.Name ?? "None")
                .Select(g => new JObject
                {
                    ["category_name"] = g.Key,
                    ["element_count"] = g.Count(),
                    ["example_id"] = g.First().Id.Value
                });

            return new JObject
            {
                ["unique_category_count"] = catList.Count(),
                ["categories"] = JArray.FromObject(catList)
            }.ToString();
        }
    }

    // =====================================================
    // 4. SetIsolatedElementsInView
    // =====================================================
    public sealed class SetIsolatedElementsInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var viewId = args.Value<int>("view_id");
            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (viewId == 0 || ids.Count == 0)
                return new JObject { ["error"] = "Missing view_id or element_ids." }.ToString();

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            using (var tx = new Transaction(doc, "Isolate Elements in View"))
            {
                tx.Start();
                view.IsolateElementsTemporary(ids.Select(i => new ElementId(i)).ToList());
                tx.Commit();
            }

            return new JObject
            {
                ["view_id"] = view.Id.Value,
                ["isolated_count"] = ids.Count
            }.ToString();
        }
    }

    // =====================================================
    // 5. GetAllUsedTypesOfAFamily
    // =====================================================
    public sealed class GetAllUsedTypesOfAFamily
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var familyName = args.Value<string>("family_name");
            if (string.IsNullOrEmpty(familyName))
                return new JObject { ["error"] = "Missing family_name." }.ToString();

            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                .Select(fs => new JObject
                {
                    ["type_id"] = fs.Id.Value,
                    ["type_name"] = fs.Name
                })
                .ToList();

            return new JObject
            {
                ["family_name"] = familyName,
                ["type_count"] = types.Count,
                ["types"] = JArray.FromObject(types)
            }.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. GetAllUsedFamiliesInModel
    // =====================================================
    public sealed class GetAllUsedFamiliesInModel
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;

            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => !f.IsInPlace)
                .Select(f => new JObject
                {
                    ["id"] = f.Id.Value,
                    ["name"] = f.Name
                })
                .OrderBy(o => o["name"].ToString())
                .ToList();

            return new JObject
            {
                ["family_count"] = families.Count,
                ["families"] = JArray.FromObject(families)
            }.ToString();
        }
    }

    // =====================================================
    // 2. GetBoundaryLines
    // =====================================================
    public sealed class GetBoundaryLines
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var all = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var geo = el.get_Geometry(new Options());
                if (geo == null) continue;

                var lines = new JArray();

                foreach (var obj in geo)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Edge e in solid.Edges)
                        {
                            var pts = e.Tessellate()
                                .Select(p => new JObject { ["X"] = p.X, ["Y"] = p.Y, ["Z"] = p.Z });
                            lines.Add(JArray.FromObject(pts));
                        }
                    }
                    else if (obj is Curve curve)
                    {
                        var tess = curve.Tessellate();
                        lines.Add(JArray.FromObject(tess.Select(p => new JObject { ["X"] = p.X, ["Y"] = p.Y, ["Z"] = p.Z })));
                    }
                }

                all.Add(new JObject
                {
                    ["element_id"] = id,
                    ["line_count"] = lines.Count,
                    ["lines"] = lines
                });
            }

            return new JObject
            {
                ["count"] = all.Count,
                ["elements"] = all
            }.ToString();
        }
    }

    // =====================================================
    // 3. SetMovementForElements
    // =====================================================
    public sealed class SetMovementForElements
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            var dx = args.Value<double?>("dx") ?? 0;
            var dy = args.Value<double?>("dy") ?? 0;
            var dz = args.Value<double?>("dz") ?? 0;

            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var vector = new XYZ(dx, dy, dz);

            using (var tx = new Transaction(doc, "Move Elements"))
            {
                tx.Start();

                foreach (var id in ids)
                {
                    var el = doc.GetElement(new ElementId(id));
                    if (el == null) continue;

                    try
                    {
                        ElementTransformUtils.MoveElement(doc, el.Id, vector);
                    }
                    catch (Exception ex)
                    {
                        // skip if invalid move
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["moved_count"] = ids.Count,
                ["translation_ft"] = new JObject { ["dx"] = dx, ["dy"] = dy, ["dz"] = dz }
            }.ToString();
        }
    }

    // =====================================================
    // 4. GetParameterValueForElementIds
    // =====================================================
    public sealed class GetParameterValueForElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var parameterName = args.Value<string>("parameter_name");
            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (string.IsNullOrEmpty(parameterName) || ids.Count == 0)
                return new JObject { ["error"] = "Missing parameter_name or element_ids." }.ToString();

            var results = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var param = el.LookupParameter(parameterName);
                if (param == null)
                {
                    results.Add(new JObject
                    {
                        ["element_id"] = id,
                        ["parameter_name"] = parameterName,
                        ["value"] = "Not Found"
                    });
                    continue;
                }

                string val = param.AsValueString() ?? param.AsString() ?? param.AsDouble().ToString();
                results.Add(new JObject
                {
                    ["element_id"] = id,
                    ["parameter_name"] = parameterName,
                    ["value"] = val
                });
            }

            return new JObject
            {
                ["parameter_name"] = parameterName,
                ["count"] = results.Count,
                ["results"] = results
            }.ToString();
        }
    }

    // =====================================================
    // 5. GetWorksetsFromElementIds
    // =====================================================
    public sealed class GetWorksetsFromElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            if (!doc.IsWorkshared)
                return new JObject { ["error"] = "Document is not workshared." }.ToString();

            var infoArray = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var worksetId = el.WorksetId;
                var ws = doc.GetWorksetTable().GetWorkset(worksetId);

                infoArray.Add(new JObject
                {
                    ["element_id"] = id,
                    ["workset_id"] = worksetId.IntegerValue,
                    ["workset_name"] = ws?.Name ?? "Unknown",
                    ["is_editable"] = ws?.IsOpen ?? false
                });
            }

            return new JObject
            {
                ["count"] = infoArray.Count,
                ["worksets"] = infoArray
            }.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. SetDeleteElements
    // =====================================================
    public sealed class SetDeleteElements
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var deleted = new List<long>();

            using (var tx = new Transaction(doc, "Delete Elements"))
            {
                tx.Start();

                foreach (var id in ids)
                {
                    try
                    {
                        var resultIds = doc.Delete(new ElementId(id));
                        deleted.AddRange(resultIds.Select(eid => eid.Value));
                    }
                    catch
                    {
                        // skip invalid ids
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["requested_count"] = ids.Count,
                ["deleted_count"] = deleted.Count,
                ["deleted_ids"] = JArray.FromObject(deleted.Distinct())
            }.ToString();
        }
    }

    // =====================================================
    // 2. GetDocumentSwitched
    // =====================================================
    public sealed class GetDocumentSwitched
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            //var app = uiapp.Application;
            //var args = string.IsNullOrWhiteSpace(argsJson)
            //    ? new JObject()
            //    : JObject.Parse(argsJson);

            //bool switchMainDoc = args.Value<bool?>("switch_main_doc") ?? false;

            //// Keep a static reference for demonstration
            //// (actual persistent switching would require external command data)
            //var docs = app.Documents;
            //Document newDoc = null;

            //if (switchMainDoc)
            //{
            //    newDoc = docs.Cast<Document>().FirstOrDefault(d => !d.IsLinked);
            //}
            //else
            //{
            //    newDoc = docs.Cast<Document>().FirstOrDefault(d => d.IsLinked);
            //}

            //if (newDoc == null)
            //    return new JObject { ["error"] = "No linked or main document found to switch." }.ToString();

            //uiapp.ActiveUIDocument = uiapp.OpenAndActivateDocument(newDoc.PathName);

            //return new JObject
            //{
            //    ["active_doc_title"] = newDoc.Title,
            //    ["active_doc_is_linked"] = newDoc.IsLinked,
            //    ["switch_main_doc"] = switchMainDoc
            //}.ToString();

            return new JObject { ["error"] = "Not implemented." }.ToString();
        }
    }

    // =====================================================
    // 3. GetAllWorksetInformation
    // =====================================================
    public sealed class GetAllWorksetInformation
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;

            if (!doc.IsWorkshared)
                return new JObject { ["error"] = "Document is not workshared." }.ToString();

            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Select(ws => new JObject
                {
                    ["id"] = ws.Id.IntegerValue,
                    ["name"] = ws.Name,
                    ["owner"] = ws.Owner ?? "None",
                    ["is_editable"] = ws.IsOpen
                })
                .ToList();

            return new JObject
            {
                ["count"] = worksets.Count,
                ["worksets"] = JArray.FromObject(worksets)
            }.ToString();
        }
    }

    // =====================================================
    // 4. GetIfElementsPassFilter
    // =====================================================
    public sealed class GetIfElementsPassFilter
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            var filterName = args.Value<string>("filter_name");

            if (ids.Count == 0 || string.IsNullOrEmpty(filterName))
                return new JObject { ["error"] = "Missing element_ids or filter_name." }.ToString();

            // Simulate filter lookup by name
            var filterElem = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

            if (filterElem == null)
                return new JObject { ["error"] = $"Filter '{filterName}' not found." }.ToString();

            var filter = filterElem.GetElementFilter();
            var resultArr = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                bool passes = filter.PassesFilter(el);
                resultArr.Add(new JObject
                {
                    ["element_id"] = id,
                    ["passes_filter"] = passes
                });
            }

            return new JObject
            {
                ["filter_name"] = filterName,
                ["results"] = resultArr
            }.ToString();
        }
    }

    // =====================================================
    // 5. GetLocationForElementIds
    // =====================================================
    public sealed class GetLocationForElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var results = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var loc = el.Location;
                if (loc is LocationPoint lp)
                {
                    results.Add(new JObject
                    {
                        ["element_id"] = id,
                        ["location_type"] = "Point",
                        ["point"] = new JObject
                        {
                            ["X"] = lp.Point.X,
                            ["Y"] = lp.Point.Y,
                            ["Z"] = lp.Point.Z
                        }
                    });
                }
                else if (loc is LocationCurve lc)
                {
                    var curve = lc.Curve;
                    var sp = curve.GetEndPoint(0);
                    var ep = curve.GetEndPoint(1);

                    results.Add(new JObject
                    {
                        ["element_id"] = id,
                        ["location_type"] = "Curve",
                        ["start"] = new JObject { ["X"] = sp.X, ["Y"] = sp.Y, ["Z"] = sp.Z },
                        ["end"] = new JObject { ["X"] = ep.X, ["Y"] = ep.Y, ["Z"] = ep.Z }
                    });
                }
            }

            return new JObject
            {
                ["count"] = results.Count,
                ["locations"] = results
            }.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. GetAllElementsShownInView
    // =====================================================
    public sealed class GetAllElementsShownInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            int viewId = args.Value<int>("view_id");
            if (viewId == 0)
                return new JObject { ["error"] = "Missing view_id." }.ToString();

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            var collector = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .Select(e => e.Id.Value)
                .ToList();

            return new JObject
            {
                ["view_id"] = view.Id.Value,
                ["element_count"] = collector.Count,
                ["element_ids"] = JArray.FromObject(collector)
            }.ToString();
        }
    }

    // =====================================================
    // 2. GetSizeInMBOfFamilies
    // =====================================================
    public sealed class GetSizeInMBOfFamilies
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var familyIds = args["family_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (familyIds.Count == 0)
                return new JObject { ["error"] = "No family_ids provided." }.ToString();

            var results = new JArray();

            foreach (var id in familyIds)
            {
                var fam = doc.GetElement(new ElementId(id)) as Family;
                if (fam == null) continue;

                double sizeMB = 0;
                try
                {
                    var path = fam.Document.PathName;
                    if (System.IO.File.Exists(path))
                    {
                        var fi = new System.IO.FileInfo(path);
                        sizeMB = fi.Length / (1024.0 * 1024.0);
                    }
                }
                catch { }

                results.Add(new JObject
                {
                    ["family_id"] = id,
                    ["family_name"] = fam.Name,
                    ["size_mb"] = Math.Round(sizeMB, 3)
                });
            }

            // Include model file size
            double modelSize = 0;
            try
            {
                var path = doc.PathName;
                if (System.IO.File.Exists(path))
                {
                    var fi = new System.IO.FileInfo(path);
                    modelSize = fi.Length / (1024.0 * 1024.0);
                }
            }
            catch { }

            return new JObject
            {
                ["family_count"] = results.Count,
                ["families"] = results,
                ["model_file_size_mb"] = Math.Round(modelSize, 3)
            }.ToString();
        }
    }

    // =====================================================
    // 3. GetObjectClassesFromElementIds
    // =====================================================
    public sealed class GetObjectClassesFromElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var results = ids
                .Select(i =>
                {
                    var el = doc.GetElement(new ElementId(i));
                    return new JObject
                    {
                        ["element_id"] = i,
                        ["class_name"] = el?.GetType().FullName ?? "Not found"
                    };
                });

            return new JObject
            {
                ["count"] = ids.Count,
                ["elements"] = JArray.FromObject(results)
            }.ToString();
        }
    }

    // =====================================================
    // 4. GetParametersFromElementId
    // =====================================================
    public sealed class GetParametersFromElementId
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            int elementId = args.Value<int>("element_id");
            if (elementId == 0)
                return new JObject { ["error"] = "Missing element_id." }.ToString();

            var el = doc.GetElement(new ElementId(elementId));
            if (el == null)
                return new JObject { ["error"] = $"Element {elementId} not found." }.ToString();

            var parameters = new JArray();
            var seenParamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // --- Instance parameters ---
            foreach (Parameter p in el.Parameters)
            {
                string name = p.Definition?.Name ?? "Unnamed";
                string val = GetParameterValue(p);

                parameters.Add(new JObject
                {
                    ["parameter_id"] = p.Id.Value,
                    ["name"] = name,
                    ["value"] = val,
                    ["source"] = "instance"
                });

                seenParamNames.Add(name);
            }

            // --- Type parameters (if applicable) ---
            if (el is Element elem)
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element typeEl = doc.GetElement(typeId);
                    if (typeEl != null)
                    {
                        foreach (Parameter p in typeEl.Parameters)
                        {
                            string name = p.Definition?.Name ?? "Unnamed";
                            if (seenParamNames.Contains(name))
                                continue; // avoid duplicates

                            string val = GetParameterValue(p);

                            parameters.Add(new JObject
                            {
                                ["parameter_id"] = p.Id.Value,
                                ["name"] = name,
                                ["value"] = val,
                                ["source"] = "type"
                            });
                        }
                    }
                }
            }

            return new JObject
            {
                ["element_id"] = elementId,
                ["parameter_count"] = parameters.Count,
                ["parameters"] = parameters
            }.ToString();
        }

        private static string GetParameterValue(Parameter p)
        {
            return p.AsValueString()
                ?? p.AsString()
                ?? (p.StorageType == StorageType.Double ? p.AsDouble().ToString() : null)
                ?? "(no value)";
        }
    }

    // =====================================================
    // 5. GetBoundingBoxesForElementIds
    // =====================================================
    public sealed class GetBoundingBoxesForElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var results = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var bbox = el.get_BoundingBox(null);
                if (bbox == null) continue;

                results.Add(new JObject
                {
                    ["element_id"] = id,
                    ["min"] = new JObject { ["X"] = bbox.Min.X, ["Y"] = bbox.Min.Y, ["Z"] = bbox.Min.Z },
                    ["max"] = new JObject { ["X"] = bbox.Max.X, ["Y"] = bbox.Max.Y, ["Z"] = bbox.Max.Z }
                });
            }

            return new JObject
            {
                ["count"] = results.Count,
                ["bounding_boxes"] = results
            }.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. SetParameterValueForElements
    // =====================================================
    public sealed class SetParameterValueForElements
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            string parameterName = args.Value<string>("parameter_name");
            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            var value = args["value"]?.ToString();

            if (string.IsNullOrEmpty(parameterName) || ids.Count == 0 || value == null)
                return new JObject { ["error"] = "Missing parameter_name, element_ids, or value." }.ToString();

            int updated = 0;

            using (var tx = new Transaction(doc, "Set Parameter Values"))
            {
                tx.Start();

                foreach (var id in ids)
                {
                    var el = doc.GetElement(new ElementId(id));
                    if (el == null) continue;

                    var p = el.LookupParameter(parameterName);
                    if (p == null) continue;

                    try
                    {
                        if (p.StorageType == StorageType.String)
                            p.Set(value);
                        else if (p.StorageType == StorageType.Double && double.TryParse(value, out double d))
                            p.Set(d);
                        else if (p.StorageType == StorageType.Integer && int.TryParse(value, out int i))
                            p.Set(i);
                        else if (p.StorageType == StorageType.ElementId && int.TryParse(value, out int eid))
                            p.Set(new ElementId(eid));

                        updated++;
                    }
                    catch { }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["parameter_name"] = parameterName,
                ["updated_count"] = updated,
                ["element_count"] = ids.Count
            }.ToString();
        }
    }

    // =====================================================
    // 2. GetGraphicOverridesViewFilters
    // =====================================================
    public sealed class GetGraphicOverridesViewFilters
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            int viewId = args.Value<int>("view_id");
            var filterIds = args["filter_ids"]?.ToObject<List<int>>() ?? new List<int>();

            if (viewId == 0 || filterIds.Count == 0)
                return new JObject { ["error"] = "Missing view_id or filter_ids." }.ToString();

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            var results = new JArray();

            foreach (var fid in filterIds)
            {
                try
                {
                    var ogs = view.GetFilterOverrides(new ElementId(fid));
                    var color = ogs.ProjectionLineColor;
                    var pattern = ogs.ProjectionLinePatternId;
                    var lw = ogs.ProjectionLineWeight;

                    results.Add(new JObject
                    {
                        ["filter_id"] = fid,
                        ["line_color"] = color != null ? new JObject
                        {
                            ["R"] = color.Red,
                            ["G"] = color.Green,
                            ["B"] = color.Blue
                        } : null,
                        ["line_pattern_id"] = pattern.Value,
                        ["line_weight"] = lw
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new JObject
                    {
                        ["filter_id"] = fid,
                        ["error"] = ex.Message
                    });
                }
            }

            return new JObject
            {
                ["view_id"] = viewId,
                ["count"] = results.Count,
                ["filters"] = results
            }.ToString();
        }
    }

    // =====================================================
    // 3. GetElementTypesForElementIds
    // =====================================================
    public sealed class GetElementTypesForElementIds
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var results = new JArray();

            foreach (var id in ids)
            {
                var el = doc.GetElement(new ElementId(id));
                if (el == null) continue;

                var type = doc.GetElement(el.GetTypeId());
                results.Add(new JObject
                {
                    ["element_id"] = id,
                    ["type_id"] = el.GetTypeId().Value,
                    ["type_name"] = type?.Name ?? "Unknown"
                });
            }

            return new JObject
            {
                ["count"] = results.Count,
                ["elements"] = results
            }.ToString();
        }
    }

    // =====================================================
    // 4. GetAllWarningsInModel
    // =====================================================
    public sealed class GetAllWarningsInModel
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;

            var warnings = doc.GetWarnings();
            var list = new JArray();

            foreach (var w in warnings)
            {
                list.Add(new JObject
                {
                    ["description"] = w.GetDescriptionText(),
                    ["severity"] = w.GetSeverity().ToString(),
                    ["element_ids"] = JArray.FromObject(w.GetFailingElements().Select(e => e.Value))
                });
            }

            return new JObject
            {
                ["warning_count"] = warnings.Count,
                ["warnings"] = list
            }.ToString();
        }
    }

    // =====================================================
    // 5. SetAdditionalPropertyForAllElements
    // =====================================================
    public sealed class SetAdditionalPropertyForAllElements
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            string propertyName = args.Value<string>("property_name");
            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            string newValue = args.Value<string>("value");

            if (string.IsNullOrEmpty(propertyName) || ids.Count == 0)
                return new JObject { ["error"] = "Missing property_name or element_ids." }.ToString();

            int updated = 0;

            using (var tx = new Transaction(doc, "Set Additional Property"))
            {
                tx.Start();

                foreach (var id in ids)
                {
                    var el = doc.GetElement(new ElementId(id));
                    if (el == null) continue;

                    var prop = el.GetType().GetProperty(propertyName);
                    if (prop == null || !prop.CanWrite) continue;

                    try
                    {
                        object converted = newValue;
                        if (prop.PropertyType == typeof(double) && double.TryParse(newValue, out double d)) converted = d;
                        else if (prop.PropertyType == typeof(int) && int.TryParse(newValue, out int i)) converted = i;

                        prop.SetValue(el, converted);
                        updated++;
                    }
                    catch { }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["property_name"] = propertyName,
                ["updated_count"] = updated,
                ["requested_count"] = ids.Count
            }.ToString();
        }
    }
}

namespace Tools
{
    // =====================================================
    // 1. GetActiveViewInRevit
    // =====================================================
    public sealed class GetActiveViewInRevit
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var view = uidoc.ActiveView;
            if (view == null)
                return new JObject { ["error"] = "No active view found." }.ToString();

            var direction = uiapp.ActiveUIDocument.ActiveView.ViewDirection;
            var dirObj = new JObject
            {
                ["X"] = direction.X,
                ["Y"] = direction.Y,
                ["Z"] = direction.Z
            };

            return new JObject
            {
                ["view_id"] = view.Id.Value,
                ["view_name"] = view.Name,
                ["view_type"] = view.ViewType.ToString(),
                ["direction"] = dirObj
            }.ToString();
        }
    }

    // =====================================================
    // 2. GetAllProjectUnits
    // =====================================================
    public sealed class GetAllProjectUnits
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var units = doc.GetUnits();
            var specs = UnitUtils.GetAllMeasurableSpecs(); // Enumerate all specs available in the Revit API
            var unitInfo = new JArray();

            foreach (var specId in specs)
            {
                try
                {
                    var fo = units.GetFormatOptions(specId);
                    if (fo == null)
                        continue;

                    unitInfo.Add(new JObject
                    {
                        ["spec"] = specId.TypeId,
                        ["display_name"] = LabelUtils.GetLabelForSpec(specId),
                        ["unit_type_id"] = fo.GetUnitTypeId().ToString(),
                        ["symbol_type_id"] = fo.GetSymbolTypeId().ToString(),
                        ["accuracy"] = fo.Accuracy,
                        ["use_project_settings"] = fo.UseDefault
                    });
                }
                catch
                {
                    // Some specs may not have format options — skip them
                }
            }

            return new JObject
            {
                ["count"] = unitInfo.Count,
                ["units"] = unitInfo
            }.ToString();
        }
    }

    // =====================================================
    // 3. GetModelCategories
    // =====================================================
    public sealed class GetModelCategories
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;

            var categories = doc.Settings.Categories
                .Cast<Category>()
                .Select(c => new JObject
                {
                    ["id"] = c.Id.Value,
                    ["name"] = c.Name,
                    ["type"] = c.CategoryType.ToString(),
                    ["visible"] = c.get_AllowsVisibilityControl(doc.ActiveView) ? true : false
                })
                .OrderBy(o => o["name"])
                .ToList();

            return new JObject
            {
                ["category_count"] = categories.Count,
                ["categories"] = JArray.FromObject(categories)
            }.ToString();
        }
    }

    // =====================================================
    // 4. SetRotationForElements
    // =====================================================
    public sealed class SetRotationForElements
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            var ids = args["element_ids"]?.ToObject<List<int>>() ?? new List<int>();
            double radians = args.Value<double?>("radians") ?? 0;

            if (ids.Count == 0)
                return new JObject { ["error"] = "No element_ids provided." }.ToString();

            var basePoint = XYZ.Zero;
            if (args["axis_start"] != null)
            {
                var axisStart = args["axis_start"];
                basePoint = new XYZ(axisStart.Value<double>("X"), axisStart.Value<double>("Y"), axisStart.Value<double>("Z"));
            }

            var axis = Line.CreateBound(basePoint, basePoint + XYZ.BasisZ);

            using (var tx = new Transaction(doc, "Rotate Elements"))
            {
                tx.Start();

                foreach (var id in ids)
                {
                    var el = doc.GetElement(new ElementId(id));
                    if (el == null) continue;

                    try
                    {
                        ElementTransformUtils.RotateElement(doc, el.Id, axis, radians);
                    }
                    catch
                    {
                        // skip invalid rotation
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["rotated_count"] = ids.Count,
                ["radians"] = radians
            }.ToString();
        }
    }

    // =====================================================
    // 5. GetUserSelectionInRevit
    // =====================================================
    public sealed class GetUserSelectionInRevit
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var sel = uidoc.Selection;
            var selectedIds = sel.GetElementIds()
                .Select(e => e.Value)
                .ToList();

            if (selectedIds.Count == 0)
                return new JObject { ["message"] = "No elements currently selected." }.ToString();

            return new JObject
            {
                ["count"] = selectedIds.Count,
                ["selected_ids"] = JArray.FromObject(selectedIds)
            }.ToString();
        }
    }


    // =====================================================
    // 1. SetCategoryVisibilityInView
    // =====================================================
    public sealed class SetCategoryVisibilityInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            int viewId = args.Value<int>("view_id");
            var categoryIds = args["category_ids"]?.ToObject<List<int>>() ?? new List<int>();
            bool visible = args.Value<bool?>("visible") ?? true;

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            if (view.IsTemplate)
                return new JObject { ["error"] = "Cannot modify category visibility on a view template directly." }.ToString();

            var modified = new JArray();
            var failed = new JArray();

            using (var tx = new Transaction(doc, "Set Category Visibility"))
            {
                tx.Start();

                foreach (var catId in categoryIds)
                {
                    try
                    {
                        var category = Category.GetCategory(doc, new ElementId(catId));
                        if (category == null)
                        {
                            failed.Add($"Category ID {catId} not found.");
                            continue;
                        }

                        if (!view.CanCategoryBeHidden(category.Id))
                        {
                            failed.Add($"{category.Name} cannot be hidden in this view type.");
                            continue;
                        }

                        view.SetCategoryHidden(category.Id, !visible);
                        modified.Add(category.Name);
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"Category ID {catId}: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            var result = new JObject
            {
                ["view_id"] = viewId,
                ["view_name"] = view.Name,
                ["action"] = visible ? "shown" : "hidden",
                ["modified_count"] = modified.Count,
                ["modified_categories"] = modified,
                ["failed_count"] = failed.Count,
                ["failed_categories"] = failed
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 2. SetIsolateCategoriesInView
    // =====================================================
    public sealed class SetIsolateCategoriesInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            int viewId = args.Value<int>("view_id");
            var categoryIds = args["category_ids"]?.ToObject<List<long>>() ?? new List<long>();

            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            if (view.IsTemplate)
                return new JObject { ["error"] = "Cannot isolate categories on view templates." }.ToString();

            var categories = doc.Settings.Categories;
            var visibleCategories = new JArray();
            var hiddenCategories = new JArray();
            var failed = new JArray();

            using (var tx = new Transaction(doc, "Isolate Categories"))
            {
                tx.Start();

                foreach (Category category in categories)
                {
                    try
                    {
                        if (category == null) continue;

                        var catId = category.Id;
                        if (!view.CanCategoryBeHidden(catId))
                            continue;

                        bool shouldHide = !categoryIds.Contains(catId.Value);
                        view.SetCategoryHidden(catId, shouldHide);

                        if (shouldHide)
                            hiddenCategories.Add(category.Name);
                        else
                            visibleCategories.Add(category.Name);
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{category?.Name ?? "Unknown"}: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            var result = new JObject
            {
                ["view_id"] = viewId,
                ["view_name"] = view.Name,
                ["action"] = "isolated",
                ["visible_count"] = visibleCategories.Count,
                ["visible_categories"] = visibleCategories,
                ["hidden_count"] = hiddenCategories.Count,
                ["hidden_categories"] = hiddenCategories,
                ["failed_count"] = failed.Count,
                ["failed_categories"] = failed
            };

            return result.ToString();
        }
    }

    // =====================================================
    // 3. SetResetCategoryVisibilityInView
    // =====================================================
    public sealed class SetResetCategoryVisibilityInView
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            int viewId = args.Value<int>("view_id");
            var view = doc.GetElement(new ElementId(viewId)) as View;
            if (view == null)
                return new JObject { ["error"] = $"View {viewId} not found." }.ToString();

            if (view.IsTemplate)
                return new JObject { ["error"] = "Cannot reset category visibility on view templates." }.ToString();

            var resetCategories = new JArray();
            var categories = doc.Settings.Categories;

            using (var tx = new Transaction(doc, "Reset Category Visibility"))
            {
                tx.Start();

                foreach (Category category in categories)
                {
                    try
                    {
                        if (category == null) continue;
                        var catId = category.Id;

                        if (view.CanCategoryBeHidden(catId))
                        {
                            view.SetCategoryHidden(catId, false);
                            resetCategories.Add(category.Name);
                        }
                    }
                    catch
                    {
                        // ignore errors for unmodifiable categories
                    }
                }

                tx.Commit();
            }

            var result = new JObject
            {
                ["view_id"] = viewId,
                ["view_name"] = view.Name,
                ["action"] = "reset",
                ["reset_count"] = resetCategories.Count,
                ["reset_categories"] = resetCategories,
                ["message"] = "All categories are now visible."
            };

            return result.ToString();
        }
    }

    public sealed class GenerateImageWithGeminiFlash
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            try
            {
                var args = string.IsNullOrWhiteSpace(argsJson)
                    ? new JObject()
                    : JObject.Parse(argsJson);

                string apiKey = args.Value<string>("api_key");
                string prompt = args.Value<string>("prompt");
                string outputPath = args.Value<string>("output_path");

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(prompt) ||
                     string.IsNullOrWhiteSpace(outputPath))
                {
                    return new JObject
                    {
                        ["error"] = "Missing required parameters: api_key, prompt, output_path."
                    }.ToString();
                }

                var imagePath = GetInputImage(uidoc.Document);
                if (!File.Exists(imagePath))
                {
                    return new JObject
                    {
                        ["error"] = $"Input image not found at '{imagePath}'."
                    }.ToString();
                }

                try
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                        byte[] imageBytes = File.ReadAllBytes(imagePath);
                        string base64Image = Convert.ToBase64String(imageBytes);

                        // Construct Gemini API request
                        var requestBody = new
                        {
                            contents = new[]
                            {
                            new
                            {
                                parts = new object[]
                                {
                                    new { text = prompt },
                                    new
                                    {
                                        inline_data = new
                                        {
                                            mime_type = "image/jpeg",
                                            data = base64Image
                                        }
                                    }
                                }
                            }
                        }
                        };

                        string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                        var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-image-preview:generateContent?key={apiKey}";

                        var response = client.PostAsync(apiUrl, content).Result;
                        string responseBody = response.Content.ReadAsStringAsync().Result;

                        if (!response.IsSuccessStatusCode)
                        {
                            return new JObject
                            {
                                ["success"] = false,
                                ["status"] = (int)response.StatusCode,
                                ["error"] = responseBody
                            }.ToString();
                        }

                        dynamic apiResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
                        var parts = apiResponse["candidates"]?[0]?["content"]?["parts"];

                        if (parts == null)
                            return new JObject { ["error"] = "No valid response parts returned from API." }.ToString();

                        foreach (var part in parts)
                        {
                            if (part["inlineData"]?["data"] != null)
                            {
                                string base64Result = (string)part["inlineData"]["data"];
                                byte[] generatedImageBytes = Convert.FromBase64String(base64Result);

                                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                                File.WriteAllBytes(outputPath, generatedImageBytes);

                                return new JObject
                                {
                                    ["success"] = true,
                                    ["prompt"] = prompt,
                                    ["input_image"] = imagePath,
                                    ["output_image"] = outputPath,
                                    ["size_bytes"] = generatedImageBytes.Length
                                }.ToString();
                            }
                        }

                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = "No image data returned from API response."
                        }.ToString();
                    }
                }
                catch (Exception ex)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = ex.Message
                    }.ToString();
                }
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["error"] = ex.Message + "\n" + ex.StackTrace
                }.ToString();
            }
        }

        private static string? GetInputImage(Document doc)
        {
            Autodesk.Revit.DB.View activeView = doc.ActiveView;
            if (activeView == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", "No active view found.");
                return null;
            }

            string tempImagePath = Path.Combine(Path.GetTempPath(), "RevitViewScreenshot.png");

            ImageExportOptions options = new ImageExportOptions
            {

                ExportRange = ExportRange.CurrentView, // Only the active view
                HLRandWFViewsFileType = ImageFileType.PNG, // File type (PNG, JPG, BMP, etc.)
                ShadowViewsFileType = ImageFileType.PNG,
                FilePath = tempImagePath, // Output path without extension
                FitDirection = FitDirectionType.Horizontal, // Fit to width
                PixelSize = 1000, // Approximate width in pixels (height auto-calculated)
                ZoomType = ZoomFitType.FitToPage, // Fit view to page
                ImageResolution = ImageResolution.DPI_300
            };
            doc.ExportImage(options);

            return tempImagePath;
        }
    }

    public sealed class ManageElementVisibilityCache
    {
        // In-memory static cache for the session
        private static readonly Dictionary<string, (long viewId, List<long> elementIds, DateTime savedAt)> _cache
            = new Dictionary<string, (long, List<long>, DateTime)>(StringComparer.OrdinalIgnoreCase);

        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            string action = args.Value<string>("action") ?? "list";
            string name = args.Value<string>("group_name");
            bool fromSelection = args.Value<bool?>("from_selection") ?? false;

            switch (action.ToLower())
            {
                case "create":
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return new JObject { ["error"] = "Missing group_name." }.ToString();

                        var ids = fromSelection
                            ? uidoc.Selection.GetElementIds().Select(e => e.Value).ToList()
                            : new FilteredElementCollector(doc, view.Id)
                                .WhereElementIsNotElementType()
                                .ToElementIds().Select(e => e.Value).ToList();

                        if (ids.Count == 0)
                            return new JObject { ["error"] = "No elements found to cache." }.ToString();

                        _cache[name] = (view.Id.Value, ids, DateTime.Now);

                        return new JObject
                        {
                            ["success"] = true,
                            ["action"] = "create",
                            ["group_name"] = name,
                            ["view_id"] = view.Id.Value,
                            ["count"] = ids.Count,
                            ["from_selection"] = fromSelection,
                            ["cached_groups"] = _cache.Count
                        }.ToString();
                    }

                case "show":
                case "hide":
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return new JObject { ["error"] = "Missing group_name." }.ToString();

                        if (!_cache.TryGetValue(name, out var entry))
                            return new JObject { ["error"] = $"Group '{name}' not found in cache." }.ToString();

                        var (viewId, elementIds, _) = entry;
                        if (view.Id.Value != viewId)
                            return new JObject { ["error"] = $"Cached group '{name}' belongs to a different view." }.ToString();

                        using (var tx = new Transaction(doc, $"{action} cached group '{name}'"))
                        {
                            tx.Start();
                            try
                            {
                                if (action == "hide")
                                    view.HideElements(elementIds.Select(id => new ElementId(id)).ToList());
                                else
                                    view.UnhideElements(elementIds.Select(id => new ElementId(id)).ToList());
                            }
                            catch (Exception ex)
                            {
                                tx.RollBack();
                                return new JObject { ["success"] = false, ["error"] = ex.Message }.ToString();
                            }
                            tx.Commit();
                        }

                        return new JObject
                        {
                            ["success"] = true,
                            ["action"] = action,
                            ["group_name"] = name,
                            ["element_count"] = elementIds.Count
                        }.ToString();
                    }

                case "select":
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return new JObject { ["error"] = "Missing group_name." }.ToString();

                        if (!_cache.TryGetValue(name, out var entry))
                            return new JObject { ["error"] = $"Group '{name}' not found in cache." }.ToString();

                        var (viewId, elementIds, _) = entry;
                        if (view.Id.Value != viewId)
                            return new JObject { ["error"] = $"Cached group '{name}' belongs to a different view." }.ToString();

                        var ids = elementIds.Select(id => new ElementId(id)).ToList();
                        uidoc.Selection.SetElementIds(ids);

                        return new JObject
                        {
                            ["success"] = true,
                            ["action"] = "select",
                            ["group_name"] = name,
                            ["view_id"] = view.Id.Value,
                            ["selected_count"] = ids.Count
                        }.ToString();
                    }

                case "list":
                    {
                        var list = _cache.Select(c => new JObject
                        {
                            ["group_name"] = c.Key,
                            ["view_id"] = c.Value.viewId,
                            ["element_count"] = c.Value.elementIds.Count,
                            ["saved_at"] = c.Value.savedAt.ToString("u")
                        });

                        return new JObject
                        {
                            ["total_groups"] = _cache.Count,
                            ["groups"] = JArray.FromObject(list)
                        }.ToString();
                    }

                case "clear":
                    {
                        _cache.Clear();
                        return new JObject
                        {
                            ["success"] = true,
                            ["action"] = "clear",
                            ["message"] = "All cached groups cleared."
                        }.ToString();
                    }

                default:
                    return new JObject { ["error"] = $"Unknown action '{action}'." }.ToString();
            }
        }
    }

    public class ExportSelectedToIFC
    {
        public static string Execute(UIApplication app, UIDocument uidoc, string argsJson)
        {
            try
            {
                Document doc = uidoc.Document;

                // Parse arguments
                var args = string.IsNullOrWhiteSpace(argsJson)
                    ? new JObject()
                    : JObject.Parse(argsJson);

                string exportPath = args.Value<string>("export_path");
                string ifcVersion = args.Value<string>("ifc_version") ?? "IFC4";

                if (string.IsNullOrWhiteSpace(exportPath))
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    exportPath = Path.Combine(desktop, "SelectedElements.ifc");
                }

                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds == null || selectedIds.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["message"] = "Please select elements to export."
                    }.ToString();
                }

                // Prepare export directory
                string exportDir = Path.GetDirectoryName(exportPath);
                string exportName = Path.GetFileNameWithoutExtension(exportPath);
                if (!Directory.Exists(exportDir))
                    Directory.CreateDirectory(exportDir);

                // Create a temporary 3D view
                View3D tempView = null;
                bool result = false;

                using (Transaction tx = new Transaction(doc, "Prepare Temporary 3D View for IFC Export"))
                {
                    tx.Start();

                    // Find a 3D view family type
                    var vft = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                    tempView = View3D.CreateIsometric(doc, vft.Id);
                    tempView.Name = $"Temp_IFC_Export_{DateTime.Now:HHmmss}";

                    // Hide all elements except selected ones
                    var allElems = new FilteredElementCollector(doc, tempView.Id)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    var toHide = allElems
                        .Where(e => !selectedIds.Contains(e.Id) && e.CanBeHidden(tempView))
                        .Select(e => e.Id)
                        .ToList();

                    if (toHide.Count > 0)
                        tempView.HideElements(toHide);

                    tx.Commit();
                }

                using (Transaction exportTrans = new Transaction(doc, "default export"))
                {
                    exportTrans.Start();

                    // Setup IFC export options
                    IFCExportOptions ifcOptions = new IFCExportOptions();
                    if (ifcVersion.Equals("IFC2x3", StringComparison.OrdinalIgnoreCase))
                        ifcOptions.FileVersion = IFCVersion.IFC2x3;
                    else
                        ifcOptions.FileVersion = IFCVersion.IFC4;

                    // Limit export to visible elements in the temporary view
                    ifcOptions.FilterViewId = tempView.Id;
                    ifcOptions.AddOption("ExportVisibleElementsInView", "true");
                    ifcOptions.AddOption("VisibleElementsOfCurrentView", "true");

                    // Export IFC (outside transaction)
                    result = doc.Export(exportDir, exportName, ifcOptions);

                    exportTrans.Commit();
                }

                // Cleanup the temporary view
                using (Transaction tx2 = new Transaction(doc, "Cleanup Temporary IFC View"))
                {
                    tx2.Start();
                    if (tempView != null && doc.GetElement(tempView.Id) != null)
                        doc.Delete(tempView.Id);
                    tx2.Commit();
                }

                // Return structured JSON
                return new JObject
                {
                    ["success"] = result,
                    ["message"] = result
                        ? $"✅ IFC export completed.\nFile: {exportPath}\nExported {selectedIds.Count} element(s)."
                        : "⚠️ IFC export failed.",
                    ["element_count"] = selectedIds.Count,
                    ["export_path"] = exportPath
                }.ToString();
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = ex.Message,
                    ["stack"] = ex.StackTrace
                }.ToString();
            }
        }
    }

    public sealed class ExportElementsToCSV
    {
        public static string Execute(UIApplication app, UIDocument uidoc, string argsJson)
        {
            try
            {
                Document doc = uidoc.Document;

                // Parse arguments
                var args = string.IsNullOrWhiteSpace(argsJson)
                    ? new JObject()
                    : JObject.Parse(argsJson);

                string exportPath = args.Value<string>("export_path");
                string scope = args.Value<string>("scope") ?? "selected";
                bool includeTypeParams = args.Value<bool?>("include_type_parameters") ?? false;

                var paramArray = args["parameters"]?.ToObject<List<string>>();
                if (paramArray == null || paramArray.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["message"] = "Please provide parameter names to export.",
                    }.ToString();
                }

                var paramNames = paramArray.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                if (paramNames.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["message"] = "No valid parameter names specified.",
                    }.ToString();
                }

                if (string.IsNullOrWhiteSpace(exportPath))
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    exportPath = Path.Combine(desktop, "Elements.csv");
                }

                // Collect elements
                IList<Element> elements;
                if (scope.Equals("visible", StringComparison.OrdinalIgnoreCase))
                {
                    elements = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .ToElements();
                }
                else
                {
                    var ids = uidoc.Selection.GetElementIds();
                    if (ids.Count == 0)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["message"] = "Please select elements to export.",
                        }.ToString();
                    }
                    elements = ids.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
                }

                if (elements.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["message"] = "No elements found to export.",
                    }.ToString();
                }

                // Build CSV
                var sb = new StringBuilder();
                var header = new List<string> { "ElementId", "Category", "Family", "Type" };
                header.AddRange(paramNames);
                sb.AppendLine(string.Join(",", header.Select(Quote)));

                foreach (var e in elements)
                {
                    string cat = e.Category?.Name ?? "";
                    string fam = (e is FamilyInstance fi) ? fi.Symbol?.Family?.Name ?? "" : "";
                    string typeName = (e is FamilyInstance fi2) ? fi2.Symbol?.Name ?? "" : e.Name ?? "";

                    var row = new List<string>
                    {
                        e.Id.Value.ToString(),
                        cat, fam, typeName
                    };

                    foreach (var paramName in paramNames)
                    {
                        string val = GetParameterValueByName(e, paramName);
                        if (includeTypeParams && string.IsNullOrEmpty(val))
                        {
                            var typeElem = doc.GetElement(e.GetTypeId());
                            val = GetParameterValueByName(typeElem, paramName);
                        }
                        row.Add(val ?? "");
                    }

                    sb.AppendLine(string.Join(",", row.Select(Quote)));
                }

                File.WriteAllText(exportPath, sb.ToString(), Encoding.UTF8);

                return new JObject
                {
                    ["success"] = true,
                    ["message"] = $"Exported {elements.Count} elements to:\n{exportPath}",
                }.ToString();

            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["error"] = ex.Message + "\n" + ex.StackTrace
                }.ToString();
            }
        }

        private static string GetParameterValueByName(Element e, string paramName)
        {
            if (e == null) return "";
            var param = e.LookupParameter(paramName);
            return GetParameterValueAsString(param);
        }

        private static string GetParameterValueAsString(Parameter p)
        {
            if (p == null) return "";
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String: return p.AsString() ?? "";
                    case StorageType.Double: return p.AsValueString() ?? p.AsDouble().ToString();
                    case StorageType.Integer: return p.AsInteger().ToString();
                    case StorageType.ElementId: return p.AsElementId().Value.ToString();
                    default: return "";
                }
            }
            catch { return ""; }
        }

        private static string Quote(string text)
        {
            if (text == null) return "";
            if (text.Contains(",") || text.Contains("\""))
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }
    }

    public sealed class GetElementsOnLevel
    {
        public static string Execute(UIApplication uiapp, UIDocument uidoc, string argsJson)
        {
            var doc = uidoc.Document;
            var args = string.IsNullOrWhiteSpace(argsJson)
                ? new JObject()
                : JObject.Parse(argsJson);

            string levelName = args.Value<string>("level_name")?.Trim();
            string exportPath = args.Value<string>("export_path");
            var categories = args["categories"]?.ToObject<List<string>>() ?? new List<string>();

            if (string.IsNullOrEmpty(levelName))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = "No level name specified."
                }.ToString();
            }

            // Find level
            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));

            if (level == null)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"Level '{levelName}' not found."
                }.ToString();
            }

            var normalizedCats = new List<string>();
            foreach (var item in categories)
            {
                var matches = doc.Settings.Categories
                 .Cast<Category>()
                 .Where(c => c.Name.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                 .Select(x => x.Name.ToLower());

                normalizedCats.AddRange(matches);
            }

            bool useCategoryFilter = normalizedCats.Count > 0;

            ElementId levelId = level.Id;

            // Collect elements on that level
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    try
                    {
                        // 1️⃣ Generic LEVEL_PARAM
                        var p = e.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                        if (p != null && p.AsElementId() == levelId)
                            return true;

                        // 2️⃣ Wall base constraint
                        p = e.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                        if (p != null && p.AsElementId() == levelId)
                            return true;

                        // 3️⃣ Columns, Foundations, etc.
                        p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                        if (p != null && p.AsElementId() == levelId)
                            return true;

                        // 4️⃣ Floors, roofs, etc.
                        p = e.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                        if (p != null && p.AsElementId() == levelId)
                            return true;

                        // 5️⃣ Fallback: Level name stored as text
                        p = e.LookupParameter("Level");
                        if (p != null && string.Equals(p.AsValueString(), levelName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch
                    {
                    }
                    return false;
                })
                .ToList();

            if (useCategoryFilter)
            {
                collector = collector
                    .Where(e => e.Category != null && normalizedCats.Contains(e.Category.Name.ToLower()))
                    .ToList();
            }

            if (collector.Count == 0)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"No elements found on level '{levelName}'" +
                                  (useCategoryFilter ? $" with categories: {string.Join(", ", categories)}" : "") + "."
                }.ToString();
            }

            // Build result JSON
            var elementInfo = collector.Select(e => new JObject
            {
                ["id"] = e.Id.Value,
                ["name"] = e.Name,
                ["category"] = e.Category?.Name ?? "",
            });

            var result = new JObject
            {
                ["success"] = true,
                ["element_count"] = collector.Count,
                ["elements"] = new JArray(elementInfo)
            };

            return result.ToString();
        }
    }
}


