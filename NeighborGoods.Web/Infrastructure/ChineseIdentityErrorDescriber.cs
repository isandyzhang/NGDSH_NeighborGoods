using Microsoft.AspNetCore.Identity;

namespace NeighborGoods.Web.Infrastructure;

/// <summary>
/// 將常見 Identity 錯誤訊息改為繁體中文。
/// </summary>
public class ChineseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "發生未知錯誤，請稍後再試。" };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"帳號「{userName}」已被使用。" };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = $"帳號「{userName}」包含無效字元。" };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"密碼長度至少需要 {length} 個字元。" };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "密碼必須至少包含一個特殊符號（例如 ! @ # ? 等）。" };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "密碼必須至少包含一個數字。" };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "密碼必須至少包含一個小寫英文字母。" };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "密碼必須至少包含一個大寫英文字母。" };
}


