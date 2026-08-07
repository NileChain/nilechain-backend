# Agentic Orchestrator — SK API Assumptions & Verification

**Package:** `Microsoft.SemanticKernel` **1.78.0** (confirmed in `NileChain.AI.csproj`)

## LLM provider (Student Bedrock Gateway — SBG)

| Setting | Value |
| --- | --- |
| Origin | `http://apiaccess.iti.net.eg` |
| Portal | `http://apiaccess.iti.net.eg/student` |
| Chat | `POST /api/v1/student/chat` |
| Env key | `SBG_API_KEY` |
| Body | `model_id`, `messages[].content`, `system_prompt`, `max_tokens` |
| Default model | `amazon.nova-lite-v1:0` |

**Not used:** Bedrock Mantle / OpenAI Chat Completions.

Because SBG has **no OpenAI tool_calls**, the orchestrator uses a JSON **ReAct** loop (`OrchestratorMode=AgenticSbgReact`).

Local secrets: `backend/.env` and `NileChain.API/appsettings.Development.local.json` (gitignored).

## Assumption (verified against installed package XML docs)

In SK **1.78**, automatic tool calling (not used for SBG) would use:

```csharp
new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true)
}
```

**Not** the older:

```csharp
ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions  // pre-1.x rename
```

`FunctionChoiceBehavior.Auto(...)` is defined in `Microsoft.SemanticKernel.Abstractions` 1.78.0.

## Behavior

| Mode | When | What runs |
| --- | --- | --- |
| **Agentic** | `OpenAI:ApiKey` / `OPENAI_KEY` set | LLM chooses among 6 KernelFunctions |
| **DeterministicFallback** | No OpenAI key | Legacy MatchingAgent → RiskAgent sequence (demo offline) |

## Guardrails

1. Max **1** `WidenSearchRadius` and **1** `ProposeNextBestMatch` per request (`OrchestrationRunState`)
2. `GenerateContract` blocked after `FlagLowRiskWarning` unless `AgentRequest.ConfirmHighRiskWarning=true` (plugin + `IFunctionInvocationFilter`)
3. Contract field validation before accepting draft
4. Template fallback if LLM/RAG throws inside `GenerateContract`

## Tool-call trail

Every run logs a full trail to ILogger **and** `Console`, and returns `AgentResponse.ToolCallTrail`.

## How to verify (with OpenAI key)

```bash
# Set key, start API Development, then:
POST https://localhost:7018/api/agent/run/{requestId}
Authorization: Bearer <factory-jwt>
Content-Type: application/json

{
  "cropType": "Wheat",
  "quantityTons": 100,
  "qualitySpecs": "Grade A",
  "pricePerTon": 10000,
  "deliveryDate": "2026-12-01T00:00:00Z",
  "factoryGovernorate": "Aswan",
  "confirmHighRiskWarning": false
}
```

### Scenario A (widen)
Use a governorate with few local farms (e.g. remote governorate). Expect trail to include `WidenSearchRadius` then a second `SearchFarms`.

### Scenario B (low risk)
Ensure top farm scores &lt; 40 (or use a sparse-profile farm). Expect `FlagLowRiskWarning` and **no** successful `GenerateContract` (blocked unless `confirmHighRiskWarning: true`).

### Scenario C (hard cap)
Force a second `WidenSearchRadius` via prompt stress or unit call — second call returns `blocked: true`, `partialResult: true`.

Inspect response JSON field `toolCallTrail` and API console for `======== AGENTIC TOOL-CALL TRAIL ========`.
