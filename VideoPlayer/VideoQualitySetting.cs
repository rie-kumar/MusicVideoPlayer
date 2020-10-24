using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicVideoPlayer.YT;
using UnityEngine;

namespace MusicVideoPlayer.Util
{
    public enum VideoQuality { Best, High, Medium, Low };

    public class VideoQualitySetting
    {
        public static string Format(VideoQuality quality)
        {
            string qualityString;
            switch (quality)
            {
                case VideoQuality.Best:
                    qualityString = "bestvideo[vcodec*=avc1]";
                    break;
                case VideoQuality.High:
                    qualityString = "bestvideo[height<=1080][vcodec*=avc1]";
                    break;
                case VideoQuality.Medium:
                    qualityString = "bestvideo[height<=720][vcodec*=avc1]";
                    break;
                case VideoQuality.Low:
                    qualityString = "bestvideo[height<=480][vcodec*=avc1]";
                    break;
                default:
                    qualityString = "bestvideo[height<=480][vcodec*=avc1]";
                    break;
            }

            if (YouTubeDownloader.hasFFMPEG)
            {
                qualityString += "+bestaudio[acodec*=mp4]";
            }

            return qualityString;
        }

        public static float[] Modes()
        {
            return new float[]
            {
                (float)VideoQuality.Best,
                (float)VideoQuality.High,
                (float)VideoQuality.Medium,
                (float)VideoQuality.Low
            };
        }

        public static string Name(VideoQuality mode)
        {
            switch (mode)
            {
                case VideoQuality.Best:
                    return "Best";
                case VideoQuality.High:
                    return "High";
                case VideoQuality.Medium:
                    return "Medium";
                case VideoQuality.Low:
                    return "Low";
                default:
                    return "?";
            }
        }
        
        public static VideoQuality FromName(string mode)
        {
            return Enum.TryParse(mode, out VideoQuality qualityParsed) ? qualityParsed : VideoQuality.High;
        }
    }
}
