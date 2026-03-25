using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Models;
using ScheduleSync.Data;
using ScheduleSync.Models;
using ScheduleSync.ViewModels;

namespace ScheduleSync.Services;

public class ScheduleService : IScheduleService
{
    private readonly ScheduleSyncDbContext _db;
    private readonly IGraphApiService _graphApi;
    private readonly INotificationService _notification;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        ScheduleSyncDbContext db,
        IGraphApiService graphApi,
        INotificationService notification,
        ILogger<ScheduleService> logger)
    {
        _db = db;
        _graphApi = graphApi;
        _notification = notification;
        _logger = logger;
    }

    public async Task<SchedulePoll> CreatePollAsync(
        CreatePollViewModel model, string organizerId, string organizerEmail, string organizerName)
    {
        var poll = new SchedulePoll
        {
            Title = model.Title,
            DurationMinutes = model.DurationMinutes,
            CandidateStartDate = model.CandidateStartDate,
            CandidateEndDate = model.CandidateEndDate,
            Deadline = model.Deadline,
            Note = model.Note,
            Status = PollStatus.Draft,
            OrganizerId = organizerId,
            OrganizerEmail = organizerEmail,
            OrganizerName = organizerName
        };

        // Add attendees
        if (model.AttendeeEmails != null)
        {
            foreach (var email in model.AttendeeEmails.Where(e => !string.IsNullOrWhiteSpace(e)))
            {
                poll.Attendees.Add(new PollAttendee
                {
                    Email = email.Trim(),
                    DisplayName = email.Trim(),
                    IsRequired = true
                });
            }
        }

        _db.SchedulePolls.Add(poll);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Poll created: {PollId} '{Title}' by {Organizer}",
            poll.PollId, poll.Title, organizerEmail);

        return poll;
    }

    public async Task<IEnumerable<PollTimeSlot>> GenerateCandidateSlotsAsync(int pollId)
    {
        var poll = await _db.SchedulePolls
            .Include(p => p.Attendees)
            .Include(p => p.TimeSlots)
            .FirstOrDefaultAsync(p => p.PollId == pollId)
            ?? throw new InvalidOperationException($"Poll {pollId} not found");

        // Get all attendee emails including organizer
        var emails = poll.Attendees.Select(a => a.Email).ToList();
        if (!emails.Contains(poll.OrganizerEmail))
        {
            emails.Insert(0, poll.OrganizerEmail);
        }

        // Use Graph API to find meeting times
        var suggestions = await _graphApi.FindMeetingTimesAsync(
            emails,
            poll.DurationMinutes,
            poll.CandidateStartDate,
            poll.CandidateEndDate);

        var newSlots = new List<PollTimeSlot>();

        if (suggestions?.MeetingTimeSuggestions != null)
        {
            foreach (var suggestion in suggestions.MeetingTimeSuggestions)
            {
                var startStr = suggestion.MeetingTimeSlot?.Start?.DateTime;
                var endStr = suggestion.MeetingTimeSlot?.End?.DateTime;

                if (startStr == null || endStr == null) continue;

                var startDt = DateTimeOffset.Parse(startStr);
                var endDt = DateTimeOffset.Parse(endStr);

                // Avoid duplicates
                if (poll.TimeSlots.Any(s => s.StartDateTime == startDt && s.EndDateTime == endDt))
                    continue;

                var slot = new PollTimeSlot
                {
                    PollId = pollId,
                    StartDateTime = startDt,
                    EndDateTime = endDt,
                    IsManuallyAdded = false,
                    IsAvailable = true
                };

                _db.PollTimeSlots.Add(slot);
                newSlots.Add(slot);
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Generated {Count} candidate slots for Poll {PollId}",
            newSlots.Count, pollId);

        return newSlots;
    }

    public async Task AddManualSlotAsync(int pollId, DateTimeOffset start, DateTimeOffset end)
    {
        var poll = await _db.SchedulePolls.FindAsync(pollId)
            ?? throw new InvalidOperationException($"Poll {pollId} not found");

        var slot = new PollTimeSlot
        {
            PollId = pollId,
            StartDateTime = start,
            EndDateTime = end,
            IsManuallyAdded = true,
            IsAvailable = true
        };

        _db.PollTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Manual slot added to Poll {PollId}: {Start} - {End}",
            pollId, start, end);
    }

    public async Task RemoveSlotAsync(int slotId)
    {
        var slot = await _db.PollTimeSlots.FindAsync(slotId)
            ?? throw new InvalidOperationException($"Slot {slotId} not found");

        // Soft delete to preserve referential integrity with responses
        slot.IsAvailable = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Slot {SlotId} marked as unavailable", slotId);
    }

    public async Task<SchedulePoll> PublishPollAsync(int pollId)
    {
        var poll = await _db.SchedulePolls
            .Include(p => p.TimeSlots)
            .FirstOrDefaultAsync(p => p.PollId == pollId)
            ?? throw new InvalidOperationException($"Poll {pollId} not found");

        if (!poll.TimeSlots.Any(s => s.IsAvailable))
        {
            throw new InvalidOperationException("Cannot publish a poll without available time slots");
        }

        poll.Status = PollStatus.Open;
        poll.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Poll {PollId} published with GUID {PollGuid}", pollId, poll.PollGuid);

        return poll;
    }

    public async Task<SchedulePoll?> GetPollByIdAsync(int pollId)
    {
        return await _db.SchedulePolls
            .Include(p => p.Attendees)
            .Include(p => p.TimeSlots.Where(s => s.IsAvailable))
            .Include(p => p.Responses)
                .ThenInclude(r => r.SelectedSlot)
            .Include(p => p.ConfirmedSlot)
            .FirstOrDefaultAsync(p => p.PollId == pollId);
    }

    public async Task<SchedulePoll?> GetPollByGuidAsync(Guid pollGuid)
    {
        return await _db.SchedulePolls
            .Include(p => p.TimeSlots.Where(s => s.IsAvailable))
            .Include(p => p.Responses)
            .FirstOrDefaultAsync(p => p.PollGuid == pollGuid);
    }

    public async Task<IEnumerable<SchedulePoll>> GetDashboardAsync(string organizerId)
    {
        return await _db.SchedulePolls
            .Include(p => p.Responses)
            .Include(p => p.TimeSlots)
            .Where(p => p.OrganizerId == organizerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PollResponse> SubmitResponseAsync(Guid pollGuid, BookingFormModel model)
    {
        var poll = await _db.SchedulePolls
            .Include(p => p.TimeSlots)
            .FirstOrDefaultAsync(p => p.PollGuid == pollGuid)
            ?? throw new InvalidOperationException("Poll not found");

        if (poll.Status != PollStatus.Open)
        {
            throw new InvalidOperationException("This poll is no longer accepting responses");
        }

        if (poll.Deadline.HasValue && DateTimeOffset.UtcNow > poll.Deadline.Value)
        {
            throw new InvalidOperationException("The deadline for this poll has passed");
        }

        var slot = poll.TimeSlots.FirstOrDefault(s => s.SlotId == model.SelectedSlotId && s.IsAvailable)
            ?? throw new InvalidOperationException("Selected time slot is not available");

        var response = new PollResponse
        {
            PollId = poll.PollId,
            SelectedSlotId = model.SelectedSlotId,
            RespondentName = model.RespondentName,
            RespondentEmail = model.RespondentEmail,
            RespondentCompany = model.RespondentCompany,
            RespondedAt = DateTimeOffset.UtcNow
        };

        _db.PollResponses.Add(response);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Response submitted for Poll {PollGuid} by {Name}",
            pollGuid, model.RespondentName);

        return response;
    }

    public async Task<SchedulePoll> ConfirmSlotAsync(int pollId, int slotId)
    {
        var poll = await _db.SchedulePolls
            .Include(p => p.Attendees)
            .Include(p => p.TimeSlots)
            .Include(p => p.Responses)
            .FirstOrDefaultAsync(p => p.PollId == pollId)
            ?? throw new InvalidOperationException($"Poll {pollId} not found");

        var slot = poll.TimeSlots.FirstOrDefault(s => s.SlotId == slotId)
            ?? throw new InvalidOperationException($"Slot {slotId} not found");

        // Create Teams meeting
        var meeting = await _graphApi.CreateTeamsMeetingAsync(
            poll.OrganizerEmail,
            poll.Title,
            slot.StartDateTime,
            slot.EndDateTime);

        var teamsJoinUrl = meeting?.JoinWebUrl;

        // Create Outlook event for all attendees
        var allEmails = poll.Attendees.Select(a => a.Email).ToList();
        // Add respondent emails
        var respondentEmails = poll.Responses.Select(r => r.RespondentEmail).Distinct();
        allEmails.AddRange(respondentEmails);

        var calendarEvent = await _graphApi.CreateEventAsync(
            poll.OrganizerEmail,
            poll.Title,
            slot.StartDateTime,
            slot.EndDateTime,
            allEmails.Distinct(),
            teamsJoinUrl,
            poll.Note);

        // Update poll
        poll.Status = PollStatus.Confirmed;
        poll.ConfirmedSlotId = slotId;
        poll.GraphEventId = calendarEvent?.Id;
        poll.TeamsJoinUrl = teamsJoinUrl;
        poll.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Poll {PollId} confirmed with slot {SlotId}", pollId, slotId);

        // Send confirmation notifications
        foreach (var response in poll.Responses)
        {
            try
            {
                await _notification.SendConfirmationAsync(poll, response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send confirmation to {Email}", response.RespondentEmail);
            }
        }

        return poll;
    }

    public async Task CancelPollAsync(int pollId)
    {
        var poll = await _db.SchedulePolls.FindAsync(pollId)
            ?? throw new InvalidOperationException($"Poll {pollId} not found");

        poll.Status = PollStatus.Cancelled;
        poll.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Poll {PollId} cancelled", pollId);
    }

    public async Task ProcessExpiredPollsAsync()
    {
        var expiredPolls = await _db.SchedulePolls
            .Where(p => p.Status == PollStatus.Open
                && p.Deadline.HasValue
                && p.Deadline.Value < DateTimeOffset.UtcNow)
            .ToListAsync();

        foreach (var poll in expiredPolls)
        {
            poll.Status = PollStatus.Expired;
            poll.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Poll {PollId} expired", poll.PollId);

            try
            {
                await _notification.SendReminderAsync(poll);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send expiry reminder for Poll {PollId}", poll.PollId);
            }
        }

        await _db.SaveChangesAsync();

        if (expiredPolls.Any())
        {
            _logger.LogInformation("Processed {Count} expired polls", expiredPolls.Count);
        }
    }
}
