using ChatSupport.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatSupport.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Admin>()
            .HasIndex(a => a.Email)
            .IsUnique();

        b.Entity<Conversation>()
            .HasIndex(c => c.PublicId)
            .IsUnique();

        b.Entity<Conversation>()
            .HasOne(c => c.AssignedAdmin)
            .WithMany(a => a.AssignedConversations)
            .HasForeignKey(c => c.AssignedAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Message>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
