# Deferred Work Log

## Deferred from: code review of 4-1-persistent-actuarial-audit-trail-sqlite (2026-06-16)

- **WAL mode for SQLite** — SQLite Write-Ahead Logging journal mode was not configured. Current retry-on-lock policy (SQLITE_BUSY error codes 5 & 6) is sufficient for POC single-user workloads. WAL should be enabled when the system moves to an enterprise/production deployment with concurrent users. Reason for deferral: enterprise lifecycle concern, not required at POC stage.
- **`SqlitePersistenceManager` coupled to concrete `DbContext`** — Constructor takes `ActuarialDbContext` rather than an abstraction interface. Acceptable for POC; a future refactor story should introduce an `IActuarialDbContext` or equivalent. Deferred as a pre-existing POC constraint.
- **Mixed nullability on `TranslationOutput`** — `required` properties on `TranslationOutput` conflict with the nullable `Output?` property on `TranslationPayload`. Pre-existing model design decision; deferred for future model hardening.

## Deferred from: code review of 4-2-aspnet-core-webapi-wrapper (2026-06-17)

- **Unit test project polluted with integration dependencies** [`ActuarialTranslationEngine.Tests.Unit.csproj`] — References `Mvc.Testing` and `Sqlite` in unit tests.
- **Missing Test Implementations in Unit Test Modifications** [`ActuarialTranslationEngine.Tests.Unit.csproj`] — No actual test code verifying endpoints in the diff.
