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

        public string GetVideoPath(IBeatmapLevel level)
        {
            VideoDatas vids;
            if (levelsVideos.TryGetValue(level, out vids)) return GetVideoPath(vids.GetActiveVideo());
            return null;
        }

        public string GetVideoPath(VideoData video)
        {
            return Path.Combine(GetLevelPath(video.level), video.videoPath);
        }

        public VideoData GetVideo(IPreviewBeatmapLevel level)
        {
            VideoDatas vids;
            if (levelsVideos.TryGetValue(level, out vids)) return vids.GetActiveVideo();
            return null;
        }

        public VideoDatas GetVideos(IPreviewBeatmapLevel level)
        {
            VideoDatas vids;
            if (levelsVideos.TryGetValue(level, out vids)) return vids;
            return null;
        }

        public static string GetLevelPath(IPreviewBeatmapLevel level)
        {
            if (level is CustomPreviewBeatmapLevel)
            {
                // Custom song
                return (level as CustomPreviewBeatmapLevel).customLevelPath;
            }
            else
            {
                // OST
                var videoFileName = level.songName;
                // strip invlid characters
                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    videoFileName = videoFileName.Replace(c, '-');
                }

                videoFileName = videoFileName.Replace('\\', '-');
                videoFileName = videoFileName.Replace('/', '-');

                return Path.Combine(Environment.CurrentDirectory, "CustomSongs", "_OST", videoFileName);
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

        public bool RemoveVideo(VideoData video)
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
            for (int i = videos.Count - 1; i >= 0; --i)
            {
                if (videos.videos[i] == null)
                {
                    videos.videos.RemoveAt(i);
                }
            }

            if (!Directory.Exists(GetLevelPath(videos.level))) Directory.CreateDirectory(GetLevelPath(videos.level));
            File.WriteAllText(Path.Combine(GetLevelPath(videos.level), "video.json"),
                JsonConvert.SerializeObject(videos));

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

            Action job = delegate
            {
                try
                {
                    float i = 0;
                    foreach (var level in levels)
                    {
                        i++;
                        var videoFileName = level.songName;
                        // strip invlid characters
                        foreach (var c in Path.GetInvalidFileNameChars())
                        {
                            videoFileName = videoFileName.Replace(c, '-');
                        }

                        videoFileName = videoFileName.Replace('\\', '-');
                        videoFileName = videoFileName.Replace('/', '-');

                        var songPath = Path.Combine(Environment.CurrentDirectory, "CustomSongs", "_OST", videoFileName);

                        if (!Directory.Exists(songPath)) continue;
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
                                    videos = LoadVideos(result,
                                        level.difficultyBeatmapSets[0].difficultyBeatmaps[0].level);
                                }
                                catch
                                {
                                    VideoData video = LoadVideo(result,
                                        level.difficultyBeatmapSets[0].difficultyBeatmaps[0].level);
                                    videos = new VideoDatas
                                    {
                                        videos = new List<VideoData> {video},
                                        level = video.level
                                    };
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
                    Plugin.logger.Error("RetrieveOSTVideoData failed:");
                    Plugin.logger.Error(e.ToString());
                }
            };

            Action finish = delegate
            {
                AreVideosLoaded = true;
                AreVideosLoading = false;

                _loadingTask = null;

                VideosLoadedEvent?.Invoke();
            };

            _loadingTask = new HMTask(job, finish);
            _loadingTask.Run();
        }

        private void RetrieveCustomLevelVideoData(Loader loader, Dictionary<string, CustomPreviewBeatmapLevel> levels)
        {
            Action job = delegate
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
                                    VideoData video = LoadVideo(result, level.Value);
                                    videos = new VideoDatas
                                    {
                                        videos = new List<VideoData> {LoadVideo(result, level.Value)},
                                        level = level.Value
                                    };
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
            };

            Action finish = delegate
            {
                AreVideosLoaded = true;
                AreVideosLoading = false;

                _loadingTask = null;

                VideosLoadedEvent?.Invoke();
            };

            _loadingTask = new HMTask(job, finish);
            _loadingTask.Run();
        }

        //Delete Video from set of videos for level
        //Return true if no videos left, false if other videos exist
        public bool DeleteVideo(VideoData video, bool alsoRemoveConfig = true)
        {
            string levelPath = GetLevelPath(video.level);
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

        private VideoData LoadVideo(string jsonPath, IPreviewBeatmapLevel level)
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

            if (File.Exists(GetVideoPath(vid)))
            {
                vid.downloadState = DownloadState.Downloaded;
            }

            return vid;
        }

        private VideoDatas LoadVideos(string jsonPath, IPreviewBeatmapLevel level)
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

                if (File.Exists(GetVideoPath(vid)))
                {
                    vid.downloadState = DownloadState.Downloaded;
                }
            }

            return vids;
        }
    }
}