using ConceptJewelleryPro.Application.Security;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Authentication;

public sealed class DemoUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public DemoUserRepository(IPasswordHasher passwordHasher)
    {
        var adminSalt = string.Empty;
        var adminHash = passwordHasher.HashPassword("admin123", out adminSalt);

        _users = new List<User>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Username = "admin",
                DisplayName = "System Administrator",
                PasswordHash = adminHash,
                PasswordSalt = adminSalt,
                Role = UserRole.Administrator,
                IsActive = true
            }
        };
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(user);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }
}
