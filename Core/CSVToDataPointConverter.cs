using System.Collections.Concurrent;

namespace Core
{
    public interface ICSVToDataPointConverter
    {
        Task<IEnumerable<WeatherDataPoint>> ConvertToDataPoints(WeatherStream stream, ConcurrentDictionary<DateTime, WeatherDataPoint> buffer);
    }

    public class CSVToDataPointConverter : ICSVToDataPointConverter
    {
        public async Task<IEnumerable<WeatherDataPoint>> ConvertToDataPoints(WeatherStream stream, ConcurrentDictionary<DateTime, WeatherDataPoint> buffer)
        {
            var result = new List<WeatherDataPoint>();

            var streamReader = new StreamReader(stream.Stream);

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();
                var segments = line.Split(';');
                var date = DateTime.Parse(segments[0]);
                if (!buffer.ContainsKey(date))
                {
                    buffer[date] = new WeatherDataPoint
                    {
                        Date = date
                    };
                }

                switch (stream.Metric)
                {
                    case MetricName.Temperature:
                        buffer[date].Temperature = NormalizeValue(segments[1]);
                        break;

                    case MetricName.Humidity:
                        buffer[date].Humidity = NormalizeValue(segments[1]);
                        break;

                    case MetricName.Rainfall:
                        buffer[date].Rainfall = NormalizeValue(segments[1]);
                        break;
                }
            }
            return result;
        }

        //-,27 to -0.27
        //12,45 to 12.45
        //-3,00 to -3.00

        //todo test
        private float NormalizeValue(string value)
        {
            var segments = value.Split(',');
            //examples
            //,24                   -,24
            if (segments[0] == "" || segments[0] == "-")
            {
                return float.Parse($"{segments[0]}0.{segments[1]}");
            }
            else
            {
                return float.Parse(value.Replace(',', '.'));
            }
        }
    }
}