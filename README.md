# anota-api-csharp · Official C#/.NET client for the [anota](https://anota.cloud) API

**[Léeme en español](README.es.md)** · [Interactive API reference](https://anota.cloud/developers) · [All SDKs](https://github.com/anotacloud/anota-api)

![CI](https://github.com/anotacloud/anota-api-csharp/actions/workflows/ci.yml/badge.svg)

Create and publish forms, edit fields and conditional logic, read and write
submissions, and wire webhooks — everything the anota REST API can do, from C#.

Targets `net8.0`, depends only on the .NET base class library (`System.Net.Http`
and `System.Text.Json`), and returns responses as `System.Text.Json.Nodes.JsonNode`
so you are never boxed in by rigid model classes.

## Install

A NuGet package is coming. Until then, add the SDK to your app by one of:

- **Clone and reference the project:**
  ```bash
  git clone https://github.com/anotacloud/anota-api-csharp.git
  ```
  ```bash
  dotnet add reference ../anota-api-csharp/src/AnotaApi/AnotaApi.csproj
  ```
- **Download** the [ZIP](https://github.com/anotacloud/anota-api-csharp/archive/refs/heads/main.zip)
  or [Tarball](https://github.com/anotacloud/anota-api-csharp/archive/refs/heads/main.tar.gz),
  then reference `src/AnotaApi/AnotaApi.csproj`.

## Quickstart

```csharp
using Anota.Api;

var anota = new AnotaClient(Environment.GetEnvironmentVariable("ANOTA_API_KEY")!);

// Create a form with one text field, then publish it.
var form = await anota.CreateFormAsync(
    title: "Contact us",
    fields: new[] { new { type = "text", label = "Name", required = true } });
var formId = form!["id"]!.GetValue<string>();

await anota.PublishFormAsync(formId);

// Read its submissions.
var submissions = await anota.ListSubmissionsAsync(formId);
Console.WriteLine(submissions!.ToJsonString());
```

A full create → add field → publish → submit → list walkthrough lives in
[`examples/EndToEnd`](examples/EndToEnd/Program.cs). Run it with:

```bash
ANOTA_API_KEY=anota_sk_... dotnet run --project examples/EndToEnd
```

## Authentication

Create an API key in your workspace at https://anota.cloud/api-keys and pass it to the
client. Keys look like `anota_sk_…` and also power the Claude MCP connector.

```csharp
var anota = new AnotaClient("anota_sk_...");
// Optional overrides: base URL and a custom HttpClient (handlers, timeouts, DI).
var custom = new AnotaClient("anota_sk_...", baseUrl: "https://anota.cloud/api/v1", httpClient: myHttpClient);
```

Every method returns `Task<JsonNode?>` — the API's JSON parsed into a `JsonNode`
(`null` for empty responses). `fields`, `field`, `rules`, `rule`, and `answers`
parameters are typed `object`, so anonymous objects and dictionaries serialize naturally.

## All methods

| # | Method | HTTP |
|---|---|---|
| 1 | `ListFormsAsync()` | `GET /forms` |
| 2 | `CreateFormAsync(title, fields, description = null)` | `POST /forms` |
| 3 | `GetFormAsync(formId)` | `GET /forms/{formId}` |
| 4 | `AddFieldsAsync(formId, fields)` | `POST /forms/{formId}/fields` |
| 5 | `EditFieldAsync(formId, fieldId, field)` | `PATCH /forms/{formId}/fields/{fieldId}` |
| 6 | `DeleteFieldAsync(formId, fieldId)` | `DELETE /forms/{formId}/fields/{fieldId}` |
| 7 | `PublishFormAsync(formId)` | `POST /forms/{formId}/publish` |
| 8 | `RenameFormAsync(formId, title)` | `PATCH /forms/{formId}` |
| 9 | `SetPdfTemplateAsync(formId, key)` | `PUT /forms/{formId}/pdf-template` |
| 10 | `DeleteFormAsync(formId)` | `DELETE /forms/{formId}` |
| 11 | `CloneFormAsync(formId)` | `POST /forms/{formId}/clone` |
| 12 | `AddLogicRulesAsync(formId, rules)` | `POST /forms/{formId}/logic-rules` |
| 13 | `EditLogicRuleAsync(formId, ruleId, rule)` | `PUT /forms/{formId}/logic-rules/{ruleId}` |
| 14 | `DeleteLogicRuleAsync(formId, ruleId)` | `DELETE /forms/{formId}/logic-rules/{ruleId}` |
| 15 | `ListSubmissionsAsync(formId, page = 1, pageSize = 25, status = null)` | `GET /forms/{formId}/submissions` |
| 16 | `GetSubmissionAsync(submissionId)` | `GET /submissions/{submissionId}` |
| 17 | `CreateSubmissionAsync(formId, answers)` | `POST /forms/{formId}/submissions` |
| 18 | `SetSubmissionStatusAsync(submissionId, status)` | `PATCH /submissions/{submissionId}/status` |
| 19 | `DeleteSubmissionAsync(submissionId)` | `DELETE /submissions/{submissionId}` |
| 20 | `SubmissionStatsAsync(formId)` | `GET /forms/{formId}/stats` |
| 21 | `ListTemplatesAsync(language = "es")` | `GET /templates` |
| 22 | `CreateFormFromTemplateAsync(templateId)` | `POST /forms/from-template/{templateId}` |
| 23 | `ListWebhooksAsync(formId)` | `GET /forms/{formId}/webhooks` |
| 24 | `AddWebhookAsync(formId, url)` | `POST /forms/{formId}/webhooks` |
| 25 | `DeleteWebhookAsync(formId, webhookId)` | `DELETE /forms/{formId}/webhooks/{webhookId}` |

`fields`/`field` are plain objects: `{ type, label, required?, options?, rows?, columns? }`.
`rules`/`rule`: `{ match: "all" | "any", if: [...], then: [...] }`. `answers` is keyed by
field id, with string or string-array values.

## Errors

Non-2xx responses raise `AnotaApiError` with the HTTP status (`int Status`) and the
server's message (the problem-details `detail`, falling back to `title`, then the raw
body). Network failures surface as the native `HttpRequestException`, not wrapped.

```csharp
try
{
    await anota.EditFieldAsync(formId, fieldId, new { label = "New label" });
}
catch (AnotaApiError ex)
{
    Console.Error.WriteLine($"{ex.Status}: {ex.Message}");
}
```

Note: once a form has been published, its existing fields are locked (`EditFieldAsync`/
`DeleteFieldAsync` return 400); you can always `AddFieldsAsync`.

## License

MIT
