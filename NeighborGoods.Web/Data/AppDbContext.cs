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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 台灣金額不需要小數點，使用 decimal(18,0)
        builder.Entity<Listing>()
            .Property(l => l.Price)
            .HasColumnType("decimal(18,0)");

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

        // Message 與 ApplicationUser 的關聯
        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // 注意：唯一性在應用層處理（GetOrCreateConversationAsync 確保 Participant1Id < Participant2Id）
        // 為了提高查詢效能，可以建立非唯一索引
        builder.Entity<Conversation>()
            .HasIndex(c => new { c.Participant1Id, c.Participant2Id });
    }
}
