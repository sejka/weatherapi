using Core;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<IWeatherDataBlobService, WeatherDataBlobService>();
builder.Services.AddTransient<ICSVToDataPointConverter, CSVToDataPointConverter>();
builder.Services.AddTransient<IWeatherDataService, WeatherDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/{deviceId}/{date}", async (string deviceId,
                                            [Required] DateTime date,
                                            WeatherDataService blobService) =>
{
    return await blobService.GetAllMetricsForDay(deviceId, DateOnly.FromDateTime(date));
});

app.Run();

app.MapGet("/{deviceId}/{date}/{metric}", async (string deviceId,
                                            [Required] DateTime date,
                                            string metric,
                                            WeatherDataService blobService) =>
{
    return await blobService.GetOneMetricForDay(deviceId, DateOnly.FromDateTime(date), (MetricName)Enum.Parse(typeof(MetricName), metric));
});