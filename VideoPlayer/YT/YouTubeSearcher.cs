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
    public class SearchResults : IList<YTResult>
    {
        public string query;
        public bool isDone;
        public readonly List<YTResult> results;

        public SearchResults(string query)
        {
            this.query = query;
            this.isDone = false;
            this.results = new List<YTResult>();
        }
        public SearchResults(string query, List<YTResult> results)
        {
            this.query = query;
            this.isDone = false;
            this.results = results;
        }

        public IEnumerator<YTResult> GetEnumerator()
        {
            return results.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) results).GetEnumerator();
        }

        public void Add(YTResult item)
        {
            results.Add(item);
        }

        public void Clear()
        {
            results.Clear();
        }

        public bool Contains(YTResult item)
        {
            return results.Contains(item);
        }

        public void CopyTo(YTResult[] array, int arrayIndex)
        {
            results.CopyTo(array, arrayIndex);
        }

        public bool Remove(YTResult item)
        {
            return results.Remove(item);
        }

        public int Count => results.Count;

        public bool IsReadOnly => ((ICollection<YTResult>) results).IsReadOnly;

        public int IndexOf(YTResult item)
        {
            return results.IndexOf(item);
        }

        public void Insert(int index, YTResult item)
        {
            results.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            results.RemoveAt(index);
        }

        public YTResult this[int index]
        {
            get => results[index];
            set => results[index] = value;
        }
    }
    public static class YouTubeSearcher
    {
        const int MaxResults = 15;
        public static SearchResults searchResults;

        static bool searchInProgress = false;
        // private static SearchResults results;

        private static bool isReadingOutput;


        //Make youtube process with my special parser to avoid stdout buffer overflows
        public static IEnumerator SearchYoutubeWithMyExeCoroutine(string query, Action callback, int resultsNumber = 5)
        {
            //If query is the same as the last one or the same as the default one I ran when the level was loaded, just wait for that to finish and use it
            if (searchResults != null && searchResults.query == query)
            {
                Plugin.logger.Debug("Waiting for other search");
                yield return new WaitUntil(() => searchResults.isDone || searchResults.Count >= 5 || searchResults.query != query);
                Plugin.logger.Debug("Done Waiting for other search");
            }
            else
            {
                searchResults = new SearchResults(query);
                Plugin.logger.Debug("Starting Search");
                var searchProcess = new Process
                {
                    StartInfo =
                    {
                        // #define USEYTDLHELPER
#if USEYTDLHELPER
                        FileName = Environment.CurrentDirectory + "/Youtube-dl/SelectJsonFromYoutubeDL.exe",
                        Arguments =
                            $"\"{Environment.CurrentDirectory + "/Youtube-dl/youtube-dl.exe"}\" \"{query}\" {resultsNumber}",
#else
                        FileName = Environment.CurrentDirectory + "/Youtube-dl/youtube-dl.exe",
                        Arguments = $"\"ytsearch{resultsNumber}:{query}\" -j -i",
#endif
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
                Plugin.logger.Info(
                    $"yt search command: \"{searchProcess.StartInfo.FileName}\" {searchProcess.StartInfo.Arguments}");
                yield return isReadingOutput = false;
                searchProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (searchResults.query != query)
                    {
                        Plugin.logger.Debug("Killing Search Process");
                        try
                        {
                            searchProcess.Kill();
                        } catch { }

                        return;
                    }
                    if (e.Data == null)
                    {
                        return;
                    }

                    Plugin.logger.Error("Error With the process:");
                    Plugin.logger.Error(e.Data);
                };
                searchProcess.OutputDataReceived += (sender, e) =>
                {
                    if (searchResults.query != query)
                    {
                        Plugin.logger.Debug("Killing Search Process");
                        try
                        {
                            searchProcess.Kill();
                        } catch { }
                        return;
                    }

                    var output = e.Data.Trim();
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
                        // Plugin.logger.Debug($"Adding: {ytResult.title}");
                        searchResults.Add(ytResult);
                        if (searchResults.Count >= resultsNumber)
                        {
                            try
                            {
                                ((Process) sender).Kill();
                            } catch {}
                        }
                    }
                    catch (Exception error)
                    {
                        Plugin.logger.Debug($"Invalid Response: {output}");
                        Plugin.logger.Error(error);
                    }
                };
                searchProcess.Exited += (sender, e) =>
                {
                    searchResults.isDone = true;
                    if(searchResults.Count != resultsNumber)
                        Plugin.logger.Warn($"Failed on {resultsNumber-searchResults.Count} queries with exitcode {((Process) sender).ExitCode}");
                    try
                    {
                        searchProcess.Kill();
                    } catch { }
                    try
                    {
                        searchProcess.Dispose();
                    } catch { }
                };
                Plugin.logger.Info($"yt search command: {searchProcess.StartInfo.FileName} {searchProcess.StartInfo.Arguments}");
                yield return searchProcess.Start();
                var fifteenSeconds = new TimeSpan(15 * TimeSpan.TicksPerSecond);
                var countdown = YouTubeDownloader.Countdown(searchProcess, fifteenSeconds);
                SharedCoroutineStarter.instance.StartCoroutine(countdown);
                // Plugin.logger.Debug("started");
                searchProcess.BeginErrorReadLine();
                searchProcess.BeginOutputReadLine();
                // Plugin.logger.Debug("Error Reading");
                // var outputs = searchProcess.StandardOutput.ReadToEnd().Split('\n');
                yield return new WaitUntil(() =>
                { try { return searchProcess.HasExited; } catch { return true; }});
                SharedCoroutineStarter.instance.StopCoroutine(countdown);
                if(searchResults.query != query)
                    yield break;
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
                // Plugin.logger.Debug(searchResults.Count.ToString());
            }
            if (callback == null || query != searchResults.query) yield break;
            Plugin.logger.Debug($"Invoking Callback");
            callback?.Invoke();
            yield return new WaitUntil((() =>  searchResults.isDone || searchResults.Count >= MaxResults));
            callback?.Invoke();
            searchInProgress = false;
        }

        private static IEnumerator SearchYoutubeWithYTDLCoroutine(string query, Action callback)
        {
            searchResults = new SearchResults(query);
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
            yield return new WaitUntil(() => { try { return searchProcess.HasExited; } catch { return true; }});
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
            string s = description.Split('\n')[0];
            return $"{title} by {author} [{duration}]\t\n{URL}\t\n{(s.Length < 128 ? s : s.Substring(0, 128))}\t\n{thumbnailURL}";
        }
    }
}