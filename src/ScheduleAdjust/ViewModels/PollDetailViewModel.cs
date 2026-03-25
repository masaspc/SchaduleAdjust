using ScheduleAdjust.Models;

namespace ScheduleAdjust.ViewModels;

public class PollDetailViewModel
{
    public int PollId { get; set; }
    public Guid PollGuid { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTimeOffset CandidateStartDate { get; set; }
    public DateTimeOffset CandidateEndDate { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public string? Note { get; set; }
    public PollStatus Status { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string? TeamsJoinUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public int? ConfirmedSlotId { get; set; }
    public PollTimeSlot? ConfirmedSlot { get; set; }

    public IList<SlotWithResponseCount> TimeSlots { get; set; } = new List<SlotWithResponseCount>();
    public IList<PollResponse> Responses { get; set; } = new List<PollResponse>();
    public IList<PollAttendee> Attendees { get; set; } = new List<PollAttendee>();

    public string BookingUrl { get; set; } = string.Empty;
}

public class SlotWithResponseCount
{
    public int SlotId { get; set; }
    public DateTimeOffset StartDateTime { get; set; }
    public DateTimeOffset EndDateTime { get; set; }
    public bool IsManuallyAdded { get; set; }
    public int ResponseCount { get; set; }
}
