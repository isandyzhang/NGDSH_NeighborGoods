# DB-First Schema Diff Baseline

This baseline is generated from `NeighborGoods.Api/ScaffoldTemp/RecoveredDbContext.cs` after scaffolding `neighborgoods-prod-db`.

## Scaffolded Tables

- `AdminMessages`
- `AspNetRoles`
- `AspNetRoleClaims`
- `AspNetUsers`
- `AspNetUserClaims`
- `AspNetUserLogins`
- `AspNetUserTokens`
- `Conversations`
- `LineBindingPending`
- `Listings`
- `ListingImages`
- `ListingTopSubmissions`
- `Messages`
- `Reviews`

## Current API vs Scaffold Gap

### DbContext Scope Gap

- Current API `NeighborGoodsDbContext` only exposes `Listings`.
- Scaffold baseline includes 14 tables with cross-table relationships and indexes.

### Listing Shape Gap

- Missing in current API model:
  - `SellerId`, `BuyerId`
  - `IsFree`, `IsCharity`, `IsTradeable`
  - `IsPinned`, `PinnedStartDate`, `PinnedEndDate`
  - `PickupLocation`
- Type/constraint differences:
  - `Price` is `decimal(18, 0)` in scaffolded DB.
  - `Description` has max length `500`.
  - `UpdatedAt` is non-null in scaffolded model.
  - Indexes exist on `SellerId` and `BuyerId`.

### Relationship Gap

- `Listings` references `AspNetUsers` via `SellerId` and `BuyerId`.
- Conversations/Messages/Reviews/ListingImages/TopSubmissions all attach to `Listings`.
- Current API model has no navigation or FK mapping for these dependencies.

## Migration Impact

- Existing API migration history only represents a listings-only schema.
- A transition migration is required to align from listings-only API schema to full Web schema.
- Testcontainers integration must apply the aligned migration set before seeding.
