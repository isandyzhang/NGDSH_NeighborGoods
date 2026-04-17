# DB-First Cutover Checklist

## Pre-Cutover Checks

- Confirm restored production target DB is healthy and reachable.
- Verify API migration history strategy:
  - API currently has `InitListingSchema` and `AlignFullWebSchema`.
  - If target DB already contains Web tables, do not run migrations blindly.
- Keep a rollback-ready DB copy before cutover.

## Safe Migration History Strategy

1. Use a staging clone of the production DB first.
2. Inspect `__EFMigrationsHistory` and compare against:
   - `20260416083608_InitListingSchema`
   - `20260417071510_AlignFullWebSchema`
3. If tables already exist from Web schema, evaluate a migration baseline/stamp approach before `database update`.

## Validation Commands

- Build API:
  - `dotnet build NeighborGoods.Api/NeighborGoods.Api.csproj -c Release`
- Build tests:
  - `dotnet build NeighborGoods.Api.Tests/NeighborGoods.Api.Tests.csproj -c Release`
- Run integration tests:
  - `dotnet test NeighborGoods.Api.Tests/NeighborGoods.Api.Tests.csproj -c Release`

## Runtime Data Safety Checks

- Validate key tables exist:
  - `Listings`, `AspNetUsers`, `Conversations`, `Messages`, `Reviews`
- Validate foreign keys:
  - `Listings.SellerId -> AspNetUsers.Id`
  - `Listings.BuyerId -> AspNetUsers.Id`
- Validate seed-independent create flow:
  - `POST /api/v1/listings` returns `201` in test environment.

## Rollback Checkpoints

- Keep previous API artifact/version ready.
- Keep pre-cutover DB snapshot/copy ready.
- If cutover fails:
  1. Roll back app version.
  2. Repoint to previous DB copy or restore point.
  3. Re-run smoke tests.
