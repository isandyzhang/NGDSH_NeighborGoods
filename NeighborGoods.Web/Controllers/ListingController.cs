using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NeighborGoods.Web.Constants;
using NeighborGoods.Web.Infrastructure;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Services;

namespace NeighborGoods.Web.Controllers;

[Authorize]
public class ListingController : BaseController
{
    private readonly IListingService _listingService;
    private readonly ILogger<ListingController> _logger;

    public ListingController(
        UserManager<ApplicationUser> userManager,
        IListingService listingService,
        ILogger<ListingController> logger)
        : base(userManager)
    {
        _listingService = listingService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ListingCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge(); // 要求重新登入
        }

        // 使用服務層建立商品
        var result = await _listingService.CreateListingAsync(model, user.Id);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "建立商品時發生錯誤");
            return View(model);
        }

        // 設定成功訊息
        TempData["SuccessMessage"] = "商品已新增成功！您可以前往「我的商品」查看。";
        
        // 之後會導到「商品詳情」或「我的商品」，暫時先回首頁
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var currentUser = await GetCurrentUserAsync();
        var currentUserId = currentUser?.Id;

        // 使用服務層取得商品詳情
        var result = await _listingService.GetListingDetailsAsync(id, currentUserId);

        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "該商品不存在或已被刪除";
            return RedirectToAction("Index", "Home");
        }

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> My()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層取得用戶商品列表
        var result = await _listingService.GetUserListingsAsync(user.Id);

        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "取得商品列表時發生錯誤";
            return View(new MyListingsViewModel());
        }

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層取得商品編輯資料
        var result = await _listingService.GetListingForEditAsync(id, user.Id);

        if (!result.Success || result.Data == null)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限修改此商品")
            {
                return Forbid();
            }
            if (result.ErrorMessage?.Contains("只有刊登中或已下架的商品才能編輯") == true)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "取得商品編輯資料時發生錯誤";
            return RedirectToAction(nameof(My));
        }

        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ListingEditViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            // 重新載入現有圖片
            var listingResult = await _listingService.GetListingForEditAsync(id, user.Id);
            if (listingResult.Success && listingResult.Data != null)
            {
                model.ExistingImages = listingResult.Data.ExistingImages;
            }
            
            return View(model);
        }

        // 使用服務層更新商品
        var result = await _listingService.UpdateListingAsync(id, model, user.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限修改此商品")
            {
                return Forbid();
            }
            if (result.ErrorMessage?.Contains("只有刊登中或已下架的商品才能編輯") == true)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "更新商品時發生錯誤");
            
            // 重新載入現有圖片
            var listingResult = await _listingService.GetListingForEditAsync(id, user.Id);
            if (listingResult.Success && listingResult.Data != null)
            {
                model.ExistingImages = listingResult.Data.ExistingImages;
            }
            
            return View(model);
        }

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層刪除商品
        var result = await _listingService.DeleteListingAsync(id, user.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限刪除此商品")
            {
                return Forbid();
            }
            if (result.ErrorMessage?.Contains("只有刊登中的商品才能刪除") == true)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "刪除商品時發生錯誤，請稍後再試";
            return RedirectToAction("Details", new { id });
        }

        return RedirectToAction("My");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Json(new { success = false, error = "未登入" });
        }

        // 驗證請求
        if (!Enum.IsDefined(typeof(ListingStatus), request.NewStatus))
        {
            return Json(new { success = false, error = "無效的商品狀態" });
        }

        // 使用服務層更新狀態
        var result = await _listingService.UpdateListingStatusAsync(id, request.NewStatus, user.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return Json(new { success = false, error = "找不到商品" });
            }
            if (result.ErrorMessage == "無權限修改此商品")
            {
                return Json(new { success = false, error = "無權限修改此商品" });
            }

            return Json(new { success = false, error = result.ErrorMessage ?? "更新狀態時發生錯誤" });
        }

        // 如果有警告訊息（例如有 Conversation 但直接標記為交易完成），返回警告但不阻止操作
        return Json(new { success = true, warning = result.Data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Challenge();
        }

        // 使用服務層重新上架商品
        var result = await _listingService.ReactivateListingAsync(id, user.Id);

        if (!result.Success)
        {
            if (result.ErrorMessage == "找不到商品")
            {
                return NotFound();
            }
            if (result.ErrorMessage == "無權限上架此商品")
            {
                return Forbid();
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "重新上架商品時發生錯誤，請稍後再試";
            return RedirectToAction("My");
        }

        TempData["SuccessMessage"] = "商品已重新上架";
        return RedirectToAction("My");
    }
}

public class UpdateStatusRequest
{
    public ListingStatus NewStatus { get; set; }
}


