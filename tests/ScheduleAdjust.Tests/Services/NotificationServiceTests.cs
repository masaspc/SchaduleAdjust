using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ScheduleAdjust.Models;
using ScheduleAdjust.Services;
using Xunit;

namespace ScheduleAdjust.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<IGraphApiService> _graphApiMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly IConfiguration _config;

    public NotificationServiceTests()
    {
        _graphApiMock = new Mock<IGraphApiService>();
        _loggerMock = new Mock<ILogger<NotificationService>>();

        var configData = new Dictionary<string, string?>
        {
            { "BaseUrl", "https://test.example.com" }
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public async Task SendBookingUrlAsync_SendsEmailWithCorrectUrl()
    {
        // Arrange
        var service = new NotificationService(_graphApiMock.Object, _config, _loggerMock.Object);
        var poll = new SchedulePoll
        {
            PollId = 1,
            PollGuid = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            Title = "Test Meeting",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Organizer",
            OrganizerId = "org-1",
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7)
        };

        // Act
        await service.SendBookingUrlAsync(poll);

        // Assert
        _graphApiMock.Verify(g => g.SendMailAsync(
            "org@test.com",
            "org@test.com",
            It.Is<string>(s => s.Contains("Test Meeting")),
            It.Is<string>(body => body.Contains("https://test.example.com/Booking/12345678-1234-1234-1234-123456789012"))),
            Times.Once);
    }

    [Fact]
    public async Task SendConfirmationAsync_IncludesTeamsLink()
    {
        // Arrange
        var service = new NotificationService(_graphApiMock.Object, _config, _loggerMock.Object);
        var confirmedSlot = new PollTimeSlot
        {
            SlotId = 1,
            StartDateTime = new DateTimeOffset(2025, 4, 1, 10, 0, 0, TimeSpan.FromHours(9)),
            EndDateTime = new DateTimeOffset(2025, 4, 1, 11, 0, 0, TimeSpan.FromHours(9))
        };
        var poll = new SchedulePoll
        {
            PollId = 1,
            Title = "Confirmed Meeting",
            OrganizerEmail = "org@test.com",
            OrganizerName = "Organizer",
            OrganizerId = "org-1",
            TeamsJoinUrl = "https://teams.microsoft.com/join/test",
            ConfirmedSlot = confirmedSlot,
            DurationMinutes = 60,
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7)
        };
        var response = new PollResponse
        {
            RespondentName = "Guest",
            RespondentEmail = "guest@test.com"
        };

        // Act
        await service.SendConfirmationAsync(poll, response);

        // Assert
        _graphApiMock.Verify(g => g.SendMailAsync(
            "org@test.com",
            "guest@test.com",
            It.Is<string>(s => s.Contains("確定")),
            It.Is<string>(body => body.Contains("https://teams.microsoft.com/join/test"))),
            Times.Once);
    }
}
