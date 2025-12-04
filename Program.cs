using TourAppBackend.src.Data;
using TourAppBackend.src.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("### CONNECTION STRING ###");
Console.WriteLine(cs ?? "NULL GELDI!");


// Veritabaný baðlantýsý
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Data layer servisleri ve repository kayýtlarý
builder.Services.AddDataLayer(connectionString);

// Business katmanýndaki servisleri ve AutoMapper kaydý
builder.Services.AddBusinessLayer();

// API controller'larý
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
