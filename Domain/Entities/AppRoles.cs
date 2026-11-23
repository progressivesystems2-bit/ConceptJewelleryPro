namespace ConceptJewelleryPro.Domain.Entities;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Operator = "Operator";

    public static string ToName(UserRole role) => role switch
    {
        UserRole.Administrator => Administrator,
        UserRole.Manager => Manager,
        UserRole.Operator => Operator,
        _ => Operator
    };
}
