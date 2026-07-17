using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;
using Anota.Api;

namespace Anota.Api.Tests;

/// <summary>
/// A fake HttpMessageHandler that records the last request it received (including
/// the buffered request body) and returns a caller-supplied canned response.
/// </summary>
public sealed class RecordingHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _responseBody;
    private readonly string? _contentType;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public RecordingHandler(HttpStatusCode status = HttpStatusCode.OK, string responseBody = "{}", string? contentType = "application/json")
    {
        _status = status;
        _responseBody = responseBody;
        _contentType = contentType;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        var response = new HttpResponseMessage(_status)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, _contentType ?? "application/json"),
        };
        return response;
    }
}

public class AnotaClientTests
{
    private static AnotaClient Client(RecordingHandler handler) =>
        new("anota_sk_test", "https://anota.cloud/api/v1", new HttpClient(handler));

    // (a) listForms sends GET {base}/forms with the Bearer header
    [Fact]
    public async Task ListForms_SendsGetWithBearerHeader()
    {
        var handler = new RecordingHandler(responseBody: "[]");
        await Client(handler).ListFormsAsync();

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("https://anota.cloud/api/v1/forms", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("anota_sk_test", handler.LastRequest.Headers.Authorization.Parameter);
    }

    // (b) createSubmission sends the JSON body {"answers":{"f_1":"hola"}}
    [Fact]
    public async Task CreateSubmission_SendsJsonBody()
    {
        var handler = new RecordingHandler();
        var answers = new Dictionary<string, object> { ["f_1"] = "hola" };
        await Client(handler).CreateSubmissionAsync("form_1", answers);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://anota.cloud/api/v1/forms/form_1/submissions", handler.LastRequest.RequestUri!.ToString());

        var body = JsonNode.Parse(handler.LastRequestBody!)!;
        Assert.Equal("hola", body["answers"]!["f_1"]!.GetValue<string>());
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    // (c) a 400 problem-details response rejects with AnotaApiError(status=400, message="Error: bad")
    [Fact]
    public async Task ErrorResponse_ThrowsAnotaApiErrorWithStatusAndDetail()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, "{\"detail\":\"Error: bad\"}");

        var ex = await Assert.ThrowsAsync<AnotaApiError>(() => Client(handler).ListFormsAsync());
        Assert.Equal(400, ex.Status);
        Assert.Equal("Error: bad", ex.Message);
    }

    // (d) listSubmissions(id, 2, 10, "New") builds ?page=2&pageSize=10&status=New
    [Fact]
    public async Task ListSubmissions_BuildsQueryString()
    {
        var handler = new RecordingHandler(responseBody: "{}");
        await Client(handler).ListSubmissionsAsync("form_1", 2, 10, "New");

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/api/v1/forms/form_1/submissions", uri.AbsolutePath);
        var query = uri.Query;
        Assert.Contains("page=2", query);
        Assert.Contains("pageSize=10", query);
        Assert.Contains("status=New", query);
    }

    // status omitted when null (default overload path)
    [Fact]
    public async Task ListSubmissions_OmitsStatusWhenNull()
    {
        var handler = new RecordingHandler(responseBody: "{}");
        await Client(handler).ListSubmissionsAsync("form_1");

        Assert.DoesNotContain("status=", handler.LastRequest!.RequestUri!.Query);
    }
}
