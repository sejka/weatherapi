using Core;
using System.Buffers;
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

app.MapGet("/{deviceId}/{date}/{metricName}", async (string deviceId,
                                                     DateTime date,
                                                     [EnumDataType(typeof(MetricName))][Required] MetricName metricName,
                                                     HttpContext context,
                                                     IWeatherDataService blobService) =>
{
    context.Response.ContentType = "application/json";

    var dateOnly = DateOnly.FromDateTime(date);
    using (var tempStream = new StreamReader((await blobService.FindStream(deviceId, dateOnly, metricName)).Stream))
    {
        await context.Response.WriteAsync("[");
        while (!tempStream.EndOfStream)
        {
            var line = await tempStream.ReadLineAsync();
            var segments = line.Split(';');
            await context.Response.WriteAsync($"{{\"date\": \"{segments[0]}\", \"temperature\":\"{segments[1]}\"}},");
        }
        await context.Response.WriteAsync("]");
        await context.Response.CompleteAsync();
    }
})
.WithName("GetWeatherForecast");

app.MapGet("/{deviceId}/{date}", async (string deviceId,
                                                     [Required] DateTime date,
                                                     HttpContext context,
                                                     WeatherDataService blobService) =>
{
    var dateOnly = DateOnly.FromDateTime(date);
    var humidityStreamTask = blobService.FindStream(deviceId, dateOnly, MetricName.Humidity);
    var rainfallStreamTask = blobService.FindStream(deviceId, dateOnly, MetricName.Rainfall);
    var temperatureStreamTask = blobService.FindStream(deviceId, dateOnly, MetricName.Temperature);
    await Task.WhenAll(humidityStreamTask, rainfallStreamTask, temperatureStreamTask);

    using (var temperatureStream = new StreamReader((await temperatureStreamTask).Stream))
    using (var humidityStream = new StreamReader((await humidityStreamTask).Stream))
    using (var rainfallStream = new StreamReader((await rainfallStreamTask).Stream))
    {
        var shared = ArrayPool<WeatherDataPoint>.Shared;
        var rented = shared.Rent(10000);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("[");
        while (!temperatureStream.EndOfStream)
        {
            var temperatureLine = await temperatureStream.ReadLineAsync();
            var humidityLine = await humidityStream.ReadLineAsync();
            var rainfallLine = await rainfallStream.ReadLineAsync();

            string FormatWeatherData(string date, string temp, string humidity, string rainfall)
            {
                return $"{{\"date\": \"{date}\", \"temperature\": \"{temp}\", \"humidity\": \"{humidity}\", \"rainfall\": \"{rainfall}\"}},";
            }

            string GetValue(string line)
            {
                var separatorPosition = line.IndexOf(';');
                return line.Substring(separatorPosition + 1);
            }

            await context.Response.WriteAsync(FormatWeatherData(temperatureLine.Split(';')[0], GetValue(temperatureLine), GetValue(humidityLine), GetValue(rainfallLine)));
        }
        await context.Response.WriteAsync("]");
        await context.Response.CompleteAsync();
    }
})
.WithName("GetAllWeatherForecast");

app.MapGet("/baseline/{date}", async (DateTime date, HttpContext context, WeatherDataService blobService) =>
{
    using (var tempStreamTask = blobService.FindStream("dockan", DateOnly.FromDateTime(date), MetricName.Temperature))
    using (var humidityStreamTask = blobService.FindStream("dockan", DateOnly.FromDateTime(date), MetricName.Humidity))
    using (var rainfallStreamTask = blobService.FindStream("dockan", DateOnly.FromDateTime(date), MetricName.Rainfall))
    {
        await Parallel.ForEachAsync(new List<Task<WeatherStream>>
        {
            tempStreamTask,humidityStreamTask,rainfallStreamTask
        }, async (x, cancellationToken) =>
        {
            var stream = (await x).Stream;
            if (stream != null)
            {
                var reader = new StreamReader(stream);
                await context.Response.WriteAsync(await reader.ReadToEndAsync());
            }
        });
    }
}).WithName("baseline");

app.Run();