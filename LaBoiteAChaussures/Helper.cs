namespace LaBoiteAChaussures
{
    public static class Helper
    {
        public static string GetRessource(string ressourceName)
        {
            return Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString(ressourceName);
        }

        public static void SetLocalSettings(LocalSettingsValue settings, object value)
        {
            MainPage.LocalSettings.Values[settings.ToString()] = value;
        }

        public static object GetLocalSettings(LocalSettingsValue settings)
        {
            return DoesLocalSettingsExists(settings) ? MainPage.LocalSettings.Values[settings.ToString()] : null;
        }

        public static bool DoesLocalSettingsExists(LocalSettingsValue settings)
        {
            if (MainPage.LocalSettings.Values[settings.ToString()] == null)
            {
                return false;
            }

            return true;
        }
    }
}
