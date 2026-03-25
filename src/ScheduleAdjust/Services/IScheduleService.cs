using ScheduleAdjust.Models;
using ScheduleAdjust.ViewModels;

namespace ScheduleAdjust.Services;

public interface IScheduleService
{
    Task<SchedulePoll> CreatePollAsync(CreatePollViewModel model, string organizerId, string organizerEmail, string organizerName);
    Task<IEnumerable<PollTimeSlot>> GenerateCandidateSlotsAsync(int pollId);
    Task AddManualSlotAsync(int pollId, DateTimeOffset start, DateTimeOffset end);
    Task RemoveSlotAsync(int slotId);
    Task<SchedulePoll> PublishPollAsync(int pollId);
    Task<SchedulePoll?> GetPollByIdAsync(int pollId);
    Task<SchedulePoll?> GetPollByGuidAsync(Guid pollGuid);
    Task<IEnumerable<SchedulePoll>> GetDashboardAsync(string organizerId);
    Task<PollResponse> SubmitResponseAsync(Guid pollGuid, BookingFormModel model);
    Task<SchedulePoll> ConfirmSlotAsync(int pollId, int slotId);
    Task CancelPollAsync(int pollId);
    Task ProcessExpiredPollsAsync();
}
