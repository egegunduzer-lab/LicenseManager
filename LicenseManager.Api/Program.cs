using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string AdminPanelCorsPolicy = "AdminPanel";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// Add services to the container.
builder.Services.AddSingleton<Ed25519KeyService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddPrivateKeyProtection(
    builder.Configuration,
    builder.Environment);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddScoped<LicenseSigningService>();
builder.Services.AddScoped<LicenseService>();
builder.Services.AddSingleton<AdminApiKeyValidator>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            AdminPanelCorsPolicy,
            policy =>
            {
                policy
                    .WithOrigins("http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
    });
}

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(AdminPanelCorsPolicy);
}

app.UseAuthorization();

app.MapControllers();

app.Run();
