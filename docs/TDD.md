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
