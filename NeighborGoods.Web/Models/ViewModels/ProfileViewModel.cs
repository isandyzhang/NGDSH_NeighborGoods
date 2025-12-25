using NeighborGoods.Web.Models.Entities;

namespace NeighborGoods.Web.Models.ViewModels;

public class ProfileViewModel
{
    public ApplicationUser User { get; set; } = null!;
    
    /// <summary>
    /// 總刊登數
    /// </summary>
    public int TotalListings { get; set; }
    
    /// <summary>
    /// 上架中的商品數
    /// </summary>
    public int ActiveListings { get; set; }
    
    /// <summary>
    /// 已售出/已捐贈的商品數（成交紀錄）
    /// </summary>
    public int CompletedListings { get; set; }
}

