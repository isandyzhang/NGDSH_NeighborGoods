namespace NeighborGoods.Web.Models.ViewModels;

public class SellerProfileViewModel
{
    public string SellerId { get; set; } = string.Empty;
    public string SellerDisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 總成交件數（只計算有評價的交易）
    /// </summary>
    public int TotalCompletedTransactions { get; set; }
    
    /// <summary>
    /// 平均評分（只計算有評價的交易）
    /// </summary>
    public double AverageRating { get; set; }
    
    /// <summary>
    /// 成交紀錄列表（只顯示有評價的交易）
    /// </summary>
    public List<CompletedTransactionItem> CompletedTransactions { get; set; } = new List<CompletedTransactionItem>();
}

public class CompletedTransactionItem
{
    public Guid ListingId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool IsFree { get; set; }
    public int Rating { get; set; }
    public string? ReviewContent { get; set; }
    public DateTime CompletedAt { get; set; }
    public string BuyerDisplayName { get; set; } = string.Empty;
}

