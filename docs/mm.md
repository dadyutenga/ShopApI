# 🏗️ E-Commerce Backend Architecture — .NET 8 (C#)

## 🚀 Overview
This document describes the full system design for a production-grade **E-Commerce backend** built with **.NET 8 minimal APIs**, **RabbitMQ**, **Redis**, and **JWT + social authentication**.  
It’s event-driven, horizontally scalable, and designed for high throughput.

---

## 🧩 Core Tech Stack

| Layer | Tech |
|-------|------|
| **Framework** | .NET 8 Minimal API (C# 12) |
| **Database** | Microsoft SQL Server 2022 (Primary) |
| **Caching / Sessions** | Redis 7 (Distributed Cache + Rate Limit + Locking) |
| **Messaging / Broker** | RabbitMQ 3 (Managed via MassTransit) |
| **Authentication** | ASP.NET Identity + JWT + OAuth2 (Social Logins) |
| **Logging** | Serilog + Seq + OpenTelemetry (Traces & Metrics) |
| **Containerization** | Docker + Compose for local; Kubernetes for prod |
| **Secrets Mgmt** | Azure Key Vault / Environment Variables |
| **CI/CD** | GitHub Actions → Docker Build → Push → Helm Deploy |
| **Gateway** | YARP (API Gateway / Reverse Proxy) |
| **Feature Flags** | Microsoft.FeatureManagement |

---

## 🧠 Domain Modules

1. **Identity Service**
   - User Registration + Login (JWT and OAuth)
   - Google, GitHub, Microsoft logins
   - Refresh tokens, roles, claims, permissions

2. **Catalog Service**
   - Products, Variants, Categories
   - Price management + images
   - Search index sync via RabbitMQ (`catalog.product.updated`)

3. **Cart Service**
   - Redis-backed cart sessions
   - Add/update/remove items (idempotent)
   - Auto-expiration (7 days TTL)

4. **Inventory Service**
   - Stock levels per warehouse
   - Reservation logic on checkout
   - Emits `inventory.reserved` / `inventory.released`

5. **Order Service**
   - CQRS pattern (read/write split)
   - Outbox table for reliable event publishing
   - Tracks lifecycle → Created → Paid → Shipped → Completed

6. **Payment Service**
   - Integrates with Stripe/PayPal (plug-in providers)
   - Webhook verification (HMAC)
   - Emits `payment.captured` / `payment.failed`

7. **Shipping Service**
   - Carrier rates, labels, tracking
   - Bound to `order.fulfilled`

8. **Notification Service**
   - Email / SMS / Push templates
   - Retry + DLQ on failures

9. **Admin Portal / Backoffice**
   - Order management, refunds, user control

---

## 🗃️ Data Schemas (Simplified)

### `users`
| Column | Type | Notes |
|--------|------|-------|
| id | GUID PK | |
| email | nvarchar(255) unique | |
| password_hash | nvarchar(max) | |
| provider | varchar(32) | local \| google \| github \| microsoft |
| provider_id | varchar(128) | |
| created_at | datetime2 | |
| updated_at | datetime2 | |

### `products`
| Column | Type | Notes |
|--------|------|-------|
| id | GUID | PK |
| slug | nvarchar(150) unique | |
| title | nvarchar(255) | |
| description | nvarchar(max) | |
| price | money | |
| currency | char(3) | |
| is_active | bit | |
| created_at | datetime2 | |

### `orders`
| Column | Type |
|--------|------|
| id | GUID PK |
| user_id | GUID FK(users) |
| status | varchar(32) |
| total | money |
| currency | char(3) |
| created_at | datetime2 |
| updated_at | datetime2 |

### `order_lines`
| Column | Type |
|--------|------|
| id | GUID |
| order_id | GUID FK |
| sku | varchar(64) |
| qty | int |
| unit_price | money |

---

## ⚙️ Messaging Model (RabbitMQ + MassTransit)

### Exchanges
| Exchange | Events |
|-----------|---------|
| `order.events` | `order.created`, `order.paid`, `order.cancelled` |
| `inventory.events` | `inventory.reserved`, `inventory.released` |
| `payment.events` | `payment.captured`, `payment.failed` |
| `user.events` | `user.registered`, `user.password.reset` |

### Example Event Contract
```csharp
public record OrderCreated(
    Guid OrderId,
    Guid UserId,
    decimal Total,
    string Currency,
    IReadOnlyList<OrderItemDto> Items,
    DateTimeOffset CreatedAt
);
