using ScheduleSync.Models;

namespace ScheduleSync.Services;

public interface INotificationService
{
    Task SendBookingUrlAsync(SchedulePoll poll);
    Task SendConfirmationAsync(SchedulePoll poll, PollResponse response);
    Task SendReminderAsync(SchedulePoll poll);
}
