using API.Core;
using API.Data;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherDataService _weatherDataService;
        private readonly ILogger<WeatherController> _logger;

        public WeatherController(ILogger<WeatherController> logger, WeatherDataService weatherDataRepository)
        {
            _weatherDataService = weatherDataRepository;
            _logger = logger;
        }

        [HttpGet("devices/{deviceId}/data/{date}/{metricName}")]
        public async Task<IEnumerable<TimeValue>> Get([Required(AllowEmptyStrings = false)] string deviceId,
                                               [Required] DateTime date,
                                               [EnumDataType(typeof(MetricName))][Required] MetricName metricName)
        {
            return (await _weatherDataService.GetOneMetricForDay(deviceId, DateOnly.FromDateTime(date), metricName)).ValuesList;
        }

        [HttpGet("devices/{deviceId}/data/{date}")]
        public async Task<IEnumerable<WeatherDataPoint>> GetAllMetrics([Required(AllowEmptyStrings = false)] string deviceId,
                                                         [Required] DateTime date)
        {
            return await _weatherDataService.GetAllMetricsForDay(deviceId, DateOnly.FromDateTime(date));
        }
    }


}