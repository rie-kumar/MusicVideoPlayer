using BeatSaberMarkupLanguage.Attributes;
using BS_Utils.Utilities;
using MusicVideoPlayer.Util;
using MusicVideoPlayer.YT;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicVideoPlayer.UI
{
    public class MVPSettings : PersistentSingleton<MVPSettings>
    {
        private Config config;

        [UIValue("positions")]
        private List<object> screenPositions = (new object[] 
        { 
            VideoPlacement.Background, 
            VideoPlacement.BackgroundLow,
            VideoPlacement.Center, 
            VideoPlacement.Left, 
            VideoPlacement.Right,
            VideoPlacement.Bottom,
            VideoPlacement.Top
        }).ToList();

        [UIValue("modes")]
        private List<object> qualityModes = (new object[]
        {
            VideoQuality.Best,
            VideoQuality.High,
            VideoQuality.Medium,
            VideoQuality.Low
        }).ToList();

        [UIValue("show-video")]
        public bool ShowVideoSettings
        {
            get => config.GetBool("General", "Show Video", true);
            set
            {
                ScreenManager.showVideo = value;
                config.SetBool("General", "Show Video", value);
            }
        }

        [UIValue("play-preview-audio")]
        public bool PlayPreviewAudio
        {
            get => config.GetBool("General", "Play Preview Audio", false);
            set
            {
                ScreenManager.playPreviewAudio = value;
                config.SetBool("General", "Play Preview Audio", value);
            }
        }

        private VideoPlacement placementMode;
        [UIValue("screen-position")]
        public VideoPlacement PlacementMode
        {
            get => placementMode;
            set
            {
                ScreenManager.Instance.SetPlacement(value);
                placementMode = value;
                config.SetString("Positions", "Video Placement", placementMode.ToString());
            }
        }

        private VideoQuality qualityMode;
        [UIValue("quality")]
        public VideoQuality QualityMode
        {
            get => qualityMode;
            set
            {
                YouTubeDownloader.Instance.quality = value; 
                qualityMode = value;
                config.SetString("Modes", "Video Quality", qualityMode.ToString());
            }
        }

        [UIAction("ReDownloadAll")]
        public void ReDownloadAll()
        {
            if (VideoLoader.videos != null)
            {
                Plugin.logger.Debug("Downloading all videos");
                if(YouTubeDownloader.Instance)
                {
                    foreach (KeyValuePair<IPreviewBeatmapLevel, VideoData> videoKVP in VideoLoader.videos)
                    {
                        Plugin.logger.Debug($"Enqueueing {videoKVP.Value.title}");
                        YouTubeDownloader.Instance.EnqueueVideo(videoKVP.Value);
                    }
                }
                else
                {
                    Plugin.logger.Warn("No YTDL Instance");
                }
            }
        }

        public void Awake()
        {
            config = new Config("MVP");
            if (Enum.TryParse(config.GetString("Positions", "Video Placement", "Bottom"), out VideoPlacement placementParsed))
                placementMode = placementParsed;
            else
                placementMode = VideoPlacement.Bottom;

            if (Enum.TryParse(config.GetString("Modes", "Video Quality", "Best"), out VideoQuality qualityParsed))
                qualityMode = qualityParsed;
            else
                qualityMode = VideoQuality.Best;
        }
    }
}
