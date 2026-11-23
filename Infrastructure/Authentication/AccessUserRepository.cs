using System.Data.OleDb;
using ConceptJewelleryPro.Application.Security;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Accounts;

namespace ConceptJewelleryPro.Infrastructure.Authentication;

public sealed class AccessUserRepository : IUserRepository
{
    private readonly Func<string?> _databasePathProvider;
    private readonly DemoUserRepository _demoUserRepository;

    public AccessUserRepository(string databasePath, IPasswordHasher passwordHasher)
    {
        _databasePathProvider = () => databasePath;
        _demoUserRepository = new DemoUserRepository(passwordHasher);
    }

    public AccessUserRepository(Func<string?> databasePathProvider, IPasswordHasher passwordHasher)
    {
        _databasePathProvider = databasePathProvider;
        _demoUserRepository = new DemoUserRepository(passwordHasher);
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var dbPath = _databasePathProvider();
        if (!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
        {
            try
            {
                var connectionString = AccessConnectionProvider.GetConnectionString(dbPath);
                const string sql = "SELECT [User], [Password], [Toolbar], [UserAdd] FROM [User]";

                await using var connection = new OleDbConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new OleDbCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var dbUser = Convert.ToString(reader["User"])?.Trim() ?? string.Empty;
                    if (string.Equals(dbUser, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        var password = Convert.ToString(reader["Password"])?.Trim() ?? string.Empty;
                        var userAdd = reader["UserAdd"] is not DBNull && Convert.ToBoolean(reader["UserAdd"]);

                        return new User
                        {
                            Id = Guid.NewGuid(),
                            Username = dbUser,
                            DisplayName = dbUser,
                            PasswordHash = password,
                            PasswordSalt = string.Empty,
                            Role = (userAdd || string.Equals(dbUser, "admin", StringComparison.OrdinalIgnoreCase)) ? UserRole.Administrator : UserRole.Operator,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }
            }
            catch
            {
                // Fall back to demo user repository if DB read fails
            }
        }

        return await _demoUserRepository.FindByUsernameAsync(normalized, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _demoUserRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return _demoUserRepository.AddAsync(user, cancellationToken);
    }
}
