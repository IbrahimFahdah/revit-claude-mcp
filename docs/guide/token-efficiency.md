# Token Efficiency Guide: Using Claude with Revit MCP Connector

## Understanding Token Usage

When you use Claude to interact with your Revit model through the MCP connector, every operation consumes tokens from your conversation budget. Understanding how tokens are used helps you work more efficiently and get more done in a single conversation.

---

## Token Cost Breakdown

### Initial Connection Cost (High)
**~48,000 - 50,000 tokens** for the first tool search

When Claude first connects to the Revit MCP connector, it loads:
- Tool definitions and schemas
- Parameter specifications and validation rules
- MCP protocol infrastructure
- Type definitions and error handling documentation
- Connection metadata

**This happens once per conversation** and is the largest token expense.

### Subsequent Operations (Low)
Once connected, individual operations are very efficient:
- **Simple queries**: 100-500 tokens (e.g., getting user selection)
- **Data retrieval**: 500-2,000 tokens (e.g., getting parameters from one element)
- **Bulk operations**: 2,000-5,000 tokens (e.g., getting 91 elements in a room)
- **Additional tool loads**: 300-1,000 tokens per new tool

---

## Best Practices for Token Efficiency

### 1. **Batch Your Work in One Conversation**

✅ **EFFICIENT** - Single conversation:
```
Question 1: Get elements in room 200
Question 2: Get parameters for element 1234567
Question 3: Export selected elements to CSV
Question 4: Check warnings in the model
Question 5: Get all families used
...and 10 more questions

Total cost: 48,000 (initial) + ~20,000 (operations) = 68,000 tokens
```

❌ **INEFFICIENT** - New conversation each time:
```
Conversation 1: Get elements in room 200
   → Cost: 48,000 + 3,000 = 51,000 tokens

Conversation 2: Get parameters for element 1234567
   → Cost: 48,000 + 1,500 = 49,500 tokens

Conversation 3: Export to CSV
   → Cost: 48,000 + 2,000 = 50,000 tokens

Total cost: 150,500 tokens (3x more expensive!)
```

**Key Takeaway**: Keep your Revit session in one long conversation. The initial ~48k token "connection fee" is paid once, then everything else is cheap.

---

### 2. **Be Specific in Your Requests**

The more specific you are, the fewer unnecessary tools Claude loads.

✅ **SPECIFIC** (Better):
- "Get elements in room 2177792"
- "Get the Name parameter from element 1234567"
- "Export elements 100, 200, 300 to CSV"

❌ **VAGUE** (More expensive):
- "Show me what tools are available for rooms"
- "Tell me about Revit elements"
- "What can you do with the model?"

**Why it matters**: Specific requests help Claude load only the exact tool needed. Vague requests force Claude to load multiple tools to explore options.

---

### 3. **Ask for One Thing at a Time (Then Build)**

Instead of asking one complex multi-part question, break it into steps:

✅ **STEP-BY-STEP**:
```
Step 1: "Get all elements in room 200"
   (Claude loads room tools)

Step 2: "Now get the 'Family and Type' parameter for those furniture elements"
   (Claude reuses already-loaded tools)

Step 3: "Export this to a CSV file"
   (Claude only loads export tools when needed)
```

❌ **ALL AT ONCE**:
```
"Get all elements in every room, their parameters, families, 
 types, locations, and export everything to multiple CSVs 
 organized by level and category"
```

This forces Claude to load many tools at once, some of which might not be needed.

---

### 4. **Tell Claude to Be Token-Efficient**

You can explicitly ask Claude to optimize for tokens:

**Examples of what to say**:
- "Be as token-efficient as possible"
- "Minimize token usage when searching for tools"
- "I'm going to ask 20 questions - load only what you need for each"
- "Use limit=1 when searching for tools"

Claude will then prioritize efficiency in how it loads tools.

---

### 5. **Plan Multi-Step Workflows**

If you have a complex workflow, outline it first:

```
"I need to:
1. Get all rooms on Level 2
2. For each room, get the furniture count
3. Export the results to CSV

Let's start with step 1."
```

This helps Claude understand the full scope and load tools efficiently.

---

## Real-World Example: Token Usage Comparison

### Scenario: Analyzing 5 Different Rooms

**Method A: New conversation for each room** ❌
```
Conv 1: Get elements in room 200      → 51,000 tokens
Conv 2: Get elements in room 201      → 51,000 tokens
Conv 3: Get elements in room 202      → 51,000 tokens
Conv 4: Get elements in room 203      → 51,000 tokens
Conv 5: Get elements in room 204      → 51,000 tokens
────────────────────────────────────────────────────
Total: 255,000 tokens
```

