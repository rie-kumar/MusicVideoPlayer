using System;
using BeatSaberMarkupLanguage;
using HMUI;

namespace MusicVideoPlayer.UI
{
    public class SettingsFlowCoordinator: FlowCoordinator
    {
        private MVPSettingsController controller;

        public void Awake()
        {
            if (!controller)
            {
                controller = BeatSaberUI.CreateViewController<MVPSettingsController>();
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    //Shows up as all uppercase, that's why there's spaces in MVP
                    SetTitle("Music Video Player Settings"); 
                    showBackButton = true;
                    ProvideInitialViewControllers(controller);
                }
            }
            catch (Exception ex)
            {
                Plugin.logger.Error(ex);
            }
        }

        protected override void BackButtonWasPressed(ViewController viewController)
        {
            BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}