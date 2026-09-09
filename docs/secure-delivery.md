# Secure user notification delivery

## Architecture

`IUserNotificationDeliveryService` receives only an explicit message kind (`FamilyInvitation` or `PasswordRecovery`), destination, action URL and non-sensitive correlation ID. Business services build links through `SecureActionLinkFactory`, which accepts only the configured HTTPS `Delivery:PublicBaseUrl`; request Host headers are never used.

The `HttpEmailNotificationProvider` is a provider adapter for a transactional email HTTP gateway. It sends a template key and action URL over HTTPS, uses an externally supplied API key and sets a stable idempotency key. No external account or credential is configured in the repository.

## Safe configuration

External delivery is disabled by default. When disabled, startup requires no credential and providers are never called. Enabling `HttpEmail` requires an HTTPS public base URL, HTTPS provider endpoint, sender and API key supplied through secret-aware configuration. Invalid enabled configuration fails startup validation.

## Failure and transaction strategy

Invitation state is committed before external delivery, so no PostgreSQL transaction remains open during network I/O. The Owner still receives the one-time link and a delivery status if the provider is disabled or fails. Password recovery always returns its generic response, independently of user existence or delivery outcome.

Transient failures and timeouts use at most two retries with bounded backoff and a per-message idempotency key. Permanent rejection is not retried. Logs contain only message kind, status, attempt count, correlation ID and exception type; destination, link, token and provider response body are excluded.

There is no outbox in this controlled single-instance milestone. A process crash after database commit and before delivery can leave an invitation requiring manual sharing, while ambiguous provider responses can still create duplicate email despite the idempotency key. A durable outbox becomes required before multi-replica production or when delivery guarantees become operationally critical.

## Privacy and limitations

- raw invitation and recovery tokens exist only in memory and the delivered HTTPS URL;
- tokens, passwords, hashes, SecurityStamp and full destinations are not logged;
- delivery messages do not include FamilyId, family name, membership or role;
- the fake/capture provider exists only in the test assembly and has no HTTP endpoint;
- external sending remains disabled until separately configured and operationally approved.
