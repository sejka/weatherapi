using API.Core;
using System.Globalization;
using System.IO.Compression;

namespace API.Data
{
    public class WeatherDataService
    {
        private readonly WeatherDataBlobService _weatherDataBlobService;
        private string _cacheLocation;
        private readonly CSVToDataPointConverter _converter;

        public WeatherDataService(WeatherDataBlobService weatherDataBlobService, CSVToDataPointConverter converter, IConfiguration config)
        {
            _weatherDataBlobService = weatherDataBlobService;
            _cacheLocation = config["CacheLocation"];
            _converter = converter;
        }

        public async Task<(MetricName Metric, IEnumerable<TimeValue> ValuesList)> GetOneMetricForDay(string deviceName, DateOnly date, MetricName metricName)
        {
            var metricNameString = Enum.GetName(metricName)?.ToLower();
            var path = $"{deviceName}/{metricNameString}";
            var filename = $"{date.ToString("o", CultureInfo.InvariantCulture)}.csv";
            string cacheHash = $"{_cacheLocation}/{deviceName}-{metricNameString}-{filename}";

            //check cache first
            if (File.Exists(cacheHash))
            {
                using (var stream = File.OpenRead(cacheHash))
                {
                    return (metricName, await _converter.ConvertToDataPoints(stream));
                }
            }

            //check not compressed
            if (await _weatherDataBlobService.Exists($"{path}/{filename}"))
            {
                using (var stream = await _weatherDataBlobService.OpenStream($"{path}/{filename}"))
                {
                    return (metricName, await _converter.ConvertToDataPoints(stream));
                }
            }

            //check if metric has an archive
            if (!await _weatherDataBlobService.Exists($"{path}/historical.zip"))
            {
                return (metricName, new List<TimeValue>());
            }

            var zipStream = await _weatherDataBlobService.OpenStream($"{path}/historical.zip");
            var zip = new ZipArchive(zipStream);

            //check if file exists in archive
            if (zip.Entries.Any(x => x.FullName == filename))
            {
                using (var stream = zip.Entries.Single(x => x.FullName == filename).Open())
                {
                    return (metricName, await _converter.ConvertToDataPoints(stream, cacheHash));
                }
            }

            return (metricName, new List<TimeValue>());

        }

        public async Task<IEnumerable<WeatherDataPoint>> GetAllMetricsForDay(string deviceId, DateOnly date)
        {
            //foreach.parallel also a good choice depending on use case (performance vs availability)
            var taskList = new List<Task<(MetricName metric, IEnumerable<TimeValue> ValuesList)>>();
            foreach (var metric in Enum.GetValues(typeof(MetricName)).Cast<MetricName>())
            {
                taskList.Add(GetOneMetricForDay(deviceId, date, metric));
            }
            var results = await Task.WhenAll(taskList);

            return results
                .AsParallel()
                .Select(x => x.ValuesList.Select(y => new { metric = x.metric, date = y.Date, value = y.Value }))
                .SelectMany(x => x)
                .GroupBy(x => x.date)
                .Select(x => new WeatherDataPoint
                {
                    Date = x.Key,
                    Humidity = x.SingleOrDefault(y => y.metric == MetricName.Humidity)?.value,
                    Temperature = x.SingleOrDefault(y => y.metric == MetricName.Temperature)?.value,
                    Rainfall = x.SingleOrDefault(y => y.metric == MetricName.Rainfall)?.value,
                });
        }
    }

    public struct TimeValue
    {
        public DateTime Date;
        public float Value;
    }

    public struct WeatherDataPoint
    {
        public DateTime Date;
        public float? Temperature;
        public float? Humidity;
        public float? Rainfall;
    }
}