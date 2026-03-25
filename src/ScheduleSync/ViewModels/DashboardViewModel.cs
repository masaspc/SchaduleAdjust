using ScheduleSync.Models;

namespace ScheduleSync.ViewModels;

public class DashboardViewModel
{
    public IEnumerable<PollSummaryItem> Polls { get; set; } = Enumerable.Empty<PollSummaryItem>();
}

public class PollSummaryItem
{
    public int PollId { get; set; }
    public string Title { get; set; } = string.Empty;
    public PollStatus Status { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public int ResponseCount { get; set; }
    public int SlotCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid PollGuid { get; set; }

    public string StatusDisplayName => Status switch
    {
        PollStatus.Draft => "下書き",
        PollStatus.Open => "公開中",
        PollStatus.Confirmed => "確定済み",
        PollStatus.Expired => "期限切れ",
        PollStatus.Cancelled => "キャンセル",
        _ => "不明"
    };

    public string StatusBadgeClass => Status switch
    {
        PollStatus.Draft => "bg-secondary",
        PollStatus.Open => "bg-primary",
        PollStatus.Confirmed => "bg-success",
        PollStatus.Expired => "bg-warning text-dark",
        PollStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };
}
