using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 商品服務介面
/// </summary>
public interface IListingService
{
    /// <summary>
    /// 搜尋商品
    /// </summary>
    Task<SearchResult<ListingIndexViewModel>> SearchListingsAsync(
        ListingSearchCriteria criteria, 
        string? currentUserId, 
        int page, 
        int pageSize);

    /// <summary>
    /// 建立商品
    /// </summary>
    Task<ServiceResult<Guid>> CreateListingAsync(
        ListingCreateViewModel model, 
        string userId);

    /// <summary>
    /// 更新商品
    /// </summary>
    Task<ServiceResult> UpdateListingAsync(
        Guid listingId,
        ListingEditViewModel model, 
        string userId);

    /// <summary>
    /// 刪除商品
    /// </summary>
    Task<ServiceResult> DeleteListingAsync(
        Guid listingId, 
        string userId);

    /// <summary>
    /// 下架商品
    /// </summary>
    Task<ServiceResult> DeactivateListingAsync(
        Guid listingId, 
        string userId);
}

