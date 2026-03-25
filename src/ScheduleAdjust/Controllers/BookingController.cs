using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleAdjust.Models;
using ScheduleAdjust.Services;
using ScheduleAdjust.ViewModels;

namespace ScheduleAdjust.Controllers;

[AllowAnonymous]
public class BookingController : Controller
{
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<BookingController> _logger;

    public BookingController(IScheduleService scheduleService, ILogger<BookingController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    // GET: /Booking/{guid}
    [HttpGet]
    public async Task<IActionResult> Index(Guid guid)
    {
        var poll = await _scheduleService.GetPollByGuidAsync(guid);

        if (poll == null)
            return NotFound();

        if (poll.Status == PollStatus.Expired || poll.Status == PollStatus.Cancelled)
            return View("Expired", new BookingViewModel { Title = poll.Title, PollGuid = guid });

        if (poll.Status == PollStatus.Confirmed)
            return View("Expired", new BookingViewModel { Title = poll.Title, PollGuid = guid });

        if (poll.Status != PollStatus.Open)
            return NotFound();

        // Check deadline
        if (poll.Deadline.HasValue && DateTimeOffset.UtcNow > poll.Deadline.Value)
            return View("Expired", new BookingViewModel { Title = poll.Title, PollGuid = guid });

        var viewModel = new BookingViewModel
        {
            PollGuid = poll.PollGuid,
            Title = poll.Title,
            OrganizerName = poll.OrganizerName,
            Note = poll.Note,
            Deadline = poll.Deadline,
            DurationMinutes = poll.DurationMinutes,
            AvailableSlots = poll.TimeSlots
                .Where(s => s.IsAvailable)
                .OrderBy(s => s.StartDateTime)
                .Select(s => new BookingSlotItem
                {
                    SlotId = s.SlotId,
                    StartDateTime = s.StartDateTime,
                    EndDateTime = s.EndDateTime
                }).ToList()
        };

        return View(viewModel);
    }

    // POST: /Booking/{guid}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid guid, BookingFormModel model)
    {
        if (!ModelState.IsValid)
        {
            // Reload poll data for view
            var poll = await _scheduleService.GetPollByGuidAsync(guid);
            if (poll == null) return NotFound();

            var viewModel = new BookingViewModel
            {
                PollGuid = poll.PollGuid,
                Title = poll.Title,
                OrganizerName = poll.OrganizerName,
                Note = poll.Note,
                Deadline = poll.Deadline,
                DurationMinutes = poll.DurationMinutes,
                AvailableSlots = poll.TimeSlots
                    .Where(s => s.IsAvailable)
                    .OrderBy(s => s.StartDateTime)
                    .Select(s => new BookingSlotItem
                    {
                        SlotId = s.SlotId,
                        StartDateTime = s.StartDateTime,
                        EndDateTime = s.EndDateTime
                    }).ToList()
            };

            return View("Index", viewModel);
        }

        try
        {
            var response = await _scheduleService.SubmitResponseAsync(guid, model);
            return RedirectToAction(nameof(Confirmed), new { guid, responseId = response.ResponseId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Booking submission failed for {Guid}", guid);
            ModelState.AddModelError("", ex.Message);

            var poll = await _scheduleService.GetPollByGuidAsync(guid);
            if (poll == null) return NotFound();

            var viewModel = new BookingViewModel
            {
                PollGuid = poll.PollGuid,
                Title = poll.Title,
                OrganizerName = poll.OrganizerName,
                Note = poll.Note,
                Deadline = poll.Deadline,
                DurationMinutes = poll.DurationMinutes,
                AvailableSlots = poll.TimeSlots
                    .Where(s => s.IsAvailable)
                    .OrderBy(s => s.StartDateTime)
                    .Select(s => new BookingSlotItem
                    {
                        SlotId = s.SlotId,
                        StartDateTime = s.StartDateTime,
                        EndDateTime = s.EndDateTime
                    }).ToList()
            };

            return View("Index", viewModel);
        }
    }

    // GET: /Booking/{guid}/Confirmed/{responseId}
    [HttpGet]
    public async Task<IActionResult> Confirmed(Guid guid, int responseId)
    {
        var poll = await _scheduleService.GetPollByGuidAsync(guid);
        if (poll == null) return NotFound();

        var response = poll.Responses.FirstOrDefault(r => r.ResponseId == responseId);
        if (response == null) return NotFound();

        var slot = poll.TimeSlots.FirstOrDefault(s => s.SlotId == response.SelectedSlotId);

        var viewModel = new BookingConfirmViewModel
        {
            Title = poll.Title,
            OrganizerName = poll.OrganizerName,
            SelectedSlotStart = slot?.StartDateTime ?? default,
            SelectedSlotEnd = slot?.EndDateTime ?? default,
            RespondentName = response.RespondentName,
            TeamsJoinUrl = poll.TeamsJoinUrl
        };

        return View(viewModel);
    }
}
