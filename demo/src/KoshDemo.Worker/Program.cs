using System;
using System.Threading;

Console.WriteLine("===============================================");
Console.WriteLine("   ⚙️ KOSH DEMO: Background Worker Started      ");
Console.WriteLine("===============================================");

string argsSummary = args.Length > 0 ? string.Join(" ", args) : "(none)";
Console.WriteLine($"[WORKER] Passed Arguments: {argsSummary}");

int cycle = 1;

while (true)
{
    Console.WriteLine($"[WORKER] [Cycle #{cycle}] Processing background job queue... (Args: {argsSummary})");
    Thread.Sleep(2000);
    Console.WriteLine($"[WORKER] [Cycle #{cycle}] Processed {Random.Shared.Next(5, 30)} messages. Queue size: 0.");

    if (cycle == 4)
    {
        Console.Error.WriteLine($"[FATAL] Unrecoverable Exception in Worker process:");
        Console.Error.WriteLine($"System.InvalidOperationException: Fatal memory allocation error in Worker queue manager");
        Console.Error.WriteLine($"   at KoshDemo.Worker.JobProcessor.ExecuteFatalTask() in /demo/src/KoshDemo.Worker/Program.cs:line 45");
        Console.Error.WriteLine($"   at KoshDemo.Worker.Program.Main(String[] args) in /demo/src/KoshDemo.Worker/Program.cs:line 18");
        Console.WriteLine($"[FATAL] Worker process crashing now...");
        Thread.Sleep(500);

        // Crash the worker process with non-zero exit code to test Kosh 'Failed' status tracking
        throw new InvalidOperationException("Fatal Worker queue crash!");
    }

    cycle++;
    Thread.Sleep(3000);
}
