using ScheduleSync.Models;

namespace ScheduleSync.Services;

public class NotificationService : INotificationService
{
    private readonly IGraphApiService _graphApi;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IGraphApiService graphApi,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _graphApi = graphApi;
        _config = config;
        _logger = logger;
    }

    public async Task SendBookingUrlAsync(SchedulePoll poll)
    {
        var baseUrl = _config["BaseUrl"] ?? "https://localhost:5001";
        var bookingUrl = $"{baseUrl}/Booking/{poll.PollGuid}";

        var subject = $"【日程調整】{poll.Title}";
        var htmlBody = $@"
            <html>
            <body>
                <h2>日程調整のご依頼</h2>
                <p>{poll.OrganizerName}さんから日程調整の依頼が届きました。</p>
                <p><strong>件名:</strong> {poll.Title}</p>
                {(string.IsNullOrEmpty(poll.Note) ? "" : $"<p><strong>メモ:</strong> {poll.Note}</p>")}
                {(poll.Deadline.HasValue ? $"<p><strong>回答期限:</strong> {poll.Deadline.Value:yyyy/MM/dd HH:mm}</p>" : "")}
                <p>下記リンクからご都合の良い日時を選択してください。</p>
                <p><a href=""{bookingUrl}"" style=""display:inline-block;padding:12px 24px;background:#0078d4;color:#fff;text-decoration:none;border-radius:4px;"">日程を選択する</a></p>
                <hr/>
                <p style=""font-size:12px;color:#666;"">このメールはScheduleSyncから自動送信されています。</p>
            </body>
            </html>";

        await _graphApi.SendMailAsync(poll.OrganizerEmail, poll.OrganizerEmail, subject, htmlBody);

        _logger.LogInformation("Booking URL sent for Poll {PollId}: {Url}", poll.PollId, bookingUrl);
    }

    public async Task SendConfirmationAsync(SchedulePoll poll, PollResponse response)
    {
        var subject = $"【日程確定】{poll.Title}";
        var confirmedSlotInfo = poll.ConfirmedSlot != null
            ? $"{poll.ConfirmedSlot.StartDateTime:yyyy/MM/dd HH:mm} - {poll.ConfirmedSlot.EndDateTime:HH:mm}"
            : "確定済み";

        var htmlBody = $@"
            <html>
            <body>
                <h2>日程が確定しました</h2>
                <p>{response.RespondentName}様</p>
                <p>以下の日程で確定いたしました。</p>
                <p><strong>件名:</strong> {poll.Title}</p>
                <p><strong>日時:</strong> {confirmedSlotInfo}</p>
                <p><strong>主催者:</strong> {poll.OrganizerName}</p>
                {(string.IsNullOrEmpty(poll.TeamsJoinUrl) ? "" : $@"<p><strong>Teams会議:</strong> <a href=""{poll.TeamsJoinUrl}"">会議に参加</a></p>")}
                <hr/>
                <p style=""font-size:12px;color:#666;"">このメールはScheduleSyncから自動送信されています。</p>
            </body>
            </html>";

        await _graphApi.SendMailAsync(poll.OrganizerEmail, response.RespondentEmail, subject, htmlBody);

        _logger.LogInformation("Confirmation sent to {Email} for Poll {PollId}",
            response.RespondentEmail, poll.PollId);
    }

    public async Task SendReminderAsync(SchedulePoll poll)
    {
        var subject = $"【期限切れ】{poll.Title}";
        var htmlBody = $@"
            <html>
            <body>
                <h2>日程調整の期限が切れました</h2>
                <p>{poll.OrganizerName}様</p>
                <p>「{poll.Title}」の回答期限が過ぎました。</p>
                <p>ダッシュボードから状況を確認してください。</p>
                <hr/>
                <p style=""font-size:12px;color:#666;"">このメールはScheduleSyncから自動送信されています。</p>
            </body>
            </html>";

        await _graphApi.SendMailAsync(poll.OrganizerEmail, poll.OrganizerEmail, subject, htmlBody);

        _logger.LogInformation("Expiry reminder sent for Poll {PollId}", poll.PollId);
    }
}
