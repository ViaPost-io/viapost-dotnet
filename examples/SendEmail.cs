using ViaPost;
using ViaPost.Models;

using var client = new ViaPostClient(Environment.GetEnvironmentVariable("VIAPOST_API_KEY")!);
var response = await client.Send.SendAsync(
    new SendEmailRequest("hello@example.com", ["customer@example.net"])
    {
        Subject = "ViaPost .NET",
        Text = "Mensagem enviada pelo SDK oficial."
    },
    Guid.NewGuid().ToString());

Console.WriteLine($"Accepted: {response.Accepted?.Count ?? 0}");
