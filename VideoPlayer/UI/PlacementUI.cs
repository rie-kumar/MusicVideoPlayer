using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;

namespace MusicVideoPlayer.UI
{
    internal class PlacementUI
    {
        private static readonly MenuButton menuButton = new MenuButton("Video Player", "MusicVideoPlayer Settings", ShowFlow, true);
        
        public static SettingsFlowCoordinator flowCoordinator;
        public static bool created = false;
        
        public static void CreateMenu()
        {
            if (!created)
            {
                MenuButtons.instance.RegisterButton(menuButton);
                created = true;
            }
        }

        public static void RemoveMenu()
        {
            if (created)
            {
                MenuButtons.instance.UnregisterButton(menuButton);
                created = false;
            }
        }

        public static void ShowFlow()
        {
            if (flowCoordinator == null)
            {
                flowCoordinator = BeatSaberUI.CreateFlowCoordinator<SettingsFlowCoordinator>();
            }

            BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(flowCoordinator, null);
        }
    }
}