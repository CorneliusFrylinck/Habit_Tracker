# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

This file is written to be sufficient on its own: everything needed to regenerate this app from an
empty repo — project structure, schema, business rules, exact SQL, UI markup/behavior, styling, and
the non-obvious gotchas that were only discovered by hitting them — is documented below.

## Project overview

This project is a habit tracking app. A user can log in/register and can have multiple habits linked
to them.

- A habit can be **completable** or **non-completable**. Non-completable habits just record entries
  (e.g. "Drink water") with no notion of "done".
- A completable habit is completed one of two ways (`HabitCompletionMethod`):
  - **Sub-habits**: progress comes from completing its sub-habits (see below). Each tracked entry is
    a plain "mark as completed" checkbox.
  - **Total**: progress is measured against a numeric target (`TargetValue` + a unit, e.g. "412
    pages"). Each tracked entry carries a numeric `Amount` instead of a checkbox. The target itself is
    either (`HabitTargetType`):
    - **Once-off**: a single cumulative goal — entry amounts sum across the habit's lifetime toward it
      (e.g. reading a 412-page book).
    - **Per entry**: the goal for *each* entry (e.g. 10 reps every session); completion is the average
      of each entry's own capped progress toward it.
- A habit can have sub-habits (example: "Reading" is the main habit; sub-habits are the specific books
  the user wants to read; the user completes those sub-habits to progress the parent). Sub-habits can
  themselves have sub-habits — the tree has no fixed depth limit.
- Non-completable habits can still record a numeric `Amount` on their entries (e.g. "Drink water" →
  500ml) — it's just never rolled up into a completion percentage, since there's nothing to complete.

## Tech stack

- .NET 10 (`net10.0`) Blazor Web App with per-component interactive render modes (see below) — not a
  single global render mode.
- PostgreSQL via Npgsql. EF Core for writes and schema (entity models never leave `Persistence`).
  Dapper for read-heavy list/tree queries.
- ASP.NET Core Identity (cookie-based) for auth, with a deliberately trimmed-down page surface (see
  "Authentication" below).
- Solution file: `Habit_Tracker.slnx` (the newer XML-based `.slnx` format, not `.sln`).

## Project structure

All six projects live under `Habit_Tracker/` and are listed in `Habit_Tracker.slnx`:

- **`Habit_Tracker/Habit_Tracker/`** (`Microsoft.NET.Sdk.Web`) — the server host. Hosts the app, serves
  static assets, renders server-side Razor components, exposes the two minimal-API endpoints the
  habit list posts to, and is the composition root (DI registration for `Application`/`Persistence`).
  Entry point: `Program.cs`.
- **`Habit_Tracker/Habit_Tracker.Client/`** (`Microsoft.NET.Sdk.BlazorWebAssembly`) — the WASM client,
  referenced by the host. Components here run in the browser and **cannot** reach Postgres/EF
  Core/Dapper directly. References `Habit_Tracker.Application` for DTOs only, never
  `Habit_Tracker.Persistence`. In practice this project only contains the template's `Counter.razor`
  demo page and the client-side auth-state plumbing — the habit-tracking UI itself is entirely
  server-rendered static SSR and never needed to go through a Web API.
- **`Habit_Tracker.Domain`** — `Habit`/`HabitEntry` entities and the `HabitCompletionMethod`/
  `HabitTargetType` enums only. No EF Core, Dapper, or ASP.NET Core references, no package
  references at all.
- **`Habit_Tracker.Application`** — DTOs and the interfaces `Persistence` implements
  (`IHabitCommands`, `IHabitQueries`). References `Domain` only — no EF Core/Dapper/Npgsql packages,
  which is what lets `Habit_Tracker.Client` reference it safely for DTOs.
- **`Habit_Tracker.Persistence`** — the only project allowed to touch the entity models directly.
  Contains the EF Core `DbContext` (including Identity's schema), entity configurations, migrations,
  the EF Core command implementations, and the Dapper query implementations. Referenced only by the
  host, never by `Habit_Tracker.Client`.
- **`Habit_Tracker.Tests`** — xUnit test project (see "Testing" below). References `Domain`,
  `Application`, `Persistence`, and the host project.

Dependency direction: `Domain` ← `Application` ← `Persistence`; the host references all four;
`Habit_Tracker.Client` references only `Application`.

### Package references per project

- **Host (`Habit_Tracker.csproj`)**: `Microsoft.EntityFrameworkCore.Design` 10.0.11,
  `Npgsql.DependencyInjection` 10.0.3, `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.10.
  `<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>` (see gotchas).
  Project references: Client, Domain, Application, Persistence.
- **Client (`Habit_Tracker.Client.csproj`)**: `Microsoft.AspNetCore.Components.Authorization` 10.0.11,
  `Microsoft.AspNetCore.Components.WebAssembly` 10.0.10. Same
  `BlazorDisableThrowNavigationException` property. `NoDefaultLaunchSettingsFile=true`,
  `StaticWebAssetProjectMode=Default`. Project reference: Application.
- **Domain**: no package references. Project references: none.
- **Application**: no package references. Project reference: Domain.
- **Persistence**: `Dapper` 2.1.79, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.10,
  `Microsoft.EntityFrameworkCore.Design` 10.0.10, `Npgsql` 10.0.3,
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. Project references: Domain, Application.
- **Tests**: `bunit` 2.9.0, `coverlet.collector` 6.0.4, `Microsoft.NET.Test.Sdk` 17.14.1,
  `Testcontainers.PostgreSql` 4.13.0, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4. Project
  references: Domain, Application, Persistence, host.

## Local Postgres

`docker-compose.yml` at the repo root spins up the dev database:

```yaml
services:
  postgres:
    image: postgres:17
    container_name: blazorapp-postgres
    environment:
      POSTGRES_DB: blazorapp
      POSTGRES_USER: blazorapp
      POSTGRES_PASSWORD: blazorapp_dev
    ports:
      - "5432:5432"
    volumes:
      - blazorapp-pgdata:/var/lib/postgresql/data
```

```
docker compose up -d
```

`Habit_Tracker/Habit_Tracker/appsettings.Development.json` has the matching connection string:
`Host=localhost;Port=5432;Database=blazorapp;Username=blazorapp;Password=blazorapp_dev` under
`ConnectionStrings:DefaultConnection`. Note the DB/user/container names stayed `blazorapp` even after
the .NET projects were renamed to `Habit_Tracker` — they're an independent naming space (infra, not
project names) and were left as-is by design.

## Domain model

**`Habit`** (`Habit_Tracker.Domain.Entities.Habit`):

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK → `ApplicationUser`, cascade delete |
| `Name` | `string` | required, max 200 |
| `IsCompletable` | `bool` | |
| `CompletionMethod` | `HabitCompletionMethod?` | only set when `IsCompletable` |
| `TargetValue` | `int?` | only set when `CompletionMethod == Total` |
| `Unit` / `UnitPlural` | `string?` | max 50 each; only set when `CompletionMethod == Total` |
| `TargetType` | `HabitTargetType?` | only set when `CompletionMethod == Total` |
| `ParentHabitId` | `Guid?` | self-referencing FK, `DeleteBehavior.Restrict` |
| `CreatedAt` | `DateTimeOffset` | |
| `ParentHabit` / `SubHabits` / `Entries` | nav properties | |

The `ParentHabit`↔`SubHabits` FK is `Restrict`, not `Cascade`, **on purpose**: deleting a parent habit
must not silently cascade-delete its sub-habits' own entry history. The app layer (`HabitCommands`)
detaches sub-habits (sets `ParentHabitId = null`) before removing a parent instead.

**`HabitEntry`** (`Habit_Tracker.Domain.Entities.HabitEntry`):

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `HabitId` | `Guid` | FK → `Habit`, cascade delete |
| `TrackedAt` | `DateTimeOffset` | indexed |
| `IsCompleted` | `bool` | only meaningful for `SubHabits`-method habits |
| `Amount` | `int?` | see storage rules below |

**`HabitCompletionMethod`** enum: `SubHabits = 0`, `Total = 1`.

**`HabitTargetType`** enum: `OnceOff = 0`, `PerEntry = 1`. Only meaningful when `CompletionMethod` is
`Total`.

Both enums are stored as plain `integer` columns (default EF convention) — no value converters.

## Application layer

DTOs (`Habit_Tracker.Application.DTOs`) mirror the domain fields plus a computed
`CompletionPercentage`:

- **`HabitDto`** — flat habit projection used for the top-level list; adds `CompletionPercentage`
  (`int`, 0–100).
- **`CreateHabitRequest`** — `UserId`, `Name` (required), `IsCompletable`, `CompletionMethod`,
  `TargetValue`, `Unit`, `UnitPlural`, `TargetType`, `ParentHabitId`.
- **`UpdateHabitRequest`** — same as create plus `HabitId`, `UserId`, and `RemovedSubHabitIds`
  (`List<Guid>`, defaults to empty) — sub-habits to detach (not delete) from this habit on save.
- **`HabitTreeNodeDto`** — recursive: a habit plus its full descendant tree. **Leaf** nodes (no
  sub-habits) carry `Entries`; **non-leaf** nodes carry `SubHabits` instead — never both.
- **`HabitEntryDto`** — `Id`, `HabitId`, `TrackedAt`, `IsCompleted`, `Amount`.

Interfaces (implemented by `Persistence`):

```csharp
public interface IHabitCommands
{
    Task<HabitDto> CreateHabitAsync(CreateHabitRequest request, CancellationToken cancellationToken = default);
    Task UpdateHabitAsync(UpdateHabitRequest request, CancellationToken cancellationToken = default);
    Task DeleteHabitAsync(Guid habitId, Guid userId, CancellationToken cancellationToken = default);
    Task AddHabitEntryAsync(Guid habitId, Guid userId, bool isCompleted, int? amount, CancellationToken cancellationToken = default);
}

public interface IHabitQueries
{
    Task<IReadOnlyList<HabitDto>> GetHabitsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    // Returns null if the habit doesn't exist or isn't owned by userId.
    Task<HabitTreeNodeDto?> GetHabitTreeAsync(Guid habitId, Guid userId, CancellationToken cancellationToken = default);
}
```

## Persistence layer

`ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` exposes
`DbSet<Habit> Habits` and `DbSet<HabitEntry> HabitEntries`, and applies entity configurations via
`builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)`.

`ApplicationUser : IdentityUser<Guid>` — no extra properties beyond the Identity defaults (no display
name, no profile fields).

Entity configurations:
- **`HabitConfiguration`** — `Name` required/max200; `Unit`/`UnitPlural` max50; `ParentHabit` FK
  `DeleteBehavior.Restrict`; `UserId` FK to `ApplicationUser` `DeleteBehavior.Cascade`; index on
  `UserId`.
- **`HabitEntryConfiguration`** — `Habit` FK `DeleteBehavior.Cascade`; index on `TrackedAt`.

### Write path — `HabitCommands` (EF Core)

`CreateHabitAsync`:
1. If `ParentHabitId` is set, verify a habit with that id **and matching `UserId`** exists — otherwise
   throw `InvalidOperationException("The selected parent habit does not exist.")`. This is the only
   thing stopping a user from parenting their habit under someone else's.
2. Call `ValidateCompletionSettings` (below).
3. `isTotal = CompletionMethod == Total`. Only persist `TargetValue`/`Unit`/`UnitPlural`/`TargetType`
   when `isTotal`; only persist `CompletionMethod` when `IsCompletable` — everything else is forced to
   `null` regardless of what the request sent, so stale field values from an unrelated
   completion-method choice can never leak into storage.
4. Returns a `HabitDto` with `CompletionPercentage = 0` (a brand-new habit has no entries yet).

`UpdateHabitAsync`: same lookup-by-id-and-userId-or-throw, same `ValidateCompletionSettings` call, same
field-gating-by-`isTotal`/`IsCompletable` as create. Additionally: for each id in
`RemovedSubHabitIds`, detach it (`ParentHabitId = null`) **but only if its current `ParentHabitId`
actually equals this habit's id** — a caller can't use this to detach an arbitrary habit that happens
to belong to someone else's tree.

`DeleteHabitAsync`: look up by id+userId or throw. Detach (not cascade) all direct sub-habits first,
then remove the habit — its own `HabitEntries` still cascade-delete via the FK.

`AddHabitEntryAsync`: look up by id+userId or throw. Reject `amount < 0` with
`InvalidOperationException("Amount cannot be negative.")`. Then:
```csharp
var isTotal = habit.CompletionMethod == HabitCompletionMethod.Total;
var usesCheckbox = habit.IsCompletable && !isTotal;
IsCompleted = usesCheckbox && isCompleted;
Amount = usesCheckbox ? null : isTotal ? (amount ?? 0) : amount;
```
In words: `Total`-method habits always get a stored `Amount` (defaulting to `0` if omitted) and never
`IsCompleted`. Non-completable habits get whatever `Amount` was passed (may be `null`) and never
`IsCompleted`. Only "completable via sub-habits" habits use the checkbox and always store
`Amount = null`.

`ValidateCompletionSettings` (private, shared by create/update — the single authoritative gate; the UI
mirrors these rules client-side but this is what actually enforces them) throws
`InvalidOperationException` with these exact messages:
- Not completable but any of `CompletionMethod`/`TargetValue`/`Unit`/`UnitPlural`/`TargetType` is set →
  `"Completion settings only apply to completable habits."`
- Completable but `CompletionMethod is null` → `"Choose how this habit is completed."`
- `CompletionMethod == Total` and `TargetValue is null or <= 0` →
  `"Enter a target amount greater than zero."`
- `CompletionMethod == Total` and `Unit`/`UnitPlural` blank → `"Enter a unit and its plural form."`
- `CompletionMethod == Total` and `TargetType is null` →
  `"Choose whether the target is once-off or per entry."`
- `CompletionMethod == SubHabits` but any of `TargetValue`/`Unit`/`UnitPlural`/`TargetType` is set →
  `"Target amount and unit only apply when completed by total."`

### Read path — `HabitQueries` (Dapper via `NpgsqlDataSource`)

`GetHabitsForUserAsync` runs one query (`HabitsSql`) that computes `CompletionPercentage` per habit in
SQL:

```sql
SELECT
    h."Id", h."Name", h."IsCompletable", h."CompletionMethod", h."TargetValue",
    h."Unit", h."UnitPlural", h."TargetType", h."ParentHabitId", h."CreatedAt",
    COALESCE(
        CASE
            WHEN h."CompletionMethod" = 1 AND h."TargetValue" > 0 AND h."TargetType" = 1
                THEN ROUND(AVG(LEAST(100.0, 100.0 * e."Amount" / h."TargetValue")) FILTER (WHERE e."Amount" IS NOT NULL))::int
            WHEN h."CompletionMethod" = 1 AND h."TargetValue" > 0
                THEN LEAST(100, ROUND(100.0 * COALESCE(SUM(e."Amount"), 0) / h."TargetValue"))::int
            ELSE ROUND(100.0 * COUNT(*) FILTER (WHERE e."IsCompleted") / NULLIF(COUNT(e."Id"), 0))::int
        END,
        0
    ) AS "CompletionPercentage"
FROM "Habits" h
LEFT JOIN "HabitEntries" e ON e."HabitId" = h."Id"
WHERE h."UserId" = @UserId
GROUP BY h."Id", h."Name", h."IsCompletable", h."CompletionMethod", h."TargetValue", h."Unit", h."UnitPlural", h."TargetType", h."ParentHabitId", h."CreatedAt"
ORDER BY h."CreatedAt"
```

Three branches, in order: per-entry-average-capped-at-100 (Total + PerEntry), sum-capped-at-100 (Total
+ OnceOff), and checkbox-completion-ratio (everything else, including non-completable habits, which
naturally yields `0/0 → NULL → COALESCE → 0`). **The `FILTER (WHERE e."Amount" IS NOT NULL)` on the
per-entry branch is required, not cosmetic** — see "Gotchas" below for why.

`GetHabitTreeAsync(habitId, userId)`:
1. Calls `GetHabitsForUserAsync` (reuses the same flat query/ownership check) and looks up `habitId` in
   the result — returns `null` if absent (covers both "doesn't exist" and "not owned by this user").
2. Builds the tree in memory via `allHabits.Where(h => h.ParentHabitId is not null).ToLookup(h =>
   h.ParentHabitId!.Value)`, then recursively (`BuildNode`).
3. Recursively collects the ids of every **leaf** node (`CollectLeafIds`) — a node with
   `SubHabits.Count == 0`.
4. If there are any leaves, runs a second query (`EntriesForHabitsSql`, `WHERE "HabitId" =
   ANY(@HabitIds)`) to fetch their entries, then attaches them **only to leaf nodes**
   (`AttachEntries`) — non-leaf nodes always have an empty `Entries` list.

### Migrations (in order)

1. `InitialCreate` — Identity's full schema (`AspNetUsers`, `AspNetRoles`, etc.) plus `Habits`
   (`Id`, `UserId`, `Name`, `IsCompletable`, `ParentHabitId`, `CreatedAt`) and `HabitEntries` (`Id`,
   `HabitId`, `TrackedAt`, `IsCompleted`).
2. `AddHabitCompletionSettings` — adds `CompletionMethod` (`integer?`), `TargetValue` (`integer?`),
   `Unit`/`UnitPlural` (`varchar(50)?`) to `Habits`.
3. `AddHabitEntryAmount` — adds `Amount` (`integer?`) to `HabitEntries`.
4. `AddHabitTargetType` — adds `TargetType` (`integer?`) to `Habits`.

Run migration commands with the persistence project as `--project` and the host as
`--startup-project`:

```
dotnet ef migrations add <Name> --project Habit_Tracker/Habit_Tracker.Persistence --startup-project Habit_Tracker/Habit_Tracker/Habit_Tracker.csproj
dotnet ef database update --project Habit_Tracker/Habit_Tracker.Persistence --startup-project Habit_Tracker/Habit_Tracker/Habit_Tracker.csproj
```

## Host project (`Habit_Tracker`)

`Program.cs` wiring, in order:
- `AddRazorComponents().AddInteractiveWebAssemblyComponents()`.
- `AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString))` and
  `AddNpgsqlDataSource(connectionString)` (both read from `ConnectionStrings:DefaultConnection`) — EF
  Core and Dapper each get their own connection abstraction from the same string.
- `AddScoped<IHabitQueries, HabitQueries>()`, `AddScoped<IHabitCommands, HabitCommands>()`.
- `AddCascadingAuthenticationState()`; `AddScoped<AuthenticationStateProvider,
  PersistingRevalidatingAuthenticationStateProvider>()`.
- `AddAuthentication(o => { o.DefaultScheme = IdentityConstants.ApplicationScheme; o.DefaultSignInScheme
  = IdentityConstants.ExternalScheme; }).AddIdentityCookies()`; `AddAuthorization()`.
- `AddIdentityCore<ApplicationUser>(o => o.SignIn.RequireConfirmedAccount = false)
  .AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>()
  .AddSignInManager().AddDefaultTokenProviders()`.
- Pipeline: `UseWebAssemblyDebugging()` in dev, else `UseExceptionHandler("/Error",
  createScopeForErrors: true)` + `UseHsts()`; `UseStatusCodePagesWithReExecute("/not-found",
  createScopeForStatusCodePages: true)`; `UseHttpsRedirection()`; `UseAuthentication()`;
  `UseAuthorization()`; `UseAntiforgery()`; `MapStaticAssets()`;
  `MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()
  .AddAdditionalAssemblies(typeof(Habit_Tracker.Client._Imports).Assembly)`;
  `MapAdditionalIdentityEndpoints()`; `MapHabitEndpoints()`.

### Minimal API endpoints (`Habits/HabitEndpointRouteBuilderExtensions.cs`)

A `/Habits` route group with `.RequireAuthorization()`:
- `POST /Habits/{habitId:guid}/Delete` — calls `DeleteHabitAsync`, redirects to `~/`.
- `POST /Habits/{habitId:guid}/Entries` — form fields `isCompleted` (bool, default false), `amount`
  (int?), `returnUrl` (string?); calls `AddHabitEntryAsync`, redirects to `returnUrl` or `~/`.

Both read the user id from `ClaimTypes.NameIdentifier` on the `ClaimsPrincipal`. **These are plain
HTML form posts, not Blazor `EditForm`s, on purpose**: the habit list is a dynamic per-row `@foreach`,
and `[SupplyParameterFromForm]` needs one statically-named form per page — a fixed-route minimal API
endpoint has no such constraint, so every row can point its `<form>`/button at the same route with a
different `{habitId}`.

## Authentication

Cookie-based ASP.NET Core Identity, but **deliberately not the full scaffolded surface**. A stock
`dotnet new blazor -au Individual` template generates a large `Components/Account/Pages/` tree
(Register, Login, Logout, RegisterConfirmation, ForgotPassword(+Confirmation), ResetPassword(+
Confirmation), ExternalLogin, LoginWith2fa, LoginWithRecoveryCode, Lockout, ConfirmEmail(+Change),
ResendEmailConfirmation, AccessDenied, and a `Manage/` sub-tree for profile/2FA/external-logins/
personal-data/password-change), plus `IdentityRedirectManager`, `IdentityUserAccessor`, and an
`IdentityNoOpEmailSender`. **This app keeps only:**

- `Components/Account/Pages/Login.razor` (`/Account/Login`) — email+password+"remember me", posts via
  `SignInManager.PasswordSignInAsync`, `[SupplyParameterFromForm]` `Input`, `[SupplyParameterFromQuery]
  ReturnUrl`.
- `Components/Account/Pages/Register.razor` (`/Account/Register`) — email+password+confirm, creates
  the user via `UserManager.CreateAsync` then signs in immediately with
  `SignInManager.SignInAsync(user, isPersistent: false)` — no email confirmation step.
- `Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` — a single `POST
  /Account/Logout` minimal-API endpoint (not a Razor component — signing out has to happen via a real
  form POST so the cookie change takes effect on the redirect that follows). Reads `returnUrl` from the
  form and redirects to it.
- `Components/Account/PersistingRevalidatingAuthenticationStateProvider.cs`.

No email confirmation, no external login providers, no 2FA, no password reset, no account management
pages, no email sender. `ApplicationUser` adds no properties beyond `IdentityUser<Guid>`. If
regenerating this from a fresh Identity-scaffolded template, delete everything else under
`Components/Account/` and don't wire up the pages this app doesn't use.

### Server ↔ WASM auth-state handoff

Because auth state must flow from the server to interactive WASM components with no way for the
browser to read the auth cookie or call back before first render, the standard Blazor Web App +
Identity pattern is used:

- **Server**: `PersistingRevalidatingAuthenticationStateProvider` (extends
  `RevalidatingServerAuthenticationStateProvider`) re-validates the user's security stamp every 30
  minutes (`RevalidationInterval`), and on `PersistentComponentState.RegisterOnPersisting(...,
  RenderMode.InteractiveWebAssembly)` persists a `UserInfo` (JSON, key `nameof(UserInfo)`) containing
  the `sub` (user id) and `name` claims into the page.
- **Client** (`Habit_Tracker.Client`): `PersistentAuthenticationStateProvider` reads that same
  `UserInfo` back via `PersistentComponentState.TryTakeFromJson`, and builds a `ClaimsPrincipal` from
  it directly — no HTTP call. Falls back to an unauthenticated `ClaimsPrincipal` if nothing was
  persisted.
- `UserInfo` (`Habit_Tracker.Client`) — `UserId`, `Name`, plus the claim-type constants `"sub"` and
  `"name"`.

Do not implement a separate custom auth/token scheme — this persisted-claims handoff is the whole
point of the pattern.

## Render modes

Per-component interactive render modes, not one global mode:

- The host registers WASM interactivity (`AddInteractiveWebAssemblyComponents()` /
  `AddInteractiveWebAssemblyRenderMode()`) and adds the client assembly via
  `AddAdditionalAssemblies(typeof(Habit_Tracker.Client._Imports).Assembly)`.
- Every habit-tracking component (`Home.razor`, `HabitDetails.razor`, the three modals,
  `HabitNodeView`, `HabitProgressRing`) is a **static server-rendered** component — none of them opt
  into `@rendermode`. All create/edit/delete/add-entry actions go through plain HTML form posts (see
  above), which is why they can stay static.
- `Habit_Tracker.Client/Pages/Counter.razor` is the only page that opts into
  `@rendermode InteractiveWebAssembly` — an unmodified template leftover, not part of the
  habit-tracking feature.
- `Weather.razor` demonstrates `[StreamRendering]`, also an unmodified template leftover.
- There is **no `InteractiveServer`** registered anywhere in this app.
- Routing is unified via `Routes.razor` in the host, which points the `Router` at both
  `typeof(Program).Assembly` and `typeof(Client._Imports).Assembly`.

## UI: pages and components

All habit-tracking UI lives under `Habit_Tracker/Habit_Tracker/Components/`.

**`Pages/Home.razor`** (`/`) — if not authenticated, shows a login link. Otherwise renders
`<AddHabitModal>`, `<EditHabitModal>`, `<AddEntryModal>` (see below), then the **top-level habits
only** (`habits.Where(h => h.ParentHabitId is null)`) as a `ul.habit-list`. Each `li.habit-list-item`
shows: a `<HabitProgressRing>` only if `habit.IsCompletable`; the habit name; a meta line — for
`Total` habits, `"{TargetValue} {unit-or-plural} {per entry|total}"` (unit pluralized off
`TargetValue == 1`), for `SubHabits` habits, `"via sub-habits"`; then `.habit-actions` icon buttons:
a View link (`/habits/{id}`), an Edit button (opens `EditHabitModal` via `openEditModal(this)`,
passing every editable field plus a JSON-serialized sub-habit id/name list through `data-*`
attributes), an Add-entry button (**shown whenever `CompletionMethod != SubHabits`** — i.e. for
`Total`-method habits and non-completable habits, not for `SubHabits`-method habits, since those are
completed by completing their sub-habits, not by tracking the parent directly), and a Delete button
(a real `<form method="post">` to `/Habits/{id}/Delete` with a JS `confirm()` guard and an
`<AntiforgeryToken>`).

**`Pages/HabitDetails.razor`** (`/habits/{HabitId:guid}`) — loads the full tree via
`IHabitQueries.GetHabitTreeAsync`; shows "not found" if null. Renders a `<HabitProgressRing>` in the
`<h1>` only if `habit.IsCompletable`. Then: if `habit.CompletionMethod != Total`, wraps
`<HabitNodeView Node="habit" />` in `<ul class="habit-node-list"><li><div
class="habit-node-detail">...</div></li></ul>`; if it **is** `Total`, renders `<HabitNodeView
Node="habit" />` directly with no wrapper. Always renders a trailing `<AddEntryModal>`.

**`Shared/HabitNodeView.razor`** — the recursive tree renderer, given a `HabitTreeNodeDto Node`:
- If `Node.SubHabits.Count > 0`: renders a `ul.habit-node-list` of `<details><summary>...</summary>
  <div class="habit-node-detail"><HabitNodeView Node="child" /></div></details>` per child — a native
  `<details>`/`<summary>` accordion, recursing into itself for arbitrary depth. Each `<summary>` shows
  a progress ring (if `child.IsCompletable`), the name, and in `.habit-node-action`: a View link
  (`/habits/{child.Id}`) if `child.SubHabits.Count > 0`, otherwise an Add-entry button
  (`onclick="event.preventDefault(); openAddEntryModal(...)"` — `preventDefault` is required so
  clicking the button doesn't also toggle the parent `<details>`).
- Else if `Node.Entries.Count == 0`: `"No entries tracked yet."`
- Else: a `ul.entry-list` of entries, each showing `TrackedAt` (`ToLocalTime().ToString("f")`) and
  either an amount badge (`entry.Amount is not null` — pluralized the same way as the list meta line)
  or, if `Node.IsCompletable`, a Completed/Not completed badge.

**`Shared/HabitProgressRing.razor`** — a 48×48 SVG ring (`[Parameter] int PercentComplete`, clamped
0–100), radius 20, `stroke-dasharray`/`stroke-dashoffset` computed from the clamped percent, rotated
`-90deg` so the fill starts at 12 o'clock, with the percentage as centered SVG text. **All numeric SVG
attributes are formatted with `CultureInfo.InvariantCulture`** — see gotchas.

**`Shared/AddHabitModal.razor`** — a native `<dialog id="add-habit-modal">` opened by a `.add-btn`
("+ Add Habit") button via `showModal()`. An `EditForm` (`FormName="create-habit"`,
`[SupplyParameterFromForm(FormName = "create-habit")]`) with: name; an `IsCompletable` checkbox
(`onchange="toggleCompletionFields()"`) that shows/hides `#completion-settings`; inside that, a
`CompletionMethod` radio group (`onchange="toggleTotalFields()"`) that shows/hides `#total-settings`;
inside that, target amount/unit/unit-plural inputs and a `TargetType` radio group; and a parent-habit
`<select>` populated from the `Habits` parameter. `IValidatableObject` on the input model duplicates
the `Total`-branch validation (target > 0, unit/unit-plural non-blank) client-side for immediate
feedback; the server-side `ValidateCompletionSettings` in `HabitCommands` is still the authoritative
check. On success, `NavigationManager.NavigateTo("/", forceLoad: true)`; on
`InvalidOperationException`, shows the message and re-opens the modal via a `reopenModal` flag + an
injected `<script>` calling `showModal()` again (since the page did a full static-SSR round trip, the
dialog's open state doesn't survive without this).

