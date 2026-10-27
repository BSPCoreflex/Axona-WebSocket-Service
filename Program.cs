using Axona_WebSocket_Service;
using System.Diagnostics;
using WebSocketSharp;

static bool RunScCommand(string arguments)
{
    try
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process? process = Process.Start(startInfo))
        {
            process?.WaitForExit();

            string? output = process?.StandardOutput.ReadToEnd();
            string? error = process?.StandardError.ReadToEnd();

            if (process?.ExitCode == 0)
            {
                return true;
            }
            else
            {
                Console.WriteLine($"sc.exe failed with exit code {process?.ExitCode}");
                Console.WriteLine($"Error: {error?.Trim()} {output?.Trim()}");
                return false;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception executing sc.exe: {ex.Message}");
        return false;
    }
}


string serviceName = "Axona Web Socket Service";

// Check for installer arguments passed by the MSI package
if (args.Length > 0)
{
    string ?pathToExe = Environment.ProcessPath;

    if (args[0].Equals("/Install", StringComparison.OrdinalIgnoreCase))
    {
        // Formatting binPath requires care with nested quotes if the path contains spaces
        string arguments = $"create \"{serviceName}\" binPath= \"\\\"{pathToExe}\\\"\" start= auto";
        RunScCommand(arguments);

        arguments = $"description \"{serviceName}\" \"Axona Desktop Device Interfaces\"";
        RunScCommand(arguments);

        arguments = $"start \"{serviceName}\"";
        RunScCommand(arguments);

        return;
    }
    else if (args[0].Equals("/Uninstall", StringComparison.OrdinalIgnoreCase))
    {
        string arguments = $"stop \"{serviceName}\"";
        RunScCommand(arguments);

        arguments = $"delete \"{serviceName}\"";
        RunScCommand(arguments);

        return;
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Enable Windows Service integration
//builder.Services.Configure<HostOptions>(options =>
//{
//  
//    options.ShutdownTimeout = TimeSpan.FromSeconds(15);
//});


// 1. Enforce Windows Service hosting behavior
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = serviceName;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
