using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Services;

var config = new ConfigurationBuilder().SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "src/OvertimeHotel.HRM.WebAdmin")).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(config.GetConnectionString("DefaultConnection")).Options;
await using var db1 = new AppDbContext(options);
await using var db2 = new AppDbContext(options);
var first = new AccountRequestLock(db1);
var second = new AccountRequestLock(db2);
const int testId = -193271;
await using (var held = await first.TryAcquireAsync(testId, default) ?? throw new Exception("First lock failed")) {
    if (await second.TryAcquireAsync(testId, default) != null) throw new Exception("Concurrent lock was allowed");
    await using var other = await second.TryAcquireAsync(testId - 1, default) ?? throw new Exception("Unrelated requests must not block each other");
}
await using var reacquired = await second.TryAcquireAsync(testId, default) ?? throw new Exception("Lock did not release");
Console.WriteLine("PASS only one worker acquires a request and the lock releases");
