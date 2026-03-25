namespace ScheduleSync.ViewModels;

public class BookingViewModel
{
    public Guid PollGuid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public int DurationMinutes { get; set; }
    public IList<BookingSlotItem> AvailableSlots { get; set; } = new List<BookingSlotItem>();
}

public class BookingSlotItem
{
    public int SlotId { get; set; }
    public DateTimeOffset StartDateTime { get; set; }
    public DateTimeOffset EndDateTime { get; set; }
}
