namespace ScheduleAdjust.Models;

public class PollResponse
{
    public int ResponseId { get; set; }
    public int PollId { get; set; }
    public int SelectedSlotId { get; set; }
    public string RespondentName { get; set; } = string.Empty;
    public string RespondentEmail { get; set; } = string.Empty;
    public string? RespondentCompany { get; set; }
    public DateTimeOffset RespondedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public SchedulePoll Poll { get; set; } = null!;
    public PollTimeSlot SelectedSlot { get; set; } = null!;
}
