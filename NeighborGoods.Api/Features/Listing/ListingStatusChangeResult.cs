namespace NeighborGoods.Api.Features.Listing;

public enum ListingStatusChangeResult
{
    Success = 0,
    NotFound = 1,
    InvalidCurrentStatus = 2,
    InvalidTransition = 3
}
