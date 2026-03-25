using Microsoft.EntityFrameworkCore;
using ScheduleAdjust.Models;

namespace ScheduleAdjust.Data;

public class ScheduleAdjustDbContext : DbContext
{
    public ScheduleAdjustDbContext(DbContextOptions<ScheduleAdjustDbContext> options)
        : base(options)
    {
    }

    public DbSet<SchedulePoll> SchedulePolls => Set<SchedulePoll>();
    public DbSet<PollAttendee> PollAttendees => Set<PollAttendee>();
    public DbSet<PollTimeSlot> PollTimeSlots => Set<PollTimeSlot>();
    public DbSet<PollResponse> PollResponses => Set<PollResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SchedulePoll
        modelBuilder.Entity<SchedulePoll>(entity =>
        {
            entity.HasKey(e => e.PollId);
            entity.HasIndex(e => e.PollGuid).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.OrganizerId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.OrganizerEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.OrganizerName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.GraphEventId).HasMaxLength(500);
            entity.Property(e => e.TeamsJoinUrl).HasMaxLength(1000);
            entity.Property(e => e.Note).HasMaxLength(2000);

            entity.HasOne(e => e.ConfirmedSlot)
                .WithMany()
                .HasForeignKey(e => e.ConfirmedSlotId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PollAttendee
        modelBuilder.Entity<PollAttendee>(entity =>
        {
            entity.HasKey(e => e.AttendeeId);
            entity.HasIndex(e => new { e.PollId, e.Email }).IsUnique();
            entity.Property(e => e.UserObjectId).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();

            entity.HasOne(e => e.Poll)
                .WithMany(p => p.Attendees)
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PollTimeSlot
        modelBuilder.Entity<PollTimeSlot>(entity =>
        {
            entity.HasKey(e => e.SlotId);
            entity.HasIndex(e => e.PollId);

            entity.HasOne(e => e.Poll)
                .WithMany(p => p.TimeSlots)
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PollResponse
        modelBuilder.Entity<PollResponse>(entity =>
        {
            entity.HasKey(e => e.ResponseId);
            entity.HasIndex(e => e.PollId);
            entity.Property(e => e.RespondentName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RespondentEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.RespondentCompany).HasMaxLength(200);

            entity.HasOne(e => e.Poll)
                .WithMany(p => p.Responses)
                .HasForeignKey(e => e.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SelectedSlot)
                .WithMany(s => s.Responses)
                .HasForeignKey(e => e.SelectedSlotId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
