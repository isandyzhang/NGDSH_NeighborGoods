namespace NeighborGoods.Web.Models.DTOs;

/// <summary>
/// 服務層操作結果
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// 服務層操作結果（帶資料）
/// </summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static new ServiceResult<T> Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

