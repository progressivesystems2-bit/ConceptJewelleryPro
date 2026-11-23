using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.WPF.Services;

public sealed class UpdateCheckResult
{
    public bool Success { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string ReleaseName { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }
    public string ReleasePageUrl { get; init; } = string.Empty;
    public string? DownloadUrl { get; init; }
    public string? AssetName { get; init; }
    public long AssetSizeBytes { get; init; }
    public string? ChecksumUrl { get; init; }
    public bool IsPreRelease { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record UpdateDownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double Percentage,
    string StatusText);

public sealed class SoftwareUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static string UserSettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ConceptJewelleryPro",
            "update_settings.json");

    private static string UpdateTempRoot =>
        Path.Combine(Path.GetTempPath(), "ConceptJewelleryPro_Updates");

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ConceptJewelleryPro", AppSettings.GetInstalledVersion()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public string GetCurrentVersion() => AppSettings.GetInstalledVersion();

    public string GetInstallDirectory() => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public UpdateSettings LoadSettings()
    {
        try
        {
            if (File.Exists(UserSettingsFilePath))
            {
                var json = File.ReadAllText(UserSettingsFilePath);
                var saved = JsonSerializer.Deserialize<UpdateSettings>(json, JsonOptions);
                if (saved is not null)
                {
                    if (string.IsNullOrWhiteSpace(saved.GitHubOwner))
                        saved.GitHubOwner = "progressivesystems2-bit";
                    if (string.IsNullOrWhiteSpace(saved.GitHubRepo))
                        saved.GitHubRepo = "ConceptJewelleryPro";
                    if (string.IsNullOrWhiteSpace(saved.AssetNamePattern))
                        saved.AssetNamePattern = "ConceptJewelleryPro-WPF";
                    return saved;
                }
            }
        }
        catch
        {
            // Fall through to appsettings.json
        }

        return AppSettings.Load().Updates ?? new UpdateSettings();
    }

