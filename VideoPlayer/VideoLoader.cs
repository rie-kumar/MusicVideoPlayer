using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using MusicVideoPlayer.YT;
using SongCore;
//using System.Text.Json;
using Newtonsoft.Json;

namespace MusicVideoPlayer.Util
{
    public class VideoLoader : MonoBehaviour
    {
        public event Action VideosLoadedEvent;
        public bool AreVideosLoaded { get; private set; }
        public bool AreVideosLoading { get; private set; }

        public bool DictionaryBeingUsed { get; private set; }

        //public static Dictionary<IPreviewBeatmapLevel, VideoData> videos { get; private set; }
        public static Dictionary<IPreviewBeatmapLevel, VideoDatas> levelsVideos { get; private set; }


        private HMTask _loadingTask;
        private bool _loadingCancelled;

        public static VideoLoader Instance;

        public static void OnLoad()
        {
            if (Instance != null) return;
            new GameObject("VideoFetcher").AddComponent<VideoLoader>();
        }

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;

            Loader.SongsLoadedEvent += RetrieveAllVideoData;

            DontDestroyOnLoad(gameObject);
        }

        public static string GetVideoPath(IBeatmapLevel level)
        {
            return levelsVideos.TryGetValue(level, out var vids) ? GetVideoPath(vids.ActiveVideo, vids.ActiveVideo.HasBeenCut) : null;
        }

        public static string GetVideoPath(VideoData video, bool getCutVideo = false)
        {
            // Plugin.logger.Info($"Video: {video?.level.songName}");
            // Plugin.logger.Info($"Cut Path: {video?.cutVideoPath}");
            // Plugin.logger.Info($"Video Path: {video?.videoPath}");
            // Plugin.logger.Info($"? Operator: {getCutVideo && !string.IsNullOrEmpty(video?.cutVideoPath)}");
            return Path.Combine(GetLevelPath(video.level), getCutVideo && !string.IsNullOrEmpty(video?.cutVideoPath) ? video.cutVideoPath : video.videoPath);
        }

        public VideoData GetVideo(IPreviewBeatmapLevel level)
        {
            return levelsVideos.TryGetValue(level, out var vids) ? vids.ActiveVideo : null;
        }

        public static VideoDatas GetVideos(IPreviewBeatmapLevel level)
        {
            return levelsVideos.TryGetValue(level, out var vids) ? vids : null;
        }

