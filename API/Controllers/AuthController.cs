using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ConceptJewelleryPro.API.Models;
using ConceptJewelleryPro.Application.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ConceptJewelleryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate a user and receive a JWT token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var result = await _authService.AuthenticateAsync(request.Username, request.Password, cancellationToken);

        if (!result.Succeeded || result.User is null)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new { message = result.ErrorMessage ?? "Invalid username or password." });
        }

        var token = GenerateJwtToken(result.User.Username, result.User.Role.ToString());
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "480");

        _logger.LogInformation("User {Username} authenticated successfully", result.User.Username);

        return Ok(new LoginResponse
        {
            Token = token,
            Username = result.User.Username,
            Role = result.User.Role.ToString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        });
    }

    private string GenerateJwtToken(string username, string role)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "ConceptJewelleryPro.API";
        var audience = _configuration["Jwt:Audience"] ?? "ConceptJewelleryPro.Clients";
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "480");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