    public void SaveSettings(UpdateSettings settings)
    {
        var dir = Path.GetDirectoryName(UserSettingsFilePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(UserSettingsFilePath, json, Encoding.UTF8);
    }

    public string GetReleasesPageUrl(UpdateSettings? settings = null)
    {
        var cfg = settings ?? LoadSettings();
        return $"https://github.com/{cfg.GitHubOwner.Trim()}/{cfg.GitHubRepo.Trim()}/releases";
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        UpdateSettings? overrideSettings = null,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        var settings = overrideSettings ?? LoadSettings();

        if (string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = currentVersion,
                Message = "GitHub repository owner and name must be configured in Update Settings."
            };
        }

        var owner = settings.GitHubOwner.Trim();
        var repo = settings.GitHubRepo.Trim();
        var endpoint = settings.IncludePreReleases
            ? $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=10"
            : $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            ApplyAuthHeader(request, settings);

            using var response = await HttpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = currentVersion,
                    ReleasePageUrl = GetReleasesPageUrl(settings),
                    Message = $"No published releases found yet on {owner}/{repo}. You are running v{currentVersion}."
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    ReleasePageUrl = GetReleasesPageUrl(settings),
                    Message = "GitHub API access denied or rate-limited. If the repository is private, add a GitHub Personal Access Token in Update Settings."
                };
            }

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            GitHubReleaseDto? release;

            if (settings.IncludePreReleases)
            {
                var releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(body, JsonOptions);
                release = releases?.FirstOrDefault(r => !r.Draft);
            }
            else
            {
                release = JsonSerializer.Deserialize<GitHubReleaseDto>(body, JsonOptions);
            }

            if (release is null)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = currentVersion,
                    ReleasePageUrl = GetReleasesPageUrl(settings),
                    Message = $"No published releases found yet on {owner}/{repo}."
                };
            }

            var latestVersion = NormalizeVersionString(release.TagName);
            var isNewer = IsNewerVersion(latestVersion, currentVersion);

            var zipAsset = SelectPackageAsset(release.Assets, settings.AssetNamePattern);
            var checksumAsset = SelectChecksumAsset(release.Assets, zipAsset?.Name);

            var message = isNewer
                ? (zipAsset is not null
                    ? $"Update v{latestVersion} is available ({FormatBytes(zipAsset.Size)})."
                    : $"Release v{latestVersion} is available on GitHub, but no .zip update package asset was attached.")
                : $"Concept Jewellery Pro is up to date (v{currentVersion}).";

            return new UpdateCheckResult
            {
                Success = true,
                IsUpdateAvailable = isNewer,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseName = string.IsNullOrWhiteSpace(release.Name) ? $"Version {latestVersion}" : release.Name,
                ReleaseNotes = string.IsNullOrWhiteSpace(release.Body) ? "No release notes provided." : release.Body,
                PublishedAt = release.PublishedAt,
                ReleasePageUrl = string.IsNullOrWhiteSpace(release.HtmlUrl) ? GetReleasesPageUrl(settings) : release.HtmlUrl,
                DownloadUrl = zipAsset?.BrowserDownloadUrl,
                AssetName = zipAsset?.Name,
                AssetSizeBytes = zipAsset?.Size ?? 0,
                ChecksumUrl = checksumAsset?.BrowserDownloadUrl,
                IsPreRelease = release.Prerelease,
                Message = message
            };
        }
        catch (OperationCanceledException)
        {
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = currentVersion,
                Message = "Update check was cancelled."
            };
        }
        catch (HttpRequestException ex)
        {
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = currentVersion,
                ReleasePageUrl = GetReleasesPageUrl(settings),
                Message = $"Unable to reach GitHub update server: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = currentVersion,
                Message = $"Update check failed: {ex.Message}"
            };
        }
    }

    public async Task<string> DownloadUpdateAsync(
        UpdateCheckResult release,
        UpdateSettings? overrideSettings = null,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(release.DownloadUrl))
        {
            throw new InvalidOperationException("No downloadable update package URL was found in this release.");
        }

        var settings = overrideSettings ?? LoadSettings();
        var safeVersion = SanitizePathSegment(string.IsNullOrWhiteSpace(release.LatestVersion) ? "latest" : release.LatestVersion);
        var downloadDir = Path.Combine(UpdateTempRoot, safeVersion);
        Directory.CreateDirectory(downloadDir);

        var fileName = string.IsNullOrWhiteSpace(release.AssetName)
            ? $"ConceptJewelleryPro-v{safeVersion}.zip"
            : Path.GetFileName(release.AssetName);
        var destinationPath = Path.Combine(downloadDir, fileName);

        progress?.Report(new UpdateDownloadProgress(0, release.AssetSizeBytes > 0 ? release.AssetSizeBytes : null, 0, "Connecting to download server..."));

        using var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUrl);
        ApplyAuthHeader(request, settings);

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? (release.AssetSizeBytes > 0 ? release.AssetSizeBytes : null);

        await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            var lastReport = DateTime.UtcNow;

            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 100 || (totalBytes.HasValue && totalRead >= totalBytes.Value))
                {
                    lastReport = now;
                    var pct = totalBytes.HasValue && totalBytes.Value > 0
                        ? Math.Min(100.0, (double)totalRead / totalBytes.Value * 100.0)
                        : 0.0;
                    var status = totalBytes.HasValue
                        ? $"Downloading {FormatBytes(totalRead)} / {FormatBytes(totalBytes.Value)} ({pct:F1}%)"
                        : $"Downloading {FormatBytes(totalRead)}...";
                    progress?.Report(new UpdateDownloadProgress(totalRead, totalBytes, pct, status));
                }
            }
        }

        // Verify SHA-256 checksum if a .sha256 asset was published with the release
        if (!string.IsNullOrWhiteSpace(release.ChecksumUrl))
        {
            progress?.Report(new UpdateDownloadProgress(release.AssetSizeBytes, release.AssetSizeBytes, 100, "Verifying SHA-256 package integrity..."));
            await VerifyChecksumAsync(destinationPath, release.ChecksumUrl, settings, cancellationToken);
        }

        progress?.Report(new UpdateDownloadProgress(release.AssetSizeBytes, release.AssetSizeBytes, 100, "Download complete and verified."));
        return destinationPath;
    }

    public void ApplyUpdateAndRestart(string zipFilePath, string targetVersion = "")
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("Update package file not found.", zipFilePath);
        }

        var installDir = GetInstallDirectory();
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            exePath = Path.Combine(installDir, "ConceptJewelleryPro.WPF.exe");
        }

        var safeVersion = SanitizePathSegment(string.IsNullOrWhiteSpace(targetVersion) ? DateTime.Now.ToString("yyyyMMddHHmmss") : targetVersion);
        var stagingDir = Path.Combine(UpdateTempRoot, $"staging_{safeVersion}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);

        // Extract and validate the ZIP package before shutting down the running app
        ZipFile.ExtractToDirectory(zipFilePath, stagingDir, overwriteFiles: true);

        // If the zip contains a single top-level directory, unwrap it
        var sourceContentDir = UnwrapSingleRootFolder(stagingDir);
        var hasExeOrDll = Directory.EnumerateFiles(sourceContentDir, "*.exe", SearchOption.TopDirectoryOnly).Any()
                          || Directory.EnumerateFiles(sourceContentDir, "*.dll", SearchOption.TopDirectoryOnly).Any();
        if (!hasExeOrDll)
        {
            throw new InvalidOperationException("The selected ZIP package does not appear to contain Concept Jewellery Pro application binaries.");
        }

        var rollbackDir = Path.Combine(UpdateTempRoot, $"rollback_{SanitizePathSegment(GetCurrentVersion())}_{DateTime.Now:yyyyMMddHHmmss}");
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConceptJewelleryPro");
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, "last_update.log");

        var scriptPath = Path.Combine(UpdateTempRoot, $"apply_update_{Guid.NewGuid():N}.ps1");
        var pid = Environment.ProcessId;

        var scriptContent = BuildUpdaterPowerShellScript(
            pid: pid,
            sourceDir: sourceContentDir,
            targetDir: installDir,
            rollbackDir: rollbackDir,
            exePath: exePath,
            stagingRoot: stagingDir,
            logFile: logFile);

        File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(psi);
    }

    private static string BuildUpdaterPowerShellScript(
        int pid,
        string sourceDir,
        string targetDir,
        string rollbackDir,
        string exePath,
        string stagingRoot,
        string logFile)
    {
        static string Esc(string s) => s.Replace("'", "''");

        return $@"
$ErrorActionPreference = 'Stop'
$logFile = '{Esc(logFile)}'
function Write-Log($msg) {{
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -Path $logFile -Value ""[$ts] $msg""
}}

try {{
    Write-Log 'Starting Concept Jewellery Pro update...'
    Write-Log 'Waiting for PID {pid} to exit...'
    $waitCount = 0
    while ((Get-Process -Id {pid} -ErrorAction SilentlyContinue) -and $waitCount -lt 60) {{
        Start-Sleep -Milliseconds 500
        $waitCount++
    }}
    Start-Sleep -Milliseconds 600

    $sourceDir = '{Esc(sourceDir)}'
    $targetDir = '{Esc(targetDir)}'
    $rollbackDir = '{Esc(rollbackDir)}'
    $exePath = '{Esc(exePath)}'
    $stagingRoot = '{Esc(stagingRoot)}'

    # Create rollback backup of existing binaries (excluding Backups folder and DB files)
    New-Item -ItemType Directory -Path $rollbackDir -Force | Out-Null
    Write-Log ""Creating rollback backup at $rollbackDir""
    Get-ChildItem -Path $targetDir -Force | Where-Object {{
        $_.Name -notin @('Backups', 'logs') -and $_.Extension -notin @('.mdb', '.accdb', '.ldb', '.db', '.log')
    }} | ForEach-Object {{
        try {{
            Copy-Item -Path $_.FullName -Destination (Join-Path $rollbackDir $_.Name) -Recurse -Force -ErrorAction SilentlyContinue
        }} catch {{ }}
    }}

    # Preserve local user configuration and database files
    $preservedFileNames = @('appsettings.json')
    $preservedExtensions = @('.mdb', '.accdb', '.ldb', '.db', '.log')

    Write-Log ""Copying updated files from $sourceDir to $targetDir""
    $sourceFiles = Get-ChildItem -Path $sourceDir -Recurse -File
    foreach ($file in $sourceFiles) {{
        $relPath = $file.FullName.Substring($sourceDir.Length).TrimStart('\', '/')
        $destPath = Join-Path $targetDir $relPath

        if ($preservedFileNames -contains $file.Name.ToLowerInvariant() -and (Test-Path $destPath)) {{
            Write-Log ""Preserving existing config: $relPath""
            continue
        }}
        if ($preservedExtensions -contains $file.Extension.ToLowerInvariant() -and (Test-Path $destPath)) {{
            Write-Log ""Preserving existing data file: $relPath""
            continue
        }}

        $destParent = Split-Path -Path $destPath -Parent
        if (-not (Test-Path $destParent)) {{
            New-Item -ItemType Directory -Path $destParent -Force | Out-Null
        }}
        Copy-Item -Path $file.FullName -Destination $destPath -Force
    }}

    Write-Log 'Update applied successfully. Relaunching application...'
    if (Test-Path $exePath) {{
        Start-Process -FilePath $exePath -WorkingDirectory $targetDir
    }}

    try {{
        Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }} catch {{ }}

    Write-Log 'Done.'
}}
catch {{
    Write-Log ""UPDATE FAILED: $($_.Exception.Message)""
    try {{
        Start-Process -FilePath '{Esc(exePath)}' -WorkingDirectory '{Esc(targetDir)}'
    }} catch {{ }}
    exit 1
}}
";
    }

    private static async Task VerifyChecksumAsync(
        string filePath,
        string checksumUrl,
        UpdateSettings settings,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, checksumUrl);
        ApplyAuthHeader(request, settings);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var expectedText = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        var expectedHash = expectedText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var actualBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        var actualHash = Convert.ToHexString(actualBytes);

        if (!string.Equals(actualHash, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidOperationException(
                $"Downloaded update failed SHA-256 integrity verification.\nExpected: {expectedHash}\nActual: {actualHash}");
        }
    }

    private static string UnwrapSingleRootFolder(string dir)
    {
        var files = Directory.GetFiles(dir);
        var subDirs = Directory.GetDirectories(dir);
        if (files.Length == 0 && subDirs.Length == 1)
        {
            return subDirs[0];
        }
        return dir;
    }

    private static void ApplyAuthHeader(HttpRequestMessage request, UpdateSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.GitHubToken.Trim());
        }
    }

    private static GitHubAssetDto? SelectPackageAsset(List<GitHubAssetDto>? assets, string? pattern)
    {
        if (assets is null || assets.Count == 0) return null;

        var zipAssets = assets
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (zipAssets.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(pattern))
        {
            var matched = zipAssets.FirstOrDefault(a =>
                a.Name.Contains(pattern.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched is not null) return matched;
        }

        return zipAssets[0];
    }

    private static GitHubAssetDto? SelectChecksumAsset(List<GitHubAssetDto>? assets, string? zipAssetName)
    {
        if (assets is null || assets.Count == 0 || string.IsNullOrWhiteSpace(zipAssetName)) return null;

        return assets.FirstOrDefault(a =>
            string.Equals(a.Name, zipAssetName + ".sha256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Name, Path.GetFileNameWithoutExtension(zipAssetName) + ".sha256", StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeVersionString(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "0.0.0";
        var cleaned = tag.Trim();
        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[1..];
        }
        return cleaned;
    }

    public static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        var latestCore = StripPreRelease(latestVersion, out var latestPre);
        var currentCore = StripPreRelease(currentVersion, out var currentPre);

        if (Version.TryParse(PadVersion(latestCore), out var latestVer) &&
            Version.TryParse(PadVersion(currentCore), out var currentVer))
        {
            var cmp = latestVer.CompareTo(currentVer);
            if (cmp != 0) return cmp > 0;

            // Same numeric version: stable release > pre-release
            if (string.IsNullOrEmpty(latestPre) && !string.IsNullOrEmpty(currentPre)) return true;
            if (!string.IsNullOrEmpty(latestPre) && string.IsNullOrEmpty(currentPre)) return false;
            return string.Compare(latestPre, currentPre, StringComparison.OrdinalIgnoreCase) > 0;
        }

        return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripPreRelease(string version, out string preRelease)
    {
        var dashIdx = version.IndexOf('-');
        if (dashIdx >= 0)
        {
            preRelease = version[(dashIdx + 1)..];
            return version[..dashIdx];
        }
        preRelease = string.Empty;
        return version;
    }

    private static string PadVersion(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0",
            2 => $"{parts[0]}.{parts[1]}.0",
            _ => v
        };
    }

    private static string SanitizePathSegment(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(input.Select(c => invalid.Contains(c) ? '_' : c));
    }

    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto> Assets { get; set; } = new();
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
