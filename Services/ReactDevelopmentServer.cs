using System.Diagnostics;

namespace LMS.Services;

// Only runs in Development. Published deployments serve prebuilt static files without Node.js.
public sealed class ReactDevelopmentServer(
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<ReactDevelopmentServer> logger) : IHostedService, IDisposable
{
    private Process? process;
    private bool started;
    public static int Port(IConfiguration configuration) => configuration.GetValue<int?>("Frontend:DevServerPort") ?? 5173;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var port = Port(configuration);
        if (port is < 1024 or > 65535) throw new InvalidOperationException("Frontend:DevServerPort must be between 1024 and 65535.");
        using (var probe = new System.Net.Sockets.TcpClient())
        {
            try
            {
                await probe.ConnectAsync(System.Net.IPAddress.Loopback, port, cancellationToken);
                throw new InvalidOperationException($"Vite port {port} is already in use. Stop the other process or set Frontend__DevServerPort.");
            }
            catch (System.Net.Sockets.SocketException) { /* The port is available. */ }
        }
        var command = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npm",
            WorkingDirectory = Path.Combine(environment.ContentRootPath, "ClientApp"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
        {
            command.ArgumentList.Add("/d");
            command.ArgumentList.Add("/c");
            command.ArgumentList.Add($"npm run dev -- --host 127.0.0.1 --port {port}");
        }
        else
        {
            foreach (var argument in new[] { "run", "dev", "--", "--host", "127.0.0.1", "--port", port.ToString() })
                command.ArgumentList.Add(argument);
        }
        var address = configuration["urls"]?.Split(';').FirstOrDefault();
        if (Uri.TryCreate(address, UriKind.Absolute, out var backend))
            command.Environment["VITE_BACKEND_PORT"] = backend.Port.ToString();
        process = new Process { StartInfo = command, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => { if (args.Data is { } line) logger.LogInformation("Vite: {Line}", line); };
        process.ErrorDataReceived += (_, args) => { if (args.Data is { } line) logger.LogWarning("Vite: {Line}", line); };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Could not start Vite.");
            started = true;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            for (var attempt = 0; attempt < 80; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidOperationException("Vite exited during startup. Check its output and whether its port is already in use.");
                try
                {
                    using var response = await client.GetAsync($"http://127.0.0.1:{port}/app/", cancellationToken);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested) { }
                await Task.Delay(250, cancellationToken);
            }
            throw new TimeoutException("Vite did not become ready. Install Node.js 22.12+ and check the frontend build output.");
        }
        catch { StopProcess(); throw; }
    }

    private void StopProcess()
    {
        if (started && process is { HasExited: false }) process.Kill(entireProcessTree: true);
        started = false;
    }
    public Task StopAsync(CancellationToken cancellationToken) { StopProcess(); return Task.CompletedTask; }
    public void Dispose() { StopProcess(); process?.Dispose(); }
}
