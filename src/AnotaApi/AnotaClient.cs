using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Anota.Api;

/// <summary>
/// A thin, dependency-free client over the anota REST API. Every method maps to one
/// endpoint and returns the parsed JSON response as a <see cref="JsonNode"/> (or
/// <c>null</c> for empty bodies). Non-2xx responses throw <see cref="AnotaApiError"/>.
/// </summary>
public sealed class AnotaClient
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <param name="apiKey">Your workspace API key (create one at https://anota.cloud/api-keys).</param>
    /// <param name="baseUrl">Override the API base URL. Defaults to https://anota.cloud/api/v1.</param>
    /// <param name="httpClient">Inject an <see cref="HttpClient"/> (for testing or custom handlers).</param>
    public AnotaClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("apiKey is required (create one at https://anota.cloud/api-keys)", nameof(apiKey));
        _apiKey = apiKey;
        _baseUrl = (baseUrl ?? "https://anota.cloud/api/v1").TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient();
    }

    private async Task<JsonNode?> RequestAsync(
        HttpMethod method,
        string path,
        object? body = null,
        IDictionary<string, string?>? query = null)
    {
        var url = _baseUrl + path;
        if (query is not null)
        {
            var parts = new List<string>();
            foreach (var (key, value) in query)
                if (value is not null)
                    parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            if (parts.Count > 0)
                url += "?" + string.Join("&", parts);
        }

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = text;
            try
            {
                if (JsonNode.Parse(text) is JsonObject problem)
                    message = problem["detail"]?.GetValue<string>()
                              ?? problem["title"]?.GetValue<string>()
                              ?? text;
            }
            catch (JsonException) { /* non-JSON body: keep raw text */ }
            throw new AnotaApiError((int)response.StatusCode, message);
        }

        return string.IsNullOrEmpty(text) ? null : JsonNode.Parse(text);
    }

    // ----- forms -----

    /// <summary>List all forms in the workspace.</summary>
    public Task<JsonNode?> ListFormsAsync() => RequestAsync(HttpMethod.Get, "/forms");

    /// <summary>Create a form with a title, an initial set of fields, and an optional description.</summary>
    public Task<JsonNode?> CreateFormAsync(string title, object fields, string? description = null) =>
        RequestAsync(HttpMethod.Post, "/forms", new { title, fields, description });

    /// <summary>Get a single form by id.</summary>
    public Task<JsonNode?> GetFormAsync(string formId) =>
        RequestAsync(HttpMethod.Get, $"/forms/{formId}");

    /// <summary>Append one or more fields to a form.</summary>
    public Task<JsonNode?> AddFieldsAsync(string formId, object fields) =>
        RequestAsync(HttpMethod.Post, $"/forms/{formId}/fields", new { fields });

    /// <summary>Edit an existing field. Fields are locked once the form is published.</summary>
    public Task<JsonNode?> EditFieldAsync(string formId, string fieldId, object field) =>
        RequestAsync(HttpMethod.Patch, $"/forms/{formId}/fields/{Uri.EscapeDataString(fieldId)}", new { field });

    /// <summary>Delete a field. Fields are locked once the form is published.</summary>
    public Task<JsonNode?> DeleteFieldAsync(string formId, string fieldId) =>
        RequestAsync(HttpMethod.Delete, $"/forms/{formId}/fields/{Uri.EscapeDataString(fieldId)}");

    /// <summary>Publish a form, making it available to accept submissions.</summary>
    public Task<JsonNode?> PublishFormAsync(string formId) =>
        RequestAsync(HttpMethod.Post, $"/forms/{formId}/publish");

    /// <summary>Change a form's title.</summary>
    public Task<JsonNode?> RenameFormAsync(string formId, string title) =>
        RequestAsync(HttpMethod.Patch, $"/forms/{formId}", new { title });

    /// <summary>Set the PDF template (by storage key) used when exporting submissions as PDF.</summary>
    public Task<JsonNode?> SetPdfTemplateAsync(string formId, string key) =>
        RequestAsync(HttpMethod.Put, $"/forms/{formId}/pdf-template", new { key });

    /// <summary>Delete a form and its submissions.</summary>
    public Task<JsonNode?> DeleteFormAsync(string formId) =>
        RequestAsync(HttpMethod.Delete, $"/forms/{formId}");

    /// <summary>Clone a form (fields and logic) into a new draft.</summary>
    public Task<JsonNode?> CloneFormAsync(string formId) =>
        RequestAsync(HttpMethod.Post, $"/forms/{formId}/clone");

    // ----- logic rules -----

    /// <summary>Add one or more conditional-logic rules to a form.</summary>
    public Task<JsonNode?> AddLogicRulesAsync(string formId, object rules) =>
        RequestAsync(HttpMethod.Post, $"/forms/{formId}/logic-rules", new { rules });

    /// <summary>Replace an existing logic rule.</summary>
    public Task<JsonNode?> EditLogicRuleAsync(string formId, string ruleId, object rule) =>
        RequestAsync(HttpMethod.Put, $"/forms/{formId}/logic-rules/{Uri.EscapeDataString(ruleId)}", new { rule });

    /// <summary>Delete a logic rule.</summary>
    public Task<JsonNode?> DeleteLogicRuleAsync(string formId, string ruleId) =>
        RequestAsync(HttpMethod.Delete, $"/forms/{formId}/logic-rules/{Uri.EscapeDataString(ruleId)}");

    // ----- submissions -----

    /// <summary>List a form's submissions, paged, optionally filtered by status.</summary>
    public Task<JsonNode?> ListSubmissionsAsync(string formId, int page = 1, int pageSize = 25, string? status = null) =>
        RequestAsync(HttpMethod.Get, $"/forms/{formId}/submissions", query: new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["status"] = status,
        });

    /// <summary>Get a single submission by id.</summary>
    public Task<JsonNode?> GetSubmissionAsync(string submissionId) =>
        RequestAsync(HttpMethod.Get, $"/submissions/{submissionId}");

    /// <summary>Create a submission. <paramref name="answers"/> is keyed by field id; values are string or string[].</summary>
    public Task<JsonNode?> CreateSubmissionAsync(string formId, object answers) =>
        RequestAsync(HttpMethod.Post, $"/forms/{formId}/submissions", new { answers });

    /// <summary>Set a submission's status (e.g. New, Read, Flagged, Spam).</summary>
    public Task<JsonNode?> SetSubmissionStatusAsync(string submissionId, string status) =>
        RequestAsync(HttpMethod.Patch, $"/submissions/{submissionId}/status", new { status });

    /// <summary>Delete a submission.</summary>
    public Task<JsonNode?> DeleteSubmissionAsync(string submissionId) =>
        RequestAsync(HttpMethod.Delete, $"/submissions/{submissionId}");

    /// <summary>Get aggregate submission statistics for a form.</summary>
    public Task<JsonNode?> SubmissionStatsAsync(string formId) =>
        RequestAsync(HttpMethod.Get, $"/forms/{formId}/stats");

    // ----- templates -----

    /// <summary>List published form templates for the given language (es, en, pt).</summary>
    public Task<JsonNode?> ListTemplatesAsync(string language = "es") =>
        RequestAsync(HttpMethod.Get, "/templates", query: new Dictionary<string, string?> { ["language"] = language });

    /// <summary>Create a new form from a template.</summary>
    public Task<JsonNode?> CreateFormFromTemplateAsync(string templateId) =>
        RequestAsync(HttpMethod.Post, $"/forms/from-template/{templateId}");

    // ----- webhooks -----

    /// <summary>List the webhooks registered on a form.</summary>
    public Task<JsonNode?> ListWebhooksAsync(string formId) =>
        RequestAsync(HttpMethod.Get, $"/forms/{formId}/webhooks");

    /// <summary>Register a webhook URL that receives <c>submission.created</c> events.</summary>
    public Task<JsonNode?> AddWebhookAsync(string formId, string url) =>
        RequestAsync(HttpMethod.Post, $"/forms/{formId}/webhooks", new { url });

    /// <summary>Delete a webhook.</summary>
    public Task<JsonNode?> DeleteWebhookAsync(string formId, string webhookId) =>
        RequestAsync(HttpMethod.Delete, $"/forms/{formId}/webhooks/{webhookId}");
}