        public static string GetLevelPath(IPreviewBeatmapLevel level)
        {
            if (level is CustomPreviewBeatmapLevel beatmapLevel)
            {
                // Custom song
                return beatmapLevel.customLevelPath;
            }
            else
            {
                // OST
                var videoFileName = level.songName;
                // strip invalid characters
                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    videoFileName = videoFileName.Replace(c, '-');
                }

                videoFileName = videoFileName.Replace('\\', '-');
                videoFileName = videoFileName.Replace('/', '-');

                return Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", "_OST", videoFileName);
            }
        }

        public bool SongHasVideo(IBeatmapLevel level)
        {
            return levelsVideos.ContainsKey(level);
        }

        public void AddVideo(VideoData video)
        {
            AddVideo(video, video.level);
        }

        public void AddVideo(VideoData video, IPreviewBeatmapLevel level)
        {
            VideoDatas thisLevelsVideos;
            if (!levelsVideos.TryGetValue(level, out thisLevelsVideos))
            {
                thisLevelsVideos = new VideoDatas
                {
                    videos = new List<VideoData> {video},
                    level = video.level
                };
                levelsVideos.Add(level, thisLevelsVideos);
            }
            else
            {
                thisLevelsVideos.Add(video);
                thisLevelsVideos.activeVideo = thisLevelsVideos.Count - 1;
            }
        }

        public void AddLevelsVideos(VideoDatas videos)
        {
            AddLevelsVideos(videos, videos.level);
        }

        public void AddLevelsVideos(VideoDatas videos, IPreviewBeatmapLevel level)
        {
            levelsVideos.Add(level, videos);
        }

        public static bool RemoveVideo(VideoData video)
        {
            VideoDatas thisLevelsVideos;
            levelsVideos.TryGetValue(video.level, out thisLevelsVideos);
            foreach (VideoData vid in thisLevelsVideos.videos)
            {
                if (vid == video)
                {
                    thisLevelsVideos.videos.Remove(video);
                    if (thisLevelsVideos.Count == 0)
                    {
                        levelsVideos.Remove(video.level);
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        public void RemoveVideos(VideoDatas videos)
        {
            //TODO: make sure this is right
            levelsVideos.Remove(videos.level);
        }

        public static void SaveVideoToDisk(VideoData video)
        {
            if (video == null) return;
            VideoDatas videos;
            levelsVideos.TryGetValue(video.level, out videos);
            SaveVideosToDisk(videos);
            //if (video == null) return;
            //if (!Directory.Exists(GetLevelPath(video.level))) Directory.CreateDirectory(GetLevelPath(video.level));
            //File.WriteAllText(Path.Combine(GetLevelPath(video.level), "video.json"), JsonConvert.SerializeObject(video));

            //using (StreamWriter streamWriter = File.CreateText(Path.Combine(GetLevelPath(video.level), "video.json")))
            //{
            //    streamWriter.Write(JsonConvert.SerializeObject(video));
            //}
        }

        public static void SaveVideosToDisk(VideoDatas videos)
        {
            if (videos == null || videos.Count == 0) return;
            for (var i = videos.Count - 1; i >= 0; --i)
            {
                if (videos.videos[i] == null)
                {
                    videos.videos.RemoveAt(i);
                }
            }

            if (!Directory.Exists(GetLevelPath(videos.level))) Directory.CreateDirectory(GetLevelPath(videos.level));
            var videoJsonPath = Path.Combine(GetLevelPath(videos.level), "video.json");
            Plugin.logger.Info($"Saving to {videoJsonPath}");
            File.WriteAllText(videoJsonPath,
            JsonConvert.SerializeObject(videos, Formatting.Indented));

            //using (StreamWriter streamWriter = File.CreateText(Path.Combine(GetLevelPath(video.level), "video.json")))
            //{
            //    streamWriter.Write(JsonConvert.SerializeObject(video));
            //}
        }

        private void RetrieveAllVideoData(Loader loader, Dictionary<string, CustomPreviewBeatmapLevel> levels)
        {
            levelsVideos = new Dictionary<IPreviewBeatmapLevel, VideoDatas>();
            RetrieveCustomLevelVideoData(loader, levels);
            RetrieveOSTVideoData();
        }

        private void RetrieveOSTVideoData()
        {
            BeatmapLevelSO[] levels = Resources.FindObjectsOfTypeAll<BeatmapLevelSO>()
                .Where(x => x.GetType() != typeof(CustomBeatmapLevel)).ToArray();
            Plugin.logger.Info("Getting OST Video Data");
            Action job = delegate
            {
                try
                {
                    float i = 0;
                    foreach (BeatmapLevelSO level in levels)
                    {
                        var soundData = new float[level.beatmapLevelData.audioClip.samples];
                        level.beatmapLevelData.audioClip.GetData(soundData, level.beatmapLevelData.audioClip.samples);
                        i++;
                        var videoFileName = level.songName;
                        // Plugin.logger.Info($"Trying for: {videoFileName}");
                        // strip invlid characters
                        foreach (var c in Path.GetInvalidFileNameChars())
                        {
                            videoFileName = videoFileName.Replace(c, '-');
                        }

                        videoFileName = videoFileName.Replace('\\', '-');
                        videoFileName = videoFileName.Replace('/', '-');

                        var songPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", "_OST", videoFileName);

                        if (!Directory.Exists(songPath))
                        {
                            continue;
                        }
                        // Plugin.logger.Info($"Using name: {videoFileName}");
                        // Plugin.logger.Info($"At Path: {songPath}");
                        // Plugin.logger.Info($"Exists");
                        var results = Directory.GetFiles(songPath, "video.json", SearchOption.AllDirectories);
                        if (results.Length == 0)
                        {
                            // Plugin.logger.Info($"No video.json");
                            continue;
                        }
                        // Plugin.logger.Info($"Found video.json");

                        var result = results[0];
                        Plugin.logger.Info(result);

                        try
                        {
                            var i1 = i;
                            HMMainThreadDispatcher.instance.Enqueue(() =>
                            {
                                VideoDatas videos;
                                if (_loadingCancelled) return;
                                IPreviewBeatmapLevel previewBeatmapLevel = level.difficultyBeatmapSets[0].difficultyBeatmaps[0].level;
                                Plugin.logger.Info($"Loading: {previewBeatmapLevel.songName}");
                                try
                                {
                                    // Plugin.logger.Info($"Loading as multiple videos");
                                    videos = LoadVideos(result, previewBeatmapLevel);
                                    videos.level = previewBeatmapLevel;
                                }
                                catch
                                {
                                    // Plugin.logger.Info($"Loading as single video");
                                    var video = LoadVideo(result, previewBeatmapLevel);
                                    videos = new VideoDatas
                                    {
                                        videos = new List<VideoData> {video},
                                        level = video.level
                                    };
                                }

                                if (videos.videos.Count != 0)
                                {
                                    AddLevelsVideos(videos);
                                    foreach (var videoData in videos)
                                    {
                                        // Plugin.logger.Info($"Found Video: {videoData.ToString()}");
                                    }
                                }
                                else
                                {
                                    // Plugin.logger.Info($"No Videos");
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            Plugin.logger.Error("Failed to load song folder: " + result);
                            Plugin.logger.Error(e.ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.logger.Error("RetrieveOSTVideoData failed:");
                    Plugin.logger.Error(e.ToString());
                }
            };

            _loadingTask = new HMTask(job, () =>
            {
                AreVideosLoaded = true;
                AreVideosLoading = false;

                _loadingTask = null;

                VideosLoadedEvent?.Invoke();
            });
            _loadingTask.Run();
        }

        private void RetrieveCustomLevelVideoData(Loader loader, Dictionary<string, CustomPreviewBeatmapLevel> levels)
        {
            _loadingTask = new HMTask(() =>
            {
                try
                {
                    float i = 0;
                    foreach (var level in levels)
                    {
                        i++;
                        var songPath = level.Value.customLevelPath;
                        var results = Directory.GetFiles(songPath, "video.json", SearchOption.AllDirectories);
                        if (results.Length == 0)
                        {
                            continue;
                        }

                        var result = results[0];

                        try
                        {
                            var i1 = i;
                            HMMainThreadDispatcher.instance.Enqueue(delegate
                            {
                                VideoDatas videos;
                                if (_loadingCancelled) return;
                                try
                                {
                                    videos = LoadVideos(result, level.Value);
                                    videos.level = level.Value;
                                    foreach (VideoData vid in videos.videos)
                                    {
                                        vid.level = level.Value;
                                    }
                                }
                                catch
                                {
                                    videos = new VideoDatas {videos = new List<VideoData> {LoadVideo(result, level.Value)}, level = level.Value};
                                }

                                if (videos != null && videos.videos.Count != 0)
                                {
                                    AddLevelsVideos(videos);
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            Plugin.logger.Error("Failed to load song folder: " + result);
                            Plugin.logger.Error(e.ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.logger.Error("RetrieveCustomLevelVideoData failed:");
                    Plugin.logger.Error(e.ToString());
                }
            }, () =>
            {
                AreVideosLoaded = true;
                AreVideosLoading = false;

                _loadingTask = null;

                VideosLoadedEvent?.Invoke();
            });
            _loadingTask.Run();
        }

        //Delete Video from set of videos for level
        //Return true if no videos left, false if other videos exist
        public static bool  DeleteVideo(VideoData video, bool alsoRemoveConfig = true)
        {
            var levelPath = GetLevelPath(video.level);
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
            }

            video.DeleteVideoFiles();

            //File.Delete(GetVideoPath(video));
            if (alsoRemoveConfig)
            {
                if (RemoveVideo(video))
                {
                    File.Delete(Path.Combine(levelPath, "video.json"));
                    levelsVideos.Remove(video.level);
                    return true;
                }
                else
                {
                    VideoDatas thisLevelsVideos = GetVideos(video.level);
                    SaveVideosToDisk(thisLevelsVideos);
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        // Load Video data from disk
        private static VideoData LoadVideo(string jsonPath, IPreviewBeatmapLevel level)
        {
            var infoText = File.ReadAllText(jsonPath);
            VideoData vid;
            try
            {
                vid = JsonUtility.FromJson<VideoData>(infoText);
            }
            catch (Exception)
            {
                Plugin.logger.Warn("Error parsing video json: " + jsonPath);
                return null;
            }

            vid.level = level;

            // if (File.Exists(GetVideoPath(vid)))
            // {
            //     vid.downloadState = DownloadState.Downloaded;
            // }
            vid.UpdateDownloadState();
            return vid;
        }

        // Load Video datas from disk
        private static VideoDatas LoadVideos(string jsonPath, IPreviewBeatmapLevel level)
        {
            var infoText = File.ReadAllText(jsonPath);
            VideoDatas vids;
            try
            {
                try
                {
                    vids = JsonConvert.DeserializeObject<VideoDatas>(infoText);
                }
                catch
                {
                    VideoData vid = JsonConvert.DeserializeObject<VideoData>(infoText);
                    vid.level = level;
                    vids = new VideoDatas {videos = new List<VideoData> {vid}, level = level};
                }
            }
            catch (Exception e)
            {
                Plugin.logger.Warn("Error parsing video json: " + jsonPath);
                Plugin.logger.Error(e.GetType().ToString());
                Plugin.logger.Error(e.StackTrace);
                return null;
            }

            foreach (VideoData vid in vids.videos)
            {
                vid.level = level;

                // if (File.Exists(GetVideoPath(vid)))
                // {
                //     vid.downloadState = DownloadState.Downloaded;
                // }
                vid.UpdateDownloadState();
            }

            return vids;
        }
    }
}