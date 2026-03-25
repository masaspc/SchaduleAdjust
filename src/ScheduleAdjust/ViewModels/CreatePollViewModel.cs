using System.ComponentModel.DataAnnotations;

namespace ScheduleAdjust.ViewModels;

public class CreatePollViewModel
{
    [Required(ErrorMessage = "タイトルは必須です")]
    [MaxLength(200)]
    [Display(Name = "タイトル")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "所要時間は必須です")]
    [Range(15, 480, ErrorMessage = "所要時間は15分〜480分で指定してください")]
    [Display(Name = "所要時間（分）")]
    public int DurationMinutes { get; set; } = 60;

    [Required(ErrorMessage = "候補期間の開始日は必須です")]
    [Display(Name = "候補期間（開始）")]
    public DateTimeOffset CandidateStartDate { get; set; } = DateTimeOffset.Now.Date.AddDays(1);

    [Required(ErrorMessage = "候補期間の終了日は必須です")]
    [Display(Name = "候補期間（終了）")]
    public DateTimeOffset CandidateEndDate { get; set; } = DateTimeOffset.Now.Date.AddDays(14);

    [Display(Name = "回答期限")]
    public DateTimeOffset? Deadline { get; set; }

    [MaxLength(2000)]
    [Display(Name = "メモ")]
    public string? Note { get; set; }

    [Display(Name = "同席者メールアドレス")]
    public List<string>? AttendeeEmails { get; set; }
}
