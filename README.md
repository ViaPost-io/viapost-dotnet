# ViaPost .NET SDK

SDK oficial server-side e assíncrono para a API pública do ViaPost. A versão beta `0.1.x` exige
.NET 8 ou superior e usa somente `HttpClient` e `System.Text.Json` no runtime.

> Beta: a API pública do pacote pode receber ajustes compatíveis antes de `1.0.0`.

## Instalação pelo GitHub Releases

O GitHub é o canal principal e não exige token para downloads públicos:

```bash
mkdir -p packages
curl -fL -o packages/ViaPost.0.1.0.nupkg \
  https://github.com/ViaPost-io/viapost-dotnet/releases/download/v0.1.0/ViaPost.0.1.0.nupkg
dotnet add package ViaPost --version 0.1.0 --source ./packages
```

Confira `SHA256SUMS` e a attestation da release antes de promover em produção. A publicação no
NuGet.org é opcional e só pode ser disparada manualmente pelos mantenedores.

## Enviar e-mail

```csharp
using ViaPost;
using ViaPost.Models;

using var viapost = new ViaPostClient(Environment.GetEnvironmentVariable("VIAPOST_API_KEY")!);

var result = await viapost.Send.SendAsync(
    new SendEmailRequest("hello@example.com", ["customer@example.net"])
    {
        Subject = "Olá do ViaPost",
        Html = "<strong>Bem-vindo!</strong>",
        Text = "Bem-vindo!"
    },
    idempotencyKey: Guid.NewGuid().ToString(),
    cancellationToken: CancellationToken.None);
```

## Recursos

- `Send`: envio com `Idempotency-Key` opcional;
- `Messages`: listagem, detalhe, eventos, engajamento, métricas e série temporal;
- `Domains`: cadastro, DNS, verificação e rotação DKIM;
- `Templates`: drafts, preview, publicação, versões, assets e reversão;
- `Webhooks`: listagem, criação e remoção;
- `Automations`: CRUD, ativação, drafts, execuções e cancelamento;
- `Usage`: consumo e limite mensal.

Todos os métodos aceitam `CancellationToken`. O timeout padrão é 60 segundos. GET/HEAD podem ser
repetidos até três vezes em `429`/`5xx`, respeitando `Retry-After`; mutações nunca são repetidas.
Respostas decodificadas são limitadas a 8 MiB.

## Configuração segura

```csharp
var options = new ViaPostClientOptions(
    Environment.GetEnvironmentVariable("VIAPOST_API_KEY")!,
    new Uri("https://api.viapost.io"))
{
    Timeout = TimeSpan.FromSeconds(60),
    MaximumResponseBytes = 8 * 1024 * 1024
};
using var viapost = new ViaPostClient(options);
```

HTTPS é obrigatório, exceto em loopback para desenvolvimento. O transporte padrão desabilita
cookies e redirects, restringe TLS a 1.2/1.3 e não inclui a API key em mensagens de erro.

## English quickstart

ViaPost's official async server-side SDK targets .NET 8+. Install the `.nupkg` from the public
GitHub Release as shown above, create `ViaPostClient` with `VIAPOST_API_KEY`, and call the typed
resource methods. Every operation accepts `CancellationToken`; only safe reads are retried.

## Contrato

`openapi.yaml` é um bundle imutável do contrato público no commit
`1daaf57b8c8bb7481b7c8633a68705428de1f90a`, SHA-256
`d1f223342ad1ca326ba716af6e508c78594e1b108958cce2ec4a1efd31a9773a`.

## Desenvolvimento

```bash
dotnet restore ViaPost.sln --locked-mode
dotnet build ViaPost.sln -c Release --no-restore
dotnet test ViaPost.sln -c Release --no-build
dotnet pack src/ViaPost/ViaPost.csproj -c Release --no-build -o artifacts
scripts/check-openapi.sh
```

Consulte [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md) e as
[evidências de TDD](docs/TDD.md).
