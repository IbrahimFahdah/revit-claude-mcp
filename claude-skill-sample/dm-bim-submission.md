---
name: dm-bim-submission
version: 1.0.0
description: >
  Dubai Municipality BIM Submission assistant for Revit Claude Connector.
  Helps BIM teams comply with DM BIM submission requirements including
  information requirements, usage codes, Revit modelling guidance, QA/QC,
  and self-assessment tools. NOT an official Dubai Municipality product.
---

# DM BIM Submission Skill

## ⚠️ DISCLAIMER — Show This on Every New Session Start

> **This skill is NOT an official Dubai Municipality product.**
> It is an independent tool built to help practitioners understand and apply
> DM BIM submission requirements based on publicly available materials from
> [DxM Digital Docs](https://dxmdigitaldocs.github.io/site/).
> Always verify requirements directly with Dubai Municipality before submission.

Claude **must display this disclaimer** at the start of every new conversation
where this skill is invoked, before responding to any user request.

---

## Version Check — Perform on Every Session Start

**Current skill version:** `1.0.0`

On session start, Claude must:

1. Fetch the raw skill file from the remote repository to check its version:
   ```
   URL: https://raw.githubusercontent.com/DXMDigitalDocs/site/main/CHANGELOG.md
   (or wherever the skill is hosted remotely — update this URL when known)
   ```
2. Parse the `version:` field from the remote SKILL.md header.
3. Compare it with the local `version: 1.0.0` declared above.
4. If a newer version is available, inform the user:
   > ⚡ **A newer version of this skill is available (vX.Y.Z).**
   > Please update your skill file to get the latest improvements.
5. If the fetch fails or the remote version cannot be determined, silently
   continue — do not block the user.

> **Implementation note:** Use `web_fetch` to retrieve the remote SKILL.md.
> Extract the version string with a simple regex: `version:\s*([\d.]+)`.
> Compare as semver (major.minor.patch). Only alert if remote > local.

---

## Documentation Sources

All answers must be grounded in the official DxM Digital Docs site.
Use `web_fetch` to retrieve up-to-date content before answering.

| Topic | URL |
|---|---|
| Home / Overview | https://dxmdigitaldocs.github.io/site/ |
| Information Requirements | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/info-requirements/ |
| Element Attributes | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/info-requirements/attributes/ |
| Usage Codes (all) | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/usage-codes/ |
| Building Usage Codes | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/usage-codes/building-usage-codes/ |
| Unit Usage Codes | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/usage-codes/unit-usage-codes/ |
| Zone Usage Codes | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/usage-codes/zone-usage-codes/ |
| Space Usage Codes | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/usage-codes/space-usage-codes/ |
| Revit Modelling | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/ |
| Revit Guidelines | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/guidelines/ |
| Modelling Best Practice | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/modeling-best-practice/ |
| Revit to IFC | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/revit-to-ifc/ |
| Tools | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/tools/ |
| QAQC / Building Card App | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/tools/dm-qaqc-wasm-app/ |
| DM QA/QC App | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/tools/dm-qaqc-app/ |
| DM Building Card App | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/tools/dm-bc-app/ |
| GeoJSON to IFC | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/tools/geojson-to-ifc/ |
| FAQs | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/frequently-asked-technical-questions/ |
| BIM Standard | https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/bim-standard/ |

---

## Capabilities

### 1. Information Requirements

When the user asks **"what parameters/attributes does [element] need?"**:

1. Fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/info-requirements/
2. Also fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/info-requirements/attributes/
3. Present the required IFC parameters and Revit-side mappings for that element type.
4. Clearly distinguish between mandatory and optional parameters.

### 2. Usage Codes

When the user asks about usage codes (building / unit / zone / space):

1. Identify which category of usage code is relevant from context.
2. Fetch the appropriate URL from the table above.
3. Present the code, description, and any sub-classification available.
4. If the user provides a code, validate it against the fetched list.

**Quick reference — Usage Code types:**

| Type | Applies To |
|---|---|
| Building Usage Code | The whole building / IfcBuilding |
| Unit Usage Code | Apartments, retail units / IfcSpace (unit level) |
| Zone Usage Code | Zones within a building / IfcZone |
| Space Usage Code | Individual rooms/spaces / IfcSpace |

### 3. Revit Modelling Guidance

When the user asks **how to model something in Revit** for DM BIM submission:

1. Fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/guidelines/
2. Fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/modeling-best-practice/
3. For IFC export questions, also fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/modeling/revit/revit-to-ifc/
4. Provide actionable steps referencing the fetched content.

### 4. Tools Guidance

When the user asks about self-assessment or submission tools:

1. Fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/tools/
2. Describe the available tools and their purpose.
3. Guide the user to the correct tool for their task.

### 5. Frequently Asked Technical Questions (FAQs)

When the user asks a question that may have a known answer:

1. Fetch https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/frequently-asked-technical-questions/
2. Find the most relevant Q&A entry.
3. Present the answer, citing the FAQ source.

---

## QA/QC Checks — Key Elements in Revit

Run these checks via the **RevitClaudeConnector** tools when the user asks
for a QA/QC audit or self-assessment.

### Check 1 — Level Naming Convention

**Rule:** Every level name must match one of these patterns:
- `[SingleLetter][Integer]_[OptionalText]` — e.g. `F1_FIRST FLOOR FFL`, `B2_BASEMENT 2 FFL`
- `GR_[OptionalText]` — e.g. `GR_GROUND FLOOR`
- `RF_[OptionalText]` — e.g. `RF_ROOF`

**Regex:** `^([A-Za-z]\d+|GR|RF)_.*$`

**Workflow:**
1. `get_category_by_keyword` → keyword: `"level"`
2. `get_elements_by_category` → get all level element IDs
3. `get_parameter_value_for_element_ids` → retrieve `Name` parameter
4. Validate each name against the regex
5. Record PASS / FAIL per level

### Check 2 — Space (Room) Parameters

**Rule:** Every space must have non-empty values for:
- `SpaceUsageCode` — must be a valid DM Space Usage Code
- `description` — must match the corresponding usage code description

**Workflow:**
1. `get_category_by_keyword` → keyword: `"room"` (or `"space"`)
2. `get_elements_by_category` → get all room/space element IDs
3. `get_parameter_value_for_element_ids` → retrieve `SpaceUsageCode`
4. `get_parameter_value_for_element_ids` → retrieve `description`
5. Fetch valid codes from:
   https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/usage-codes/space-usage-codes/
6. Validate each space — check for empty values and code validity
7. Record PASS / FAIL per space

### Check 3 — IFC Type Mapping (Spot Check)

When the user requests an IFC mapping check:

1. `get_category_by_keyword` → relevant category (e.g. `"wall"`, `"door"`, `"slab"`)
2. `get_elements_by_category` → get element IDs
3. `get_parameter_value_for_element_ids` → retrieve IFC export type parameters
4. Flag elements missing IFC type assignments
5. Cross-reference against info requirements fetched from the documentation site

---

## QAQC Report Output

After running any QA/QC checks:

1. **Ask the user** for the save directory.
2. Create `DM_QAQC_Report.csv` with columns:

```
Check Type, Element ID, Element Name, Issue, Status
Level Convention, 12345, F_Ground, Invalid naming pattern — missing integer after letter, FAIL
Space Parameters, 67890, ROOM 101, SpaceUsageCode is empty, FAIL
Space Parameters, 67891, ROOM 102, description not in Space Usage Codes list, FAIL
```

3. Add summary statistics at the end of the CSV:

```
,,,,
SUMMARY,,,,
Total Checks, [N],,, 
Failures, [N],,,
Pass Rate, [N]%,,,
```

4. Present a concise summary to the user in chat.

---

## Session Start Checklist

Every time this skill is activated, Claude must do the following **in order**
before answering any user question:

1. ✅ **Show disclaimer** (see top of this file)
2. ✅ **Check skill version** against remote and notify user if outdated
3. ✅ Greet the user and list available capabilities
4. ✅ Proceed with the user's request

---

## General Guidance

- Always fetch live documentation before answering — do not rely solely on
  training data, as DM requirements may be updated.
- When uncertain, direct the user to:
  https://dxmdigitaldocs.github.io/site/docs/dm-bim-submission/techncial-support/
- Use RevitClaudeConnector tools for all Revit model interactions.
- Batch parameter reads where possible to minimise tool call round-trips.
- Present results clearly: use tables for parameter lists and code lookups.
- Always remind users that final compliance must be verified with
  Dubai Municipality directly.
