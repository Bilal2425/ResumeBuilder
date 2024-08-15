using System.Configuration;
using System.Reflection;
using BaseApi.Data.Contexts;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

//Adding CORS service

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://localhost:4200") //Angular URL
                          .AllowAnyHeader()
                          .AllowAnyMethod());
});

// Configure MongoDB settings from appsettings.json
builder.Services.Configure<MongoDbSettings>(
builder.Configuration.GetSection(nameof(MongoDbSettings)));

// Register ApplicationDbContext (Dependency Injection)
builder.Services.AddSingleton<ApplicationDbContext>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Base API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Base API V1");
    c.RoutePrefix = string.Empty;
});

//Use CORS
app.UseCors("AllowSpecificOrigin");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();



app.Run();
