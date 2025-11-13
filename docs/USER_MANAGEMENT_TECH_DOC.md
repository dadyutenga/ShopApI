# User Management Service (vNext)

## Role Hierarchy
- **Admin** – unrestricted system control. Required for bootstrap. Can manage all users and assign roles.
- **Manager** – elevated operational access but no RBAC changes.
- **Support** – limited support tooling, view-only for customer data.
- **Customer** – end-user identity, authenticated exclusively through the OTP flow.

## Bootstrap Flow
1. On application start the service queries MySQL for any `User` rows with `Role = Admin` and also checks `SystemSettings["bootstrap.locked"]`.
2. `GET /api/bootstrap/status` returns whether bootstrap is still available.
3. `POST /api/bootstrap/complete` accepts either:
   - Environment-seeded credentials (`BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD`).
   - A signed setup token produced with the `BOOTSTRAP_SETUP_SIGNING_KEY` (payload contains email/password and an expiry).
4. After the admin user is created the service writes an immutable `audit_logs` record (`bootstrap_admin_created`), marks `SystemSettings["bootstrap.locked"] = true`, emits `user.registered` on `user.events`, and permanently blocks further bootstrap attempts.

## Authentication APIs
| Route | Description | Notes |
| --- | --- | --- |
| `POST /api/auth/register` | Local registration (primarily for internal onboarding). | Password hashed via Argon2id. |
| `POST /api/auth/login` | Password login for Admin/Manager/Support. | Enforces lockout after 5 failures, ready for 2FA flag. |
| `POST /api/auth/request-otp` | Begin customer OTP-only login. | Generates 6-digit OTP, hashed via SHA-256 and stored at `otp:{userId}` with 2 minute TTL. Rate limited via `rate:otp:{ip}`. |
| `POST /api/auth/resend-otp` | Refresh TTL on existing OTP. | Guarded via `otp:resend:{userId}` (30s). |
| `POST /api/auth/verify-otp` | Complete OTP login, issues JWT/refresh pair. | Publishes `otp.verified`. |
| `GET /api/auth/verify-email?token=` | Handles OAuth email verification links. | Token signed with `EMAIL_VERIFICATION_SIGNING_KEY`. |
| `POST /api/auth/refresh` / `POST /api/auth/logout` | Token lifecycle. | Refresh tokens cached in Redis (`refresh:{token}`). |

## OAuth Flow
1. User completes provider callback (`/api/auth/{provider}/callback`).
2. Service upserts a `User` + `CustomerProfile` (inactive until verified).
3. Generates signed verification token and responds with `OAuthPendingResponse` containing `/api/auth/verify-email?token=...`.
4. After GET verification, `IsEmailVerified` (and `CustomerProfile.IsEmailVerified`) flip to true and `email.verification.completed` is published.

## Admin Management APIs (JWT + Admin role required)
- `POST /api/admin/register-manager`
- `POST /api/admin/register-support`
- `POST /api/admin/register-customer`
- `GET /api/admin/users?role=&status=&email=`
- `GET /api/admin/users/{id}`
- `PATCH /api/admin/users/{id}/role`
- `PATCH /api/admin/users/{id}/status`
- `DELETE /api/admin/users/{id}` (soft delete)

Every call records an `audit_logs` entry (JSON metadata) and emits lifecycle RabbitMQ events with correlation IDs for traceability.

## JSON Schemas (abridged)
```jsonc
UserDto {
  id: string (uuid),
  email: string,
  role: "Admin"|"Manager"|"Support"|"Customer",
  isActive: bool,
  isEmailVerified: bool,
  createdAt: string (ISO-8601),
  customer: {
    phoneNumber?: string,
    isPhoneVerified: bool,
    isEmailVerified: bool,
    twoFactorEnabled: bool
  } | null
}

RequestOtpRequest { email: string }
VerifyOtpRequest { email: string, otp: string (6 digits) }
BootstrapCompleteRequest { email?: string, password?: string, setupToken?: string }
UpdateUserRoleRequest { role: string enum }
UpdateUserStatusRequest { isActive: bool }
```

## Redis Key Patterns
| Key | Purpose | TTL |
| --- | --- | --- |
| `session:{userId}` | Cached session snapshot for quick lookup. | 10 minutes |
| `refresh:{token}` | Refresh token store. | 7 days |
| `blacklist:{jti}` | JWT blacklist for revoked access tokens. | Access token TTL |
| `otp:{userId}` | SHA-256 hash of the OTP payload. | 2 minutes |
| `otp:attempts:{userId}` | Verification attempts counter. | 2 minutes |
| `otp:resend:{userId}` | Resend throttle guard. | 30 seconds |
| `rate:otp:{ip}` | Rate limit counter (max 5 per minute). | 1 minute |
| `loginfail:{email}` | Password lockout counter. | 15 minutes |
| `jwt:key:{kid}` | Serialized RSA key material for JWT rotation. | 60 days |

## RabbitMQ
- **Exchange `user.events`** (durable topic)
  - `user.registered`
  - `user.updated`
  - `user.role.assigned`
  - `user.deactivated`
  - `email.verification.sent`
  - `email.verification.completed`
- **Exchange `otp.events`** (durable topic)
  - `otp.generated`
  - `otp.verified`

MassTransit config binds DLQ queues (`*.error`) automatically so failed consumers flow into durable dead-letter queues.

## JWT Claims
```
sub            → user id (Guid)
nameidentifier → user id (Guid)
email          → email address
role           → role name
provider       → identity provider
jti            → unique token id (for blacklisting)
```
Tokens are signed via RSA (Argon key rotation) with `kid` header used to resolve the public key.

## Rate Limiting & Abuse Controls
- OTP generation: max 5 requests per IP per minute (HTTP 400 when exceeded).
- OTP verification: max 3 invalid attempts per issuance, state cleared afterward.
- Login lockout: 5 failed password attempts triggers 15-minute lock.
- Resend OTP guard: 30-second cool-down between re-issues.

## Audit Trail
All Admin endpoints plus bootstrap events write to `audit_logs` with immutable JSON payloads. Logs are linked to actors/targets and survive user deletion.

