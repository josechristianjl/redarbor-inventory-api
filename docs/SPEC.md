# Inventory Management API — Functional Specification

Status: Candidate-approved baseline (v0.3, architecture references updated), 2026-11-25
Source: Redarbor Senior Developer technical assessment, generated 2026-10-26.

## 1. Scope
Implement authenticated REST CRUD for products and categories, and registration of inventory entries and exits. Use .NET 8+, SQL Server, Entity Framework for reads, Dapper for writes, unit tests, TDD, SOLID, design patterns, Swagger, environment-based configuration and Docker Compose. CQRS is preferred. Publish a public repository with execution/debugging instructions and AI evidence. Answer the assessment's three decision-making questions separately using actual experience.

## 2. Proposed business rules (not explicitly specified by Redarbor)
- Product: integer ID, unique SKU, name, optional description, category ID, current stock, active flag.
- Category: integer ID, unique name, optional description, active flag.
- Inventory movement: integer ID, product ID, type (Entry or Exit), positive integer quantity, UTC creation timestamp.
- Products start with zero stock. Stock cannot be changed through product CRUD.
- Product creation and category assignment require an active category.
- Only active products can receive movements. Exit quantity cannot exceed current stock.
- Updating stock and inserting a movement must be atomic; concurrent exits cannot result in negative stock. Roll back both operations on failure.
- Movements are immutable; no update/delete endpoint is exposed.
- Product deletion is logical and allowed only at zero stock. Category deletion is logical and blocked while active products reference it. Historical records remain available in the database. Inactive products/categories are excluded from ordinary reads. SKU and category name remain reserved after deletion.
- Validate nonblank names (max 150 characters), SKU (max 50), optional description (max 500). Trim inputs; uniqueness is case-insensitive. Quantities must fit a positive SQL int; reject stock overflow.

## 3. Proposed HTTP contract
Base path: /api/v1. All business endpoints require a valid OAuth2 access token. Keycloak is the selected authorization server; see ARCHITECTURE.md.

| Method | Route | Successful response |
|---|---|---|
| GET | /products | 200, paginated active products |
| GET | /products/{id} | 200, product |
| POST | /products | 201, product identifier and Location header |
| PUT | /products/{id} | 204 |
| DELETE | /products/{id} | 204 |
| GET | /categories | 200, paginated active categories |
| GET | /categories/{id} | 200, category |
| POST | /categories | 201, category identifier and Location header |
| PUT | /categories/{id} | 204 |
| DELETE | /categories/{id} | 204 |
| POST | /inventory-movements | 201, movement identifier |

Product write fields: sku, name, description, categoryId. Category write fields: name, description. Movement write fields: productId, type (Entry or Exit), quantity. List pagination: page defaults to 1, pageSize defaults to 20, maximum 100; deterministic ID ordering. Responses include the relevant entity fields; lists include items, page, pageSize and totalCount.

Errors use Problem Details: 400 malformed or invalid input; 401 missing/invalid token; 403 insufficient permission if authorization policy applies; 404 missing or inactive resource; 409 duplicate SKU/category name, insufficient stock, blocked deletion or stock overflow. Unexpected errors return 500 without internal details.

## 4. Acceptance criteria
- Valid product/category CRUD works and invalid input is rejected.
- A new product has zero stock; entry of 10 followed by exit of 4 leaves 6.
- Exit of 7 from stock 6 fails without changing stock or adding a movement.
- Zero or negative movement quantity fails without writes.
- Two concurrent exits of 4 from stock 5 produce one success and one conflict, leaving stock 1 and exactly one exit record.
- A failed movement insert rolls back the stock update.
- Deletion rules preserve history and prevent active products from referencing inactive categories, including concurrent operations.
- Requests without a valid token cannot access business endpoints.
- EF handles application reads; Dapper handles application writes.
- A fresh configured environment can build and start the required services using Docker Compose.
- Unit tests cover critical business rules; targeted SQL Server integration tests verify transactions and concurrency.

## 5. Out of scope
Multiple warehouses, suppliers, orders, pricing, UI, microservices, event brokers, separate read databases, cloud deployment and advanced reporting. No extra features unless the core acceptance criteria are met.

## 6. Architecture decisions
See ARCHITECTURE.md for the approved technical design. Functional acceptance criteria remain unchanged.
