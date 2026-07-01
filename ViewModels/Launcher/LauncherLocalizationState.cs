using IconGrid.Helpers.Settings;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherLocalizationState
    {
        public bool ApplyLanguage(string language)
        {
            var previousMissingMessage = PawnIoMissingMessage;
            var previousDownloadLink = PawnIoDownloadLink;

            UpdatePawnIoTexts(language);

            return previousMissingMessage != PawnIoMissingMessage
                || previousDownloadLink != PawnIoDownloadLink;
        }

        public string Get(string language, string key)
        {
            return LocalizationHelper.Get(language, key);
        }

        public string PawnIoMissingMessage { get; private set; } = "CPU temperatures require PawnIO.";

        public string PawnIoDownloadLink { get; private set; } = "Download PawnIO";

        public void UpdatePawnIoTexts(string language)
        {
            PawnIoMissingMessage = LocalizationHelper.Get(language, "PawnIoMissingMessage");
            PawnIoDownloadLink = LocalizationHelper.Get(language, "PawnIoDownloadLink");
        }
    }
}
