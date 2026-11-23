using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class WebServerStatusInfo
{
    public bool IsRunning { get; set; }
    public int? ProcessId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string StatusText { get; set; } = "Stopped";
    public bool IsAutoStartInstalled { get; set; }
    public string AutoStartStatusText { get; set; } = "Not Installed";
}

public interface IWebServerManagerService
{
    string AutoDetectExePath();
    Task<WebServerStatusInfo> GetStatusAsync(string host, string port, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> StartServerAsync(string exePath, string host, string port);
    Task<(bool Success, string Message)> StopServerAsync(string? exePath = null, string? port = null);
    Task<(bool Success, string Message)> InstallAutoStartServiceAsync(string exePath, string host, string port);
    Task<(bool Success, string Message)> RemoveAutoStartServiceAsync(string? exePath = null, string? port = null);
}

public sealed class WebServerManagerService : IWebServerManagerService
{
    public const string TaskName = "ConceptJewellery_WebServer";
    private const string ProcessName = "ConceptJewelleryPro.API";

    public string AutoDetectExePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var searchRoots = new List<string>
        {
            baseDir,
            Path.Combine(baseDir, "publish-standalone"),
            Path.Combine(baseDir, "publish-jewel"),
            Path.Combine(baseDir, "API"),
            Path.GetFullPath(Path.Combine(baseDir, "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "publish-standalone")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "publish-standalone")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "API", "publish-jewel")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "API", "bin", "Release", "net8.0", "win-x64")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "API", "bin", "Release", "net8.0")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "API", "bin", "Debug", "net8.0")),
            @"E:\repos\ConceptJewelleryPro\publish-standalone",
            @"E:\repos\ConceptJewelleryPro\API\publish-jewel"
        };

        foreach (var dir in searchRoots)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                var directExe = Path.Combine(dir, $"{ProcessName}.exe");
                if (File.Exists(directExe)) return directExe;

                // Recursive shallow check (1 level)
                var found = Directory.GetFiles(dir, $"{ProcessName}.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (found is not null) return found;
            }
            catch { }
        }

        return string.Empty;
    }

