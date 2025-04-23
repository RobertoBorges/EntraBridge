    namespace EntraBridge.Models;

    // Simple view model for application data
    public class ApplicationViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string ApplicationType { get; set; } = "A";
    }