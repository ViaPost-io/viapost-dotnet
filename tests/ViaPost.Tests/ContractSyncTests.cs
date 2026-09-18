using System.Net;
using System.Text;
using System.Text.Json;
using ViaPost.Models;
using Xunit;

namespace ViaPost.Tests;

public sealed class ContractSyncTests
{
    [Fact]
    public async Task Expanded_contract_resources_use_every_typed_route()
    {
        var handler = new CaptureHandler();
        using var client = new ViaPostClient(
            new ViaPostClientOptions("vp_test_contract", new Uri("https://api.example.test")),
            new HttpClient(handler));
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var relatedId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var json = JsonDocument.Parse("{}").RootElement.Clone();

        await client.Contacts.ListAsync(new ContactListOptions("cursor", 25, "person"));
        await client.Contacts.CreateAsync(new CreateContactRequest("person@example.net"));
        await client.Contacts.GetAsync(id);
        await client.Contacts.UpdateAsync(id, new UpdateContactRequest { Subscribed = OptionalValue.From(false) });
        await client.Contacts.DeleteAsync(id);
        await client.Contacts.ImportAsync(Encoding.UTF8.GetBytes("email,first_name,last_name,subscribed,properties\nperson@example.net,,,true,{}\n"));

        await client.Events.ListAsync();
        await client.Events.CreateAsync(new CreateCustomEventRequest("purchase") { Schema = json });
        await client.Events.SendAsync(new SendCustomEventRequest("purchase", id) { Properties = json });
        await client.Events.UpdateAsync(id, new UpdateCustomEventRequest(json));
        await client.Events.DeleteAsync(id);

        await client.InboundMessages.ListAsync(new InboundMessageListOptions(DateTimeOffset.Parse("2026-09-16T00:00:00Z"), 20, id, "subject", "7d", true));
        await client.InboundMessages.GetAsync(id);
        await client.InboundMessages.GetRawAsync(id);

        await client.Segments.ListAsync(new SegmentListOptions("cursor", 30, "buyers"));
        await client.Segments.CreateAsync(new CreateSegmentRequest("Buyers") { Description = "Recent buyers" });
        await client.Segments.GetAsync(id);
        await client.Segments.UpdateAsync(id, new UpdateSegmentRequest { Description = OptionalValue.From<string?>(null) });
        await client.Segments.DeleteAsync(id);
        await client.Segments.ListContactsAsync(id, new ContactListOptions("cursor", 10, "person"));
        await client.Segments.AddContactAsync(id, new SegmentContactRequest(relatedId));
        await client.Segments.RemoveContactAsync(id, relatedId);
        await client.Segments.PreviewAsync(new SegmentPreviewRequest(JsonDocument.Parse("{\"operator\":\"all\",\"rules\":[]}").RootElement.Clone()) { Limit = 20 });

        await client.Suppressions.ListAsync(new SuppressionListOptions("cursor", 40, "person", "manual", "active", "manual"));
        await client.Suppressions.CreateAsync(new CreateSuppressionRequest("person@example.net", "manual") { Note = "Requested by customer" });
        await client.Suppressions.ImportAsync(Encoding.UTF8.GetBytes("email,reason,expires_at,note\nperson@example.net,manual,,requested\n"));
        await client.Suppressions.ExportAsync(new SuppressionExportOptions("person", "manual", "active", "manual"));
        await client.Suppressions.GetAsync(id, new SuppressionHistoryOptions("history-cursor", 75));
        await client.Suppressions.ReleaseAsync(id, new ReleaseSuppressionRequest(1, true, "Customer requested release"));

        await client.Themes.ListAsync();
        await client.Themes.CreateAsync(new CreateThemeRequest("Default", json));
        await client.Themes.DeleteAsync(id);
        await client.Domains.GetHealthAsync(id);
        await client.Domains.GetInboundAsync(id);
        await client.Messages.ListTimelineAsync(new MessageTimelineOptions("cursor", 10, "7d", "delivered", id));
        await client.Messages.CancelAsync(id);
        await client.Send.SendBatchAsync(new BatchSendRequest([new BatchSendMessage("batch-1", new SendEmailRequest("sender@example.net", ["person@example.net"]) { Text = "Hello" })]));

        Assert.Equal(
        [
            "GET /v1/contacts?cursor=cursor&limit=25&search=person",
            "POST /v1/contacts",
            "GET /v1/contacts/11111111-1111-1111-1111-111111111111",
            "PATCH /v1/contacts/11111111-1111-1111-1111-111111111111",
            "DELETE /v1/contacts/11111111-1111-1111-1111-111111111111",
            "POST /v1/contacts/import",
            "GET /v1/events",
            "POST /v1/events",
            "POST /v1/events/send",
            "PATCH /v1/events/11111111-1111-1111-1111-111111111111",
            "DELETE /v1/events/11111111-1111-1111-1111-111111111111",
            "GET /v1/inbound-messages?cursor=2026-09-16T00%3A00%3A00.0000000%2B00%3A00&limit=20&domain_id=11111111-1111-1111-1111-111111111111&search=subject&period=7d&has_attachments=true",
            "GET /v1/inbound-messages/11111111-1111-1111-1111-111111111111",
            "GET /v1/inbound-messages/11111111-1111-1111-1111-111111111111/raw",
            "GET /v1/segments?cursor=cursor&limit=30&search=buyers",
            "POST /v1/segments",
            "GET /v1/segments/11111111-1111-1111-1111-111111111111",
            "PATCH /v1/segments/11111111-1111-1111-1111-111111111111",
            "DELETE /v1/segments/11111111-1111-1111-1111-111111111111",
            "GET /v1/segments/11111111-1111-1111-1111-111111111111/contacts?cursor=cursor&limit=10&search=person",
            "POST /v1/segments/11111111-1111-1111-1111-111111111111/contacts",
            "DELETE /v1/segments/11111111-1111-1111-1111-111111111111/contacts/22222222-2222-2222-2222-222222222222",
            "POST /v1/segments/preview",
            "GET /v1/suppressions?cursor=cursor&limit=40&search=person&reason=manual&state=active&origin=manual",
            "POST /v1/suppressions",
            "POST /v1/suppressions/import",
            "GET /v1/suppressions/export?search=person&reason=manual&state=active&origin=manual",
            "GET /v1/suppressions/11111111-1111-1111-1111-111111111111?history_cursor=history-cursor&history_limit=75",
            "POST /v1/suppressions/11111111-1111-1111-1111-111111111111/release",
            "GET /v1/themes",
            "POST /v1/themes",
            "DELETE /v1/themes/11111111-1111-1111-1111-111111111111",
            "GET /v1/domains/11111111-1111-1111-1111-111111111111/health",
            "GET /v1/domains/11111111-1111-1111-1111-111111111111/inbound",
            "GET /v1/messages/events?cursor=cursor&limit=10&period=7d&type=delivered&message_id=11111111-1111-1111-1111-111111111111",
            "POST /v1/messages/11111111-1111-1111-1111-111111111111/cancel",
            "POST /v1/send/batch"
        ], handler.Requests);
        Assert.Equal("text/csv", handler.ContentTypes["/v1/contacts/import"]);
        Assert.Equal("text/csv", handler.ContentTypes["/v1/suppressions/import"]);
        Assert.Equal("text/csv", handler.AcceptTypes["/v1/suppressions/export?search=person&reason=manual&state=active&origin=manual"]);
    }