    public async Task<WebServerStatusInfo> GetStatusAsync(string host, string port, CancellationToken cancellationToken = default)
    {
        var status = new WebServerStatusInfo();
        var cleanPort = string.IsNullOrWhiteSpace(port) ? "5080" : port.Trim();
        var cleanHost = string.IsNullOrWhiteSpace(host) || host == "0.0.0.0" ? "localhost" : host.Trim();
        status.Url = $"http://{cleanHost}:{cleanPort}";

        // 1. Check running processes
        var processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length > 0)
        {
            status.IsRunning = true;
            status.ProcessId = processes[0].Id;
            status.StatusText = $"Running (PID {processes[0].Id}, Port {cleanPort})";
        }
        else
        {
            // Fallback: ping HTTP in case running under a different host/wrapper
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(1500) };
                var resp = await client.GetAsync($"http://localhost:{cleanPort}/api/diagnostics/status", cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    status.IsRunning = true;
                    status.StatusText = $"Running on Port {cleanPort}";
                }
            }
            catch
            {
                status.IsRunning = false;
                status.StatusText = "Stopped";
            }
        }

        // 2. Check Auto-start / Scheduled Task status
        var taskInstalled = IsTaskInstalled();
        var startupShortcutInstalled = IsStartupShortcutInstalled();

        if (taskInstalled || startupShortcutInstalled)
        {
            status.IsAutoStartInstalled = true;
            status.AutoStartStatusText = taskInstalled
                ? "Active in Windows Task Scheduler (Always Runs on Boot, Auto-Restarts on Crash)"
                : "Active in Windows Startup (Auto-Restarts on Crash)";
        }
        else
        {
            status.IsAutoStartInstalled = false;
            status.AutoStartStatusText = "Not Configured";
        }

        return status;
    }

    public async Task<(bool Success, string Message)> StartServerAsync(string exePath, string host, string port)
    {
        var resolvedExe = string.IsNullOrWhiteSpace(exePath) ? AutoDetectExePath() : exePath.Trim();
        if (string.IsNullOrWhiteSpace(resolvedExe) || !File.Exists(resolvedExe))
        {
            return (false, "Cannot start server: ConceptJewelleryPro.API.exe not found. Please browse and specify the executable path.");
        }

        // Check if already running
        var existing = Process.GetProcessesByName(ProcessName);
        if (existing.Length > 0)
        {
            return (true, $"Web Server is already running (PID: {existing[0].Id}).");
        }

        var cleanPort = string.IsNullOrWhiteSpace(port) ? "5080" : port.Trim();
        var cleanHost = string.IsNullOrWhiteSpace(host) ? "0.0.0.0" : host.Trim();
        var workingDir = Path.GetDirectoryName(resolvedExe) ?? AppContext.BaseDirectory;
        TryDeleteFile(Path.Combine(workingDir, ".stop_server"));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = resolvedExe,
                Arguments = $"--urls \"http://{cleanHost}:{cleanPort}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);
            if (proc is null)
            {
                return (false, "Failed to launch server process.");
            }

            // Give it 1.5 seconds to initialize
            await Task.Delay(1500);

            return (true, $"Web Server started successfully on port {cleanPort} (PID: {proc.Id}).");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to start server: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> StopServerAsync(string? exePath = null, string? port = null)
    {
        try
        {
            var resolvedExe = string.IsNullOrWhiteSpace(exePath) ? AutoDetectExePath() : exePath.Trim();
            var exeDir = !string.IsNullOrWhiteSpace(resolvedExe) && File.Exists(resolvedExe)
                ? Path.GetDirectoryName(resolvedExe)
                : string.Empty;

            // 1. Write .stop_server flag to immediately break any watchdog batch loops
            if (!string.IsNullOrWhiteSpace(exeDir) && Directory.Exists(exeDir))
            {
                try { File.WriteAllText(Path.Combine(exeDir, ".stop_server"), "stop"); } catch { }
            }
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, ".stop_server"), "stop"); } catch { }

            // 2. If scheduled task is running, end it first with both standard and elevated execution
            if (IsTaskInstalled())
            {
                RunCommandHidden("schtasks.exe", $"/End /TN \"{TaskName}\"");
                RunCommandHidden("schtasks.exe", $"/End /TN \"{TaskName}\"", runAsAdmin: true);
            }

            // 3. Kill watchdog cmd windows by title
            RunCommandHidden("taskkill.exe", "/F /FI \"WINDOWTITLE eq ConceptJewelleryPro*Watchdog*\"");
            RunCommandHidden("taskkill.exe", "/F /FI \"WINDOWTITLE eq ConceptJewellery*\"");

            // 4. Terminate any watchdog scripts or API processes via PowerShell/CIM
            var killScript = "Get-CimInstance Win32_Process | Where-Object { " +
                "($_.Name -match '^(cmd|wscript|cscript|dotnet|ConceptJewelleryPro\\.API)\\.exe$') -and " +
                "($_.CommandLine -match 'ConceptJewellery_Watchdog|ConceptJewellery_SilentWatchdog|ConceptJewellery_WebServer|ConceptJewelleryPro\\.API') -and " +
                "($_.ProcessId -ne $PID) " +
                "} | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }";
            RunCommandHidden("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{killScript}\"");

            // 5. Terminate all ConceptJewelleryPro.API processes via taskkill (tree kill)
            RunCommandHidden("taskkill.exe", $"/F /T /IM {ProcessName}.exe");

            // 6. Direct .NET Process API kill
            var processes = Process.GetProcessesByName(ProcessName);
            foreach (var p in processes)
            {
                try { p.Kill(true); } catch { }
            }

            // 7. Terminate any orphan process holding the web server port
            var cleanPort = string.IsNullOrWhiteSpace(port) ? "5080" : port.Trim();
            if (int.TryParse(cleanPort, out _))
            {
                var portKillScript = $"Get-NetTCPConnection -LocalPort {cleanPort} -ErrorAction SilentlyContinue | " +
                    "Select-Object -ExpandProperty OwningProcess -Unique | " +
                    $"Where-Object {{ $_ -ne $PID }} | " +
                    "ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }";
                RunCommandHidden("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{portKillScript}\"");
            }

            await Task.Delay(500);

            // 8. If any ConceptJewelleryPro.API process remains (elevated session), trigger elevated taskkill
            if (Process.GetProcessesByName(ProcessName).Length > 0)
            {
                RunCommandHidden("taskkill.exe", $"/F /T /IM {ProcessName}.exe", runAsAdmin: true);
                await Task.Delay(500);
            }

            var stillRunning = Process.GetProcessesByName(ProcessName).Length > 0;
            if (stillRunning)
            {
                return (false, "Web Server process could not be terminated. Please run the WPF app as Administrator to kill elevated background tasks.");
            }

            return (true, "Web Server and background tasks have been completely stopped.");
        }
        catch (Exception ex)
        {
            return (false, $"Error stopping server: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> InstallAutoStartServiceAsync(string exePath, string host, string port)
    {
        var resolvedExe = string.IsNullOrWhiteSpace(exePath) ? AutoDetectExePath() : exePath.Trim();
        if (string.IsNullOrWhiteSpace(resolvedExe) || !File.Exists(resolvedExe))
        {
            return (false, "Executable ConceptJewelleryPro.API.exe not found. Please locate it before installing background service.");
        }

        var cleanPort = string.IsNullOrWhiteSpace(port) ? "5080" : port.Trim();
        var cleanHost = string.IsNullOrWhiteSpace(host) ? "0.0.0.0" : host.Trim();
        var exeDir = Path.GetDirectoryName(resolvedExe) ?? AppContext.BaseDirectory;

        // Clear any previous stop signal
        TryDeleteFile(Path.Combine(exeDir, ".stop_server"));
        TryDeleteFile(Path.Combine(AppContext.BaseDirectory, ".stop_server"));

        // 1. Create robust Watchdog runner script with clean exit condition in the server directory
        var watchdogBat = Path.Combine(exeDir, "ConceptJewellery_Watchdog.bat");
        var silentVbs = Path.Combine(exeDir, "ConceptJewellery_SilentWatchdog.vbs");

        var batContent = new StringBuilder();
        batContent.AppendLine("@echo off");
        batContent.AppendLine("title ConceptJewelleryPro_Watchdog");
        batContent.AppendLine(":watchdog_loop");
        batContent.AppendLine("if exist \"%~dp0.stop_server\" exit /b 0");
        batContent.AppendLine($"\"{resolvedExe}\" --urls \"http://{cleanHost}:{cleanPort}\"");
        batContent.AppendLine("if exist \"%~dp0.stop_server\" exit /b 0");
        batContent.AppendLine("timeout /t 2 /nobreak >nul");
        batContent.AppendLine("if exist \"%~dp0.stop_server\" exit /b 0");
        batContent.AppendLine("goto watchdog_loop");
        File.WriteAllText(watchdogBat, batContent.ToString(), Encoding.ASCII);

        var vbsContent = $"Set WshShell = CreateObject(\"WScript.Shell\")\r\nWshShell.Run \"cmd.exe /c \"\"{watchdogBat}\"\"\", 0, False\r\n";
        File.WriteAllText(silentVbs, vbsContent, Encoding.ASCII);

        // 2. Register Windows Task Scheduler Task with auto-restart on failure
        var xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>ConceptJewelleryPro Always-On Web App and API Server</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>999</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{resolvedExe}</Command>
      <Arguments>--urls &quot;http://{cleanHost}:{cleanPort}&quot;</Arguments>
      <WorkingDirectory>{exeDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

        var tempXml = Path.Combine(Path.GetTempPath(), $"{TaskName}_Task.xml");
        File.WriteAllText(tempXml, xmlContent, Encoding.Unicode);

        bool taskRegistered = false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Create /TN \"{TaskName}\" /XML \"{tempXml}\" /F",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var proc = Process.Start(psi);
            if (proc is not null)
            {
                await proc.WaitForExitAsync();
                taskRegistered = proc.ExitCode == 0;
            }
        }
        catch
        {
            taskRegistered = false;
        }
        finally
        {
            try { File.Delete(tempXml); } catch { }
        }

        // 3. Setup Startup folder fallback shortcut as secondary/dual protection
        CreateStartupShortcut(silentVbs);

        // 4. Start the service immediately
        if (taskRegistered)
        {
            RunCommandHidden("schtasks.exe", $"/Run /TN \"{TaskName}\"");
        }
        else
        {
            await StartServerAsync(resolvedExe, cleanHost, cleanPort);
        }

        await Task.Delay(1000);

        var msg = taskRegistered
            ? "Background service registered in Windows Task Scheduler! It will start automatically when Windows opens and auto-restart within 1 minute if it crashes."
            : "Background watchdog registered in Windows Startup! It will start automatically when Windows opens and continuously restart if it crashes.";

        return (true, msg);
    }

    public async Task<(bool Success, string Message)> RemoveAutoStartServiceAsync(string? exePath = null, string? port = null)
    {
        try
        {
            var resolvedExe = string.IsNullOrWhiteSpace(exePath) ? AutoDetectExePath() : exePath.Trim();
            var exeDir = !string.IsNullOrWhiteSpace(resolvedExe) && File.Exists(resolvedExe)
                ? Path.GetDirectoryName(resolvedExe)
                : string.Empty;

            // 1. Signal watchdog script to exit immediately
            if (!string.IsNullOrWhiteSpace(exeDir) && Directory.Exists(exeDir))
            {
                try { File.WriteAllText(Path.Combine(exeDir, ".stop_server"), "stop"); } catch { }
            }
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, ".stop_server"), "stop"); } catch { }

            // 2. If Task Scheduler task exists, END running instance FIRST, then DELETE registration
            if (IsTaskInstalled())
            {
                RunCommandHidden("schtasks.exe", $"/End /TN \"{TaskName}\"");
                RunCommandHidden("schtasks.exe", $"/End /TN \"{TaskName}\"", runAsAdmin: true);

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{TaskName}\" /F",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var p = Process.Start(psi);
                    if (p is not null) await p.WaitForExitAsync();
                }
                catch { }

                RunCommandHidden("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F");
            }

            // 3. Remove Startup folder watchdog shortcuts
            RemoveStartupShortcut();

            // 4. Completely terminate server, watchdog cmd/vbs, and any processes holding the port
            await StopServerAsync(exePath, port);

            // 5. Clean up watchdog scripts and marker files from server publish folder so it cannot resurrect
            if (!string.IsNullOrWhiteSpace(exeDir) && Directory.Exists(exeDir))
            {
                TryDeleteFile(Path.Combine(exeDir, "ConceptJewellery_Watchdog.bat"));
                TryDeleteFile(Path.Combine(exeDir, "ConceptJewellery_SilentWatchdog.vbs"));
                TryDeleteFile(Path.Combine(exeDir, ".stop_server"));
            }

            var baseDir = AppContext.BaseDirectory;
            TryDeleteFile(Path.Combine(baseDir, "ConceptJewellery_Watchdog.bat"));
            TryDeleteFile(Path.Combine(baseDir, "ConceptJewellery_SilentWatchdog.vbs"));
            TryDeleteFile(Path.Combine(baseDir, ".stop_server"));

            // 6. Verify that executable is unlocked and not running
            await Task.Delay(500);
            var procs = Process.GetProcessesByName(ProcessName);
            if (procs.Length > 0)
            {
                RunCommandHidden("taskkill.exe", $"/F /T /IM {ProcessName}.exe", runAsAdmin: true);
                await Task.Delay(500);
            }

            return (true, "Background service and auto-start task successfully removed. All web server processes terminated, and publish files are unlocked for replacement.");
        }
        catch (Exception ex)
        {
            return (false, $"Error removing background service: {ex.Message}");
        }
    }

    public static bool IsTaskInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStartupShortcutInstalled()
    {
        try
        {
            var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var commonFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            return File.Exists(Path.Combine(userFolder, "ConceptJewellery_WebServer.vbs")) ||
                   File.Exists(Path.Combine(commonFolder, "ConceptJewellery_WebServer.vbs"));
        }
        catch
        {
            return false;
        }
    }

    private static void CreateStartupShortcut(string silentVbsPath)
    {
        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder) && File.Exists(silentVbsPath))
            {
                var targetVbs = Path.Combine(startupFolder, "ConceptJewellery_WebServer.vbs");
                File.Copy(silentVbsPath, targetVbs, overwrite: true);
            }
        }
        catch { }
    }

    private static void RemoveStartupShortcut()
    {
        var locations = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        };

        foreach (var dir in locations)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            TryDeleteFile(Path.Combine(dir, "ConceptJewellery_WebServer.vbs"));
            TryDeleteFile(Path.Combine(dir, "ConceptJewellery_WebServer.bat"));
            TryDeleteFile(Path.Combine(dir, "ConceptJewellery_WebServer.lnk"));
            TryDeleteFile(Path.Combine(dir, "ConceptJewellery_Watchdog.bat"));
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }
        catch { }
    }

    private static void RunCommandHidden(string fileName, string arguments, bool runAsAdmin = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = runAsAdmin,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (runAsAdmin)
            {
                psi.Verb = "runas";
            }
            using var proc = Process.Start(psi);
            proc?.WaitForExit(4000);
        }
        catch { }
    }
}
