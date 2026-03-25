using Microsoft.Graph.Models;

namespace ScheduleSync.Services;

public interface IGraphApiService
{
    /// <summary>
    /// Gets schedule/free-busy information for the specified users.
    /// </summary>
    Task<IEnumerable<ScheduleInformation>> GetScheduleAsync(
        IEnumerable<string> userEmails,
        DateTimeOffset start,
        DateTimeOffset end);

    /// <summary>
    /// Finds available meeting times for the specified attendees.
    /// </summary>
    Task<MeetingTimeSuggestionsResult?> FindMeetingTimesAsync(
        IEnumerable<string> attendeeEmails,
        int durationMinutes,
        DateTimeOffset start,
        DateTimeOffset end);

    /// <summary>
    /// Creates a calendar event with the specified attendees.
    /// </summary>
    Task<Event?> CreateEventAsync(
        string organizerEmail,
        string subject,
        DateTimeOffset start,
        DateTimeOffset end,
        IEnumerable<string> attendeeEmails,
        string? teamsJoinUrl = null,
        string? body = null);

    /// <summary>
    /// Creates a Teams online meeting.
    /// </summary>
    Task<OnlineMeeting?> CreateTeamsMeetingAsync(
        string organizerEmail,
        string subject,
        DateTimeOffset start,
        DateTimeOffset end);

    /// <summary>
    /// Searches for users in the directory.
    /// </summary>
    Task<IEnumerable<User>> GetUsersAsync(string? searchFilter = null);

    /// <summary>
    /// Sends an email via Graph API.
    /// </summary>
    Task SendMailAsync(
        string fromEmail,
        string toEmail,
        string subject,
        string htmlBody);
}
