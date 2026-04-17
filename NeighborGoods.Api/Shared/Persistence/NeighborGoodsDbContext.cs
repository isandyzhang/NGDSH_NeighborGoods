using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;
using ListingEntity = NeighborGoods.Api.Features.Listing.Listing;

namespace NeighborGoods.Api.Shared.Persistence;

public sealed class NeighborGoodsDbContext(DbContextOptions<NeighborGoodsDbContext> options) : DbContext(options)
{
    public DbSet<AdminMessage> AdminMessages => Set<AdminMessage>();
    public DbSet<AspNetRole> AspNetRoles => Set<AspNetRole>();
    public DbSet<AspNetRoleClaim> AspNetRoleClaims => Set<AspNetRoleClaim>();
    public DbSet<AspNetUser> AspNetUsers => Set<AspNetUser>();
    public DbSet<AspNetUserClaim> AspNetUserClaims => Set<AspNetUserClaim>();
    public DbSet<AspNetUserLogin> AspNetUserLogins => Set<AspNetUserLogin>();
    public DbSet<AspNetUserToken> AspNetUserTokens => Set<AspNetUserToken>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<LineBindingPending> LineBindingPendings => Set<LineBindingPending>();
    public DbSet<ListingEntity> Listings => Set<ListingEntity>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<ListingTopSubmission> ListingTopSubmissions => Set<ListingTopSubmission>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminMessage>(entity =>
        {
            entity.HasIndex(e => e.SenderId, "IX_AdminMessages_SenderId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Content).HasMaxLength(1000);

            entity.HasOne(d => d.Sender).WithMany(p => p.AdminMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedName] IS NOT NULL)");

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedUserName] IS NOT NULL)");

            entity.Property(e => e.DisplayName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.LineUserId).HasMaxLength(100);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasIndex(e => e.ListingId, "IX_Conversations_ListingId");

            entity.HasIndex(e => new { e.Participant1Id, e.Participant2Id, e.ListingId }, "IX_Conversations_Participant1Id_Participant2Id_ListingId");

            entity.HasIndex(e => e.Participant2Id, "IX_Conversations_Participant2Id");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Listing).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ListingId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Participant1).WithMany(p => p.ConversationParticipant1s)
                .HasForeignKey(d => d.Participant1Id)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Participant2).WithMany(p => p.ConversationParticipant2s)
                .HasForeignKey(d => d.Participant2Id)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<LineBindingPending>(entity =>
        {
            entity.ToTable("LineBindingPending");

            entity.HasIndex(e => e.LineUserId, "IX_LineBindingPending_LineUserId");

            entity.HasIndex(e => e.Token, "IX_LineBindingPending_Token").IsUnique();

            entity.HasIndex(e => e.UserId, "IX_LineBindingPending_UserId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Token).HasMaxLength(32);

            entity.HasOne(d => d.User).WithMany(p => p.LineBindingPendings).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<ListingEntity>(entity =>
        {
            entity.ToTable("Listings");
            entity.HasIndex(e => e.BuyerId, "IX_Listings_BuyerId");

            entity.HasIndex(e => e.SellerId, "IX_Listings_SellerId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Category).HasDefaultValue(9);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PickupLocation).HasDefaultValue(3);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Buyer).WithMany(p => p.ListingBuyers).HasForeignKey(d => d.BuyerId);

            entity.HasOne(d => d.Seller).WithMany(p => p.ListingSellers).HasForeignKey(d => d.SellerId);
        });

        modelBuilder.Entity<ListingImage>(entity =>
        {
            entity.HasIndex(e => e.ListingId, "IX_ListingImages_ListingId");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Listing).WithMany(p => p.ListingImages).HasForeignKey(d => d.ListingId);
        });

        modelBuilder.Entity<ListingTopSubmission>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt, "IX_ListingTopSubmissions_CreatedAt");

            entity.HasIndex(e => e.ListingId, "IX_ListingTopSubmissions_ListingId");

            entity.HasIndex(e => e.ReviewedByAdminId, "IX_ListingTopSubmissions_ReviewedByAdminId");

            entity.HasIndex(e => e.Status, "IX_ListingTopSubmissions_Status");

            entity.HasIndex(e => e.UserId, "IX_ListingTopSubmissions_UserId");

            entity.Property(e => e.FeedbackDetail).HasMaxLength(1000);
            entity.Property(e => e.FeedbackTitle).HasMaxLength(200);
            entity.Property(e => e.PhotoBlobName).HasMaxLength(500);

            entity.HasOne(d => d.Listing).WithMany(p => p.ListingTopSubmissions).HasForeignKey(d => d.ListingId);

            entity.HasOne(d => d.ReviewedByAdmin).WithMany(p => p.ListingTopSubmissionReviewedByAdmins).HasForeignKey(d => d.ReviewedByAdminId);

            entity.HasOne(d => d.User).WithMany(p => p.ListingTopSubmissionUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasIndex(e => e.ConversationId, "IX_Messages_ConversationId");

            entity.HasIndex(e => e.SenderId, "IX_Messages_SenderId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Content).HasMaxLength(50);

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages).HasForeignKey(d => d.ConversationId);

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => e.BuyerId, "IX_Reviews_BuyerId");

            entity.HasIndex(e => new { e.ListingId, e.BuyerId }, "IX_Reviews_ListingId_BuyerId").IsUnique();

            entity.HasIndex(e => e.SellerId, "IX_Reviews_SellerId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Content).HasMaxLength(500);

            entity.HasOne(d => d.Buyer).WithMany(p => p.ReviewBuyers)
                .HasForeignKey(d => d.BuyerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Listing).WithMany(p => p.Reviews).HasForeignKey(d => d.ListingId);

            entity.HasOne(d => d.Seller).WithMany(p => p.ReviewSellers)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }
}
