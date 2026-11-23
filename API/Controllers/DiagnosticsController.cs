using System.Data.OleDb;
using ConceptJewelleryPro.API.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class DiagnosticsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ISettingsRepository _settingsRepository;

    public DiagnosticsController(IConfiguration configuration, ISettingsRepository settingsRepository)
    {
        _configuration = configuration;
        _settingsRepository = settingsRepository;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var configuredPath = _configuration["DatabasePath"];
        var baseDir = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        // Search for all MDB files in application scope
        var discoveredMdbs = new List<string>();
        try
        {
            discoveredMdbs.AddRange(Directory.GetFiles(baseDir, "*.mdb", SearchOption.AllDirectories));
        }
        catch { }

        try
        {
            if (currentDir != baseDir)
            {
                discoveredMdbs.AddRange(Directory.GetFiles(currentDir, "*.mdb", SearchOption.AllDirectories));
            }
        }
        catch { }

        discoveredMdbs = discoveredMdbs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Check active database path
        var activeDb = DatabasePathResolver.ResolveDatabasePath(_configuration);
        var dbExists = !string.IsNullOrWhiteSpace(activeDb) && System.IO.File.Exists(activeDb);

        // Test database connection across multiple providers (ACE 16, ACE 12, Jet 4.0)
        string? dbConnectionError = null;
        bool dbConnectionSuccess = false;
        string? activeWorkingProvider = null;
        int userCount = 0;

        if (dbExists)
        {
            var (testSuccess, workingProvider, testError) = AccessConnectionProvider.TestConnection(activeDb);
            if (testSuccess && workingProvider != null)
            {
                activeWorkingProvider = workingProvider;
                var connStr = $"Provider={workingProvider};Data Source={activeDb};Persist Security Info=False;";
                try
                {
                    await using var conn = new OleDbConnection(connStr);
                    await conn.OpenAsync(cancellationToken);
                    await using var cmd = new OleDbCommand("SELECT COUNT(*) FROM [USER]", conn);
                    var countObj = await cmd.ExecuteScalarAsync(cancellationToken);
                    userCount = Convert.ToInt32(countObj);
                    dbConnectionSuccess = true;
                }
                catch (Exception ex)
                {
                    dbConnectionError = ex.Message;
                }
            }
            else
            {
                dbConnectionError = testError;
            }
        }

        string? savePathSetting = null;
        try
        {
            savePathSetting = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey, cancellationToken);
        }
        catch { }

        return Ok(new
        {
            status = "Online",
            serverTime = DateTime.UtcNow.ToString("o"),
            configuredPath,
            activeResolvedDatabasePath = activeDb,
            databaseFileExists = dbExists,
            discoveredMdbFiles = discoveredMdbs,
            activeWorkingProvider,
            candidateProvidersTested = AccessConnectionProvider.CandidateProviders,
            dbConnectionSuccess,
            dbConnectionError,
            userRecordsInDb = userCount,
            savePathSetting,
            baseDirectory = baseDir,
            currentDirectory = currentDir
        });
    }
}
