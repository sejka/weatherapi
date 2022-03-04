namespace API.Data
{
    public class CSVToDataPointConverter
    {
        public async Task<IEnumerable<TimeValue>> ConvertToDataPoints(Stream stream, string cacheLocation = "")
        {
            var result = new List<TimeValue>();
            using (var reader = new StreamReader(stream))
            {
                Stream cacheStream = new MemoryStream();
                if (cacheLocation != "")
                {
                    cacheStream = File.OpenWrite(cacheLocation);
                }

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();

                    var segments = line?.Split(';');

                    if (cacheLocation != "")
                    {
                        cacheStream.Write(line);
                    }

                    if (segments == null || segments.Length < 2)
                    {
                        continue;
                    }

                    result.Add(new TimeValue
                    {
                        Date = DateTime.Parse(segments[0]),
                        Value = NormalizeValue(segments[1])
                    });
                }
                cacheStream.Close();
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