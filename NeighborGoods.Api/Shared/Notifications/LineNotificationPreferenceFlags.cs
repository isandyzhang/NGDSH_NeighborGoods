namespace NeighborGoods.Api.Shared.Notifications;

[Flags]
public enum LineNotificationPreferenceFlags
{
    None = 0,
    NewListings = 1 << 0,
    PriceDrop = 1 << 1,
    MessageDigest = 1 << 2
}
