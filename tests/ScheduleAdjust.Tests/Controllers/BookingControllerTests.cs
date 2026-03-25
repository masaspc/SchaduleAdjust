using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ScheduleAdjust.Controllers;
using ScheduleAdjust.Models;
using ScheduleAdjust.Services;
using ScheduleAdjust.ViewModels;

namespace ScheduleAdjust.Tests.Controllers;

public class BookingControllerTests
{
    private readonly Mock<IScheduleService> _scheduleServiceMock;
    private readonly Mock<ILogger<BookingController>> _loggerMock;
    private readonly BookingController _controller;

    public BookingControllerTests()
    {
        _scheduleServiceMock = new Mock<IScheduleService>();
        _loggerMock = new Mock<ILogger<BookingController>>();
        _controller = new BookingController(_scheduleServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Index_WithInvalidGuid_ReturnsNotFound()
    {
        // Arrange
        var guid = Guid.NewGuid();
        _scheduleServiceMock.Setup(s => s.GetPollByGuidAsync(guid))
            .ReturnsAsync((SchedulePoll?)null);

        // Act
        var result = await _controller.Index(guid);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_WithValidOpenPoll_ReturnsView()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var poll = new SchedulePoll
        {
            PollId = 1,
            PollGuid = guid,
            Title = "Test Meeting",
            Status = PollStatus.Open,
            DurationMinutes = 60,
            OrganizerName = "Organizer",
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com"
        };
        poll.TimeSlots.Add(new PollTimeSlot
        {
            SlotId = 1,
            PollId = 1,
            StartDateTime = DateTimeOffset.Now.AddDays(1),
            EndDateTime = DateTimeOffset.Now.AddDays(1).AddHours(1),
            IsAvailable = true
        });

        _scheduleServiceMock.Setup(s => s.GetPollByGuidAsync(guid))
            .ReturnsAsync(poll);

        // Act
        var result = await _controller.Index(guid);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BookingViewModel>(viewResult.Model);
        Assert.Equal("Test Meeting", model.Title);
        Assert.Single(model.AvailableSlots);
    }

    [Fact]
    public async Task Index_WithExpiredPoll_ReturnsExpiredView()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var poll = new SchedulePoll
        {
            PollId = 1,
            PollGuid = guid,
            Title = "Expired Meeting",
            Status = PollStatus.Expired,
            DurationMinutes = 60,
            OrganizerName = "Organizer",
            CandidateStartDate = DateTimeOffset.Now,
            CandidateEndDate = DateTimeOffset.Now.AddDays(7),
            OrganizerId = "org-1",
            OrganizerEmail = "org@test.com"
        };

        _scheduleServiceMock.Setup(s => s.GetPollByGuidAsync(guid))
            .ReturnsAsync(poll);

        // Act
        var result = await _controller.Index(guid);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Expired", viewResult.ViewName);
    }

    [Fact]
    public async Task Submit_WithValidModel_RedirectsToConfirmed()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var model = new BookingFormModel
        {
            SelectedSlotId = 1,
            RespondentName = "Test User",
            RespondentEmail = "test@test.com"
        };

        _scheduleServiceMock.Setup(s => s.SubmitResponseAsync(guid, model))
            .ReturnsAsync(new PollResponse
            {
                ResponseId = 42,
                PollId = 1,
                SelectedSlotId = 1,
                RespondentName = "Test User",
                RespondentEmail = "test@test.com"
            });

        // Act
        var result = await _controller.Submit(guid, model);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Confirmed", redirectResult.ActionName);
    }
}
