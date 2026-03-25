using Microsoft.Graph.Models;

namespace ScheduleAdjust.Services;

/// <summary>
/// Stub implementation for offline development without Graph API credentials.
/// </summary>
public class StubGraphApiService : IGraphApiService
{
    private readonly ILogger<StubGraphApiService> _logger;

    public StubGraphApiService(ILogger<StubGraphApiService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<ScheduleInformation>> GetScheduleAsync(
        IEnumerable<string> userEmails, DateTimeOffset start, DateTimeOffset end)
    {
        _logger.LogInformation("[STUB] GetScheduleAsync called");

        var schedules = userEmails.Select(email => new ScheduleInformation
        {
            ScheduleId = email,
            ScheduleItems = new List<ScheduleItem>()
        });

        return Task.FromResult(schedules);
    }

    public Task<MeetingTimeSuggestionsResult?> FindMeetingTimesAsync(
        IEnumerable<string> attendeeEmails, int durationMinutes,
        DateTimeOffset start, DateTimeOffset end)
    {
        _logger.LogInformation("[STUB] FindMeetingTimesAsync called");

        var suggestions = new List<MeetingTimeSuggestion>();
        var current = start;
        var duration = TimeSpan.FromMinutes(durationMinutes);

        // Generate sample slots during business hours (9:00-17:00)
        while (current < end && suggestions.Count < 10)
        {
            if (current.Hour >= 9 && current.Hour + (durationMinutes / 60.0) <= 17
                && current.DayOfWeek != DayOfWeek.Saturday
                && current.DayOfWeek != DayOfWeek.Sunday)
            {
                suggestions.Add(new MeetingTimeSuggestion
                {
                    MeetingTimeSlot = new TimeSlot
                    {
                        Start = new DateTimeTimeZone
                        {
                            DateTime = current.UtcDateTime.ToString("o"),
                            TimeZone = "UTC"
                        },
                        End = new DateTimeTimeZone
                        {
                            DateTime = current.Add(duration).UtcDateTime.ToString("o"),
                            TimeZone = "UTC"
                        }
                    },
                    Confidence = 100
                });
            }

            current = current.AddHours(1);
            // Skip to next day's 9 AM if past business hours
            if (current.Hour >= 17)
            {
                var nextDay = current.Date.AddDays(1).AddHours(9);
                current = new DateTimeOffset(nextDay, start.Offset);
            }
        }

        var result = new MeetingTimeSuggestionsResult
        {
            MeetingTimeSuggestions = suggestions
        };

        return Task.FromResult<MeetingTimeSuggestionsResult?>(result);
    }

    public Task<Event?> CreateEventAsync(
        string organizerEmail, string subject, DateTimeOffset start, DateTimeOffset end,
        IEnumerable<string> attendeeEmails, string? teamsJoinUrl = null, string? body = null)
    {
        _logger.LogInformation("[STUB] CreateEventAsync: {Subject}", subject);

        var ev = new Event
        {
            Id = Guid.NewGuid().ToString(),
            Subject = subject,
            Start = new DateTimeTimeZone
            {
                DateTime = start.UtcDateTime.ToString("o"),
                TimeZone = "UTC"
            },
            End = new DateTimeTimeZone
            {
                DateTime = end.UtcDateTime.ToString("o"),
                TimeZone = "UTC"
            }
        };

        return Task.FromResult<Event?>(ev);
    }

    public Task<OnlineMeeting?> CreateTeamsMeetingAsync(
        string organizerEmail, string subject, DateTimeOffset start, DateTimeOffset end)
    {
        _logger.LogInformation("[STUB] CreateTeamsMeetingAsync: {Subject}", subject);

        var meeting = new OnlineMeeting
        {
            Id = Guid.NewGuid().ToString(),
            Subject = subject,
            JoinWebUrl = $"https://teams.microsoft.com/l/meetup-join/stub-{Guid.NewGuid():N}",
            StartDateTime = start,
            EndDateTime = end
        };

        return Task.FromResult<OnlineMeeting?>(meeting);
    }

    public Task<IEnumerable<User>> GetUsersAsync(string? searchFilter = null)
    {
        _logger.LogInformation("[STUB] GetUsersAsync: {Filter}", searchFilter);

        var users = new List<User>
        {
            new User { Id = "user1", DisplayName = "田中 太郎", Mail = "tanaka@example.com" },
            new User { Id = "user2", DisplayName = "鈴木 花子", Mail = "suzuki@example.com" },
            new User { Id = "user3", DisplayName = "佐藤 一郎", Mail = "sato@example.com" }
        };

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            users = users.Where(u =>
                (u.DisplayName?.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (u.Mail?.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        return Task.FromResult<IEnumerable<User>>(users);
    }

    public Task SendMailAsync(string fromEmail, string toEmail, string subject, string htmlBody)
    {
        _logger.LogInformation("[STUB] SendMailAsync: From={From}, To={To}, Subject={Subject}",
            fromEmail, toEmail, subject);
        return Task.CompletedTask;
    }
}
