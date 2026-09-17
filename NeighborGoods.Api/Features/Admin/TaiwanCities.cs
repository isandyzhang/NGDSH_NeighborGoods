namespace NeighborGoods.Api.Features.Admin;

public static class TaiwanCities
{
    public static readonly IReadOnlyList<string> All =
    [
        "臺北市",
        "新北市",
        "桃園市",
        "臺中市",
        "臺南市",
        "高雄市",
        "基隆市",
        "新竹市",
        "嘉義市",
        "新竹縣",
        "苗栗縣",
        "彰化縣",
        "南投縣",
        "雲林縣",
        "嘉義縣",
        "屏東縣",
        "宜蘭縣",
        "花蓮縣",
        "臺東縣",
        "澎湖縣",
        "金門縣",
        "連江縣",
    ];

    public static bool IsValid(string? city) =>
        string.IsNullOrWhiteSpace(city) || All.Contains(city.Trim());
}
