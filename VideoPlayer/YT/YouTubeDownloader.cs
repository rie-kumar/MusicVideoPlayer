using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MusicVideoPlayer.Util;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MusicVideoPlayer.UI;

namespace MusicVideoPlayer.YT
{
    public class YouTubeDownloader : MonoBehaviour
    {
        class VideoDownload
        {
            public VideoData video;
            public int downloadAttempts;
            public float timeSinceLastUpdate;

            public VideoDownload(VideoData video)
            {
                this.video = video;
                downloadAttempts = 0;
                timeSinceLastUpdate = 0;
            }

            public void Update()
            {
                timeSinceLastUpdate = 0;
            }
        }

        public uint VideosDownloading { get; private set; }

        public uint IncrementDownloadCount()
        {
            return ++VideosDownloading;
        }

        public uint DecrementDownloadCount()
        {
            return --VideosDownloading;
        }

        const float TimeoutDuration = 20;
        const int MaxRetries = 3;

        public event Action<VideoData> downloadProgress;

        public VideoQuality quality = VideoQuality.Medium;

        Queue<VideoDownload> videoQueue;
        bool downloading;
        bool updated;
        public static bool hasFFMPEG { get; private set; }

        Process ydl;

        public static YouTubeDownloader Instance = null;

        public static void OnLoad()
        {
            if (!Instance)
            {
                Instance = new GameObject("YoutubeDownloader").AddComponent<YouTubeDownloader>();
                Instance.VideosDownloading = 0;
                DontDestroyOnLoad(Instance);
                Instance.videoQueue = new Queue<VideoDownload>();
                Instance.quality = MVPSettings.instance.QualityMode;
                Instance.downloading = false;
                Instance.updated = false;
                Instance.UpdateYDL();
                try
                {
                    var ffmpegProcess = new Process
                    {
                        StartInfo =
                        {
                            FileName = Environment.CurrentDirectory + "/Youtube-dl/ffmpeg.exe",
                            Arguments = "-version",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    YouTubeDownloader.externalProcesses.Add(ffmpegProcess);
                    ffmpegProcess.Start();
                    hasFFMPEG = true;
                    Plugin.logger.Info("Has ffmpeg -> Will download and mux audio and video");
                }
                catch
                {
                    hasFFMPEG = false;
                    Plugin.logger.Info("Does not have ffmpeg -> Will only download video");
                }
            }
        }

        public void EnqueueVideo(VideoData video)
        {
            videoQueue.Enqueue(new VideoDownload(video));
            if (!downloading)
            {
                video.downloadState = DownloadState.Queued;
                downloadProgress?.Invoke(video);
                StartCoroutine(DownloadVideo());
            }
        }

        public void DequeueVideo(VideoData video)
        {
            video.downloadState = DownloadState.Cancelled;
            downloadProgress?.Invoke(video);
        }

        private string EscapeStringForPythonStringFormatter(string input)
        {
            string[] replaces = {"%", "{", "}"};
            foreach (string replaceChar in replaces)
            {
                input = input.Replace(replaceChar, replaceChar + replaceChar);
            }

            return input;
        }

        public Process MakeYoutubeProcessAndReturnIt(VideoData video)
        {
            string levelPath = VideoLoader.GetLevelPath(video.level);
            if (!Directory.Exists(levelPath)) Directory.CreateDirectory(levelPath);

            string videoFileName = Path.GetInvalidFileNameChars().Aggregate(video.title, (current, c) => current.Replace(c, '-'));
            // Strip invalid characters

            videoFileName = videoFileName.Replace('\\', '-');
            videoFileName = videoFileName.Replace('/', '-');

            video.videoPath = videoFileName + ".mp4";


            Plugin.logger.Info("Name Created");
            // Download the video via youtube-dl 
            var ytProcess = new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory + "/Youtube-dl/youtube-dl.exe",
                    Arguments = "https://www.youtube.com" + video.URL +
                                " -f \"" + VideoQualitySetting.Format(quality) + "\"" + // Formats
                                " --no-cache-dir" + // Don't use temp storage
                                $" -o \"{EscapeStringForPythonStringFormatter(videoFileName)}.%(ext)s\"" +
                                " --no-playlist" + // Don't download playlists, only the first video
                                " --no-part" + // Don't store download in parts, write directly to file
                                (hasFFMPEG
                                    ? " --recode-video mp4"
                                    : ""
                                ) + //Try to recode the video if ffmpeg is installed to fix issue with improper encoding
                                " --no-mtime" + //Video last modified will be when it was downloaded, not when it was uploaded to youtube
                                " --socket-timeout 10" + //Retry if no response in 10 seconds Note: Not if download takes more than 10 seconds but if the time between any 2 messages from the server is 10 seconds
                                " --no-continue" //overwrite existing file and force re-download
                    ,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = levelPath
                },
                EnableRaisingEvents = true,
                //I think these are added only after Process Started
                //PriorityClass = ProcessPriorityClass.RealTime,
                PriorityBoostEnabled = true
            };
            YouTubeDownloader.externalProcesses.Add(ytProcess);
            return ytProcess;
        }

