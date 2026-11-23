using System.Text;
using ConceptJewelleryPro.API.Services;
using ConceptJewelleryPro.Application.Authentication;
using ConceptJewelleryPro.Application.Security;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Infrastructure.Accounts;
using ConceptJewelleryPro.Infrastructure.Authentication;
using ConceptJewelleryPro.Infrastructure.Security;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database path ────────────────────────────────────────────────────────
var databasePath = DatabasePathResolver.ResolveDatabasePath(builder.Configuration);

// ── Dependency Injection ─────────────────────────────────────────────────
var passwordHasher = new PasswordHasher();
builder.Services.AddSingleton<IPasswordHasher>(passwordHasher);

builder.Services.AddSingleton<IUserRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessUserRepository(dbPath, passwordHasher);
});

builder.Services.AddSingleton<ISettingsRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessSettingsRepository(dbPath);
});

builder.Services.AddScoped<IAuthenticationService, LoginService>();

builder.Services.AddSingleton<IInvoiceParsingService, AzureInvoiceParsingService>();

builder.Services.AddSingleton<LocalPrinterSettingsStore>();
builder.Services.AddSingleton<IBarcodePrintService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new BarcodePrintService(
        sp.GetRequiredService<LocalPrinterSettingsStore>(),
        sp.GetRequiredService<ISettingsRepository>(),
        () => config["SoftwarePath"]);
});

// ── Mobile App Repositories ──────────────────────────────────────────────
builder.Services.AddSingleton<IBarcodeCreationRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessBarcodeCreationRepository(dbPath);
});

builder.Services.AddSingleton<IPurityRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessPurityRepository(dbPath);
});

builder.Services.AddSingleton<ICatalogueRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessCatalogueRepository(dbPath);
});

builder.Services.AddSingleton<ICustomerRequirementRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessCustomerRequirementRepository(dbPath);
});

builder.Services.AddSingleton<ICustomerRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessCustomerRepository(dbPath);
});

builder.Services.AddSingleton<IOrderRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessOrderRepository(dbPath);
});

builder.Services.AddSingleton<IWhatsAppBillTrackerRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessWhatsAppBillTrackerRepository(dbPath);
});

builder.Services.AddSingleton<IBillRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessBillRepository(dbPath);
});

builder.Services.AddSingleton<ISalesVouTypeRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var dbPath = DatabasePathResolver.ResolveDatabasePath(config);
    return new AccessSalesVouTypeRepository(dbPath);
});

// ── JWT Authentication ───────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key must be configured in appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ConceptJewelleryPro.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ConceptJewelleryPro.Clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── Controllers & Swagger ────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConceptJewelleryPro API",
        Version = "v1",
        Description = "Online Cloud API for ConceptJewelleryPro."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGciOiJIUzI1NiIs..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build & Configure Pipeline ───────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("v1/swagger.json", "ConceptJewelleryPro API v1");
    options.RoutePrefix = "swagger";
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
