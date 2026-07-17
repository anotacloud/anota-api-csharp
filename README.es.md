# anota-api-csharp · Cliente oficial de C#/.NET para la API de [anota](https://anota.cloud)

**[Read me in English](README.md)** · [Referencia interactiva de la API](https://anota.cloud/developers) · [Todos los SDK](https://github.com/anotacloud/anota-api)

![CI](https://github.com/anotacloud/anota-api-csharp/actions/workflows/ci.yml/badge.svg)

Crea y publica formularios, edita campos y lógica condicional, lee y escribe
respuestas, y conecta webhooks — todo lo que la API REST de anota puede hacer, desde C#.

Apunta a `net8.0`, depende solo de la biblioteca base de .NET (`System.Net.Http`
y `System.Text.Json`), y devuelve las respuestas como `System.Text.Json.Nodes.JsonNode`,
así que nunca te limitan las clases de modelo rígidas.

## Instalación

Cuando se publique la primera versión, instala desde NuGet:

```bash
dotnet add package Anota.Api
```

El paquete se publica automáticamente al crear una release en GitHub (queda disponible
en [nuget.org](https://www.nuget.org/packages/Anota.Api) tras esa primera ejecución).
Mientras tanto, o para trabajar desde el código fuente, agrega el SDK a tu aplicación de
una de estas formas:

- **Clona y referencia el proyecto:**
  ```bash
  git clone https://github.com/anotacloud/anota-api-csharp.git
  ```
  ```bash
  dotnet add reference ../anota-api-csharp/src/AnotaApi/AnotaApi.csproj
  ```
- **Descarga** el [ZIP](https://github.com/anotacloud/anota-api-csharp/archive/refs/heads/main.zip)
  o el [Tarball](https://github.com/anotacloud/anota-api-csharp/archive/refs/heads/main.tar.gz),
  y luego referencia `src/AnotaApi/AnotaApi.csproj`.

## Inicio rápido

```csharp
using Anota.Api;

var anota = new AnotaClient(Environment.GetEnvironmentVariable("ANOTA_API_KEY")!);

// Crea un formulario con un campo de texto y publícalo.
var form = await anota.CreateFormAsync(
    title: "Contáctanos",
    fields: new[] { new { type = "text", label = "Nombre", required = true } });
var formId = form!["id"]!.GetValue<string>();

await anota.PublishFormAsync(formId);

// Lee sus respuestas.
var submissions = await anota.ListSubmissionsAsync(formId);
Console.WriteLine(submissions!.ToJsonString());
```

Un recorrido completo de crear → agregar campo → publicar → enviar → listar está en
[`examples/EndToEnd`](examples/EndToEnd/Program.cs). Ejecútalo con:

```bash
ANOTA_API_KEY=anota_sk_... dotnet run --project examples/EndToEnd
```

## Autenticación

Crea una clave de API en tu workspace en https://anota.cloud/api-keys y pásala al
cliente. Las claves se ven como `anota_sk_…` y también habilitan el conector MCP de Claude.

```csharp
var anota = new AnotaClient("anota_sk_...");
// Opcionales: la URL base y un HttpClient personalizado (handlers, timeouts, DI).
var custom = new AnotaClient("anota_sk_...", baseUrl: "https://anota.cloud/api/v1", httpClient: myHttpClient);
```

Cada método devuelve `Task<JsonNode?>` — el JSON de la API analizado en un `JsonNode`
(`null` para respuestas vacías). Los parámetros `fields`, `field`, `rules`, `rule` y
`answers` son de tipo `object`, así que los objetos anónimos y los diccionarios se
serializan de forma natural.

## Todos los métodos

| # | Método | HTTP |
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

`fields`/`field` son objetos simples: `{ type, label, required?, options?, rows?, columns? }`.
`rules`/`rule`: `{ match: "all" | "any", if: [...], then: [...] }`. `answers` está indexado
por id de campo, con valores de tipo string o arreglo de strings.

## Errores

Las respuestas que no sean 2xx lanzan `AnotaApiError` con el código de estado HTTP
(`int Status`) y el mensaje del servidor (el `detail` de problem-details, con respaldo en
`title` y luego el cuerpo sin procesar). Los fallos de red se propagan como la excepción
nativa `HttpRequestException`, sin envolverse.

```csharp
try
{
    await anota.EditFieldAsync(formId, fieldId, new { label = "Nueva etiqueta" });
}
catch (AnotaApiError ex)
{
    Console.Error.WriteLine($"{ex.Status}: {ex.Message}");
}
```

Nota: una vez que un formulario se publica, sus campos existentes quedan bloqueados
(`EditFieldAsync`/`DeleteFieldAsync` devuelven 400); siempre puedes usar `AddFieldsAsync`.

## Licencia

MIT
