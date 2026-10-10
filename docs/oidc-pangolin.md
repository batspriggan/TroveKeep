# Optional OIDC login (Pangolin)

TroveKeep authenticates users two ways:

- **Local accounts** (email + password) — always available, controlled by the cookie session in
  `AuthCookieFactory`.
- **OpenID Connect (Pangolin)** — optional, enabled only when `Auth:Oidc:Enabled` is `true` **and**
  an `Authority` + `ClientId` are configured. When it is off, the application behaves exactly as
  before: nothing is registered, no route changes.

Both methods end in the **same application cookie**. OIDC only proves *who* the person is once;
TroveKeep still decides what that account may do.

## Two layers of authentication

This is important to get right when Pangolin sits in front of TroveKeep:

| Layer | Question it answers | Who owns it |
| --- | --- | --- |
| **Resource access** (Pangolin) | "May this browser even reach the TroveKeep host?" | Pangolin (reverse proxy / SSO portal) |
| **Application user** (TroveKeep) | "Which TroveKeep account is this, with which role and collections?" | TroveKeep (`users` collection, `UserRole`, collection memberships) |

A Pangolin login does **not** by itself grant access to any TroveKeep data. At the end of the OIDC
round-trip the callback resolves the external identity to a local account
(`IAuthService.ResolveExternalUserAsync`) and issues the normal cookie:

- if the email already belongs to a TroveKeep account, the external identity is **linked to it**
  on first login and the account's existing role/collections apply;
- if no account exists, one is auto-provisioned with `Role = Guest` (see below);
- if the email is missing, or the subject is already linked to a *different* identity, the login is
  refused and the user is sent to `/login?error=…`.

## Local accounts, auto-provisioning and roles

- An external-only account is created with `PasswordHash = null`, `ExternalProvider = "oidc"` and
  `ExternalSubject = <sub>`. It can later set a local password from Settings → Change password and
  then log in with either method.
- New accounts start as **`Guest`** — the least privilege role (Table Planner only). They will not
  see the main inventory until an **admin promotes them** (Users admin screen / `users` collection)
  and adds them to the collections they should access.
- Provider-side configuration should normally let only the intended people log in at all (see the
  resource/SSO policy in Pangolin). TroveKeep applies its own role check on top.

## Step-by-step: Pangolin configuration

Pangolin's UI labels change between versions; the fields below are the OIDC/client equivalents to
look for (resource or "OIDC client" for a self-hosted app).

1. In Pangolin, create a **new resource** (or OIDC client) for TroveKeep.
2. Choose the **OIDC / OpenID Connect** authentication method (not basic auth, not Pangolin's own
   SSO-only mode unless you also enable the OIDC client).
3. Set the **Redirect URI** (a.k.a. callback / allowed callback URL) to exactly:
   ```
   https://<trovekeep-host>/api/auth/oidc/callback
   ```
   For local testing it may be `http://localhost:5221/api/auth/oidc/callback`; the path must match
   `Auth:Oidc:Authority`'s provider and the configured `CallbackPath` byte for byte.
4. Note the generated **Client ID** and **Client Secret**.
5. Request the scopes **`openid profile email`** (at minimum `openid email`). TroveKeep needs the
   `email` claim to match/provision the account; without it the login is refused.
6. Make sure the *Authority* used by TroveKeep is the provider's issuer/discovery root, i.e. the URL
   under which `/.well-known/openid-configuration` is served.

## TroveKeep configuration

`appsettings.json` (or the equivalent Docker Compose environment variables):

```json
"Auth": {
  "Oidc": {
    "Enabled": true,
    "Authority": "https://pangolin.example.com",
    "ClientId": "trovekeep",
    "ClientSecret": "…",
    "Scopes": "openid profile email",
    "AutoProvision": true,
    "RequireHttpsMetadata": true
  }
}
```

As environment variables (double-underscore nesting):

```
Auth__Oidc__Enabled=true
Auth__Oidc__Authority=https://pangolin.example.com
Auth__Oidc__ClientId=trovekeep
Auth__Oidc__ClientSecret=…
Auth__Oidc__Scopes=openid profile email
Auth__Oidc__AutoProvision=true
Auth__Oidc__RequireHttpsMetadata=true
```

Notes:

- **`Enabled` is an explicit opt-in.** Even with credentials present, OIDC stays off until
  `Enabled=true`. If `Enabled=true` but `Authority`/`ClientId` are empty, OIDC stays off and the
  application logs a warning instead of failing at startup.
- **`RequireHttpsMetadata`** must stay `true` in production. Set it to `false` only for a local
  development provider that serves plain HTTP.
- **`AutoProvision`** documents that unknown identities become `Guest` accounts. Provisioning is
  performed by `AuthService.ResolveExternalUserAsync` (this is the behaviour today).
- The redirect URI sent to the provider is `<public base URL>/api/auth/oidc/callback`. Run
  TroveKeep behind the same host/proxy path that the provider has registered.

## The frontend

The login page calls the anonymous endpoint:

```
GET /api/auth/providers   →   { "local": true, "oidc": true }
```

The "Sign in with Pangolin" button is rendered only when `oidc` is `true`, and it navigates to
`/api/auth/oidc/challenge`. When OIDC is disabled, the endpoint returns **404** (not 500), so a
client probing it gets an unambiguous "not available".

## First login and promotion

1. The user opens `/login` and clicks **Sign in with Pangolin**.
2. After the provider round-trip, TroveKeep provisions (or links) the account and sets the session
   cookie, then redirects to `/`.
3. If the account was just created it is a **`Guest`**: it lands on the Table Planner and is kept
   away from the main inventory.
4. An **admin** opens the Users screen, promotes the account to `User` (or `Admin`) and adds it to
   the intended collections. The user refreshes / logs in again and the new role applies.

## Disabling OIDC (local-only fallback)

Set `Auth:Oidc:Enabled=false` (or remove the whole `Auth:Oidc` section) and restart. The OIDC
scheme is not registered, `/api/auth/providers` reports `oidc: false`, the button disappears from
the login page and `/api/auth/oidc/challenge` returns 404. Local email/password login keeps working
unchanged, which is what gets you back in if the identity provider is misconfigured — keep at least
one local `Admin` account (the bootstrap admin, or a promoted user with a password) for exactly
this case.
