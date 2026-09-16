# Evidências de TDD

Plano de comportamentos para a versão `0.1.0`:

1. rejeitar HTTP fora de loopback;
2. autenticar com Bearer, identificar o SDK e enviar `Idempotency-Key` sem expor a chave;
3. repetir somente GET/HEAD em 429/5xx e respeitar `Retry-After`;
4. limitar a resposta decodificada a 8 MiB;
5. produzir erros tipados, com redaction da chave e `request_id`;
6. validar limites críticos do OpenAPI antes de acessar a rede;
7. expor os sete recursos beta com paths e paginação corretos;
8. propagar `CancellationToken` e aplicar timeout padrão de 60 segundos;
9. empacotar uma biblioteca consumível num projeto isolado.
10. expor detalhe e download raw de mensagens sem registrar conteúdo sensível;
11. cobrir atualização, entregas, teste, replay e rotação de webhooks;
12. rejeitar URLs privadas/loopback e redigir URLs/secrets em representações textuais;
13. desserializar o payload redigido de webhooks em uma allowlist tipada;
14. tornar retries de release independentes dos bytes não determinísticos de um novo `dotnet pack`;
15. publicar no NuGet somente o pacote exato de uma GitHub Release pública e atestada;
16. aceitar downloads raw válidos de até 40 MiB sem elevar o limite JSON de 8 MiB;
17. manter respostas de erro do endpoint raw sob o limite JSON, sem mascarar erros pequenos.

## Ciclos Red → Green → Refactor

O teste de cada comportamento é adicionado primeiro. Os comandos e seus resultados são registrados
neste arquivo durante a implementação.

- **Red inicial:** `dotnet test ViaPost.sln -c Release` falhou com `CS0234`/`CS0246`, pois
  `ViaPostClient`, os recursos e os modelos ainda não existiam.
- **Green do transporte e recursos:** após implementar o mínimo, os sete testes iniciais passaram.
- **Refactor:** o transporte foi centralizado, queries foram escapadas num único helper, erros foram
  separados por categoria e os DTOs receberam extensão tolerante para compatibilidade beta.
- **Red de integração dos recursos:** o inventário completo das rotas encontrou `?days=14?domain_id`
  ao compor uma segunda query; o helper passou a usar `&` quando a URL já contém `?` e toda a suíte
  voltou a ficar verde.
- **Red de paridade do contrato:** os testes falharam por ausência de `MessageDetail`, download raw,
  operações avançadas de webhooks e modelos associados; a implementação tipada e o transporte
  binário compartilhado fizeram os 24 testes passarem.
- **Red de segurança:** testes exigiram HTTPS público, bloquearam aliases de localhost e IPs não
  roteáveis e detectaram vazamento em `ToString()`; validação local e redaction fecharam os casos.
- **Red de contrato tipado:** o teste de uma entrega não compilou enquanto `payload_redacted` era um
  `JsonElement`; o DTO fechado passou a expor somente os cinco campos permitidos pelo OpenAPI.
- **Red de supply chain:** dois `dotnet pack` do mesmo snapshot produziram arquivos diferentes, e o
  workflow anterior comparava esses bytes em retries. O fluxo passou a verificar e reutilizar os
  assets já publicados, com checksum, source SHA e signer workflow fixados; o dispatch NuGet não
  recompila e rejeita drafts.
- **Red do limite raw:** um teste acima de 8 MiB não compilou enquanto não havia um limite próprio;
  o transporte passou a aplicar 40 MiB somente ao download RFC 5322 e manteve JSON em 8 MiB.
- **Red do limite de erro raw:** o endpoint aceitava até 40 MiB também em respostas de erro e um
  override raw baixo mascarava o erro tipado; respostas não bem-sucedidas agora usam o teto JSON.
- **Green final host-only:** build Release sem warnings, 31/31 testes, format, OpenAPI, auditoria
  NuGet, actionlint, package/checksum e consumer com cache NuGet vazio passaram. Docker permaneceu
  desativado por solicitação operacional e não foi usado.
