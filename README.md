# FashionHub API

A layered ASP.NET Core Web API for an e-commerce platform. The solution follows a clean, multi-layer architecture with Domain, Application, Infrastructure, and Presentation projects, and includes authentication, products, cart, orders, and payment flows (Stripe and Paymob).

## Table of Contents

- Overview
- Architecture
- Features
- What Makes It Stand Out
- Tech Stack
- Getting Started
- Configuration
- Database and Migrations
- Data Seeding
- API Endpoints (High Level)
- Testing
- Logging
- Project Structure
- Security Notes
- Troubleshooting

## Overview

FashionHub API provides a backend for a fashion e-commerce application with:

- User authentication and roles (Admin, Customer)
- Product catalog with search, filters, and featured/on-sale lists
- Shopping cart management
- Order management
- Payment integration via Stripe and Paymob

## Architecture

The solution uses a layered architecture:

- Domain: entities, enums, and repository interfaces
- Application: DTOs, services (business logic), and mappers
- Infrastructure: EF Core, repositories, external services, and data seed
- Presentation: API controllers, middleware, and dependency injection

This separation ensures testability and maintainability.

## Features

- Auth: login, register customer, change password, reset password
- Products: CRUD, search, featured, on-sale, soft delete, hard delete
- Cart: add/update/remove items, increase/decrease quantity, clear cart
- Orders: create from cart, list orders, update status
- Payments: create payment intent, handle gateway webhooks, finalize orders

## What Makes It Stand Out

- Multi-gateway payments with a unified webhook router (Stripe + Paymob)
- Payment-to-order automation with idempotent success handling
- Defensive cart recovery when customer records are missing
- Product catalog caching with targeted invalidation for fast reads
- Soft delete with global query filters to keep data recoverable
- Rich EF Core configurations with indexes and constraints
- Structured ServiceResult pattern for consistent API error handling
- Built-in data seeding for fast local setup (users, products, carts, orders)
- Role-based access control for Admin vs Customer flows
- Centralized exception middleware with structured logging

## Tech Stack

- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity + JWT
- Serilog
- Stripe SDK
- Paymob (via HTTP client)

## Getting Started

### Prerequisites

- .NET SDK (compatible with the solution)
- SQL Server (local or remote)

### Run the API

1. Update connection strings and secrets in appsettings.json
2. Apply migrations
3. Run the API

Example (PowerShell):

    dotnet build
    dotnet run --project Presentation/Presentation.csproj

The API will start and expose endpoints under /api.

## Configuration

Configuration is loaded from Presentation/appsettings.json and appsettings.Development.json.

Key sections:

- ConnectionStrings: DefaultConnection
- JWT: Issuer, Audience, Key, DurationInDays
- EmailSettings: SMTP and sender settings
- Stripe: SecretKey, WebhookSecret
- Paymob: SecretKey, PublicKey, HmacSecret, DefaultMethod, PaymentMethods
- SeedUserPasswords: Admin, Customer

Make sure to keep production secrets out of source control.

## Database and Migrations

- DbContext: Infrastructure/Data/ApplicationDbContext.cs
- Entity configurations: Infrastructure/Data/Config/\*.cs

Apply migrations:

    dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project Presentation/Presentation.csproj

## Data Seeding

On startup, the API runs data seeding:

- Roles: Admin, Customer
- Admin user and sample customers
- Products from JSON
- Carts and orders from JSON

Seed logic is in Infrastructure/Data/DataSeed/FashionHubDataSeed.cs.

## API Endpoints (High Level)

- Auth
  - POST /api/auth/login
  - POST /api/auth/register-customer
  - POST /api/auth/forgot-password
  - POST /api/auth/reset-password
  - POST /api/auth/change-password
  - GET /api/auth/my-profile

- Products
  - GET /api/products
  - GET /api/products/{id}
  - GET /api/products/search?term=...
  - GET /api/products/category/{category}
  - GET /api/products/featured
  - GET /api/products/sale
  - POST /api/products
  - PUT /api/products/{id}
  - PATCH /api/products/{id}/stock
  - PATCH /api/products/{id}/status
  - PATCH /api/products/{id}/featured
  - DELETE /api/products/{id}/soft
  - DELETE /api/products/{id}/hard

- Cart (Authorized)
  - GET /api/cart
  - POST /api/cart/items
  - PUT /api/cart/items
  - PUT /api/cart/items/{cartItemId}/increase
  - PUT /api/cart/items/{cartItemId}/decrease
  - DELETE /api/cart/items/{cartItemId}
  - DELETE /api/cart/clear
  - GET /api/cart/count
  - GET /api/cart/check-product/{productId}

- Orders
  - POST /api/orders
  - GET /api/orders/{id}
  - GET /api/orders
  - PUT /api/orders/{id}/status
  - GET /api/orders/admin

- Payments
  - GET /api/payment/methods
  - POST /api/payment/create-payment-intent
  - POST /api/payment/webhook/{gateway}
  - POST /api/payment/force-success/{gatewayPaymentId}

## Testing

Test project: FashionHub.Tests

Run tests:

    dotnet test FashionHub.Tests/FashionHub.Tests.csproj

## Logging

- Serilog is configured in Presentation/Extensions/SerilogExtensions.cs
- Exception handling middleware logs unhandled exceptions

## Project Structure

- Application/ (services, DTOs, mapping)
- Domain/ (entities, enums, interfaces)
- Infrastructure/ (EF Core, repositories, external services)
- Presentation/ (controllers, middleware, DI setup)
- FashionHub.Tests/ (unit tests)

## Security Notes

- Never commit real secrets to source control
- Use strong JWT keys and rotate them regularly
- The payment force-success endpoint is intended for debugging only

## Troubleshooting

- If seed JSON files are not found, ensure the working directory is the solution root
- If Stripe webhooks fail, verify the configured webhook secret
- If Paymob callbacks fail, verify HMAC secret and method configuration
