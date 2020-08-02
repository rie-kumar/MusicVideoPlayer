using System;
// using HtmlAgilityPack;
using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace MusicVideoPlayer.YT
{
    public class YouTubeSearcher
    {
        const int MaxResults = 15;
        public static List<YTResult> searchResults;

        static bool searchInProgress = false;

        private static bool isReadingOutput;


        //Make youtube process with my special parser to avoid buffer overflows
        private static IEnumerator SearchYoutubeWithMyExeCoroutine(string query, Action callback)
        {
            searchResults = new List<YTResult>();
            int resultsNumber = 5;
            Plugin.logger.Debug("Starting Search");
            Process searchProcess = new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory + "/Youtube-dl/SelectJsonFromYoutubeDL.exe",
                    Arguments = $"\"{Environment.CurrentDirectory + "/Youtube-dl/youtube-dl.exe"}\" \"{query}\" {resultsNumber}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true,
                //I think these are added only after Process Started
                //PriorityClass = ProcessPriorityClass.RealTime,
                PriorityBoostEnabled = true
            };
            Plugin.logger.Debug("Process Made");
            Plugin.logger.Info($"yt command: \"{searchProcess.StartInfo.FileName}\" {searchProcess.StartInfo.Arguments}");
            isReadingOutput = false;
            searchProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                Plugin.logger.Error("Error With the process:");
                Plugin.logger.Error(e.Data);
            };
            searchProcess.OutputDataReceived += (sender, e) =>
            {
                var output = e.Data.Trim();
                Plugin.logger.Info(e.Data);
                if (string.IsNullOrWhiteSpace(output))
                {
                    return;
                }
                else if (output.Contains("yt command exited"))
                {
                    Plugin.logger.Debug("Done with Youtube Search, Processing...");
                    return;
                }
                else if (output.Contains("yt command"))
                {
                    Plugin.logger.Debug($"Running with {output}");
                    return;
                }

                try
                {
                    var trimmedLine = output;
                    YTResult ytResult = MakeYtResult(trimmedLine);
                    searchResults.Add(ytResult);
                    if (searchResults.Count >= resultsNumber)
                    {
                        ((Process)sender).Kill();
                    }
                }
                catch (Exception error)
                {
                    Plugin.logger.Debug($"Invalid Response: {output}");
                    Plugin.logger.Error(error);
                }
            };
            Plugin.logger.Debug("Error Set");
            searchProcess.Start();
            var fifteenSeconds = new TimeSpan(15 * TimeSpan.TicksPerSecond);
            IEnumerator countdown = YouTubeDownloader.Countdown(searchProcess, fifteenSeconds);
            SharedCoroutineStarter.instance.StartCoroutine(countdown);
            Plugin.logger.Debug("started");
            searchProcess.BeginErrorReadLine();
            searchProcess.BeginOutputReadLine();
            Plugin.logger.Debug("Error Reading");
            // var outputs = searchProcess.StandardOutput.ReadToEnd().Split('\n');
            yield return new WaitUntil(() => searchProcess.HasExited);
            SharedCoroutineStarter.instance.StopCoroutine(countdown);
            // foreach (var output in outputs)
            // {
            //     if (string.IsNullOrWhiteSpace(output))
            //     {
            //         continue;
            //     }
            //     else if (output.Contains("yt command exited"))
            //     {
            //         Plugin.logger.Debug("Done with Youtube Search, Processing...");
            //         continue;
            //     }
            //     else if (output.Contains("yt command"))
            //     {
            //         Plugin.logger.Debug($"Running with {output.Trim()}");
            //         continue;
            //     }
            //
            //     try
            //     {
            //         var trimmedLine = output.Trim();
            //         YTResult ytResult = MakeYtResult(trimmedLine);
            //         searchResults.Add(ytResult);
            //     }
            //     catch (Exception e)
            //     {
            //         Plugin.logger.Debug($"Invalid Response: {output.Trim()}");
            //         Plugin.logger.Error(e);
            //     }
            // }

            Plugin.logger.Debug(searchResults.Count.ToString());
            Plugin.logger.Debug("Calling Back");
            Plugin.logger.Debug((callback == null).ToString());
            callback?.Invoke();
            searchInProgress = false;
        }

        private static IEnumerator SearchYoutubeWithYTDLCoroutine(string query, Action callback)
        {
            searchResults = new List<YTResult>();
            Plugin.logger.Debug("Starting Search");
            Process searchProcess = new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory + "/Youtube-dl/youtube-dl.exe",
                    Arguments = $"ytsearch3:\"{query}\" -j",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true,
                //I think these are added only after Process Started
                //PriorityClass = ProcessPriorityClass.RealTime,
                //PriorityBoostEnabled = true
            };
            Plugin.logger.Debug("Process Made");
            Plugin.logger.Info($"yt command: {searchProcess.StartInfo.FileName} {searchProcess.StartInfo.Arguments}");
            isReadingOutput = false;
            // searchProcess.Exited += (sender, args) => {
            //     Plugin.logger.Debug("Process Exited");
            //     Plugin.logger.Debug("Calling Back");
            //     Plugin.logger.Debug((callback == null).ToString());
            //     callback?.Invoke();
            //     searchInProgress = false;
            //     // StartCoroutine(SearchExited(callback));
            // };
            // Plugin.logger.Debug("Exit Hooked");
            searchProcess.OutputDataReceived += (sender, e) =>
            {
                isReadingOutput = true;
                Plugin.logger.Debug("Output");
                var ytResult = MakeYtResult(e.Data.Trim());
                searchResults.Add(ytResult);
                Plugin.logger.Debug("Done Output");
                Plugin.logger.Debug(searchResults.Count.ToString());
                isReadingOutput = false;
            };
            Plugin.logger.Debug("Output Hooked");
            searchProcess.ErrorDataReceived += (sender, e) =>
            {
                Plugin.logger.Debug("Error");
                if (e.Data == null)
                {
                    return;
                }

                Plugin.logger.Error(e.Data);
            };
            Plugin.logger.Debug("Error Set");
            searchProcess.Start();
            Plugin.logger.Debug("started");
            // searchProcess.BeginOutputReadLine();
            // Plugin.logger.Debug("Output Reading");
            searchProcess.BeginErrorReadLine();
            Plugin.logger.Debug("Error Reading");
            yield return new WaitUntil(() => searchProcess.HasExited);
            // string[] outputs = searchProcess.StandardOutput.ReadToEnd().Split('\n');
            // foreach (var output in outputs)
            // {
            //     if (output == null || output == "" || output.Contains("yt command"))
            //     {
            //         Plugin.logger.Error(output);
            //         continue;
            //     }
            //
            //     var trimmedLine = output.Trim();
            //     YTResult ytResult = MakeYTResult(trimmedLine);
            // }
            // Plugin.logger.Debug("Process Exited");
            // Plugin.logger.Debug("Calling Back");
            // Plugin.logger.Debug((callback == null).ToString());
            callback?.Invoke();
            searchInProgress = false;
        }

        private static YTResult MakeYtResult(string trimmedLine)
        {
            JObject videoSearchJson = JsonConvert.DeserializeObject(trimmedLine) as JObject;
            // TimeSpan duration = TimeSpan.FromSeconds(double.Parse(videoSearchJson["duration"].ToString()));
            YTResult ytResult = new YTResult
            {
                author = videoSearchJson["uploader_id"].ToString(),
                description = videoSearchJson["description"].ToString()
            };
            try
            {
                var duration = TimeSpan.FromSeconds(double.Parse(videoSearchJson["duration"].ToString()));
                ytResult.duration = duration.Hours > 0
                    ? $"{duration.Hours}:{duration.Minutes}:{duration.Seconds}"
                    : $"{duration.Minutes}:{duration.Seconds}";
            }
            catch (FormatException)
            {
                ytResult.duration = videoSearchJson["duration"].ToString();
            }
            ytResult.thumbnailURL = videoSearchJson["thumbnail"].ToString().Replace("/vi_webp/", "/vi/").Replace(".webp", ".jpg").Split('?')[0];
            ytResult.title = videoSearchJson["title"].ToString();
            ytResult.URL = videoSearchJson["webpage_url"].ToString().Replace("https://www.youtube.com", "");
            return ytResult;
        }

        private static IEnumerator SearchExited(Action callback)
        {
            Plugin.logger.Debug("Process Exited");
            // yield return new WaitForSecondsRealtime(1);
            yield return new WaitUntil(() => !isReadingOutput);

            // var restOfOutput = searchProcess.StandardOutput.ReadToEnd();
            // Plugin.logger.Debug(restOfOutput);
            // var lineOutput = restOfOutput.Split('\n');
            // Plugin.logger.Debug(lineOutput.Length.ToString());
            // foreach (var line in lineOutput)
            // {
            // var trimmedLine = line.Trim();
            // if (trimmedLine == "")
            // {
            //     Plugin.logger.Debug("Skipping");
            //     continue;
            // }
            // Plugin.logger.Debug(trimmedLine);
            // JObject videoSearchJson = JsonConvert.DeserializeObject(trimmedLine) as JObject;
            // TimeSpan duration = TimeSpan.FromSeconds(double.Parse(videoSearchJson["duration"]?.ToString()));
            // searchResults.Add(new YTResult
            // {
            //     author = videoSearchJson["uploader_id"]?.ToString(),
            //     description = videoSearchJson["description"]?.ToString(),
            //     duration = duration.Hours > 0 ? $"{duration.Hours}:{duration.Minutes}:{duration.Seconds}" : $"{duration.Minutes}:{duration.Seconds}",
            //     thumbnailURL = videoSearchJson["thumbnail"]?.ToString(),
            //     title = videoSearchJson["title"]?.ToString(),
            //     URL = videoSearchJson["webpage_url"]?.ToString()
            // });
            // Plugin.logger.Debug(searchResults.Count.ToString());
            // Plugin.logger.Debug("Disposing");
            // searchProcess.Dispose();
            Plugin.logger.Debug("Calling Back");
            Plugin.logger.Debug((callback == null).ToString());
            callback?.Invoke();
            searchInProgress = false;
            // }
        }

