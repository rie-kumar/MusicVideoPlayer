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
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicVideoPlayer.UI
{
    public class SettingsController: BSMLResourceViewController
    {
        public override string ResourceName => "MusicVideoPlayer.UI.Views.settings.bsml";
        
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
            get => Settings.instance.ShowVideoSettings;
            set => Settings.instance.ShowVideoSettings = value;
            
        }
        
        [UIValue("rotate-360")]
        public bool RotateIn360
        {
            get => Settings.instance.RotateIn360;
            set => Settings.instance.RotateIn360 = value;
        }
        
        [UIValue("preload-search")]
        public bool PreloadSearch
        {
            get => Settings.instance.PreloadSearch;
            set => Settings.instance.PreloadSearch = value;
        }

        [UIValue("play-preview-audio")]
        public bool PlayPreviewAudio
        {
            get => Settings.instance.PlayPreviewAudio;
            set => Settings.instance.PlayPreviewAudio = value;
        }
        
        [UIValue("screen-position")]
        public VideoPlacement PlacementMode
        {
            get => Settings.instance.PlacementMode;
            set => Settings.instance.PlacementMode = value;
        }

        [UIValue("quality")]
        public VideoQuality QualityMode
        {
            get => Settings.instance.QualityMode;
            set => Settings.instance.QualityMode = value;
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
        
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            ScreenManager.Instance.ShowScreen();
        }
        
        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            ScreenManager.Instance.HideScreen();
        }
    }
}