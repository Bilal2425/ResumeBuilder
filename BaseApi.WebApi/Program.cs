using System.Configuration;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using BaseApi.Business;
using BaseApi.Data.Contexts;
using BaseApi.Model;
using BaseApi.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;



var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Adding CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://localhost:4200", "https://localhost:4200") //Angular URL
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials());

    options.AddPolicy("AllowAllOrigins",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod());
});

// Configure MongoDB settings from appsettings.json
builder.Services.Configure<MongoDbSettings>(
builder.Configuration.GetSection(nameof(MongoDbSettings)));

//// Configure PostgreSQL settings from appsettings.json
// builder.Services.Configure<PostgreSqlSettings>(
// builder.Configuration.GetSection(nameof(PostgreSqlSettings)));


// Register ApplicationDbContext (Dependency Injection)
builder.Services.AddSingleton<ApplicationDbContext>();

// Register PDF service
builder.Services.AddSingleton<PdfService>();

// Register ResumeParserService
builder.Services.AddSingleton<BaseApi.Business.ResumeParserService>();

// Register UserDbContext
var postgreSqlConnection = builder.Configuration.GetConnectionString("PostgreSqlConnection") 
                           ?? "Host=localhost;Database=UserDB;Username=postgres;Password=postgres";

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(postgreSqlConnection,
    b => b.MigrationsAssembly("BaseApi.Data")));

//register JwtSettings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                ?? jwtSettings["SecretKey"] 
                ?? "Default_Temporary_Secret_Key_For_Development_Only_123456";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters 
    {
       ValidateIssuer = true,
       ValidateAudience = true,
       ValidateLifetime = true,
       ValidateIssuerSigningKey = true,
       ValidIssuer = jwtSettings["Issuer"],
       ValidAudience = jwtSettings["Audience"],
       IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
       
    };
});

builder.Services.AddSwaggerGen(c =>
{
c.SwaggerDoc("v1", new OpenApiInfo { Title = "Base API", Version = "v1" });

    c.DocInclusionPredicate((docName, ApiDescription) =>
    {
        return !ApiDescription.RelativePath.Contains("configuration") &&
               !ApiDescription.RelativePath.Contains("outputcache");
    });

// Enabling JWT Bearer token in Swagger
c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your token in the text input below.\nExample: \"Bearer yourToken\""

    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] { }
        }
    });
});

var app = builder.Build();

// Global Exception Handler Middleware
app.UseMiddleware<BaseApi.WebApi.Middleware.ExceptionHandlingMiddleware>();

// Automatically run EF Core PostgreSQL migrations in Production/Docker environments
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<UserDbContext>();
        if (context.Database.IsRelational())
        {
            logger.LogInformation("Applying PostgreSQL database migrations...");
            // Retry mechanism for database readiness in docker compose
            int retries = 5;
            while (retries > 0)
            {
                try
                {
                    context.Database.Migrate();
                    logger.LogInformation("Database migrations applied successfully.");
                    break;
                }
                catch (Exception ex)
                {
                    retries--;
                    logger.LogWarning($"PostgreSQL database not ready yet. Retrying in 3 seconds... ({retries} retries left). Error: {ex.Message}");
                    System.Threading.Thread.Sleep(3000);
                    if (retries == 0) throw;
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations.");
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Base API V1");
    c.RoutePrefix = string.Empty;
});

// Use CORS
app.UseCors("AllowSpecificOrigin");

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
