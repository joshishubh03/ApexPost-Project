# ADR 0028: Trust forwarded headers only from configured proxies

- Status: Accepted
- Date: 2026-10-25

## Context

The administrative and partner allowlists use `HttpContext.Connection.RemoteIpAddress`. In deployments behind a reverse proxy, the socket address is the proxy unless trusted forwarded headers are processed first. Accepting caller-controlled forwarding headers would let a direct client spoof an allowlisted address.

## Decision

Register `ForwardedHeadersMiddleware` before correlation, request logging, HTTPS redirection, and IP restrictions. Process only `X-Forwarded-For` and `X-Forwarded-Proto`, with a one-hop limit. Clear the framework's default trusted network and proxy entries, then add only exact IP addresses from `Security:ForwardedHeaders:KnownProxies`. The default list is empty, so forwarded headers are ignored until an operator configures the actual proxy address. Do not process `X-Forwarded-Host`.

## Alternatives considered

- Trust all forwarding headers: rejected because a client could forge the address used by the IP allowlists.
- Trust a broad private network: rejected because unrelated hosts on that network could be treated as the proxy.
- Leave proxy support undocumented and rely on socket addresses: rejected because allowlists would identify the proxy rather than the originating client.

## Consequences

A direct local deployment needs no forwarded-header configuration. A proxied deployment must configure every immediate proxy IP and ensure the proxy replaces or safely appends forwarding headers. The one-hop setting is intentional for the current deployment shape; a multi-proxy topology requires an explicit reviewed change to both the trust list and hop limit. Integration tests verify both accepted forwarding from a configured proxy and rejection of spoofed forwarding from an untrusted source.