**`Shared/EditHabitModal.razor`** — same shape as Add, `FormName="edit-habit"`, plus hidden inputs
`#edit-habit-id` and `#edit-removed-subhabits` (a CSV of ids, rebuilt by `updateRemovedSubHabits()`
whenever a checkbox in `#edit-subhabits-list` changes). `openEditModal(button)` (called from Home's
per-row Edit button) reads the button's `data-*` attributes and populates every field, including
rebuilding the sub-habit removal checkboxes from the JSON in `data-subhabits`, then calls
`showModal()`.

**`Shared/AddEntryModal.razor`** — one shared `<dialog id="add-entry-modal">` reused everywhere an
Add-entry button appears. A plain `<form id="add-entry-form" method="post">` (not an `EditForm`) with
an `<AntiforgeryToken>`, a hidden `returnUrl`, an amount `<input>` wrapper, and a "mark as completed"
checkbox wrapper. `openAddEntryModal(habitId, isCompletable, completionMethod, unit, unitPlural)` sets
the form's `action` to `/Habits/{habitId}/Entries`, sets `returnUrl` to
`window.location.pathname`, resets both inputs, computes `showCheckbox = (isCompletable === 'true')
&& completionMethod !== 'Total'` and toggles the two wrappers accordingly, sets the amount label to
`"Amount ({unitPlural})"` when a plural unit was passed, then calls `showModal()`.

