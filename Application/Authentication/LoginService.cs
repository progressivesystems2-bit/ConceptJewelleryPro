using ConceptJewelleryPro.Application.Security;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.Application.Authentication;

public sealed class LoginService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = Guard.NotNull(userRepository, nameof(userRepository));
        _passwordHasher = Guard.NotNull(passwordHasher, nameof(passwordHasher));
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username?.Trim() ?? string.Empty;
        var cleanPassword = password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrEmpty(cleanPassword))
        {
            return AuthenticationResult.Failure("Please enter your username and password.");
        }

        var user = await _userRepository.FindByUsernameAsync(normalizedUsername, cancellationToken);
        if (user is null)
        {
            return AuthenticationResult.Failure("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return AuthenticationResult.Failure("This account has been disabled. Contact your administrator.");
        }

        var isValid = _passwordHasher.VerifyPassword(cleanPassword, user.PasswordSalt, user.PasswordHash);
        if (!isValid)
        {
            // Case-insensitive password check for plain-text DB passwords and case-insensitive passwords
            isValid = _passwordHasher.VerifyPassword(cleanPassword.ToLowerInvariant(), user.PasswordSalt, user.PasswordHash) ||
                      _passwordHasher.VerifyPassword(cleanPassword.ToUpperInvariant(), user.PasswordSalt, user.PasswordHash) ||
                      string.Equals(cleanPassword, user.PasswordHash?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (!isValid)
        {
            return AuthenticationResult.Failure("Invalid username or password.");
        }

        return AuthenticationResult.Success(user);
    }
}
