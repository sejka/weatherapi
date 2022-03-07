using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;

namespace Core
{
    public interface IWeatherDataService
    {
        Task<WeatherStream> FindStream(string deviceName, DateOnly date, MetricName metricName);
        Task<IEnumerable<WeatherDataPoint>> GetAllMetricsForDay(string deviceId, DateOnly date);

        Task<IEnumerable<WeatherDataPoint>> GetOneMetricForDay(string deviceId, DateOnly date, MetricName metricName);
    }

    public class WeatherDataService : IWeatherDataService
    {
        private readonly IWeatherDataBlobService _weatherDataBlobService;
        private string _cacheLocation;
        private readonly ICSVToDataPointConverter _converter;

        public WeatherDataService(IWeatherDataBlobService weatherDataBlobService, ICSVToDataPointConverter converter, IConfiguration config)
        {
            _weatherDataBlobService = weatherDataBlobService;
            _cacheLocation = config["CacheLocation"];
            _converter = converter;
        }

        public async Task<WeatherStream> FindStream(string deviceName, DateOnly date, MetricName metricName)
        {
            var metricNameString = Enum.GetName(metricName)?.ToLower();
            var path = $"{deviceName}/{metricNameString}";
            var filename = $"{date.ToString("o", CultureInfo.InvariantCulture)}.csv";
            string cacheHash = $"{_cacheLocation}/{deviceName}-{metricNameString}-{filename}";

            //check not compressed
            Task<bool> isStandalone = _weatherDataBlobService.Exists($"{path}/{filename}");
            var isCompressed = _weatherDataBlobService.Exists($"{path}/historical.zip");
            if (await isStandalone)
            {
                var stream = await _weatherDataBlobService.OpenStream($"{path}/{filename}");

                return new WeatherStream
                {
                    Stream = stream,
                    Metric = metricName
                };
            }

            //check if metric has an archive
            if (!(await isCompressed))
            {
                return null;
            }

            var zipStream = await _weatherDataBlobService.OpenStream($"{path}/historical.zip");
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                //check if file exists in archive
                if (zip.Entries.Any(x => x.FullName == filename))
                {
                    //todo not async :( https://github.com/dotnet/runtime/issues/1541
                    var stream = zip.Entries.Single(x => x.FullName == filename).Open();

                    return new WeatherStream
                    {
                        Stream = stream,
                        Metric = metricName
                    };
                }
            }

            return null;
        }

        public async Task<IEnumerable<WeatherDataPoint>> GetAllMetricsForDay(string deviceId, DateOnly date)
        {
            //foreach.parallel also a good choice depending on use case (performance vs availability)
            var taskList = new List<Task<WeatherStream>>();
            foreach (var metric in Enum.GetValues(typeof(MetricName)).Cast<MetricName>())
            {
                taskList.Add(FindStream(deviceId, date, metric));
            }
            var results = new ConcurrentDictionary<DateTime, WeatherDataPoint>();
            await Parallel.ForEachAsync(taskList, async (x, cancellationToken) => await _converter.ConvertToDataPoints(await x, results));

            return results.Values.OrderBy(x => x.Date);
        }

        public async Task<IEnumerable<WeatherDataPoint>> GetOneMetricForDay(string deviceId, DateOnly date, MetricName metricName)
        {
            var results = new ConcurrentDictionary<DateTime, WeatherDataPoint>();

            var stream = await FindStream(deviceId, date, metricName);
            return await _converter.ConvertToDataPoints(stream, results);
        }
    }

    public class WeatherDataPoint
    {
        public DateTime Date { get; set; }
        public float? Temperature { get; set; } = null;
        public float? Humidity { get; set; } = null;
        public float? Rainfall { get; set; } = null;
    }
}