        public Process MakeDebugYoutubeProcessAndReturnIt(VideoData video)
        {
            string levelPath = VideoLoader.GetLevelPath(video.level);
            if (!Directory.Exists(levelPath)) Directory.CreateDirectory(levelPath);

            string videoFileName = video.title;
            // Strip invalid characters
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                videoFileName = videoFileName.Replace(c, '-');
            }

            videoFileName = videoFileName.Replace('\\', '-');
            videoFileName = videoFileName.Replace('/', '-');

            video.videoPath = videoFileName + ".mp4";


            Plugin.logger.Info("Name Created");
            // Download the video via youtube-dl 
            var ytProcess = new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory + "/Youtube-dl/youtube-dl.exe",
                    Arguments = "https://www.youtube.com" + video.URL +
                                " -f \"" + VideoQualitySetting.Format(quality) + "\"" + // Formats
                                " --no-cache-dir" + // Don't use temp storage
                                $" -o \"{EscapeStringForPythonStringFormatter(videoFileName)}.%(ext)s\"" +
                                " --no-playlist" + // Don't download playlists, only the first video
                                " --no-part" + // Don't store download in parts, write directly to file
                                (hasFFMPEG
                                    ? " --recode-video mp4"
                                    : ""
                                ) + //Try to recode the video if ffmpeg is installed to fix issue with improper encoding
                                " --no-mtime" + //Video last modified will be when it was downloaded, not when it was uploaded to youtube
                                " --socket-timeout 10" + //Retry if no response in 10 seconds Note: Not if download takes more than 10 seconds but if the time between any 2 messages from the server is 10 seconds
                                " --no-continue" //overwrite existing file and force re-download
                    ,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WorkingDirectory = levelPath
                },
                EnableRaisingEvents = true,
                //I think these are added only after Process Started
                //PriorityClass = ProcessPriorityClass.RealTime,
                //PriorityBoostEnabled = true
            };
            YouTubeDownloader.externalProcesses.Add(ytProcess);

            return ytProcess;
        }

        public void StartDownload(VideoData video, bool logProgress = true)
        {
            video.downloadState = DownloadState.Queued;
            downloadProgress?.Invoke(video);
            Plugin.logger.Info("Done Invoking Download Progress?");
            StartCoroutine(DownloadVideo(video, logProgress));
            Plugin.logger.Info("Started Download Coroutine");
        }

        //Must Call using StartCoroutine
        private IEnumerator DownloadVideo(VideoData video, bool logProgress = true)
        {
            Plugin.logger.Info($"Starting Download with {video.title}");
            IncrementDownloadCount();
            if (!updated) yield return new WaitUntil(() => updated);
            //Plugin.logger.Debug("Update Finished");
            if (video.downloadState == DownloadState.Cancelled)
            {
//                Plugin.logger.Debug("Download Cancelled");
                yield return null;
            }
            else
            {
                Plugin.logger.Info("Downloading: " + video.title);

                video.downloadState = DownloadState.Downloading;
//                Plugin.logger.Debug("Set State");
                downloadProgress?.Invoke(video);
//                Plugin.logger.Debug("Invoked");

                Process localDownloader = MakeYoutubeProcessAndReturnIt(video);

                Plugin.logger.Info(
                    $"yt command: {localDownloader.StartInfo.FileName} {localDownloader.StartInfo.Arguments}");

                yield return localDownloader.Start();
                var countdown = Countdown(localDownloader, new TimeSpan(0, 1, 0));
                StartCoroutine(countdown);

//                Plugin.logger.Debug("Started Downloaded For Realsies");
                // Hook up our output to console
                localDownloader.BeginOutputReadLine();
                localDownloader.BeginErrorReadLine();

                localDownloader.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null) return;
                    Regex rx = new Regex(@"(\d*).\d%+");
                    Match match = rx.Match(e.Data);
                    if (match.Success)
                    {
                        video.downloadProgress =
                            float.Parse(match.Value.Substring(0, match.Value.Length - 1)) / 100;
                        downloadProgress?.Invoke(video);

                        if (video.downloadState == DownloadState.Cancelled)
                        {
                            (sender as Process)?.Kill();
                            Plugin.logger.Info("Cancelled");
                            VideoLoader.DeleteVideo(video, false);
                            try
                            {
                                ((Process) sender)?.Dispose();
                            }
                            catch { }

                            YouTubeDownloader.externalProcesses.Remove(sender as Process);
                        }
                    }

                    if (logProgress) Plugin.logger.Info(e.Data);
                };
                localDownloader.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data.Length < 3) return;
                    Plugin.logger.Error(e.Data);
                    //TODO: check these errors are problems - re-download or skip file when an error occurs
                    //video.downloadState = DownloadState.Cancelled;
                    downloadProgress?.Invoke(video);
                    if (video.downloadState == DownloadState.Cancelled || e.Data.Contains("Unable to extract video data"))
                    {
                        DownloadCancelled((Process)sender, video);
                    }
                };
                localDownloader.Exited += (sender, e) =>
                {
                    StopCoroutine(countdown);
                    DecrementDownloadCount();
                    if (video.downloadState == DownloadState.Cancelled)
                    {
                        Plugin.logger.Info("Cancelled");
                        VideoLoader.DeleteVideo(video, false);
                    }
                    else
                    {
                        video.downloadState = DownloadState.Downloaded;
                        VideoLoader.SaveVideoToDisk(video);
                        StartCoroutine(VerifyDownload(video));
                        Plugin.logger.Info(
                            $"Done Downloading {video.title} with {VideosDownloading} remaining downloads");
                    }

                    try
                    {
                        localDownloader?.Dispose();
                    }
                    catch { }
                    YouTubeDownloader.externalProcesses.Remove(localDownloader);
                };
            }
        }

        private IEnumerator DownloadVideo()
        {
            downloading = true;
            IncrementDownloadCount();
            if (!updated) yield return new WaitUntil(() => updated);
            VideoDownload download = videoQueue.Peek();
            VideoData video = download.video;
//            Plugin.logger.Debug($"Starting Download with {video.title}");

            if (video.downloadState == DownloadState.Cancelled || download.downloadAttempts > MaxRetries)
            {
                // skip
                videoQueue.Dequeue();

                if (videoQueue.Count > 0)
                {
//                    Plugin.logger.Debug($"Starting Next Download");
                    // Start next download
                    DownloadVideo();
                }
                else
                {
//                    Plugin.logger.Debug($"Done Download");
                    // queue empty
                    downloading = false;
                    yield break;
                }
            }

            Plugin.logger.Info("Downloading: " + video.title);

            IEnumerator countdown = Countdown(download);
            StopCoroutine(countdown);

            video.downloadState = DownloadState.Downloading;
            downloadProgress?.Invoke(video);
            download.Update();

            ydl = MakeYoutubeProcessAndReturnIt(video);

            Plugin.logger.Info($"yt command: {ydl.StartInfo.FileName} {ydl.StartInfo.Arguments}");

            ydl.Start();

            // Hook up our output to console
            ydl.BeginOutputReadLine();
            ydl.BeginErrorReadLine();

            int logCount = 0;
            ydl.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Regex rx = new Regex(@"(\d*).\d%+");
                    Match match = rx.Match(e.Data);
                    if (match.Success)
                    {
                        video.downloadProgress = Single.Parse(match.Value.Substring(0, match.Value.Length - 1)) / 100;
                        downloadProgress?.Invoke(video);
                        download.Update();

                        if (video.downloadState == DownloadState.Cancelled)
                        {
                            DownloadCancelled((Process) sender, video);
                        }
                    }

                    if(++logCount%10 == 0 || video.downloadProgress > .95)
                        Plugin.logger.Info(e.Data);
                }
            };

            ydl.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data.Length < 3) return;
                Plugin.logger.Error(e.Data);
                //TODO: check these errors are problems - re-download or skip file when an error occurs
                //video.downloadState = DownloadState.Cancelled;
                downloadProgress?.Invoke(video);
                download.Update();

                if (video.downloadState == DownloadState.Cancelled || e.Data.Contains("Unable to extract video data"))
                {
                    DownloadCancelled((Process)sender, video);
                }
            };

            ydl.Exited += (sender, e) =>
            {
                StopCoroutine(countdown);

                if (video.downloadState == DownloadState.Cancelled)
                {
                    Plugin.logger.Info("Cancelled");
                    VideoLoader.DeleteVideo(video);
                }
                else
                {
                    // video.downloadState = DownloadState.Downloaded;
                    video.UpdateDownloadState();
                    VideoLoader.SaveVideoToDisk(video);
                    StartCoroutine(VerifyDownload(video));
                }

                videoQueue.Dequeue();

                if (videoQueue.Count > 0)
                {
                    // Start next download
//                    Plugin.logger.Debug("Starting Next Download");
                    DownloadVideo();
                }
                else
                {
                    // queue empty
                    downloading = false;
                }

                try
                {
                    ydl?.Dispose();
                }
                catch { }
                DecrementDownloadCount();
            };
        }

        //Called whenever a YouTube Download process is quit to clean up the extras
        private static void DownloadCancelled(Process ytdlProcess, VideoData video = null)
        {
            ytdlProcess?.Kill();
            try
            {
                ytdlProcess?.Dispose();
            }
            catch { }
            YouTubeDownloader.externalProcesses.Remove(ytdlProcess);
            // if (video != null && File.Exists(video.videoPath))
            // {
            //     File.Delete(video.videoPath);
            // }
        }

        public static List<Process> externalProcesses = new List<Process>();

        public void OnApplicationQuit()
        {
            StopAllCoroutines();
            try
            {
                ydl.Close(); // or .Kill()
                ydl?.Dispose();
            }
            catch
            {
                // ignored
            }

            externalProcesses.ForEach(process =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    process.Dispose();
                }
                catch(InvalidOperationException){}
                catch(NullReferenceException){}
                catch (Exception e)
                {
                    Plugin.logger.Error("Non-critical error on process kill");
                    Plugin.logger.Error(e);
                }
            });
        }

        private IEnumerator VerifyDownload(VideoData video)
        {
            yield return new WaitForSecondsRealtime(1);

            if (File.Exists(VideoLoader.GetVideoPath(video, false)))
            {
                // video okay?
                downloadProgress?.Invoke(video);
            }
        }
        IEnumerator Countdown(VideoDownload download)
        {
            while (download.timeSinceLastUpdate < TimeoutDuration)
            {
                download.timeSinceLastUpdate += Time.deltaTime;
                yield return null;
            }

            Timeout();
        }

        public static IEnumerator Countdown(Process timeoutProcess, TimeSpan? timeoutDuration = null)
        {
            if(timeoutDuration == null) timeoutDuration = new TimeSpan(TimeSpan.TicksPerMinute);
            var counter = new Stopwatch();
            counter.Start();
            while (counter.ElapsedTicks < timeoutDuration?.Ticks)
            {
                yield return null;
            }

            Timeout(timeoutProcess);
        }

        public static bool Timeout(Process processToKill)
        {
            processToKill.Kill(); // kill process don't dispose, let it exit
            return processToKill.HasExited;
        }
        private void Timeout()
        {
            VideoDownload download = videoQueue.Dequeue();
            ydl.Close(); // or .Kill()
            try
            {
                ydl?.Dispose();
            }
            catch { }
            download.downloadAttempts++;
            videoQueue.Enqueue(download);
            DownloadVideo();
        }

        private void UpdateYDL()
        {
            ydl = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    Arguments = $" /C \"{Environment.CurrentDirectory}/Youtube-dl/youtube-dl.exe\" -U",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            ydl.Start();

            ydl.BeginOutputReadLine();
            ydl.BeginErrorReadLine();

            ydl.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Plugin.logger.Info(e.Data);
                }
            };

            ydl.ErrorDataReceived += (sender, e) =>
            {
                Plugin.logger.Warn(e.Data);
                //to do: check these errors problems - redownload or skip file when an error occurs
            };

            ydl.Exited += (sender, e) =>
            {
                updated = true;
                Plugin.logger.Info("Youtube-DL update complete");
                try
                {
                    ydl?.Dispose();
                }
                catch
                {
                    // ignored
                }
            };
        }
    }
}