using Microsoft.EntityFrameworkCore;
using ScheduleAdjust.Data;

namespace ScheduleAdjust.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ScheduleAdjustDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ScheduleAdjustDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ScheduleAdjustDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
