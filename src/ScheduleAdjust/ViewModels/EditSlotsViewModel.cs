using System.ComponentModel.DataAnnotations;
using ScheduleAdjust.Models;

namespace ScheduleAdjust.ViewModels;

public class EditSlotsViewModel
{
    public int PollId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public PollStatus Status { get; set; }

    public IList<PollTimeSlot> ExistingSlots { get; set; } = new List<PollTimeSlot>();

    [Display(Name = "開始日時")]
    public DateTimeOffset? NewSlotStart { get; set; }

    [Display(Name = "終了日時")]
    public DateTimeOffset? NewSlotEnd { get; set; }
}
