# Security Policy

## Supported versions

Security fixes are provided for the newest `0.1.x` beta release.

## Reporting a vulnerability

Do not open a public issue. Use GitHub's private vulnerability reporting for
`ViaPost-io/viapost-dotnet` or contact the security channel listed at
https://viapost.io/security. Do not include real API keys, message contents or personal data.

The SDK never logs credentials, rejects non-loopback plain HTTP, disables cookies and redirects in
its default transport, caps decoded responses and retries only safe reads. Consumers supplying a
custom `HttpClient` are responsible for applying equivalent handler controls and configuring its
own timeout no shorter than the SDK operation timeout.
