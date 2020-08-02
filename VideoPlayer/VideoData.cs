using MusicVideoPlayer.YT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MusicVideoPlayer.Util;
using Newtonsoft.Json;

namespace MusicVideoPlayer
{
    public enum DownloadState { NotDownloaded, Queued, Downloading, Downloaded, Cancelled };

    [Serializable()]
    public class VideoData
    {
        public string title;
        public string author;
        public string description;
        public string duration;
        public string URL;
        public string thumbnailURL;
        public bool loop = false;
        public int offset = 0; // ms
        public string videoPath;
        //Guess and Cut Stuff
        [JsonProperty("hasBeenCut")]
        private bool _hasBeenCut = false;
        [JsonIgnore]
        public bool hasBeenCut
        {
            get
            {
                string ret;
                if (level is CustomPreviewBeatmapLevel beatmapLevel)
                {
                    // Custom song
                    ret = beatmapLevel.customLevelPath;
                }
                else
                {
                    // OST
                    var videoFileName = this.level.songName;
                    // strip invalid characters
                    foreach (var c in Path.GetInvalidFileNameChars())
                    {
                        videoFileName = videoFileName.Replace(c, '-');
                    }

                    videoFileName = videoFileName.Replace('\\', '-');
                    videoFileName = videoFileName.Replace('/', '-');

                    ret = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomSongs", "_OST", videoFileName);
                }

                return _hasBeenCut && File.Exists(Path.Combine(ret, cutVideoPath));
            }
            set => _hasBeenCut = value;
        }

        public bool needsCut = false;
        public string cutCommand;
        public string[] cutVideoArgs = { "", "", "" };
        public string cutVideoPath;
        [JsonIgnore]
        public string CorrectVideoPath => hasBeenCut ? cutVideoPath : videoPath; 
        
        [System.NonSerialized]
        public IPreviewBeatmapLevel level;
        [System.NonSerialized]
        public float downloadProgress = 0f;
        [System.NonSerialized]
        public DownloadState downloadState = DownloadState.NotDownloaded;

        public new string ToString()
        {
            return $"{title} by {author} [{duration}] {(needsCut ? (hasBeenCut ? "Was Cut" : "Needs Cut" ) : "Don't Cut")} \n {URL} \n {description} \n {thumbnailURL}";
        }

        public VideoData() { }
        public VideoData(string id, IPreviewBeatmapLevel level)
        {
            title = $"Video Id {id}";
            author = "Author Unknown";
            description = "Video Information unknown, to get it search normally";
            duration = "5:00";
            URL = $"/watch?v={id}";
            thumbnailURL = $"https://i.ytimg.com/vi/{id}/maxresdefault.jpg";
            this.level = level;
        }
        public VideoData(YTResult ytResult, IPreviewBeatmapLevel level)
        {
            title = ytResult.title;
            author = ytResult.author;
            description = ytResult.description;
            duration = ytResult.duration;
            URL = ytResult.URL;
            thumbnailURL = ytResult.thumbnailURL;
            this.level = level;
        }

        public void ResetGuess()
        {

            hasBeenCut = false;
            cutCommand = null;
            cutVideoArgs = new[]{"", "", ""};
            cutVideoPath = null;

        }
    }

    [Serializable()]
    // Do Not Make enumerable
    public class VideoDatas
    {
        public int activeVideo = 0;
        public List<VideoData> videos;
        [JsonIgnore]
        public int Count => videos.Count;
        [NonSerialized, JsonIgnore]  
        public IPreviewBeatmapLevel level;
        [JsonIgnore]
        public VideoData ActiveVideo => videos[activeVideo];
        public void Add(VideoData video) => videos.Add(video);

        public IEnumerator<VideoData> GetEnumerator()
        {
            return videos.GetEnumerator();
        }

        // IEnumerator IEnumerable.GetEnumerator()
        // {
        //     return GetEnumerator();
        // }
    }
}