**`Components/App.razor`** — the root HTML document: Bootstrap CSS, `app.css`, the auto-generated
`Habit_Tracker.styles.css` (CSS isolation bundle), `<ImportMap>`, `favicon.png`, `<HeadOutlet>`,
`<Routes>`, `blazor.web.js` — all referenced via the `@Assets[...]` fingerprinting API, not raw
`<link href>`.

**`Components/Layout/MainLayout.razor`** / **`NavMenu.razor`** — stock template layout; the nav brand
reads `"Habit_Tracker"`; the `<AuthorizeView>` block shows "Hello, {name}" + a Logout form when
authenticated, or Register/Login links when not.

**`Components/Pages/Weather.razor`**, **`Client/Pages/Counter.razor`**, **`Components/Pages/
Error.razor`**, **`Components/Pages/NotFound.razor`** — unmodified `dotnet new blazor` template
pages, kept as harmless leftovers. Not part of the habit-tracking feature; don't remove or "fix" them
as part of habit-tracking work.

## Styling (`wwwroot/app.css`)

Stock template CSS (Bootstrap classes, `.blazor-error-boundary`, validation styles) plus these
habit-tracking-specific rules appended at the bottom:

```css
.habit-list, .habit-node-list, .entry-list { list-style: none; padding: 0; max-width: 32rem; }
.habit-list-item, .entry-list li { display: flex; align-items: center; gap: 1rem; padding: 0.5rem 0; border-bottom: 1px solid #e0e0e0; }
.habit-name { font-weight: 500; }
.habit-meta { color: #666; font-size: 0.85rem; }
.habit-actions { display: flex; gap: 0.25rem; margin-left: auto; }
.icon-btn { display: inline-flex; align-items: center; justify-content: center; width: 2rem; height: 2rem; border: none; background: transparent; color: #555; border-radius: 0.375rem; cursor: pointer; text-decoration: none; }
.icon-btn:hover { background: #eee; color: #222; }
.icon-btn-danger:hover { background: #fbdada; color: #a30000; }

.habit-node-list summary { display: flex; align-items: center; gap: 1rem; padding: 0.5rem 0; cursor: pointer; list-style: none; }
.habit-node-list summary::-webkit-details-marker { display: none; }
.habit-node-list summary::marker { content: ""; }
.habit-node-list summary::after {
    content: ""; display: inline-block; width: 0.85rem; height: 0.85rem;
    border-style: solid; border-color: #666; border-width: 0 3px 3px 0;
    transform: rotate(45deg); transition: transform 0.15s ease; margin-bottom: 0.2rem;
}
.habit-node-list details[open] > summary::after { transform: rotate(-135deg); margin-bottom: -0.15rem; }
.habit-node-action { margin-left: auto; display: flex; }
.habit-node-detail { padding: 0.5rem 0 0.5rem 3.5rem; }

.progress-ring__track { fill: none; stroke: #e0e0e0; stroke-width: 4; }
.progress-ring__fill { fill: none; stroke: #1b6ec2; stroke-width: 4; stroke-linecap: round; transition: stroke-dashoffset 0.3s ease; }
.progress-ring__label { font-size: 0.65rem; fill: #333; }

.add-btn { float: right; background-color: dodgerblue; color: whitesmoke; border-radius: 1rem; border: 1px solid gray; }
#add-habit-modal { width: 50%; border-radius: 1rem; border: 1px solid rgba(150, 150, 150, 0.3); }
#edit-habit-modal { border-radius: 1rem; border: 1px solid rgba(150, 150, 150, 0.3); }
```