**Method B: One conversation, sequential questions** ✅
```
Q1: Get elements in room 200          → 51,000 tokens (initial load)
Q2: Get elements in room 201          → 3,500 tokens
Q3: Get elements in room 202          → 3,500 tokens
Q4: Get elements in room 203          → 3,500 tokens
Q5: Get elements in room 204          → 3,500 tokens
────────────────────────────────────────────────────
Total: 65,000 tokens (4x more efficient!)
```

**Savings: 190,000 tokens (74% reduction)**

---

## Understanding the ~48k Token Initial Cost

You might wonder: "Why is the first tool search so expensive?"

### What Gets Loaded:

When Claude connects to the Revit MCP connector for the first time in a conversation, the system loads:

1. **Tool Schemas** (~3,000-5,000 tokens)
   - Function names and descriptions
   - Parameter definitions (name, type, required/optional)
   - Validation rules (min/max values, allowed options)

2. **MCP Protocol Infrastructure** (~15,000 tokens)
   - Communication protocol specifications
   - Request/response format definitions
   - Connection metadata

3. **Type Definitions** (~10,000 tokens)
   - Data type specifications (integer, string, array, etc.)
   - Schema validation rules
   - JSON Schema standards

4. **Error Handling** (~5,000 tokens)
   - Error codes and descriptions
   - Recovery procedures
   - Debugging information

5. **Examples & Documentation** (~10,000 tokens)
   - Usage examples
   - Best practices
   - Parameter examples

**Total: ~48,000 tokens**

This is a **one-time cost per conversation** that enables reliable, type-safe communication between Claude and your Revit model.

---

## Common Mistakes to Avoid

### ❌ Mistake 1: Starting Fresh Conversations
**Problem**: Reloading the 48k token infrastructure every time
**Solution**: Keep working in the same conversation

### ❌ Mistake 2: Asking Exploratory Questions
**Problem**: "What Revit tools do you have?" loads many unnecessary tools
**Solution**: Ask for what you specifically need

### ❌ Mistake 3: Combining Unrelated Tasks
**Problem**: "Get room data AND check warnings AND export families" in one request
**Solution**: Break into separate, sequential questions

### ❌ Mistake 4: Not Planning Ahead
**Problem**: Asking random questions without a workflow
**Solution**: Outline your workflow before starting

---

## Quick Reference Card

| Operation Type | Approximate Token Cost | Notes |
|---------------|----------------------|-------|
| **First tool search** | 48,000-50,000 | One-time per conversation |
| **Additional tool loads** | 300-1,000 per tool | When new capabilities needed |
| **Simple queries** | 100-500 | Get selection, get active view |
| **Single element data** | 500-2,000 | Get parameters from one element |
| **Bulk operations** | 2,000-5,000 | Get 50-100 elements with data |
| **Large exports** | 5,000-10,000 | Complex data extraction/export |

---

## Token Budget Planning

Most Claude plans have different token budgets per conversation:

### Example Calculation:
**Starting budget**: 190,000 tokens

**One conversation doing 20 Revit operations**:
- Initial connection: 48,000 tokens
- 20 operations @ avg 2,000 tokens each: 40,000 tokens
- **Total used**: 88,000 tokens
- **Remaining**: 102,000 tokens ✅ Plenty left!

**Same 20 operations in 20 separate conversations**:
- 20 conversations × 50,000 avg tokens each: 1,000,000 tokens
- **Result**: Would exceed budget after ~4 questions ❌

---

## Pro Tips

### Tip 1: Keep a "Working Session" Chat
Create one dedicated conversation for your daily Revit work. Keep it open all day and ask all your questions there.

### Tip 2: Use Descriptive Requests
Instead of: "Check room 200"
Say: "Get elements in room 2177792"

The element ID is more precise and helps Claude find the exact tool faster.

### Tip 3: Chain Related Questions
```
✅ Good:
"Get elements in room 200"
[Claude responds]
"Now get their Family and Type parameters"
[Claude responds]
"Export to CSV"
```

This reuses loaded tools efficiently.

### Tip 4: Explicitly Request Efficiency
If you know you'll have many questions, start with:
"I need to perform 30 Revit queries today. Please optimize for token efficiency."

---

## Summary: The Golden Rules

1. **One conversation = One work session** - Never start a new chat unnecessarily
2. **Be specific, not exploratory** - Ask for exactly what you need
3. **Sequential, not simultaneous** - One question at a time builds on loaded tools
4. **Tell Claude your intent** - "Be token-efficient" helps guide behavior
5. **Plan ahead** - Know your workflow before you start

**Follow these rules and you'll maximize your productivity while minimizing token usage.**

---

## Need Help?

If you're unsure about token efficiency for a specific workflow, you can ask Claude:
- "How many tokens will this operation cost?"
- "What's the most efficient way to do [task]?"
- "How should I structure this workflow to minimize tokens?"

Claude can estimate costs and suggest optimizations before you commit to a particular approach.

---
