using BS_Utils.Utilities;
using MusicVideoPlayer.Util;
using MusicVideoPlayer.YT;
using SongCore.Utilities;
using UnityEngine.PlayerLoop;

namespace MusicVideoPlayer
{
    public class Settings
    {
        public static Config config { get; private set; }
        public static Settings instance { get; private set; }

        public static void Init()
        {
            instance = new Settings();
        }
        private Settings()
        {
            config = new Config("MVP");
        }

        public VideoQuality QualityMode
        {
            get => VideoQualitySetting.FromName(
                config.GetString("Modes", "Video Quality", "")
            );
            set
            {
                YouTubeDownloader.Instance.quality = value;
                config.SetString("Modes", "Video Quality", value.ToString());
            }
        }
        
        public VideoPlacement PlacementMode
        {
            get => VideoPlacementSetting.FromName(
                config.GetString("Positions", "Video Placement", "")
            );
            set
            {
                ScreenManager.Instance.SetPlacement(value);
                config.SetString("Positions", "Video Placement", value.ToString());
            }
        }
        
        public bool ShowVideoSettings
        {
            get => config.GetBool("General", "Show Video", true);
            set
            {
                ScreenManager.showVideo = value;
                config.SetBool("General", "Show Video", value);
                if (value)
                {
                    ScreenManager.Instance.ShowScreen();
                }
                else
                {
                    ScreenManager.Instance.HideScreen();
                }
            }
        }
        
        public bool RotateIn360
        {
            get => config.GetBool("General", "Rotate in 360", true);
            set
            {
                ScreenManager.rotateIn360 = value;
                config.SetBool("General", "Rotate in 360", value);
            }
        }
        
        public bool PreloadSearch
        {
            get => config.GetBool("General", "Preload Search", false);
            set
            {
                config.SetBool("General", "Preload Search", value);
            }
        }
        
        public bool PlayPreviewAudio
        {
            get => config.GetBool("General", "Play Preview Audio", false);
            set
            {
                ScreenManager.playPreviewAudio = value;
                config.SetBool("General", "Play Preview Audio", value);
            }
        }
    }
}