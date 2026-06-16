namespace DataWarehousePower.Helper
{
    public static class DateTimeHelper
    {
        private static IConfiguration? _configuration;

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the timezone offset hours from appsettings. Default is 8 for UTC+8 (Singapore/Malaysia)
        /// </summary>
        public static int GetTimezoneOffsetHours()
        {
            if (_configuration == null)
                return 8; // Default to UTC+8

            return _configuration.GetValue<int>("TimezoneSettings:OffsetHours", 8);
        }

        /// <summary>
        /// Converts UTC DateTime to local timezone based on appsettings
        /// </summary>
        public static DateTime ToLocalTime(DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            var offsetHours = GetTimezoneOffsetHours();
            return utcDateTime.AddHours(offsetHours);
        }

        /// <summary>
        /// Gets current local time based on timezone offset from appsettings
        /// </summary>
        public static DateTime GetCurrentLocalTime()
        {
            var offsetHours = GetTimezoneOffsetHours();
            return DateTime.UtcNow.AddHours(offsetHours);
        }

        /// <summary>
        /// Converts local time to UTC for database storage
        /// </summary>
        public static DateTime ToUtcTime(DateTime localDateTime)
        {
            var offsetHours = GetTimezoneOffsetHours();
            return localDateTime.AddHours(-offsetHours);
        }

        /// <summary>
        /// Formats DateTime to standard display format with local timezone
        /// </summary>
        public static string FormatToDisplayString(DateTime dateTime, string format = "dd/MM/yyyy HH:mm:ss")
        {
            //var localTime = ToLocalTime(dateTime);
            return dateTime.ToString(format);
        }

        /// <summary>
        /// Formats nullable DateTime to standard display format with local timezone
        /// </summary>
        public static string FormatToDisplayString(DateTime? dateTime, string format = "dd/MM/yyyy HH:mm:ss", string nullValue = "N/A")
        {
            if (!dateTime.HasValue)
                return nullValue;

            return FormatToDisplayString(dateTime.Value, format);
        }

        /// <summary>
        /// Formats DateTime for short display (date only)
        /// </summary>
        public static string FormatToShortDateString(DateTime dateTime)
        {
            return FormatToDisplayString(dateTime, "dd/MM/yyyy");
        }

        /// <summary>
        /// Formats DateTime for short display with time
        /// </summary>
        public static string FormatToShortString(DateTime dateTime)
        {
            return FormatToDisplayString(dateTime, "dd/MM/yyyy HH:mm");
        }

        /// <summary>
        /// Formats nullable DateTime for short display (date only)
        /// </summary>
        public static string FormatToShortDateString(DateTime? dateTime, string nullValue = "N/A")
        {
            return FormatToDisplayString(dateTime, "dd/MM/yyyy", nullValue);
        }

        /// <summary>
        /// Formats nullable DateTime for short display with time
        /// </summary>
        public static string FormatToShortString(DateTime? dateTime, string nullValue = "N/A")
        {
            return FormatToDisplayString(dateTime, "dd/MM/yyyy HH:mm", nullValue);
        }
    }
}
