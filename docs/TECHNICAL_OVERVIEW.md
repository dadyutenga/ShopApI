# User Management Service – Technical Overview

This document describes the authentication and admin management surface area added in this revision. Use it together with `ShopAPI.postman_collection.json` for hands-on exploration.

## Bootstrapping

1. On startup the service checks `system_settings.bootstrap_locked` and existing `Admin` users.
2. `GET /api/bootstrap/status` returns `isLocked`, `bootstrapEnabled`, and `hasAdminUsers`.
3. `POST /api/bootstrap/complete` accepts either:
   - `email` + `password` payload (defaults from `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD`).
   - `setupToken` signed with `BOOTSTRAP_TOKEN_SECRET` containing `email`/`password` claims.
4. Once the admin user is persisted, an immutable `bootstrap_admin_created` audit log is written and `bootstrap_locked=true` which permanently disables the endpoint.

## Roles

`UserRole` now supports `Admin`, `Manager`, `Support`, and `Customer`. RBAC is enforced through JWT role claims.

## Authentication APIs

| Route | Description |
| --- | --- |
| `POST /api/auth/register` | Local registration for any role, returns OTP instructions. |
| `POST /api/auth/verify-email/otp` | Confirms email ownership via 6 digit OTP. |
| `GET /api/auth/verify-email?token=...` | Signed OAuth verification link. |
| `POST /api/auth/login` | Password login reserved for Admin/Manager/Support roles. |
| `POST /api/auth/request-otp` | Customer login flow – issues OTP with rate-limit/guard. |
| `POST /api/auth/resend-otp` | Refreshes TTL if not expired. |
| `POST /api/auth/verify-otp` | Validates OTP and issues JWT/refresh tokens. |
| `POST /api/auth/refresh` | Exchanges refresh token. |
| `POST /api/auth/logout` | Clears refresh-token cookie. |

### OTP lifecycle

* OTPs are 6 digits, SHA-256 hashed, stored at `otp:{userId}` for 2 minutes.
* Attempts tracked at `otp:{userId}:attempts` (max 3).
* Resend guard is 30 seconds via `otp:{userId}:meta` payload.
* Abuse mitigation: per-IP rate limit key `rate:otp:{ip}` (5 per 10 minutes).
* Events: `otp.generated` and `otp.verified` published to RabbitMQ `otp.events` exchange.

### OAuth

Google/GitHub/Microsoft callbacks now return `202 Accepted` plus `verificationLink` when `IsEmailVerified` is false. Token issuance occurs only after the link is visited.

## Admin APIs

All routes require `Authorization: Bearer <token>` with `Admin` role.

* `POST /api/admin/register-manager`
* `POST /api/admin/register-support`
* `POST /api/admin/register-customer`
* `GET /api/admin/users?role=&status=&email=`
* `GET /api/admin/users/{id}`
* `PATCH /api/admin/users/{id}/role`
* `PATCH /api/admin/users/{id}/status`
* `DELETE /api/admin/users/{id}` (soft delete)

Each mutation writes to `audit_logs` and emits a RabbitMQ message (`user.registered`, `user.role.assigned`, `user.deactivated`, `user.deleted`).

## Models & Schemas

### User
```
{
  "id": "uuid",
  "email": "string",
  "passwordHash": "pbkdf2",
  "role": "Admin|Manager|Support|Customer",
  "isActive": true,
  "isDeleted": false,
  "isEmailVerified": false,
  "isPhoneVerified": false,
  "twoFactorEnabled": false,
  "phoneNumber": "+1555...",
  "timestamps": {
    "createdAt": "ISO-8601",
    "updatedAt": "ISO-8601"
  }
}
```

### AuditLog
```
{
  "id": "uuid",
  "userId": "uuid",
  "action": "string",
  "metadata": "json",
  "correlationId": "string",
  "createdAt": "ISO-8601",
  "isImmutable": false
}
```

## Redis key patterns

| Key | Purpose |
| --- | --- |
| `session:{userId}` | Cached user session snippet (10 min). |
| `blacklist:{jti}` | Revoked JWT ids. |
| `refresh:{token}` | Refresh token store (7 days). |
| `loginfail:{email}` | Failed password attempts (15 min). |
| `otp:{userId}` | OTP hash payload (2 min). |
| `otp:{userId}:attempts` | Attempt counter. |
| `otp:{userId}:meta` | Resend guard metadata. |
| `rate:otp:{ip}` | Per-IP OTP throttling (10 min). |

## RabbitMQ topology

* Exchanges
  * `user.events` (durable topic) – `user.registered`, `user.role.assigned`, `user.deactivated`, `user.deleted`, `email.verification.*`
  * `otp.events` (durable topic) – `otp.generated`, `otp.verified`
* All messages carry a `correlationId` plus user metadata.
* Configure DLQs/bindings per environment policy.

## JWT tokens

* RSA (RS256) signing with rotating keys stored in Redis (`jwt:key:{kid}`) and `kid` header set.
* Claims include `sub`, `email`, `role`, `provider`, `jti`.
* Refresh tokens stored in Redis and revoked after rotation.

## Rate limiting & security

* PBKDF2-SHA512 password hashing with 150k iterations.
* Redis-backed login attempt tracking (`MAX_FAILED_ATTEMPTS = 5`).
* CORS whitelist via `CORS_ORIGINS` environment variable.
* Structured Serilog logging plus OpenTelemetry instrumentation.

## Bootstrap lock flow

1. Bootstrap status endpoint shows whether admin users exist.
2. `POST /api/bootstrap/complete` allowed only when `bootstrapEnabled = true`.
3. After success, `system_settings.bootstrap_locked` is set which prevents re-entry.
4. Immutable audit entry `bootstrap_admin_created` is retained for compliance.
