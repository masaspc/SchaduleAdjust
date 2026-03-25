using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Moq;
using ScheduleAdjust.Models;
using ScheduleAdjust.Services;
using ScheduleAdjust.Tests.Helpers;
using ScheduleAdjust.ViewModels;
using Xunit;

namespace ScheduleAdjust.Tests.Services;

public class ScheduleServiceTests
{
    private readonly Mock<IGraphApiService> _graphApiMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ILogger<ScheduleService>> _loggerMock;

    public ScheduleServiceTests()
    {
        _graphApiMock = new Mock<IGraphApiService>();
        _notificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<ScheduleService>>();
    }

    private ScheduleService CreateService(Data.ScheduleAdjustDbContext db)
    {
        return new ScheduleService(db, _graphApiMock.Object, _notificationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreatePollAsync_SetsGuidAndDraftStatus()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var model = new CreatePollViewModel
        {
            Title = "Test Meeting",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now.AddDays(1),
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            AttendeeEmails = new List<string> { "user1@test.com" }
        };

        // Act
        var poll = await service.CreatePollAsync(model, "org-1", "org@test.com", "Organizer");

        // Assert
        Assert.NotEqual(Guid.Empty, poll.PollGuid);
        Assert.Equal(PollStatus.Draft, poll.Status);
        Assert.Equal("Test Meeting", poll.Title);
        Assert.Single(poll.Attendees);
        Assert.Equal("user1@test.com", poll.Attendees.First().Email);
    }

    [Fact]
    public async Task GenerateCandidateSlotsAsync_CallsGraphAndCreatesSlots()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var poll = new SchedulePoll
        {
            Title = "Test",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now.AddDays(1),
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Org"
        };
        poll.Attendees.Add(new PollAttendee { Email = "user1@test.com", DisplayName = "User 1" });
        db.SchedulePolls.Add(poll);
        await db.SaveChangesAsync();

        // Use explicit JST business hours to pass the filter
        var jstOffset = TimeSpan.FromHours(9);
        var start = new DateTimeOffset(2026, 4, 1, 10, 0, 0, jstOffset); // Wed 10:00 JST
        var end = new DateTimeOffset(2026, 4, 1, 11, 0, 0, jstOffset);   // Wed 11:00 JST

        _graphApiMock.Setup(g => g.FindMeetingTimesAsync(
            It.IsAny<IEnumerable<string>>(),
            60,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new MeetingTimeSuggestionsResult
            {
                MeetingTimeSuggestions = new List<MeetingTimeSuggestion>
                {
                    new MeetingTimeSuggestion
                    {
                        MeetingTimeSlot = new TimeSlot
                        {
                            Start = new DateTimeTimeZone { DateTime = start.UtcDateTime.ToString("o"), TimeZone = "UTC" },
                            End = new DateTimeTimeZone { DateTime = end.UtcDateTime.ToString("o"), TimeZone = "UTC" }
                        },
                        Confidence = 100
                    }
                }
            });

        // Act
        var slots = await service.GenerateCandidateSlotsAsync(poll.PollId);

        // Assert
        Assert.Single(slots);
        _graphApiMock.Verify(g => g.FindMeetingTimesAsync(
            It.IsAny<IEnumerable<string>>(), 60,
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitResponseAsync_WhenPollNotOpen_ThrowsInvalidOperation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var poll = new SchedulePoll
        {
            Title = "Test",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            Status = PollStatus.Draft,
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Org"
        };
        db.SchedulePolls.Add(poll);
        await db.SaveChangesAsync();

        var model = new BookingFormModel
        {
            SelectedSlotId = 1,
            RespondentName = "Test",
            RespondentEmail = "test@test.com"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitResponseAsync(poll.PollGuid, model));
    }

    [Fact]
    public async Task SubmitResponseAsync_WhenValid_CreatesResponse()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var poll = new SchedulePoll
        {
            Title = "Test",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            Status = PollStatus.Open,
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Org"
        };
        db.SchedulePolls.Add(poll);
        await db.SaveChangesAsync();

        var slot = new PollTimeSlot
        {
            PollId = poll.PollId,
            StartDateTime = DateTimeOffset.Now.AddDays(2),
            EndDateTime = DateTimeOffset.Now.AddDays(2).AddHours(1),
            IsAvailable = true
        };
        db.PollTimeSlots.Add(slot);
        await db.SaveChangesAsync();

        var model = new BookingFormModel
        {
            SelectedSlotId = slot.SlotId,
            RespondentName = "Respondent",
            RespondentEmail = "respondent@test.com",
            RespondentCompany = "Test Corp"
        };

        // Act
        var response = await service.SubmitResponseAsync(poll.PollGuid, model);

        // Assert
        Assert.Equal("Respondent", response.RespondentName);
        Assert.Equal(slot.SlotId, response.SelectedSlotId);
        Assert.Equal(1, db.PollResponses.Count());
    }

    [Fact]
    public async Task PublishPollAsync_WithNoSlots_ThrowsInvalidOperation()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var poll = new SchedulePoll
        {
            Title = "Test",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            Status = PollStatus.Draft,
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Org"
        };
        db.SchedulePolls.Add(poll);
        await db.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PublishPollAsync(poll.PollId));
    }

    [Fact]
    public async Task ConfirmSlotAsync_CreatesEventAndSetsStatus()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var poll = new SchedulePoll
        {
            Title = "Test",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            Status = PollStatus.Open,
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Org"
        };
        db.SchedulePolls.Add(poll);
        await db.SaveChangesAsync();

        var slot = new PollTimeSlot
        {
            PollId = poll.PollId,
            StartDateTime = DateTimeOffset.Now.AddDays(2),
            EndDateTime = DateTimeOffset.Now.AddDays(2).AddHours(1),
            IsAvailable = true
        };
        db.PollTimeSlots.Add(slot);
        await db.SaveChangesAsync();

        _graphApiMock.Setup(g => g.CreateTeamsMeetingAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new OnlineMeeting
            {
                JoinWebUrl = "https://teams.microsoft.com/test"
            });

        _graphApiMock.Setup(g => g.CreateEventAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new Event { Id = "event-123" });

        // Act
        var result = await service.ConfirmSlotAsync(poll.PollId, slot.SlotId);

        // Assert
        Assert.Equal(PollStatus.Confirmed, result.Status);
        Assert.Equal(slot.SlotId, result.ConfirmedSlotId);
        Assert.Equal("https://teams.microsoft.com/test", result.TeamsJoinUrl);
        Assert.Equal("event-123", result.GraphEventId);
    }

    [Fact]
    public async Task ProcessExpiredPollsAsync_ExpiresOverduePolls()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var poll = new SchedulePoll
        {
            Title = "Expired Test",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now.AddDays(-7),
            CandidateEndDate = DateTimeOffset.Now.AddDays(-1),
            Deadline = DateTimeOffset.UtcNow.AddHours(-1),
            Status = PollStatus.Open,
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Org"
        };
        db.SchedulePolls.Add(poll);
        await db.SaveChangesAsync();

        // Act
        await service.ProcessExpiredPollsAsync();

        // Assert
        var updated = db.SchedulePolls.First(p => p.PollId == poll.PollId);
        Assert.Equal(PollStatus.Expired, updated.Status);
    }
}
