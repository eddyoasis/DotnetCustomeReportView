namespace DataWarehousePower.Models.AppSettings
{
    public class SmtpAppSetting
    {
        public bool IsEnabled { get; set; }
        public string Host { get; set; } = string.Empty;
        public string EmailFrom { get; set; } = string.Empty;
        public List<string> EmailTo { get; set; } = new List<string>();
        public bool IsTestEmailTo { get; set; }
    }
}