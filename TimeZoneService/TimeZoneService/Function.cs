using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.TimeZones;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace TimeZoneService
{
    public class Functions
    {
        public APIGatewayProxyResponse GetAllTimeZones(APIGatewayProxyRequest request, ILambdaContext context)
        {
            List<TimeZoneInfo> timeZones = new List<TimeZoneInfo>();

            foreach (var location in TzdbDateTimeZoneSource.Default.ZoneLocations)
            {
                timeZones.Add(GetZoneInfo(location));
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(timeZones),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };

            return response;
        }

        public APIGatewayProxyResponse GetSingleTimeZone(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string timeZoneId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey("Id"))
                timeZoneId = request.PathParameters["Id"];

            if (!String.IsNullOrEmpty(timeZoneId))
            {
                // Url decode the TZID
                timeZoneId = WebUtility.UrlDecode(timeZoneId);

                var location = TzdbDateTimeZoneSource.Default.ZoneLocations.FirstOrDefault(
                    l => String.Compare(l.ZoneId, timeZoneId, StringComparison.OrdinalIgnoreCase) == 0);

                if (location != null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = JsonConvert.SerializeObject(GetZoneInfo(location)),
                        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                    };
                }
            }
           
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound
            };
        }

        private TimeZoneInfo GetZoneInfo(TzdbZoneLocation location)
        {
            var zone = DateTimeZoneProviders.Tzdb[location.ZoneId];

            // Get the start and end of the year in this zone
            var startOfYear = zone.AtStartOfDay(new LocalDate(2017, 1, 1));
            var endOfYear = zone.AtStrictly(new LocalDate(2018, 1, 1).AtMidnight().PlusNanoseconds(-1));

            // Get all intervals for current year
            var intervals = zone.GetZoneIntervals(startOfYear.ToInstant(), endOfYear.ToInstant()).ToList(); 

            // Try grab interval with DST. If none present, grab first one we can find
            var interval = intervals.FirstOrDefault(i => i.Savings.Seconds > 0) ?? intervals.FirstOrDefault();

            return new TimeZoneInfo
            {
                TimeZoneId = location.ZoneId,
                Offset = interval.StandardOffset.ToTimeSpan(),
                DstOffset = interval.WallOffset.ToTimeSpan(),
                CountryCode = location.CountryCode,
                CountryName = location.CountryName
            };
        }
    }
}
