using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// Dependency Injection
builder.Services.AddScoped<IMedicineService, MedicineService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();

builder.Services.AddDbContext<PharmacyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// To override database init in WebApplicationFactory
builder.Services.AddScoped<DbInitializerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Auto-migrate and seed in development
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
    db.Database.EnsureCreated(); // Or Migrate()
    DbInitializer.Initialize(db);
}

app.UseAuthorization();
app.MapControllers();
app.Run();

// For integration tests
public partial class Program { }

public class DbInitializerService
{
    private readonly PharmacyDbContext _context;
    public DbInitializerService(PharmacyDbContext context) => _context = context;
    public void Initialize() => DbInitializer.Initialize(_context);
}
