using Microsoft.EntityFrameworkCore;
using ScheduleSync.Data;

namespace ScheduleSync.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ScheduleSyncDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ScheduleSyncDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ScheduleSyncDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
