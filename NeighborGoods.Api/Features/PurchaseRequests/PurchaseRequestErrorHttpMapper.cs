namespace NeighborGoods.Api.Features.PurchaseRequests;

public static class PurchaseRequestErrorHttpMapper
{
    public static int ToStatusCode(string code)
    {
        return code switch
        {
            "CONVERSATION_ACCESS_DENIED" or "PURCHASE_REQUEST_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
            "CONVERSATION_NOT_FOUND" or "LISTING_NOT_FOUND" or "PURCHASE_REQUEST_NOT_FOUND" => StatusCodes.Status404NotFound,
            "PURCHASE_REQUEST_ALREADY_PENDING" or "PURCHASE_REQUEST_NOT_PENDING" or "PURCHASE_REQUEST_EXPIRED"
                or "PURCHASE_REQUEST_INVALID_STATE" or "LISTING_NOT_AVAILABLE" or "LISTING_INVALID_STATUS_TRANSITION"
                => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
