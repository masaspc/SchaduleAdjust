namespace ScheduleSync.ViewModels;

public class BookingConfirmViewModel
{
    public string Title { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public DateTimeOffset SelectedSlotStart { get; set; }
    public DateTimeOffset SelectedSlotEnd { get; set; }
    public string RespondentName { get; set; } = string.Empty;
    public string? TeamsJoinUrl { get; set; }
}