    [Fact]
    public async Task Explicit_null_is_serialized_for_patch_fields()
    {
        var handler = new CaptureHandler();
        using var client = new ViaPostClient(
            new ViaPostClientOptions("vp_test_optional", new Uri("https://api.example.test")),
            new HttpClient(handler));

        await client.Contacts.UpdateAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new UpdateContactRequest { FirstName = OptionalValue.From<string?>(null) });

        Assert.Equal("{\"first_name\":null}", handler.Bodies.Single());
    }

    [Fact]
    public void Expanded_contract_models_redact_personal_and_operational_data()
    {
        var contact = new Contact { Email = "person@example.net", FirstName = "Private" };
        var inbound = new InboundMessageDetail
        {
            FromAddress = "sender@example.net",
            ToAddress = "recipient@example.net",
            Subject = "Private subject",
            BodyPlain = "Private body",
            RawMessageUrl = new Uri("https://storage.example.test/secret")
        };
        var attachment = new InboundAttachment
        {
            Filename = "customer-record.pdf",
            DownloadUrl = new Uri("https://storage.example.test/attachment")
        };
        var release = new ReleaseSuppressionRequest(1, true, "Private operational justification");
        var history = new SuppressionHistoryEvent { Justification = "Private audit justification" };

        Assert.DoesNotContain("person@example.net", contact.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private", contact.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sender@example.net", inbound.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("recipient@example.net", inbound.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private subject", inbound.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private body", inbound.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("storage.example.test", inbound.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("customer-record.pdf", attachment.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("storage.example.test", attachment.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private operational justification", release.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private audit justification", history.ToString(), StringComparison.Ordinal);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public Dictionary<string, string?> ContentTypes { get; } = [];
        public Dictionary<string, string?> AcceptTypes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add($"{request.Method.Method} {path}");
            AcceptTypes[path] = request.Headers.Accept.SingleOrDefault()?.MediaType;
            if (request.Content is not null)
            {
                ContentTypes[path] = request.Content.Headers.ContentType?.MediaType;
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            var contentType = path.Contains("/raw", StringComparison.Ordinal) ? "message/rfc822"
                : path.Contains("/suppressions/export", StringComparison.Ordinal) ? "text/csv"
                : "application/json";
            var body = contentType == "application/json" ? "{}" : "content";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType)
            };
        }
    }
}