The accordion chevron is a **CSS-only thin outline chevron** (two rotated border edges), not a Unicode
glyph or an image: closed = `rotate(45deg)`, open = `rotate(-135deg)`, driven purely by the `[open]`
attribute `<details>` toggles natively — no JS needed for the open/close animation itself.

## Testing (`Habit_Tracker.Tests`)

xUnit, with two distinct strategies:

- **Persistence integration tests** (`Persistence/HabitCommandsTests.cs`,
  `Persistence/HabitQueriesTests.cs`) run against a **real** ephemeral Postgres 17 container via
  `Testcontainers.PostgreSql`, because the Dapper queries use Postgres-specific dialect (`LEAST`,
  `FILTER (WHERE ...)`, `::int` casts, `ANY(@array)`) that an in-memory or SQLite provider can't stand
  in for. `Infrastructure/PostgresContainerFixture` (`IAsyncLifetime`) starts one container and runs
  `Database.MigrateAsync()`; `Infrastructure/PostgresCollection` (`[CollectionDefinition]` +
  `ICollectionFixture<PostgresContainerFixture>`) shares that one container across every test class in
  the `"Postgres"` collection while still serializing those classes relative to each other (xUnit runs
  different collections in parallel, but classes within one collection sequentially).
  `Infrastructure/TestDataHelper.CreateTestUserAsync` inserts an `ApplicationUser` row directly
  (bypassing `UserManager` — password hashing is irrelevant here, only the FK from `Habits.UserId`
  matters).
  - `HabitCommandsTests` covers every `ValidateCompletionSettings` branch, the parent-ownership check
    on create, selective sub-habit detachment on update (including the "can't detach a habit that
    isn't actually your child" case), cascade-vs-detach behavior on delete, and every
    `AddHabitEntryAsync` storage-rule branch (negative amount, non-completable amount storage/omission,
    Total defaulting to zero, checkbox path ignoring amount).
  - `HabitQueriesTests` covers per-user isolation, all three `CompletionPercentage` formulas (checkbox
    ratio, once-off sum capped at 100, per-entry average capped at 100), **and** a regression test for
    the Postgres `LEAST()`-ignores-NULL bug (a legacy no-amount entry must not be counted in the
    per-entry average), plus `GetHabitTreeAsync` null cases and multi-level tree building with entries
    attached only at leaves.
- **Component tests** (`Components/HabitProgressRingTests.cs`, `Components/HabitNodeViewTests.cs`) use
  bUnit. `HabitProgressRingTests` covers percent clamping and — critically — an
  `InvariantCulture` regression test that sets `CultureInfo.CurrentCulture` to `de-DE` and asserts no
  comma appears in `stroke-dasharray`/`stroke-dashoffset`. `HabitNodeViewTests` covers leaf/accordion
  rendering, View-vs-Add-entry link branching at every tree depth, singular/plural unit rendering, and
  that the progress ring is hidden for non-completable habits.

  **bUnit 2.9.0 API note**: inherit `BunitContext`, not `TestContext` (obsolete warning); call
  `Render<T>(...)`, not `RenderComponent<T>(...)` (obsolete as a **compile error**, CS0619, in this
  version).

**Deliberately out of scope** (a scoping decision, not an oversight): no `WebApplicationFactory`-based
HTTP tests of the `/Habits/{id}/Delete` / `/Habits/{id}/Entries` minimal API endpoints, no Login/
Register/Logout flow tests, and no bUnit tests of `AddHabitModal`/`EditHabitModal` — bUnit can't drive
the static-SSR `[SupplyParameterFromForm]` pipeline that caused the real production bugs found while
building this app (see gotchas), so a form-submission bug there would need `WebApplicationFactory` +
an actual HTML form post, not bUnit.

Run everything with (Docker must be running):

```
dotnet test Habit_Tracker.slnx
```

## Known implementation gotchas (reproduce these exactly)

These were each discovered by hitting a real bug while building this app. Skipping them will silently
reintroduce the same bug:

1. **SVG culture bug**: SVG numeric attributes (`stroke-dasharray`, `stroke-dashoffset`) must be
   formatted with `CultureInfo.InvariantCulture`. Under a comma-decimal server locale (e.g. `de-DE`),
   the default `double.ToString()` renders e.g. `"125,66"`, which SVG parses as a two-value
   `dasharray` list — the progress ring silently renders full/solid regardless of the actual
   percentage. Guarded by `HabitProgressRingTests.UsesInvariantDecimalSeparatorRegardlessOfCurrentCulture`.
2. **Postgres `LEAST()`/`GREATEST()` ignore `NULL` args instead of propagating them.** The per-entry
   completion branch must `FILTER (WHERE e."Amount" IS NOT NULL)`, not `WHERE e."Id" IS NOT NULL` —
   otherwise a legacy entry with a `NULL` amount (e.g. from before a habit was reconfigured from
   `SubHabits` to `Total`) gets scored as 100% complete instead of being excluded from the average.
   Guarded by `HabitQueriesTests.GetHabitsForUserAsync_TotalPerEntry_IgnoresLegacyEntriesWithNoAmount`.
3. **Multiple `[SupplyParameterFromForm]` properties need distinct explicit `FormName`s.** Two such
   properties on the same page/component tree without explicit, unique `FormName` values produce a
   runtime 500: "EditForm requires either a Model parameter, or an EditContext parameter." This is why
   `AddHabitModal`/`EditHabitModal` each declare `FormName="create-habit"`/`"edit-habit"` explicitly —
   and it works correctly even though the `EditForm` lives in a nested shared component, not the
   routable page itself.
4. **Buttons inside `<summary>` need `event.preventDefault()`.** Clicking a button nested inside a
   `<summary>` also toggles the parent `<details>` by default (native browser behavior) — the
   Add-entry button inside `HabitNodeView`'s accordion rows calls
   `onclick="event.preventDefault(); openAddEntryModal(...)"` to suppress that.
5. **`NavigationManager.NavigateTo(..., forceLoad: true)` after a static-SSR form post requires
   `<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`** in both the
   host's and the Client's `.csproj` — every modal navigates this way after a successful submit.
6. **Delete/Add-entry can't be Blazor `EditForm` posts.** The habit list is a dynamic per-row
   `@foreach`, and `[SupplyParameterFromForm]` needs one statically-named form per page/subtree — a
   loop can't provide that. They're plain HTML `<form>` posts to fixed minimal-API routes
   (`/Habits/{id}/Delete`, `/Habits/{id}/Entries`) instead, each row differing only by `{habitId}` in
   the URL.

## Azure deployment

Deployed to Azure App Service + Azure Database for PostgreSQL Flexible Server, both provisioned
directly (not via the Portal's bundled "Web App + Database" flow — that flow rejects Free/Trial
subscriptions with "Selected subscription type is not supported"). Resources: resource group
`rg-habit-tracker` (South Africa North), Postgres Flexible Server `habittracker-pg` (Burstable
B1ms, PG 17, database `habittracker`), App Service Plan `asp-habit-tracker` (Linux, F1 Free — this
app never uses `InteractiveServer`/SignalR circuits, so the Free tier's lack of WebSocket-friendly
always-on hosting is fine), Web App `habit-tracker-cf` (.NET 10 on Linux).

Deploys via GitHub Actions on every push to `main` (`.github/workflows/main_habit-tracker-cf.yml`).
Each of these was a real, non-obvious failure hit while setting this up — reproduce them exactly:

1. **Connection string must be type `Custom`, not `PostgreSQL`.** Azure exposes a `PostgreSQL`-typed
   App Service connection string as an env var prefixed `POSTGRESQLCONNSTR_*`, but .NET's
   `EnvironmentVariablesConfigurationProvider` only special-cases `CUSTOMCONNSTR_`/`SQLCONNSTR_`/
   `SQLAZURECONNSTR_`/`MYSQLCONNSTR_` back into `ConnectionStrings:<name>` — `POSTGRESQLCONNSTR_` isn't
   in that list, so `builder.Configuration.GetConnectionString("DefaultConnection")` silently returns
   null. Set it with `az webapp config connection-string set --connection-string-type Custom`.
2. **`ForwardedHeadersMiddleware` is required**, registered as the very first thing after
   `builder.Build()` in `Program.cs`, with `KnownNetworks`/`KnownProxies` explicitly `.Clear()`'d (the
   default only trusts loopback, and Azure's front-end isn't loopback) — otherwise
   `UseHttpsRedirection`/Identity's secure cookies can't tell the original request was HTTPS.
