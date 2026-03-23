# User Guide

## Claude Panel
Toggling the Claude button shows/hides the Claude Panel. When the panel is shown, the Claude UI will be snapped inside the panel area. Note that Claude Desktop should already be manually launched before using the panel. 

If you prefer to keep Claude Desktop in its own separate window, you can close the panel and work with Claude normally.

![Claude Panel](/RevitClaudeUI1.png)

## Tools

You can explore current tools that you can use in the connector using the Tool Lookup view.

![Tools](/RevitClaudeUI2.png)


## Skills

Skills let you automate repetitive workflows by chaining tools together and controlling how Claude executes them and processes the output.

A skill is a `.skill` file (a renamed ZIP archive — you can inspect its contents by adding `.zip` to the filename and extracting it). A sample skill is included in the [`claude-skill-sample/`](https://github.com/IbrahimFahdah/revit-claude-mcp/tree/master/claude-skill-sample) folder of this repository as a starting point.

### Included Skill: `bim-qaqc-auditor`

The `bim-qaqc-auditor.skill` is a ready-to-use BIM quality assurance skill that automatically validates your Revit model against common BIM standards and generates a detailed CSV report. It checks:

- **Level naming conventions** — ensures levels follow the pattern `[Letter][Number]_[Text]` (e.g. `F1_GROUND FLOOR`, `B2_BASEMENT 2 FFL`)
- **Room/Space parameters** — verifies every room has a valid `SpaceUsageCode` and `description` matching the bundled usage codes reference spreadsheet

#### How to Load a Skill in Claude

1. Open **Claude Desktop** and go to **Settings → Claude AI Skills**
2. Click **Add Skill** and browse to the `bim-qaqc-auditor.skill` file
3. The skill named `bim-qaqc-auditor` will appear in your skills list and become available in your conversations

#### Prompt Examples to Trigger the Skill

Once the skill is loaded, Claude will automatically apply it when you ask something like:

- `"Run the BIM QA/QC auditor on the current model"`
- `"Use the bim-qaqc-auditor skill to check my Revit model"`
- `"Audit this model for level naming and space parameter issues"`
- `"Run QA/QC checks and save a report to my Desktop"`

Claude will follow the skill's workflow — querying levels and rooms via the connector tools, validating against the bundled usage codes spreadsheet, and producing a `QAQC.csv` report at a location you specify.

#### Want to Build Your Own Skills?

You can create custom skills tailored to your own workflows and validation rules. To learn more, see the official Claude documentation on building skills:
[https://code.claude.com/docs/en/skills](https://code.claude.com/docs/en/skills)

## Performance
Working with AI Assistance in Revit is a different experience than using native Revit tools.
Running commands directly in Revit will almost always be faster than asking an AI to do the same thing. The AI connector shines for certain use cases as mentioned in the "AI Assistance in Revit" section of the [What Is This?](
what-is-this.md) page, but if you just want to quickly get something done, the native Revit tools will usually be the way to go.

To improve the performance and token usage of the Revit Claude connector, you can:
- Use skills to chain multiple steps together in one go, instead of asking Claude to run separate commands one at a time.
- Be specific in your requests to avoid unnecessary back-and-forth. For example, instead of asking "What are the issues with this model?" you can ask "List all walls that are not connected to a floor."
- Use the Tool Lookup view to see what tools are available and how to use them effectively, instead of asking Claude to figure out which tool to use for a vague request.
- Avoid asking Claude to do things that are already easily done with native tools, like "select
- In Claude, disable any unnecessary tools that you don't need for your current task, so that Claude has fewer options to consider when deciding how to execute your commands.

![ClaudeToolControl](/ClaudeToolControl.png)

## Demos
[🔗Introducing RevitClaudeConnector (Part 1) ](https://www.linkedin.com/posts/dr-ibrahim-fahdah-13aa80a4_introducing-revitclaudeconnector-part-activity-7381345934884093952-BzG3/)

[🔗Claude AI Connector for Revit (Part 2) ](https://www.linkedin.com/posts/dr-ibrahim-fahdah-13aa80a4_claude-ai-connector-for-revit-part-2-activity-7383548162617270272-X2Fv/)

[🔗Supercharge Claude AI Connector for Revit with Your Own Tools ](https://www.linkedin.com/posts/dr-ibrahim-fahdah-13aa80a4_supercharge-claude-ai-connector-for-revit-activity-7384469142105554945-3UXt/)

[🔗When Revit Claude AI Connector Meets Claude Skills ](https://www.linkedin.com/posts/dr-ibrahim-fahdah-13aa80a4_ai-revit-bim-activity-7387709051360419840-BR_N/)