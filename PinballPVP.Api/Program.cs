using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValidation(); // Make sure to add validation services

// Make the connection to the database, DefaultConnection is defined on appsettings.json files
builder.Services.AddDbContext<PinballPVPContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"))
    .UseSnakeCaseNamingConvention()
);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
