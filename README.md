# ViaPost .NET SDK

SDK oficial server-side e assíncrono para a API pública autenticada do ViaPost. A versão beta `0.2.x` exige
.NET 8 ou superior e usa somente `HttpClient` e `System.Text.Json` no runtime.

> Beta: a API pública do pacote pode receber ajustes compatíveis antes de `1.0.0`.

## Instalação pelo GitHub Releases

O GitHub é o canal principal e não exige token para downloads públicos:

```bash
mkdir -p packages
gh release download v0.2.0 --repo ViaPost-io/viapost-dotnet --dir packages \
  --pattern 'ViaPost.0.2.0.nupkg' --pattern 'ViaPost.0.2.0.snupkg' --pattern SHA256SUMS
(cd packages && sha256sum -c SHA256SUMS)
source_sha="$(gh api repos/ViaPost-io/viapost-dotnet/commits/v0.2.0 --jq .sha)"
gh attestation verify packages/ViaPost.0.2.0.nupkg \
  --repo ViaPost-io/viapost-dotnet \
  --source-digest "$source_sha" \
  --signer-workflow ViaPost-io/viapost-dotnet/.github/workflows/release.yml
dotnet add package ViaPost --version 0.2.0 --source ./packages
```

O exemplo valida checksum e proveniência antes da instalação. No macOS sem `sha256sum`, use
`shasum -a 256 -c SHA256SUMS`. A publicação no NuGet.org é opcional e só pode ser disparada
manualmente pelos mantenedores.

Mantenedores publicam no NuGet somente a partir da própria tag, depois da aprovação do ambiente
protegido `nuget`; o workflow baixa e republica o artefato já atestado, sem recompilar:

```bash
gh workflow run release.yml --repo ViaPost-io/viapost-dotnet --ref v0.2.0 \
  -f tag=v0.2.0 -f publish_nuget=true
```

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
- `Messages`: listagem, detalhe com conteúdo autorizado, download RFC 5322, eventos, engajamento, métricas e série temporal;
- `Domains`: cadastro, DNS, verificação e rotação DKIM;
- `Templates`: drafts, preview, publicação, versões, assets e reversão;
- `Webhooks`: listagem, criação, atualização, remoção, entregas, teste, replay e rotação de secret;
- `Automations`: CRUD, ativação, drafts, execuções e cancelamento;
- `Contacts`: CRUD e paginação da audiência;
- `Events`: catálogo e envio de eventos personalizados;
- `InboundMessages`: listagem, detalhe autenticado e download RFC 5322;
- `Segments`: CRUD, paginação e associação de contatos;
- `Suppressions`: CRUD operacional, histórico, importação e exportação CSV;
- `Themes`: listagem, criação e remoção;
- `Usage`: consumo e limite mensal.

Todos os métodos aceitam `CancellationToken`. O timeout padrão é 60 segundos. GET/HEAD podem ser
repetidos até três vezes em `429`/`5xx`, respeitando `Retry-After`; mutações nunca são repetidas.
Respostas JSON decodificadas e corpos de erro são limitados a 8 MiB. Downloads raw RFC 5322 e
exportações CSV usam limites independentes de 40 MiB. `MaximumRawMessageBytes` e
`MaximumExportBytes` podem ser reduzidos conforme a aplicação, mas nunca ultrapassam o teto
defensivo de 128 MiB.

### Endpoints públicos de status

`/v1/health`, `/v1/status` e `/v1/status/subscriptions*` são endpoints anônimos da página pública,
não recursos de integração autenticada por API key. Por isso eles não são expostos por
`ViaPostClient`: o cliente sempre envia o Bearer somente para `https://api.viapost.io` (ou para a
base explicitamente configurada) e nunca encaminha a credencial para `status.viapost.io`. Para
inscrições humanas, use [status.viapost.io](https://status.viapost.io/).

## Configuração segura

```csharp
var options = new ViaPostClientOptions(
    Environment.GetEnvironmentVariable("VIAPOST_API_KEY")!,
    new Uri("https://api.viapost.io"))
{
    Timeout = TimeSpan.FromSeconds(60),
    MaximumResponseBytes = 8 * 1024 * 1024,
    MaximumRawMessageBytes = 40 * 1024 * 1024,
    MaximumExportBytes = 40 * 1024 * 1024
};
using var viapost = new ViaPostClient(options);
```

HTTPS é obrigatório, exceto em loopback para desenvolvimento. O transporte padrão desabilita
cookies e redirects, restringe TLS a 1.2/1.3 e não inclui a API key em mensagens de erro,
serialização ou representação textual. Secrets retornados uma única vez por criação/rotação de
webhook usam `SensitiveString`: chame `Reveal()` explicitamente para armazená-los no destino seguro;
serialização e logs comuns recebem somente `[REDACTED]`.

## English quickstart

ViaPost's official async server-side SDK targets .NET 8+. Install the `.nupkg` from the public
GitHub Release as shown above, create `ViaPostClient` with `VIAPOST_API_KEY`, and call the typed
resource methods. Every operation accepts `CancellationToken`; only safe reads are retried.

## Contrato

`openapi.yaml` é um bundle imutável do contrato público no commit
`891adebbe79a26178fb780ec986172c890a5e261`, SHA-256
`cb61b81b3276679426504eae4161e610eb5520aca2cd71cd267ed62628c518e4`. A representação publicada
em `https://docs.viapost.io/openapi/public.yaml` tem SHA-256
`f1b1fc0f198a2b0b36f0e893515dad191d6bb7d139fcf1e942c036bfa2f5169b` e é comparada
semanticamente, pois a serialização YAML publicada é deliberadamente diferente. Os metadados
auditáveis estão em `openapi-source.json`.

O monitor baixa esse contrato somente por HTTPS, com timeout, corpo máximo de 8 MiB e no máximo
três redirects na mesma origem.

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
