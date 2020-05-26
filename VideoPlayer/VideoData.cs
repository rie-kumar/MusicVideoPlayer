using MusicVideoPlayer.YT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UnityEngine;

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
        
        [System.NonSerialized]
        public IPreviewBeatmapLevel level;
        [System.NonSerialized]
        public float downloadProgress = 0f;
        [System.NonSerialized]
        public DownloadState downloadState = DownloadState.NotDownloaded;

        public new string ToString()
        {
            return String.Format("{0} by {1} [{2}] \n {3} \n {4} \n {5}", title, author, duration, URL, description, thumbnailURL);
        }

        public VideoData() { }
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
    }

    [Serializable()]
    public class VideoDatas
    {
        public int activeVideo = 0;
        public List<VideoData> videos;
        [JsonIgnore]
        public int Count => videos.Count;
        [NonSerialized]
        public IPreviewBeatmapLevel level;
        public VideoData GetActiveVideo()
        {
            return videos[activeVideo];
        }
        public void Add(VideoData video) {
            videos.Add(video);
        }

        //public IEnumerator<VideoData> GetEnumerator()
        //{
        //    return videos.GetEnumerator();
        //}

        //IEnumerator IEnumerable.GetEnumerator()
        //{
        //    return GetEnumerator();
        //}
    }
}
