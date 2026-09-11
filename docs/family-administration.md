# Family onboarding and administration

## Scope

The pilot-ready flow is available only when `MultiFamily:Enabled=true` in Development or Testing. Public registration has an independent fail-safe switch, `Registration:Enabled`, which defaults to `false`.

When registration is enabled, a visitor can create one new identity and one new Family. The server creates the `AppUser`, `Family`, default `FamilySettings` and initial active Owner membership in one serializable database transaction. The request contains only email, password and family name; tenant and Owner identifiers are never accepted as authority. A failure rolls back the complete unit.

The invitation path remains the only way to join an existing Family. It continues to use the hashed, expiring, single-use token workflow and the channel-neutral `IUserNotificationDeliveryService`.

## Roles and Owner invariant

- Owner can invite Admin or Member, revoke pending invitations, change an existing member to Owner/Admin/Member and remove a membership.
- Admin can view members and manage the business data allowed by the existing authorization matrix, but cannot mutate memberships or invitations.
- Member cannot view or administer the membership roster.
- The model supports multiple active Owners. Owner changes are serialized and revalidate the acting Owner inside the transaction. Demotion or removal is rejected when it would leave fewer than one active Owner.

Promoting an existing active member of the current Family to Owner is the supported ownership handover. There is no separate endpoint that accepts a target Family or Owner identifier.

## Family selection

Users with multiple active memberships choose the current Family in the existing selector. Selection is stored in the protected server session and revalidated against the authenticated user, active membership and active Family on every resolution. A removed membership invalidates the previous choice. All tenant-aware repositories continue to obtain `FamilyId` only from `ICurrentFamilyContext`.

## Operational boundaries

This milestone does not enable multi-family mode or registration in production, run migrations at startup, import JSON, configure a delivery provider or change the legacy JSON runtime. Production rollout, distributed session/rate-limit storage, backup/restore rehearsal and cutover remain separate gates.
