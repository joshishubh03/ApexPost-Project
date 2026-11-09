# Security - ApexPost.Api and the courier app (Week 4 Part D)

## 1. Authentication design (JWT bearer)

- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` validates the **issuer**,
  **audience**, **lifetime** (30 s clock skew) and **signing key** of every token. The values come from
  configuration:

  | Setting | Where |
  |---|---|
  | `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` (not secret) |
  | `Jwt:SigningKey` (Base64, at least 32 bytes) | **User Secrets** in Development, a secret store in production. Startup fails if it is missing. |

  ```powershell
  dotnet user-secrets set "Jwt:SigningKey" "$([Convert]::ToBase64String((1..32 | % { [byte](Get-Random -Max 256) })))" --project ApexPost.Api
  dotnet user-secrets set "PartnerApi:Keys:demo-partner" "<long random value>" --project ApexPost.Api
  ```
- **Development tokens** come from the **Development-only** `GET /dev/token?roles=Courier&courierId=courier-007`
  (also `roles=Admin` or `roles=Admin,Courier`, and `expired=true` for the 401 demo). Tokens carry `sub`,
  `courierId` and `role`. The endpoint is not mapped outside Development and is excluded from the OpenAPI document.
- **Problem Details:** 401 (missing, invalid or expired token) and 403 (wrong role) are returned as
  `application/problem+json`. 401 responses carry `WWW-Authenticate: Bearer`, and the detail says whether the token
  was missing, invalid or expired.

## 2. Authorization

| Endpoint | Rule |
|---|---|
| `GET` shipments, scan history | `[Authorize]`: any authenticated user |
| `POST` scans (upload), `PATCH` status | `[Authorize(Roles = "Courier")]` |
| `DELETE` shipment | `[Authorize(Roles = "Admin")]` |
| `POST` / `PUT` shipment | `[Authorize(Roles = "Admin")]` (office staff create and edit shipments) |
| `GET /api/v2/partner/shipments/{tn}` | Partner API key (no JWT); read-only; no recipient personal data |

**Courier ownership:** a courier may upload scans only under the `courierId` in their own token. The controller
passes the claim to `CourierShipmentService.UploadScansAsync`, which throws `ScanOwnershipException` if any scan
uses another courier ID. The handler maps it to **403**. A Courier token without a `courierId` claim gets 403 too.

## 3. Partner API keys

- `X-Api-Key` is checked by `PartnerApiKeyMiddleware`, which runs only for `/api/v{n}/partner/...`. A missing or
  invalid key returns **401** Problem Details with `WWW-Authenticate: ApiKey header="X-Api-Key"`.
- Keys live in configuration (`PartnerApi:Keys:{partner}`) from **User Secrets** or a secret store. They are
  never hard-coded or committed (`appsettings.json` has an empty `Keys` object). At startup each key is hashed
  with SHA-256 and only the hash is kept. Comparison uses `CryptographicOperations.FixedTimeEquals` so timing
  does not reveal how much of a key matched. The partner name, never the key, is logged.

## 4. OAuth 2.0 concepts (what a production setup uses)

**Roles in OAuth 2.0 / OpenID Connect**
- The **identity provider (IdP)** / authorization server (for example Entra ID, Auth0 or Keycloak)
  authenticates people and systems and issues tokens. The API never sees passwords.
- The **access token (JWT)** is short-lived (minutes). It is signed by the IdP and says who the caller is
  (`sub`, `courierId`) and what they may do (`role`, scopes), for one audience (`apexpost-api`). The API
  validates the signature with the IdP's public keys (JWKS), plus the issuer, audience and expiry.
- The **refresh token** is long-lived and only goes to the IdP, to get a new access token without signing in again.
  It is stored securely (SecureStorage on the phone), can be revoked, and should be rotated on every use.

**Authorization Code + PKCE (courier mobile app)**
1. The app creates a random `code_verifier` and its SHA-256 `code_challenge`.
2. It opens the IdP login page in the system browser (`WebAuthenticator`) with `response_type=code`,
   `code_challenge` and the `redirect_uri` (`apexpost-courier://callback`).
3. The courier signs in (with MFA) at the IdP, and the IdP redirects back with a one-time **authorization code**.
4. The app exchanges the code **plus the original `code_verifier`** at the token endpoint and receives the access
   and refresh tokens.

PKCE matters because a mobile app is a *public client*: it cannot keep a client secret. If another app intercepts
the redirect and steals the code, the code is useless without the verifier.

