using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.Calendar.GetSchedule;
using Microsoft.Graph.Users.Item.FindMeetingTimes;
using Microsoft.Graph.Users.Item.SendMail;

namespace ScheduleAdjust.Services;

public class GraphApiService : IGraphApiService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphApiService> _logger;

    public GraphApiService(GraphServiceClient graphClient, ILogger<GraphApiService> logger)
    {
        _graphClient = graphClient;
        _logger = logger;
    }

    public async Task<IEnumerable<ScheduleInformation>> GetScheduleAsync(
        IEnumerable<string> userEmails,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var emailList = userEmails.ToList();
        _logger.LogInformation("Getting schedule for {Count} users from {Start} to {End}",
            emailList.Count, start, end);

        try
        {
            var requestBody = new GetSchedulePostRequestBody
            {
                Schedules = emailList,
                StartTime = new DateTimeTimeZone
                {
                    DateTime = start.UtcDateTime.ToString("o"),
                    TimeZone = "UTC"
                },
                EndTime = new DateTimeTimeZone
                {
                    DateTime = end.UtcDateTime.ToString("o"),
                    TimeZone = "UTC"
                },
                AvailabilityViewInterval = 30
            };

            // Use the first email as the requesting user
            var result = await _graphClient.Users[emailList.First()]
                .Calendar.GetSchedule
                .PostAsGetSchedulePostResponseAsync(requestBody);

            return result?.Value ?? Enumerable.Empty<ScheduleInformation>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule for users");
            throw;
        }
    }

    public async Task<MeetingTimeSuggestionsResult?> FindMeetingTimesAsync(
        IEnumerable<string> attendeeEmails,
        int durationMinutes,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var emails = attendeeEmails.ToList();
        _logger.LogInformation("Finding meeting times for {Count} attendees", emails.Count);

        try
        {
            var attendees = emails.Select(email => new AttendeeBase
            {
                EmailAddress = new EmailAddress { Address = email },
                Type = AttendeeType.Required
            }).ToList();

            var requestBody = new FindMeetingTimesPostRequestBody
            {
                Attendees = attendees,
                MeetingDuration = TimeSpan.FromMinutes(durationMinutes),
                TimeConstraint = new TimeConstraint
                {
                    ActivityDomain = ActivityDomain.Work,
                    TimeSlots = new List<TimeSlot>
                    {
                        new TimeSlot
                        {
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
                        }
                    }
                },
                MaxCandidates = 20,
                IsOrganizerOptional = false
            };

            var result = await _graphClient.Users[emails.First()]
                .FindMeetingTimes
                .PostAsync(requestBody);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find meeting times");
            throw;
        }
    }

    public async Task<Event?> CreateEventAsync(
        string organizerEmail,
        string subject,
        DateTimeOffset start,
        DateTimeOffset end,
        IEnumerable<string> attendeeEmails,
        string? teamsJoinUrl = null,
        string? body = null)
    {
        _logger.LogInformation("Creating event '{Subject}' for {Organizer}", subject, organizerEmail);

        try
        {
            var bodyContent = body ?? string.Empty;
            if (!string.IsNullOrEmpty(teamsJoinUrl))
            {
                bodyContent += $"\n\n<a href=\"{teamsJoinUrl}\">Teams会議に参加</a>";
            }

            var newEvent = new Event
            {
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
                },
                Attendees = attendeeEmails.Select(email => new Attendee
                {
                    EmailAddress = new EmailAddress { Address = email },
                    Type = AttendeeType.Required
                }).ToList(),
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = bodyContent
                }
            };

            var createdEvent = await _graphClient.Users[organizerEmail]
                .Events
                .PostAsync(newEvent);

            _logger.LogInformation("Event created with ID: {EventId}", createdEvent?.Id);
            return createdEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event for {Organizer}", organizerEmail);
            throw;
        }
    }

    public async Task<OnlineMeeting?> CreateTeamsMeetingAsync(
        string organizerEmail,
        string subject,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _logger.LogInformation("Creating Teams meeting '{Subject}'", subject);

        try
        {
            var meeting = new OnlineMeeting
            {
                Subject = subject,
                StartDateTime = start,
                EndDateTime = end
            };

            var createdMeeting = await _graphClient.Users[organizerEmail]
                .OnlineMeetings
                .PostAsync(meeting);

            _logger.LogInformation("Teams meeting created with join URL: {JoinUrl}",
                createdMeeting?.JoinWebUrl);
            return createdMeeting;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Teams meeting");
            throw;
        }
    }

    public async Task<IEnumerable<User>> GetUsersAsync(string? searchFilter = null)
    {
        _logger.LogInformation("Getting users with filter: {Filter}", searchFilter ?? "(none)");

        try
        {
            var usersResponse = await _graphClient.Users
                .GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Top = 50;
                    requestConfig.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName" };

                    if (!string.IsNullOrWhiteSpace(searchFilter))
                    {
                        requestConfig.QueryParameters.Filter =
                            $"startsWith(displayName, '{searchFilter}') or startsWith(mail, '{searchFilter}')";
                    }
                });

            return usersResponse?.Value ?? Enumerable.Empty<User>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users");
            throw;
        }
    }

    public async Task SendMailAsync(
        string fromEmail,
        string toEmail,
        string subject,
        string htmlBody)
    {
        _logger.LogInformation("Sending email from {From} to {To}: {Subject}", fromEmail, toEmail, subject);

        try
        {
            var requestBody = new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = htmlBody
                    },
                    ToRecipients = new List<Recipient>
                    {
                        new Recipient
                        {
                            EmailAddress = new EmailAddress { Address = toEmail }
                        }
                    }
                },
                SaveToSentItems = true
            };

            await _graphClient.Users[fromEmail]
                .SendMail
                .PostAsync(requestBody);

            _logger.LogInformation("Email sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email");
            throw;
        }
    }
}
