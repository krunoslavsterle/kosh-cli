using System;
using System.Threading;

Console.WriteLine("===============================================");
Console.WriteLine("   KOSH DEMO: Database Migrations Runner       ");
Console.WriteLine("===============================================");

string[] migrations = new[]
{
    "202608050001_CreateUsersTable",
    "202608050002_CreateServicesTable",
    "202608050003_CreateLogsIndex",
    "202608050004_SeedInitialAdminUser"
};

Console.WriteLine($"[INFO] Starting database migration check (Target: PostgreSQL 16 @ localhost:5432)...");
Thread.Sleep(800);

foreach (var migration in migrations)
{
    Console.WriteLine($"[APPLYING] Migration '{migration}'...");
    Thread.Sleep(600);
    Console.WriteLine($"[SUCCESS]  Migration '{migration}' applied cleanly (execution time: {Random.Shared.Next(15, 85)}ms).");
}

Console.WriteLine("===============================================");
Console.WriteLine("[SUCCESS] All 4 migrations applied successfully!");
Console.WriteLine("===============================================");
