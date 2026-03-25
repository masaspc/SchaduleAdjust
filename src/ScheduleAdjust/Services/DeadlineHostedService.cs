namespace ScheduleAdjust.Services;

public class DeadlineHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DeadlineHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

    public DeadlineHostedService(IServiceProvider services, ILogger<DeadlineHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadlineHostedService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduleService>();
                await scheduleService.ProcessExpiredPollsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired polls");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("DeadlineHostedService stopped");
    }
}
