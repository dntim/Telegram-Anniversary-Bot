using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TelegramAnniversaryBot.Models;

public partial class AnniversaryReminderBotDbContext : DbContext
{
    public AnniversaryReminderBotDbContext()
    {
    }

    public AnniversaryReminderBotDbContext(DbContextOptions<AnniversaryReminderBotDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AnniversaryReminderBotEvent> AnniversaryReminderBotEvents { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Cyrillic_General_CI_AS");

        modelBuilder.Entity<AnniversaryReminderBotEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_anniversary-reminder");

            entity.ToTable("anniversaryReminderBot");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CongratsAlternativeNames).HasColumnName("congratsAlternativeNames");
            entity.Property(e => e.CongratsAtTime).HasColumnName("congratsAtTime");
            entity.Property(e => e.CongratsTargetIds).HasColumnName("congratsTargetIds");
            entity.Property(e => e.CreatedById).HasColumnName("createdById");
            entity.Property(e => e.DateTimeCreated)
                .HasColumnType("datetime")
                .HasColumnName("dateTimeCreated");
            entity.Property(e => e.DateTimeLastCongratulated)
                .HasColumnType("datetime")
                .HasColumnName("dateTimeLastCongratulated");
            entity.Property(e => e.Event).HasColumnName("event");
            entity.Property(e => e.EventDate).HasColumnName("eventDate");
            entity.Property(e => e.NotifyChatId).HasColumnName("notifyChatId");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
