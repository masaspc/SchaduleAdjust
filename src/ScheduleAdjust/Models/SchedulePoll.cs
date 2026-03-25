namespace ScheduleAdjust.Models;

public class SchedulePoll
{
    public int PollId { get; set; }
    public Guid PollGuid { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTimeOffset CandidateStartDate { get; set; }
    public DateTimeOffset CandidateEndDate { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public string? Note { get; set; }
    public PollStatus Status { get; set; } = PollStatus.Draft;

    public string OrganizerId { get; set; } = string.Empty;
    public string OrganizerEmail { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;

    public int? ConfirmedSlotId { get; set; }
    public string? GraphEventId { get; set; }
    public string? TeamsJoinUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public PollTimeSlot? ConfirmedSlot { get; set; }
    public ICollection<PollAttendee> Attendees { get; set; } = new List<PollAttendee>();
    public ICollection<PollTimeSlot> TimeSlots { get; set; } = new List<PollTimeSlot>();
    public ICollection<PollResponse> Responses { get; set; } = new List<PollResponse>();
}
