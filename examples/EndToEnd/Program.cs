using Anota.Api;

// End-to-end walkthrough of the anota API:
//   create a form -> add a field -> publish -> create a submission -> list submissions.
//
// Run with your API key in the environment:
//   ANOTA_API_KEY=anota_sk_... dotnet run --project examples/EndToEnd

var apiKey = Environment.GetEnvironmentVariable("ANOTA_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Set ANOTA_API_KEY (create a key at https://anota.cloud/api-keys).");
    return 1;
}

var anota = new AnotaClient(apiKey);

Console.WriteLine("1. Creating a form...");
var form = await anota.CreateFormAsync(
    title: "Contact us",
    fields: new[]
    {
        new { type = "text", label = "Name", required = true },
    },
    description: "Created by the C# SDK end-to-end example");
var formId = form!["id"]!.GetValue<string>();
Console.WriteLine($"   form id: {formId}");

Console.WriteLine("2. Adding an email field...");
var withField = await anota.AddFieldsAsync(formId, new[]
{
    new { type = "email", label = "Email", required = true },
});
// The newly added field's id is what a submission answers against.
var emailFieldId = withField!["fields"]!.AsArray()[^1]!["id"]!.GetValue<string>();
Console.WriteLine($"   field id: {emailFieldId}");

Console.WriteLine("3. Publishing the form...");
await anota.PublishFormAsync(formId);

Console.WriteLine("4. Creating a submission...");
var submission = await anota.CreateSubmissionAsync(formId, new Dictionary<string, object>
{
    [emailFieldId] = "ada@example.com",
});
Console.WriteLine($"   submission id: {submission!["id"]!.GetValue<string>()}");

Console.WriteLine("5. Listing submissions...");
var submissions = await anota.ListSubmissionsAsync(formId, page: 1, pageSize: 25);
Console.WriteLine(submissions!.ToJsonString());

return 0;