**Client Credentials (partner systems)**
- A partner's back-end is a *confidential client*. It authenticates **itself**, not a user, at the token endpoint
  with `client_id` plus a secret or a certificate / `private_key_jwt`, using `grant_type=client_credentials` and a
  scope such as `apexpost.tracking.read`.
- It receives a short-lived access token and calls the API with `Authorization: Bearer`. There is no user and no
  refresh token: it simply requests a new token when the old one expires.

**Why API keys are weaker than OAuth 2.0 tokens**
- An API key is a **long-lived static secret**. If it leaks (logs, Git, a proxy), it works until someone notices
  and rotates it. An access token expires within minutes.
- A key carries **no scopes, audience or expiry**. It is all-or-nothing, so least privilege and per-operation
  consent are impossible.
- The key is **sent on every request**, which increases exposure. OAuth sends the client secret only to the IdP.
- There is **no standard revocation, rotation or auditing** and no MFA. The API has to manage keys itself.
- A key identifies a *system*, not a *user*, so per-user accountability is lost.

That is why the partner route is read-only and returns no personal data. The target design moves partners to
Client Credentials.

**Moving from development tokens to a real identity provider**
1. Register the API and the two clients (courier app: public, Authorization Code + PKCE; partner: confidential,
   Client Credentials) at the IdP. Define the `Courier` and `Admin` app roles and a `courierId` claim mapping.
2. In `Program.cs`, set `options.Authority = "https://login.example/{tenant}"` and `options.Audience`. Signing keys
   are then downloaded and rotated automatically from the IdP's metadata (`/.well-known/openid-configuration`,
   JWKS). Remove `Jwt:SigningKey`, `DevelopmentTokenIssuer` and the `/dev/token` endpoint.
3. In the MAUI app, replace the Login page's token paste / dev endpoint with `WebAuthenticator` (or MSAL), store
   the access and refresh tokens in SecureStorage, and refresh silently in `AuthHeaderHandler` before expiry.
4. Map IdP group or role claims to `Courier` / `Admin` (`RoleClaimType`) and keep the courier-ownership check.
5. Replace partner API keys with Client Credentials, and keep API keys only during a migration window.

## 5. Transport security (HTTPS, HSTS)

- `app.UseHttpsRedirection()` runs in **every** environment. `app.UseHsts()` (365 days, include subdomains) runs
  **outside Development**: HSTS is cached by browsers, and pinning `localhost` to HTTPS with a development
  certificate would break other local sites.
- **Why bearer tokens and API keys must never travel over plain HTTP:** they are *bearer* credentials, meaning
  whoever holds one *is* the caller. Over HTTP anyone on the path can read the `Authorization` / `X-Api-Key`
  header and replay it, and tampering is not detected. Examples include public Wi-Fi, a compromised router, a
  mobile carrier proxy or a corporate proxy. TLS gives confidentiality, integrity and server authentication.
  HSTS stops the first request from going out over HTTP (SSL-stripping).
- The Android app sets `usesCleartextTraffic="false"`, and the API base URLs are `https://` only.
- **OpenAPI:** `/openapi/v1.json` and `/openapi/v2.json` declare a `Bearer` (HTTP, JWT) security scheme and a
  `PartnerApiKey` (header `X-Api-Key`) scheme. Use **Authorize** in Swagger UI (Development) to test with a
  `/dev/token` token. Hiding documentation is not a security measure: every endpoint is protected by
  authentication and authorization.

### Development certificate bypass (DEBUG only)

The Android emulator does not trust the ASP.NET Core development certificate that the local API uses (on
`https://10.0.2.2:7301`, which is the host's loopback). `DevelopmentCertificates.CreateHandler()` exists **only
under `#if DEBUG`**. It accepts certificate errors **only** for loopback hosts and `10.0.2.2`; every other host is
still validated normally. Release builds do not contain this code at all, so certificate validation can never be
switched off in production.

## 6. Secure token handling in the app

- The token is stored **only** in MAUI `SecureStorage` (Android Keystore, Windows DPAPI) through
  `SecureStorageTokenStore`. It is never stored in `Preferences`, files or logs.
- `AuthHeaderHandler` (a `DelegatingHandler` on the typed client) attaches the token. On **401** it clears the
  stored token and raises `SessionEvents.SignedOut`, and the app navigates to the Login page.
- Logging records method, path and status only, never tokens, API keys or request bodies.
- No real credentials, signing keys, API keys or production URLs are in the repository. All data is fictional.
