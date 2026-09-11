using System.Net;
using System.Text;
using System.Text.Json;
using ViaPost.Models;
using Xunit;

namespace ViaPost.Tests;

public sealed class ResourceRoutesTests
{
    [Fact]
    public async Task All_beta_resource_operations_use_the_public_contract_routes()
    {
        var handler = new CaptureAllHandler();
        using var client = new ViaPostClient(
            new ViaPostClientOptions("vp_test_routes", new Uri("https://api.example.test")),
            new HttpClient(handler));
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var versionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var graph = JsonDocument.Parse("{}").RootElement.Clone();

        await client.Messages.GetAsync(id);
        await client.Messages.ListEventsAsync(id);
        await client.Messages.GetEngagementAsync(14);
        await client.Messages.GetTimeseriesAsync(14);
        await client.Messages.GetMetricsAsync(14, id);

        await client.Domains.CreateAsync(new CreateDomainRequest("example.com"));
        await client.Domains.GetAsync(id);
        await client.Domains.GetDnsAsync(id);
        await client.Domains.VerifyAsync(id);
        await client.Domains.RotateDkimAsync(id);
        await client.Domains.DeleteAsync(id);

        await client.Templates.CreateAsync(new CreateTemplateRequest("Welcome"));
        await client.Templates.GetAsync(id);
        await client.Templates.CreateAssetAsync(id, new CreateTemplateAssetRequest("logo.png", "image/png"));
        await client.Templates.UpdateDraftAsync(id, new UpdateTemplateDraftRequest(graph, []));
        await client.Templates.DuplicateAsync(id);
        await client.Templates.PreviewAsync(id);
        await client.Templates.PublishAsync(id);
        await client.Templates.ListVersionsAsync(id);
        await client.Templates.GetVersionAsync(id, versionId);
        await client.Templates.RevertAsync(id, versionId);
        await client.Templates.ArchiveAsync(id);
        await client.Templates.DeleteAsync(id);

        await client.Webhooks.CreateAsync(new CreateWebhookRequest(new Uri("https://hooks.example.test/inbound"), ["message.delivered"]));
        await client.Webhooks.DeleteAsync(id);

        await client.Automations.CreateAsync(new CreateAutomationRequest("Onboarding"));
        await client.Automations.GetAsync(id);
        await client.Automations.RenameAsync(id, new RenameAutomationRequest("Renamed"));
        await client.Automations.UpdateDraftAsync(id, new UpdateAutomationDraftRequest(graph));
        await client.Automations.ActivateAsync(id);
        await client.Automations.DisableAsync(id);
        await client.Automations.DuplicateAsync(id);
        await client.Automations.ListRunsAsync(id, new AutomationRunListOptions("cursor", 20, "running"));
        await client.Automations.GetRunAsync(id, versionId);
        await client.Automations.CancelRunAsync(id, versionId);
        await client.Automations.DeleteAsync(id);

        Assert.Equal(
        [
            "GET /v1/messages/11111111-1111-1111-1111-111111111111",
            "GET /v1/messages/11111111-1111-1111-1111-111111111111/events",
            "GET /v1/messages/engagement?days=14",
            "GET /v1/messages/timeseries?days=14",
            "GET /v1/messages/metrics?days=14&domain_id=11111111-1111-1111-1111-111111111111",
            "POST /v1/domains", "GET /v1/domains/11111111-1111-1111-1111-111111111111",
            "GET /v1/domains/11111111-1111-1111-1111-111111111111/dns",
            "POST /v1/domains/11111111-1111-1111-1111-111111111111/verify",
            "POST /v1/domains/11111111-1111-1111-1111-111111111111/dkim/rotate",
            "DELETE /v1/domains/11111111-1111-1111-1111-111111111111",
            "POST /v1/templates", "GET /v1/templates/11111111-1111-1111-1111-111111111111",
            "POST /v1/templates/11111111-1111-1111-1111-111111111111/assets",
            "PATCH /v1/templates/11111111-1111-1111-1111-111111111111/draft",
            "POST /v1/templates/11111111-1111-1111-1111-111111111111/duplicate",
            "POST /v1/templates/11111111-1111-1111-1111-111111111111/preview",
            "POST /v1/templates/11111111-1111-1111-1111-111111111111/publish",
            "GET /v1/templates/11111111-1111-1111-1111-111111111111/versions",
            "GET /v1/templates/11111111-1111-1111-1111-111111111111/versions/22222222-2222-2222-2222-222222222222",
            "POST /v1/templates/11111111-1111-1111-1111-111111111111/versions/22222222-2222-2222-2222-222222222222/revert",
            "POST /v1/templates/11111111-1111-1111-1111-111111111111/archive",
            "DELETE /v1/templates/11111111-1111-1111-1111-111111111111",
            "POST /v1/webhooks", "DELETE /v1/webhooks/11111111-1111-1111-1111-111111111111",
            "POST /v1/automations", "GET /v1/automations/11111111-1111-1111-1111-111111111111",
            "PATCH /v1/automations/11111111-1111-1111-1111-111111111111",
            "PATCH /v1/automations/11111111-1111-1111-1111-111111111111/draft",
            "POST /v1/automations/11111111-1111-1111-1111-111111111111/activate",
            "POST /v1/automations/11111111-1111-1111-1111-111111111111/disable",
            "POST /v1/automations/11111111-1111-1111-1111-111111111111/duplicate",
            "GET /v1/automations/11111111-1111-1111-1111-111111111111/runs?cursor=cursor&limit=20&status=running",
            "GET /v1/automations/11111111-1111-1111-1111-111111111111/runs/22222222-2222-2222-2222-222222222222",
            "POST /v1/automations/11111111-1111-1111-1111-111111111111/runs/22222222-2222-2222-2222-222222222222/cancel",
            "DELETE /v1/automations/11111111-1111-1111-1111-111111111111"
        ], handler.Requests);
    }

    private sealed class CaptureAllHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method.Method} {request.RequestUri!.PathAndQuery}");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
