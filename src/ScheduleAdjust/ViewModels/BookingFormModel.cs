using System.ComponentModel.DataAnnotations;

namespace ScheduleAdjust.ViewModels;

public class BookingFormModel
{
    [Required(ErrorMessage = "日時を選択してください")]
    public int SelectedSlotId { get; set; }

    [Required(ErrorMessage = "お名前は必須です")]
    [MaxLength(200)]
    [Display(Name = "お名前")]
    public string RespondentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [MaxLength(256)]
    [Display(Name = "メールアドレス")]
    public string RespondentEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "会社名")]
    public string? RespondentCompany { get; set; }
}
