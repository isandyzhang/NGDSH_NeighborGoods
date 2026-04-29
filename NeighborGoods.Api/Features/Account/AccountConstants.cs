namespace NeighborGoods.Api.Features.Account;

public static class AccountConstants
{
    public const int VerificationCodeLength = 6;
    public const int VerificationCodeExpiresInMinutes = 10;
    public const int VerificationCodeResendCooldownSeconds = 60;
    public const int VerificationCodeMaxSendsPerHour = 5;
    public const int MaxDisplayNameLength = 50;
}
