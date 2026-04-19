namespace NeighborGoods.Api.Features.Listing;

public enum ListingStatusChangeResult
{
    Success = 0,
    NotFound = 1,
    InvalidCurrentStatus = 2,
    InvalidTransition = 3,
    InvalidDonatedListingType = 4,
    InvalidTradeListingType = 5,
    ReactivateInvalidState = 6,
    MaxActiveListingsReached = 7
}