#if false
        static IEnumerator SearchYoutubeCoroutine(string search, Action callback)
        {
            searchInProgress = true;
            searchResults = new List<YTResult>();

            // get youtube results
            string url = "https://www.youtube.com/results?q=" + Uri.EscapeDataString(search);
            Plugin.logger.Info($"Searching with URL: {url}");
            UnityWebRequest request = UnityWebRequest.Get(url);

            yield return request.SendWebRequest();

            if (request.error != null)
            {
                Plugin.logger.Warn("Search: An Error occured while searching. " + request.error);
                yield break;
            }

            Plugin.logger.Info(request.responseCode.ToString());

            MemoryStream stream = new MemoryStream(request.downloadHandler.data);

            HtmlDocument doc = new HtmlDocument();
            doc.Load(stream, System.Text.Encoding.UTF8);

            var videoNodes = doc.DocumentNode.SelectNodes("//*[contains(concat(' ', @class, ' '),'yt-lockup-video')]");
            Plugin.logger.Info(doc.Text);
            Dictionary<string, string> responseHeaders = request.GetResponseHeaders();
            foreach (KeyValuePair<string, string> keyValuePair in responseHeaders)
            {
                Plugin.logger.Info($"{keyValuePair.Key}: {keyValuePair.Value}");
            }

            if (videoNodes == null)
            {
                Plugin.logger.Info("Search: No results found matching: " + search);
            }
            else
            {
                for (int i = 0; i < Math.Min(MaxResults, videoNodes.Count); i++)
                {
                    var node = HtmlNode.CreateNode(videoNodes[i].InnerHtml);
                    YTResult data = new YTResult();

                    // title
                    var titleNode = node.SelectSingleNode("//*[contains(concat(' ', @class, ' '),'yt-uix-tile-link')]");
                    if (titleNode == null)
                    {
                        continue;
                    }

                    data.title = HttpUtility.HtmlDecode(titleNode.InnerText);

                    // description
                    var descNode =
                        node.SelectSingleNode("//*[contains(concat(' ', @class, ' '),'yt-lockup-description')]");
                    if (descNode == null)
                    {
                        continue;
                    }

                    data.description = HttpUtility.HtmlDecode(descNode.InnerText);

                    // duration
                    var durationNode = node.SelectSingleNode("//*[contains(concat(' ', @class, ' '),'video-time')]");
                    if (durationNode == null)
                    {
                        // no duration means this is a live streamed video
                        continue;
                    }

                    data.duration = HttpUtility.HtmlDecode(durationNode.InnerText);

                    // author node
                    var authorNode =
                        node.SelectSingleNode("//*[contains(concat(' ', @class, ' '),'yt-lockup-byline')]");
                    if (authorNode == null)
                    {
                        continue;
                    }

                    data.author = HttpUtility.HtmlDecode(authorNode.InnerText);

                    // url
                    var urlNode = node.SelectSingleNode("//*[contains(concat(' ', @class, ' '),'yt-uix-tile-link')]");
                    if (urlNode == null)
                    {
                        continue;
                    }

                    data.URL = urlNode.Attributes["href"].Value;

                    var thumbnailNode = node.SelectSingleNode("//img");
                    if (thumbnailNode == null)
                    {
                        continue;
                    }

                    if (thumbnailNode.Attributes["data-thumb"] != null)
                    {
                        data.thumbnailURL = thumbnailNode.Attributes["data-thumb"].Value;
                    }
                    else
                    {
                        data.thumbnailURL = thumbnailNode.Attributes["src"].Value;
                    }

                    // append data to results
                    searchResults.Add(data);
                }
            }

            callback?.Invoke();
            searchInProgress = false;
        }
#endif

        public static void Search(string query, Action callback)
        {
            if (searchInProgress) SharedCoroutineStarter.instance.StopCoroutine("SearchYoutubeWithMyExeCoroutine");
            SharedCoroutineStarter.instance.StartCoroutine(
                YouTubeSearcher.SearchYoutubeWithMyExeCoroutine(query, callback));
        }
    }

    public class YTResult
    {
        public string title;
        public string author;
        public string description;
        public string duration;
        public string URL;
        public string thumbnailURL;

        public new string ToString()
        {
            return $"{title} by {author} [{duration}] \n {URL} \n {description} \n {thumbnailURL}";
        }
    }
}