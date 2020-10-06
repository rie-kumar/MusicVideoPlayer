using BeatSaberMarkupLanguage.Attributes;
using BS_Utils.Utilities;
using ModestTree;
using MusicVideoPlayer.Util;
using MusicVideoPlayer.YT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicVideoPlayer.UI
{
    public class MVPSettings : PersistentSingleton<MVPSettings>
    {
        private Config config;

        [UIValue("positions")] private List<object> screenPositions = new object[]
        {
            VideoPlacement.BackgroundHigh,
            VideoPlacement.BackgroundMid,
            VideoPlacement.BackgroundLow,
            VideoPlacement.Center,
            VideoPlacement.Left,
            VideoPlacement.Right,
            VideoPlacement.Bottom,
            VideoPlacement.Top
        }.ToList();

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
        [UIValue("rotate-360")]
        public bool RotateIn360
        {
            get => config.GetBool("General", "Rotate in 360", true);
            set
            {
                ScreenManager.rotateIn360 = value;
                config.SetBool("General", "Rotate in 360", value);
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

        [UIComponent("howManyDeleted")] private TextMeshProUGUI howManyDeleted;

        [UIComponent("DeleteAllButton")]
        private Button deleteAllButton;

        [UIComponent("DownloadAllButton")]
        private Button downloadAllButton;

        [UIAction("DeleteAll")]
        public void DeleteAll()
        {
            deleteAllButton.enabled = false;
            downloadAllButton.enabled = false;
            StartCoroutine(DeleteAllCouroutine());
        }

        public IEnumerator DeleteAllCouroutine()
        {
            if (VideoLoader.levelsVideos != null)
            {
                Plugin.logger.Debug("Deleting all videos");
                if (YouTubeDownloader.Instance)
                {
                    int levelCount = 0;
                    foreach (KeyValuePair<IPreviewBeatmapLevel, VideoDatas> keyValuePair in VideoLoader.levelsVideos)
                    {
                        levelCount += keyValuePair.Value.Count;
                    }
                    howManyDeleted.text = $"0/{levelCount}";
                    int counter = 0;
                    foreach (KeyValuePair<IPreviewBeatmapLevel, VideoDatas> videoKvp in VideoLoader.levelsVideos)
                    {
                        //if (counter > 30) continue;
                        foreach (VideoData video in videoKvp.Value.videos)
                        {
                            ++counter;
                            string levelPath = VideoLoader.GetLevelPath(video.level);
                            var dir = new DirectoryInfo(levelPath);
                            foreach (var file in dir.EnumerateFiles(video.videoPath.Substring(0, video.videoPath.Length - 4) + "*"))
                            {
                                Plugin.logger.Debug($"Deleting {file.Name}");
                                try
                                {
                                    file.Delete();
                                }
                                catch (System.IO.IOException e)
                                {
                                    //Re-Delete
                                    Plugin.logger.Error(e);
                                }
                                catch (Exception e)
                                {
                                    Plugin.logger.Error(e);
                                }
                                howManyVideosDone.text = $"{counter}/{levelCount}";
                            }
                            yield return null;
                        }
                    }
                    deleteAllButton.enabled = true;
                    downloadAllButton.enabled = true;
                }
                else
                {
                    Plugin.logger.Warn("No YTDL Instance");
                }
            }
        }


        [UIComponent("howManyVideoDone")] private TextMeshProUGUI howManyVideosDone;

        [UIAction("ReDownloadAll")]
        public void ReDownloadAll()
        {
            deleteAllButton.enabled = false;
            downloadAllButton.enabled = false;
            StartCoroutine(DownloadAll());
        }


        public IEnumerator DownloadAll()
        {
            if (VideoLoader.levelsVideos != null)
            {
                Plugin.logger.Debug("Downloading all videos");
                if (YouTubeDownloader.Instance)
                {
                    int levelCount = 0;
                    foreach (KeyValuePair<IPreviewBeatmapLevel, VideoDatas> keyValuePair in VideoLoader.levelsVideos)
                    {
                        levelCount += keyValuePair.Value.Count;
                    }
                    int levelTotal = levelCount;
                    howManyVideosDone.text = $"0/{levelCount}";
                    int processCount = 0;
                    int counter = 0;
                    foreach (KeyValuePair<IPreviewBeatmapLevel, VideoDatas> videoKVP in VideoLoader.levelsVideos)
                    {
                        //if(counter > 30) continue;
                        string levelPath = VideoLoader.GetLevelPath(videoKVP.Value.level);
                        var dir = new DirectoryInfo(levelPath);
                        foreach (VideoData video in videoKVP.Value.videos)
                        {
                            ++counter;
                            //Remove Old Video First
                            foreach (var file in dir.EnumerateFiles(video.videoPath.Substring(0, video.videoPath.Length - 4) + "*"))
                            {
                                Plugin.logger.Debug($"Deleting {file.Name}");
                                try
                                {
                                    file.Delete();
                                }
                                catch (System.IO.IOException e)
                                {
                                    //Re-Delete
                                    Plugin.logger.Error(e);
                                }
                                catch (Exception e)
                                {
                                    Plugin.logger.Error(e);
                                }
                            }
                            Plugin.logger.Info($"{YouTubeDownloader.Instance.VideosDownloading} videos currently");
                            yield return new WaitUntil(() => YouTubeDownloader.Instance.VideosDownloading < 10);

                            YouTubeDownloader.Instance.StartDownload(video, false);
                            Plugin.logger.Info($"Video {video.title} downloading {YouTubeDownloader.Instance.VideosDownloading} videos currently");
                            yield return null;
                        }
                    }
                    yield return new WaitUntil(() => YouTubeDownloader.Instance.VideosDownloading < 1);
                    deleteAllButton.enabled = true;
                    downloadAllButton.enabled = true;
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
            placementMode = Enum.TryParse(config.GetString("Positions", "Video Placement", "BackgroundMid"), out VideoPlacement placementParsed) ? placementParsed : VideoPlacement.BackgroundMid;
            qualityMode = Enum.TryParse(config.GetString("Modes", "Video Quality", "Best"), out VideoQuality qualityParsed) ? qualityParsed : VideoQuality.High;
        }
    }
}