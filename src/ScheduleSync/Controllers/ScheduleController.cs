using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleSync.Services;
using ScheduleSync.ViewModels;

namespace ScheduleSync.Controllers;

[Authorize]
public class ScheduleController : Controller
{
    private readonly IScheduleService _scheduleService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _config;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(
        IScheduleService scheduleService,
        INotificationService notificationService,
        IConfiguration config,
        ILogger<ScheduleController> logger)
    {
        _scheduleService = scheduleService;
        _notificationService = notificationService;
        _config = config;
        _logger = logger;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string GetUserEmail() =>
        User.FindFirstValue("preferred_username") ?? User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    private string GetUserName() =>
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    // GET: /Schedule
    public async Task<IActionResult> Index()
    {
        var polls = await _scheduleService.GetDashboardAsync(GetUserId());

        var viewModel = new DashboardViewModel
        {
            Polls = polls.Select(p => new PollSummaryItem
            {
                PollId = p.PollId,
                Title = p.Title,
                Status = p.Status,
                Deadline = p.Deadline,
                ResponseCount = p.Responses.Count,
                SlotCount = p.TimeSlots.Count(s => s.IsAvailable),
                CreatedAt = p.CreatedAt,
                PollGuid = p.PollGuid
            })
        };

        return View(viewModel);
    }

    // GET: /Schedule/Create
    public IActionResult Create()
    {
        return View(new CreatePollViewModel());
    }

    // POST: /Schedule/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePollViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var poll = await _scheduleService.CreatePollAsync(
                model, GetUserId(), GetUserEmail(), GetUserName());

            TempData["SuccessMessage"] = "調整ページを作成しました。候補日を確認・編集してください。";
            return RedirectToAction(nameof(EditSlots), new { id = poll.PollId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create poll");
            ModelState.AddModelError("", "作成に失敗しました。もう一度お試しください。");
            return View(model);
        }
    }

    // GET: /Schedule/EditSlots/{id}
    public async Task<IActionResult> EditSlots(int id)
    {
        var poll = await _scheduleService.GetPollByIdAsync(id);
        if (poll == null || poll.OrganizerId != GetUserId())
            return NotFound();

        var viewModel = new EditSlotsViewModel
        {
            PollId = poll.PollId,
            Title = poll.Title,
            DurationMinutes = poll.DurationMinutes,
            Status = poll.Status,
            ExistingSlots = poll.TimeSlots.Where(s => s.IsAvailable).OrderBy(s => s.StartDateTime).ToList()
        };

        return View(viewModel);
    }

    // POST: /Schedule/FindSlots/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FindSlots(int id)
    {
        try
        {
            await _scheduleService.GenerateCandidateSlotsAsync(id);
            TempData["SuccessMessage"] = "候補日時を自動算出しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find slots for poll {PollId}", id);
            TempData["ErrorMessage"] = "候補日時の算出に失敗しました。";
        }

        return RedirectToAction(nameof(EditSlots), new { id });
    }

    // POST: /Schedule/AddSlot/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSlot(int id, DateTimeOffset newSlotStart, DateTimeOffset newSlotEnd)
    {
        if (newSlotStart >= newSlotEnd)
        {
            TempData["ErrorMessage"] = "開始日時は終了日時より前に設定してください。";
            return RedirectToAction(nameof(EditSlots), new { id });
        }

        try
        {
            await _scheduleService.AddManualSlotAsync(id, newSlotStart, newSlotEnd);
            TempData["SuccessMessage"] = "候補日時を追加しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add slot to poll {PollId}", id);
            TempData["ErrorMessage"] = "候補日時の追加に失敗しました。";
        }

        return RedirectToAction(nameof(EditSlots), new { id });
    }

    // POST: /Schedule/RemoveSlot/{slotId}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSlot(int slotId, int pollId)
    {
        try
        {
            await _scheduleService.RemoveSlotAsync(slotId);
            TempData["SuccessMessage"] = "候補日時を削除しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove slot {SlotId}", slotId);
            TempData["ErrorMessage"] = "候補日時の削除に失敗しました。";
        }

        return RedirectToAction(nameof(EditSlots), new { id = pollId });
    }

    // POST: /Schedule/Publish/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        try
        {
            var poll = await _scheduleService.PublishPollAsync(id);
            TempData["SuccessMessage"] = "調整ページを公開しました。";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish poll {PollId}", id);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(EditSlots), new { id });
        }
    }

    // GET: /Schedule/Detail/{id}
    public async Task<IActionResult> Detail(int id)
    {
        var poll = await _scheduleService.GetPollByIdAsync(id);
        if (poll == null || poll.OrganizerId != GetUserId())
            return NotFound();

        var baseUrl = _config["BaseUrl"] ?? "https://localhost:5001";

        var viewModel = new PollDetailViewModel
        {
            PollId = poll.PollId,
            PollGuid = poll.PollGuid,
            Title = poll.Title,
            DurationMinutes = poll.DurationMinutes,
            CandidateStartDate = poll.CandidateStartDate,
            CandidateEndDate = poll.CandidateEndDate,
            Deadline = poll.Deadline,
            Note = poll.Note,
            Status = poll.Status,
            OrganizerName = poll.OrganizerName,
            TeamsJoinUrl = poll.TeamsJoinUrl,
            CreatedAt = poll.CreatedAt,
            ConfirmedSlotId = poll.ConfirmedSlotId,
            ConfirmedSlot = poll.ConfirmedSlot,
            BookingUrl = $"{baseUrl}/Booking/{poll.PollGuid}",
            Attendees = poll.Attendees.ToList(),
            Responses = poll.Responses.ToList(),
            TimeSlots = poll.TimeSlots
                .Where(s => s.IsAvailable)
                .OrderBy(s => s.StartDateTime)
                .Select(s => new SlotWithResponseCount
                {
                    SlotId = s.SlotId,
                    StartDateTime = s.StartDateTime,
                    EndDateTime = s.EndDateTime,
                    IsManuallyAdded = s.IsManuallyAdded,
                    ResponseCount = poll.Responses.Count(r => r.SelectedSlotId == s.SlotId)
                }).ToList()
        };

        return View(viewModel);
    }

    // POST: /Schedule/Confirm/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, int slotId)
    {
        try
        {
            await _scheduleService.ConfirmSlotAsync(id, slotId);
            TempData["SuccessMessage"] = "日程を確定しました。Outlook予定とTeams会議が作成されました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm poll {PollId} with slot {SlotId}", id, slotId);
            TempData["ErrorMessage"] = "確定処理に失敗しました: " + ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    // POST: /Schedule/Cancel/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _scheduleService.CancelPollAsync(id);
            TempData["SuccessMessage"] = "調整をキャンセルしました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel poll {PollId}", id);
            TempData["ErrorMessage"] = "キャンセルに失敗しました。";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Schedule/SendLink/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendLink(int id)
    {
        try
        {
            var poll = await _scheduleService.GetPollByIdAsync(id);
            if (poll == null) return NotFound();

            await _notificationService.SendBookingUrlAsync(poll);
            TempData["SuccessMessage"] = "調整URLを送信しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send link for poll {PollId}", id);
            TempData["ErrorMessage"] = "URL送信に失敗しました。";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }
}
