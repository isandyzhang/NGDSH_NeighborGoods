using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Web.Models.Entities;

namespace NeighborGoods.Web.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<LineBindingPending> LineBindingPending => Set<LineBindingPending>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 台灣金額不需要小數點，使用 decimal(18,0)
        builder.Entity<Listing>()
            .Property(l => l.Price)
            .HasColumnType("decimal(18,0)");

        // 設定 Listing 字串欄位長度限制
        builder.Entity<Listing>()
            .Property(l => l.Title)
            .HasMaxLength(200);
        
        builder.Entity<Listing>()
            .Property(l => l.Description)
            .HasMaxLength(500);

        // 設定 Message 字串欄位長度限制
        builder.Entity<Message>()
            .Property(m => m.Content)
            .HasMaxLength(50);

        // 設定 Review 字串欄位長度限制
        builder.Entity<Review>()
            .Property(r => r.Content)
            .HasMaxLength(500);

        // 設定 ApplicationUser 字串欄位長度限制
        builder.Entity<ApplicationUser>()
            .Property(u => u.DisplayName)
            .HasMaxLength(50);
        
        builder.Entity<ApplicationUser>()
            .Property(u => u.LineUserId)
            .HasMaxLength(100);

        // Listing 與 ListingImage 的關聯（一對多）
        builder.Entity<ListingImage>()
            .HasOne(li => li.Listing)
            .WithMany(l => l.Images)
            .HasForeignKey(li => li.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conversation 與 Message 的關聯（一對多）
        builder.Entity<Message>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conversation 與 ApplicationUser 的關聯
        builder.Entity<Conversation>()
            .HasOne(c => c.Participant1)
            .WithMany()
            .HasForeignKey(c => c.Participant1Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Conversation>()
            .HasOne(c => c.Participant2)
            .WithMany()
            .HasForeignKey(c => c.Participant2Id)
            .OnDelete(DeleteBehavior.Restrict);

        // Conversation 與 Listing 的關聯（多對一，一個商品可以有多個對話）
        builder.Entity<Conversation>()
            .HasOne(c => c.Listing)
            .WithMany()
            .HasForeignKey(c => c.ListingId)
            .OnDelete(DeleteBehavior.Restrict);

        // Message 與 ApplicationUser 的關聯
        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review 與 Listing 的關聯（一對一，一個商品只能有一筆評價）
        builder.Entity<Review>()
            .HasOne(r => r.Listing)
            .WithMany()
            .HasForeignKey(r => r.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Review 與 ApplicationUser 的關聯（賣家）
        builder.Entity<Review>()
            .HasOne(r => r.Seller)
            .WithMany()
            .HasForeignKey(r => r.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review 與 ApplicationUser 的關聯（買家）
        builder.Entity<Review>()
            .HasOne(r => r.Buyer)
            .WithMany()
            .HasForeignKey(r => r.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review 的唯一約束：一個商品，每個評價者只能評價一次
        // 支持互相評價：買家評價賣家，賣家評價買家
        builder.Entity<Review>()
            .HasIndex(r => new { r.ListingId, r.BuyerId })
            .IsUnique();

        // 注意：唯一性在應用層處理（GetOrCreateConversationAsync 確保 Participant1Id < Participant2Id 和 ListingId）
        // 為了提高查詢效能，可以建立非唯一索引
        builder.Entity<Conversation>()
            .HasIndex(c => new { c.Participant1Id, c.Participant2Id, c.ListingId });

        // LineBindingPending 與 ApplicationUser 的關聯
        builder.Entity<LineBindingPending>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // LineBindingPending 索引
        builder.Entity<LineBindingPending>()
            .HasIndex(p => p.UserId);
        
        builder.Entity<LineBindingPending>()
            .HasIndex(p => p.Token)
            .IsUnique();
        
        builder.Entity<LineBindingPending>()
            .HasIndex(p => p.LineUserId);
        
        // Token 長度限制
        builder.Entity<LineBindingPending>()
            .Property(p => p.Token)
            .HasMaxLength(32);
    }
}
