namespace TelegramAnniversaryBot.Models;

public partial class AnniversaryReminderBotEvent
{
    public Guid Id { get; set; }

    public long NotifyChatId { get; set; }

    public string CongratsTargetIds { get; set; } = null!;

    public string CongratsAlternativeNames { get; set; } = null!;

    public string Event { get; set; } = null!;

    public DateOnly EventDate { get; set; }

    public TimeOnly CongratsAtTime { get; set; }

    public DateTime? DateTimeLastCongratulated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public long CreatedById { get; set; }
}