3. **The Linux container's entry-point auto-detection needs an explicit startup command.** The
   host's publish output legitimately contains *two* `.runtimeconfig.json` files —
   `Habit_Tracker.runtimeconfig.json` (the host) and `Habit_Tracker.Client.runtimeconfig.json` (the
   WASM client's own runtimeconfig, copied in as part of the Blazor Web App static-asset bundling) —
   and Oryx's auto-detect requires exactly one, silently falling back to Azure's built-in placeholder
   site (no error, `state: Running`, HTTP 200) if it finds two. Set an explicit startup command:
   `az webapp config set --startup-file "dotnet Habit_Tracker.dll"`.
4. **CI must publish only the host project, not the whole solution.** `dotnet publish` with no
   project argument at the solution level also publishes `Habit_Tracker.Tests` (pulling in bUnit,
   coverlet, Testcontainers, xunit, etc.) into the same output folder, which compounds gotcha #3 with
   even more ambiguous entry points. The workflow publishes
   `Habit_Tracker/Habit_Tracker/Habit_Tracker.csproj` explicitly.
5. **Zip deploy does not clean the target directory first** (`CleanOutputPath: False` in the Kudu
   deployment log) — files from a bad deploy (e.g. gotcha #4 before it was fixed) persist in
   `/home/site/wwwroot` and keep confusing entry-point detection even after a later, correct deploy.
   Recovery requires clearing it out by hand (Kudu `/api/command`, `bash -c "rm -rf
   /home/site/wwwroot/*"` — note the command endpoint does *not* go through a shell by default, so
   glob expansion and `&&` chaining silently don't work unless you invoke `bash -c "..."` explicitly).
6. **GitHub Actions deploys via OIDC federated login, not a publish profile.** A publish-profile
   secret was the first approach tried, but Azure CLI's output redaction masks the password in
   *every* form of output (`-o json`, `-o xml`, even raw `az rest` passthrough) — there is no flag to
   get the real value back out, so a freshly-fetched publish profile is unusable. Instead: an Azure AD
   app registration + service principal with `Contributor` scoped to the resource group, a federated
   credential trusting `token.actions.githubusercontent.com`, and `azure/login@v2` +
   `azure/webapps-deploy@v3` (no secret stored). **The federated credential's `subject` must include
   GitHub's immutable owner/repo IDs**, not just the names —
   `repo:CorneliusFrylinck@40593090/Habit_Tracker@1333524639:ref:refs/heads/main`, not
   `repo:CorneliusFrylinck/Habit_Tracker:ref:refs/heads/main` — otherwise login fails with
   `AADSTS700213: No matching federated identity record found`.
7. **SCM basic-auth publishing credentials are disabled by default** on a fresh subscription (used
   only transiently here to debug via Kudu's VFS/command API) — leave it disabled
   (`basicPublishingCredentialsPolicies/scm` → `properties.allow=false`) unless actively debugging.
8. **Fresh subscriptions need their resource providers registered before first use** —
   `Microsoft.DBforPostgreSQL` and `Microsoft.Web` both needed `az provider register --namespace
   <name>` the first time, otherwise creation fails with `MissingSubscriptionRegistration`.
9. **Git Bash (MSYS2) mangles any CLI argument starting with `/subscriptions/...`** — it auto-converts
   the leading `/` as if it were a Windows path, corrupting `--scope` on `az role assignment create`
   into a bogus value and producing an unrelated-looking `(MissingSubscription) The request did not
   have a subscription or a valid tenant level resource provider.` error. Run that specific command
   from PowerShell instead (or prefix the value to defeat MSYS2's path conversion).

## Commands

Run all commands from the repository root (`C:\Dev\BlazorApp`), operating on `Habit_Tracker.slnx`.

```
docker compose up -d
dotnet build Habit_Tracker.slnx
dotnet run --project Habit_Tracker/Habit_Tracker/Habit_Tracker.csproj
dotnet watch --project Habit_Tracker/Habit_Tracker/Habit_Tracker.csproj
dotnet test Habit_Tracker.slnx
```

There are no lint configs or CI pipelines in this repo currently.

Local dev URLs (from `Habit_Tracker/Habit_Tracker/Properties/launchSettings.json`):
- `https` profile: `https://localhost:7131` / `http://localhost:5231`
- `http` profile: `http://localhost:5231`
