using BeatSaberMarkupLanguage.Attributes;
using BS_Utils.Utilities;
using ModestTree;
using MusicVideoPlayer.Util;
using MusicVideoPlayer.YT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;

namespace MusicVideoPlayer.UI
{
    public class MVPSettings : PersistentSingleton<MVPSettings>
    {
        private Config config;

        [UIValue("positions")] private List<object> screenPositions = (new object[]
        {
            VideoPlacement.Background,
            VideoPlacement.BackgroundLow,
            VideoPlacement.Center,
            VideoPlacement.Left,
            VideoPlacement.Right,
            VideoPlacement.Bottom,
            VideoPlacement.Top
        }).ToList();

        [UIValue("modes")] private List<object> qualityModes = (new object[]
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



        [UIComponent("howManyVideoDone")] private TextMeshProUGUI howManyVideosDone;

        [UIAction("ReDownloadAll")]
        public void ReDownloadAll()
        {
            StartCoroutine(DownloadAll());
        }

        public IEnumerator DownloadAll()
        {
            if (VideoLoader.levelsVideos != null)
            {
                Plugin.logger.Debug("Downloading all videos");
                if (YouTubeDownloader.Instance)
                {
                    int levelCount = VideoLoader.levelsVideos.Count;
                    int levelTotal = levelCount;
                    howManyVideosDone.text = $"0/{VideoLoader.levelsVideos.Count}";
                    int processCount = 0;
                    foreach (KeyValuePair<IPreviewBeatmapLevel, VideoDatas> videoKVP in VideoLoader.levelsVideos)
                    {
                        foreach (VideoData video in videoKVP.Value.videos)
                        {
                            string command = $"rm {VideoLoader.GetLevelPath(video.level)}\\{video.videoPath}";
                            Process ytProcess = YouTubeDownloader.Instance.MakeYoutubeProcessAndReturnIt(video);
                            ytProcess.Exited += (sender, e) =>
                            {
                                VideoLoader.SaveVideosToDisk(videoKVP.Value);
                                ytProcess.Dispose();
                                --processCount;
                                --levelTotal;
                                Plugin.logger.Debug($"Video {video.title} downloaded {levelTotal} videos left");
                                howManyVideosDone.text = $"{levelCount - levelTotal}/{levelCount}";
                            };
                            Plugin.logger.Debug($"{processCount} videos currently");
                            yield return new WaitUntil(() => processCount < 10);
                            ytProcess.Start();
                            ++processCount;
                            Plugin.logger.Debug($"Video {video.title} downloading {processCount} videos currently");
                            yield return null;
                        }
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
            placementMode = Enum.TryParse(config.GetString("Positions", "Video Placement", "Bottom"), out VideoPlacement placementParsed) ? placementParsed : VideoPlacement.Bottom;

            qualityMode = Enum.TryParse(config.GetString("Modes", "Video Quality", "Best"), out VideoQuality qualityParsed) ? qualityParsed : VideoQuality.Best;
        }
    }
}