using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Application.Authentication;

public sealed class AuthenticationResult
{
    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public User? User { get; }

    private AuthenticationResult(bool succeeded, string? errorMessage, User? user)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        User = user;
    }

    public static AuthenticationResult Success(User user) => new(true, null, user);

    public static AuthenticationResult Failure(string errorMessage) => new(false, errorMessage, null);
}
