namespace ScheduleSync.Models;

public class PollAttendee
{
    public int AttendeeId { get; set; }
    public int PollId { get; set; }
    public string UserObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;

    // Navigation
    public SchedulePoll Poll { get; set; } = null!;
}
