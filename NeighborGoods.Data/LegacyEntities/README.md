# LegacyEntities

These types mirror **existing SQL Server tables** (Identity, messaging, reviews, etc.) and are registered on `NeighborGoodsDbContext`. They were originally produced via EF Core `dbcontext scaffold` from the production schema and are maintained here alongside API-owned migrations.

The **`Listings`** row entity used for EF mapping is `NeighborGoods.Data.Listings.Listing` (not a second `Listing` type in this folder).
