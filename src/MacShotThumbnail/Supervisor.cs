using System.Diagnostics;

namespace MacShotThumbnail;

internal static class Supervisor
{
    public static int Run()
    {
        using var mutex = new Mutex(true, "Local\\MacShotThumbnail.Supervisor", out bool first);
        if (!first) return 0;
        int failures = 0;
        while (true)
        {
            var started = Stopwatch.StartNew();
            try
            {
                using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--background")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppContext.BaseDirectory
                }) ?? throw new InvalidOperationException("MacShot could not be started.");
                child.WaitForExit();
                if (child.ExitCode == 0) return 0;
                Settings.LogEvent($"MacShot exited unexpectedly ({child.ExitCode}); restarting.");
            }
            catch (Exception error) { Settings.Log(error); }
            if (started.Elapsed > TimeSpan.FromMinutes(5)) failures = 0;
            if (++failures > 3) return 1;
            Thread.Sleep(5000);
        }
    }
}
