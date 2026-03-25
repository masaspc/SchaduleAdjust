namespace ScheduleSync.Models;

public class PollTimeSlot
{
    public int SlotId { get; set; }
    public int PollId { get; set; }
    public DateTimeOffset StartDateTime { get; set; }
    public DateTimeOffset EndDateTime { get; set; }
    public bool IsManuallyAdded { get; set; }
    public bool IsAvailable { get; set; } = true;

    // Navigation
    public SchedulePoll Poll { get; set; } = null!;
    public ICollection<PollResponse> Responses { get; set; } = new List<PollResponse>();
}
