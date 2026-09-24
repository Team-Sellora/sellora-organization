# Identity & scope — setup and rollout

**Goal:** a staff member or shop owner is created from the Sellora web app in one action. Nobody opens the WSO2 console, and nobody copies database IDs into user claims.

## How it works now

| Question | Answered by |
|---|---|
| Who are you? (`sub`, `roles`, `companyId`) | WSO2 IS — set once, when Organization creates the login |
| Where do you sit? (sales rep ID, agency, territory, provinces, shop) | Organization — `GET /api/me/scope`, by the token's `sub` |

- **`GET /api/me/scope`** (any Sellora role): resolves the caller from `staff_profile.identity_sub` (or `shop.owner_identity_sub` for shop owners), reusing `HierarchyScopeResolver`. 404 when the login has no active profile.
- **Order and Inventory** call it with the user's own token, cache it per user for 5 minutes, and no longer read `salesRepId` / `agencyId` / `shopId` / `provinceId` claims. Company Admins skip the lookup. If Organization is down, scoped roles get 503.
- **`POST /api/staff`** (Company Admin: any staff role; Agency Operator: Sales Rep): creates the WSO2 login over SCIM2 with the admin's own `companyId`, adds it to the role, then saves the staff profile with `identity_sub` = the new login's ID. If the save fails, the login is deleted.
- **`POST /api/shops`** with `ownerEmail` (and no `ownerIdentitySub`) creates the Shop Owner's login the same way. Passing `ownerIdentitySub` still links an existing login.
- The response carries a **temporary password once** (`Cache-Control: no-store`). It is never stored.

## 1. Create the provisioning client in WSO2 IS (once per environment)

1. IS Console → **Applications** → **New Application** → **Standard-Based Application** → OAuth2.0/OIDC. Name it `sellora-provisioning`.
2. **Protocol** tab: allowed grant types = **Client Credentials only**. Save and copy the **Client ID** and **Client Secret**.
3. **API Authorization** tab → **Authorize an API resource** → under *Management APIs*:
   - **SCIM2 Users API**: create, delete, list/view
   - **SCIM2 Roles API**: view, update
4. Check the Sellora roles exist (`sellora-infra/seed-roles.sh`).

If creating a user fails with a username error, the IS doesn't accept email addresses as usernames: Console → **Login & Registration** → username validation → allow email. The SCIM payloads mirror `sellora-infra/seed-users.sh`, including its warning about the custom schema URN.

## 2. App Service settings

**Organization** (staging slot):
```
IdentityProvisioning__BaseUrl                   = https://<your IS host>
IdentityProvisioning__ClientId                  = <from step 1>
IdentityProvisioning__ClientSecret              = <from step 1>
IdentityProvisioning__AllowUntrustedCertificate = true   # only if IS uses a self-signed cert
```
Without these, `POST /api/staff` returns 503 "User provisioning is not configured" — nothing half-works.

**Inventory** (new):
```
Dependencies__Organization__BaseUrl = https://<organization staging URL>
```

**Order:** nothing new — it already has `Dependencies__Organization__BaseUrl`.

## 3. Link the users that already exist (last manual step, ever)

Existing profiles were seeded with placeholder subs (`seed:…`). Before deploying the new Order and Inventory, link each real IS login, or those users will see nothing.

Find each login's ID: IS Console → **User Management → Users** → open the user → **User ID** (the same value that appears as `sub` in their token).

```sql
-- Organization database: who is linked, and who isn't
SELECT sp.staff_profile_id, sp.display_name, sp.email, sp.role, sp.identity_sub, c.name AS company
FROM staff_profile sp
JOIN company c ON c.company_id = sp.company_id
ORDER BY c.name, sp.role;

-- Link one profile
UPDATE staff_profile SET identity_sub = '<IS user id>' WHERE staff_profile_id = '<profile id>';

-- Shop owners
UPDATE shop SET owner_identity_sub = '<IS user id>' WHERE shop_id = '<shop id>';
```

Check: log in as that user and call `GET /api/me/scope` on Organization. It should return their role and IDs, not 404.

## 4. Deploy order

1. **Organization** (the endpoint must exist first).
2. Link existing users (step 3).
3. **Order** and **Inventory**.
4. **Web** (Team page, owner email on the shop form).
5. Remove the `salesRepId` / `agencyId` claim configuration from WSO2 and `configure-apps.sh`.

## Security notes

- The company for a new login always comes from the creating admin's token, never from the request body.
- The provisioning client can only manage users and roles; its secret lives only in App Service settings.
- Scope is derived server-side from the verified `sub`, so a user cannot claim another rep's or agency's ID.
