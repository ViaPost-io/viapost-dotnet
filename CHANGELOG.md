# Changelog

## [0.2.1] - 2026-09-21

- Sincroniza o bundle OpenAPI 3.1 com o contrato público publicado, incluindo importação de
  contatos, domínios de tracking, segmentos de audiência, configurações de inbound e fundações de
  broadcasts. Esta atualização de artefato não declara novos métodos tipados que ainda não foram
  implementados pelo cliente.

## [0.2.0] - 2026-09-16

- Sincroniza a superfície autenticada com o contrato público para contatos, eventos personalizados,
  mensagens recebidas, segmentos, supressões e temas.
- Adiciona importação/exportação CSV de supressões, downloads raw de inbound e campos PATCH capazes
  de diferenciar ausência de `null` explícito.
- Torna o monitor de OpenAPI semântico e registra a origem, os hashes do snapshot e da representação
  publicada.
- Documenta a exclusão intencional dos endpoints anônimos de health, status e inscrições para nunca
  encaminhar API keys ao domínio da página pública.
- Separa os limites configuráveis de downloads RFC 5322 e exportações CSV, mantendo JSON e corpos
  de erro em 8 MiB e impondo teto defensivo de 128 MiB aos conteúdos brutos.
- Impede serialização/log acidental da API key e encapsula secrets de webhook em acesso explícito
  com serialização redigida.
- Endurece o downloader de contrato com HTTPS, timeout, corpo limitado e redirects same-origin.

## [0.1.0] - 2026-09-16

- Primeiro beta oficial para .NET 8+.
- Recursos de envio, mensagens, domínios, templates, webhooks, automações e uso.
- Detalhe de mensagem com conteúdo autorizado e download do arquivo RFC 5322 submetido.
- Atualização de webhooks, inspeção de entregas, teste, replay e rotação de secret.
- Transporte endurecido, erros tipados, retries seguros, timeout e limite de resposta.
- Distribuição GitHub-first com NuGet opcional apenas manual.
