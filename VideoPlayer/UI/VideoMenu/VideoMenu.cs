#pragma warning disable 649
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Parser;
using BS_Utils.Utilities;
using HMUI;
using MusicVideoPlayer.Util;
using MusicVideoPlayer.YT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BS_Utils.Gameplay;
using IPA.Config.Data;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Image = UnityEngine.UI.Image;
using Screen = HMUI.Screen;

// ReSharper disable UnusedMember.Local

namespace MusicVideoPlayer
{
    public class VideoMenu : PersistentSingleton<VideoMenu>
    {
        #region Fields

        [UIObject("root-object")] private GameObject root;

        #region Rect Transform

        [UIComponent("video-details")] private RectTransform videoDetailsViewRect;

        [UIComponent("video-search-results")] private RectTransform videoSearchResultsViewRect;

        #endregion

        #region Text Mesh Pro

        [UIComponent("video-title")] private TextMeshProUGUI videoTitleText;

        [UIComponent("current-video-description")]
        private TextMeshProUGUI currentVideoDescriptionText;

        [UIComponent("current-video-offset")] private TextMeshProUGUI currentVideoOffsetText;

        [UIComponent("preview-button")] private TextMeshProUGUI previewButtonText;

        // [UIComponent("delete-button")] private TextMeshProUGUI deleteButtonText;

        [UIComponent("delete-video-button")] private TextMeshProUGUI deleteVideoButtonText;

        //[UIComponent("add-button")]
        //private TextMeshProUGUI addButtonText;

        [UIComponent("search-results-loading")]
        private TextMeshProUGUI searchResultsLoadingText;

        // [UIComponent("cut-video")] private TextMeshProUGUI cutVideoButtonTextMesh;

        [UIComponent("download-state-text")] private TextMeshProUGUI downloadStateText;

        [UIComponent("offset-magnitude-button")]
        private TextMeshProUGUI offsetMagnitudeButtonText;

        #endregion

        #region Buttons

        [UIComponent("video-list")] private CustomListTableData customListTableData;

        [UIComponent("offset-decrease-button")]
        private Button offsetDecreaseButton;

        [UIComponent("offset-increase-button")]
        private Button offsetIncreaseButton;

        [UIComponent("delete-button")] private Button deleteButton;

        [UIComponent("delete-video-button")] private Button deleteVideoButton;

        [UIComponent("add-button")] private Button addButton;

        [UIComponent("guess-offset")] private Button guessButton;

        [UIComponent("download-button")] private Button downloadButton;

        [UIComponent("refine-button")] private Button refineButton;

        [UIComponent("preview-button")] private Button previewButton;

        [UIComponent("cut-video")] private Button cutVideoButton;

        [UIComponent("search-button")] private Button searchButton;

        #endregion

        [UIComponent("search-keyboard")] private ModalKeyboard searchKeyboard;

        #region Params

        [UIParams] private BSMLParserParams parserParams;

        private Vector3 videoPlayerDetailScale = new Vector3(0.57f, 0.57f, 1f);
        private Vector3 videoPlayerDetailPosition = new Vector3(-2.35f, 1.22f, 1.0f);
        private Vector3 videoPlayerDetailRotation = new Vector3(0f, 295f, 0f);

        private VideoData selectedVideo;

        public static SongPreviewPlayer songPreviewPlayer;

        private VideoMenuStatus statusViewer;

        private bool isPreviewing = false;

        // private bool isOffsetInSeconds = false;

        private bool isActive = false;

        private IPreviewBeatmapLevel selectedLevel;

        private IEnumerator updateSearchResultsCoroutine = null;

        private int selectedCell;

        #endregion

        #endregion

        public void OnLoad()
        {
            Setup();
        }

        internal void Setup()
        {
            YouTubeDownloader.Instance.downloadProgress += VideoDownloaderDownloadProgress;
            BSEvents.levelSelected += HandleDidSelectLevel;
            BSEvents.gameSceneLoaded += GameSceneLoaded;
            songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().First();

            videoDetailsViewRect.gameObject.SetActive(true);
            videoSearchResultsViewRect.gameObject.SetActive(false);

            statusViewer = root.AddComponent<VideoMenuStatus>();
            statusViewer.DidEnable += StatusViewerDidEnable;
            statusViewer.DidDisable += StatusViewerDidDisable;

            Resources.FindObjectsOfTypeAll<MissionSelectionMapViewController>().FirstOrDefault().didActivateEvent +=
                MissionSelectionDidActivate;
        }

        #region Public Methods


        public bool isSameVideo(VideoData videoData, VideoDatas videoDatas)
        {
            return selectedLevel != videoData.level || videoTitleText.text == "No Video" ||
                   videoDatas.videos.IndexOf(videoData) != videoDatas.activeVideo;
        }
        public void LoadVideoSettingsIfSameVideo(VideoData videoData)
        {
            VideoDatas videoDatas = VideoLoader.GetVideos(videoData.level);
            if (isSameVideo(videoData, videoDatas))
            {
                // Plugin.logger.Info("Not Same Video");
                return;
            }
            LoadVideoSettings(videoData, false);
        }

        public void LoadVideoSettings(VideoData videoData, bool checkForVideo = true)
        {
            // Plugin.logger.Info($"Stopping Preview");

            StopPreview(false);

            // Plugin.logger.Info($"Stopped Preview");

            if (videoData == null && checkForVideo && selectedLevel != null)
            {
                var videoDatas = VideoLoader.GetVideos(selectedLevel);
                videoData = videoDatas?.ActiveVideo;
            }

            selectedVideo = videoData;

            if (videoData != null)
            {
                Plugin.logger.Info($"Loading: {videoData?.title} for level: {selectedLevel?.songName}");
                videoTitleText.text = $"[{selectedVideo.duration}] {selectedVideo.title}";
                currentVideoDescriptionText.text = selectedVideo.description;
                currentVideoOffsetText.text = selectedVideo.offset.ToString();
                EnableButtons(true);
            }
            else
            {
                Plugin.logger.Info($"Clearing Settings");
                ClearSettings();
            }

            LoadVideoDownloadState();

            Plugin.logger.Debug($"Has Loaded: {selectedVideo} Video is: {downloadStateText.text}");
            ScreenManager.Instance.PrepareVideo(selectedVideo);
        }

        public void ClearSettings()
        {
            videoTitleText.text = "No Video";
            currentVideoDescriptionText.text = "";
            currentVideoOffsetText.text = "N/A";
            EnableButtons(false);
        }

        public void Activate()
        {
            isActive = true;
            ScreenManager.Instance.ShowScreen();
            ChangeView(false);
        }

        public void Deactivate()
        {
            StopPreview(false);

            isActive = false;
            selectedVideo = null;

            videoTitleText.text = "No Video";
            currentVideoDescriptionText.text = "";
            currentVideoOffsetText.text = "N/A";
            EnableButtons(false);
            
            ScreenManager.Instance.SetPlacement(Settings.instance.PlacementMode);
            ScreenManager.Instance.HideScreen();
        }

        #endregion

        #region Private Methods

        private void EnableButtons(bool enable)
        {
            offsetDecreaseButton.interactable = enable;
            offsetIncreaseButton.interactable = enable;
            guessButton.interactable = enable;
            cutVideoButton.interactable = enable;
            deleteButton.interactable = enable;
            deleteVideoButton.interactable = enable;
            addButton.interactable = enable;

            if (selectedVideo == null || selectedVideo.downloadState != DownloadState.Downloaded)
            {
                enable = false;
                // Plugin.logger.Debug($"{selectedVideo}, {selectedVideo?.downloadState}!={DownloadState.Downloaded}");
                //TODO: add option to download video from video.json at game boot code here, may cause lag at loading if downloading too many
            }

            previewButton.interactable = enable;

            searchButton.interactable = selectedLevel != null;
        }

        private void SetPreviewState()
        {
            previewButtonText.text = isPreviewing ? "Stop" : "Preview";
        }

        private bool StopPreview(bool stopPreviewMusic)
        {
            isPreviewing = false;
            ScreenManager.Instance.PrepareVideo(selectedVideo);

            if (stopPreviewMusic)
            {
                songPreviewPlayer.FadeOut();
            }

            SetPreviewState();
            return true;
        }

        private void ChangeView(bool searchView)
        {
            StopPreview(false);
            ResetSearchView();
            videoDetailsViewRect.gameObject.SetActive(!searchView);
            videoSearchResultsViewRect.gameObject.SetActive(searchView);

            if (!searchView)
            {
                parserParams.EmitEvent("hide-keyboard");

                if (isActive)
                {
                    ScreenManager.Instance.SetScale(videoPlayerDetailScale);
                    ScreenManager.Instance.SetPosition(videoPlayerDetailPosition);
                    ScreenManager.Instance.SetRotation(videoPlayerDetailRotation);
                }

                LoadVideoSettings(selectedVideo);
            }
            else
            {
                ScreenManager.Instance.SetPlacement(Settings.instance.PlacementMode);
            }
        }

        private void ResetSearchView()
        {
            if (updateSearchResultsCoroutine != null)
            {
                StopCoroutine(updateSearchResultsCoroutine);
            }

            StopCoroutine(SearchLoading());

            System.Diagnostics.Debug.Assert(customListTableData.data != null, "customListTableData.data != null");
            if (customListTableData.data != null || customListTableData.data.Count > 0)
            {
                customListTableData.data.Clear();
                customListTableData.tableView.ReloadData();
            }

            selectedCell = -1;
        }

        private void UpdateOffset(bool isDecreasing)
        {
            if (isPreviewing)
            {
                StopPreview(true);
            }

            if (selectedVideo == null) return;
            int magnitude = isDecreasing ? offsetMagnitude * -1 : offsetMagnitude;

            selectedVideo.offset += magnitude;
            currentVideoOffsetText.text = selectedVideo.offset.ToString();
            Save();
        }

        private void Save()
        {
            Save(selectedVideo);
        }

        private void Save(VideoData videoData)
        {
            if (selectedVideo == null) return;
            selectedVideo.loop = false;
            // StopPreview(false);
            VideoLoader.SaveVideoToDisk(videoData);
        }

        private void UpdateVideoDependentButtons()
        {
            if (selectedVideo.downloadState == DownloadState.Downloading)
            {
                deleteVideoButtonText.SetText("Cancel");
                cutVideoButton.interactable = guessButton.interactable = false;
            }
            else if (selectedVideo.downloadState == DownloadState.NotDownloaded ||
                     selectedVideo.downloadState == DownloadState.Cancelled)
            {
                deleteVideoButtonText.SetText("Re-Download");
                cutVideoButton.interactable = guessButton.interactable = false;
            }
            else
            {
                deleteVideoButtonText.SetText("Delete Video");
                cutVideoButton.interactable = guessButton.interactable = true;
            }
        }

        private void LoadVideoDownloadState()
        {
            var state = "Unknown";

            if (selectedVideo != null)
            {
                switch (selectedVideo.downloadState)
                {
                    case DownloadState.NotDownloaded:
                        state = "No Video";
                        break;
                    case DownloadState.Queued:
                        state = "Queued";
                        break;
                    case DownloadState.Downloading:
                        state = $"Downloading {selectedVideo.downloadProgress * 100}%";
                        break;
                    case DownloadState.Downloaded:
                        state = "Downloaded";
                        break;
                    case DownloadState.Cancelled:
                        state = "Cancelled";
                        break;
                }

                UpdateVideoDependentButtons();
            }

            downloadStateText.text = selectedVideo == null ? state : $"{state} And {(selectedVideo.needsCut && !selectedVideo.HasBeenCut ? "Needs cut" : "Doesn't need cut")}";
        }

        private IEnumerator UpdateSearchResults(IEnumerable<YTResult> results)
        {
            Plugin.logger.Info("Updating Search Results");
            List<CustomListTableData.CustomCellInfo> videoCells = new List<CustomListTableData.CustomCellInfo>();

            foreach (var result in results)
            {
                var description = $"[{result.duration}] {result.description}";
                var item = new CustomListTableData.CustomCellInfo(result.title, description);

                var request = UnityWebRequestTexture.GetTexture(result.thumbnailURL);
                yield return request.SendWebRequest();
                if (request.isNetworkError || request.isHttpError)
                    Plugin.logger.Debug(request.error);
                else
                {
                    var tex = ((DownloadHandlerTexture) request.downloadHandler).texture;
                    item.icon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
                }

                videoCells.Add(item);
            }

            customListTableData.data = videoCells;
            customListTableData.tableView.ReloadData();

            refineButton.interactable = true;
            searchResultsLoadingText.gameObject.SetActive(false);
        }

        private IEnumerator SearchLoading()
        {
            int count = 0;
            string loadingText = "Loading Results";
            searchResultsLoadingText.gameObject.SetActive(true);

            while (searchResultsLoadingText.gameObject.activeInHierarchy)
            {
                string periods = string.Empty;
                count++;

                for (int i = 0; i < count; i++)
                {
                    periods += ".";
                }

                if (count == 3)
                {
                    count = 0;
                }

                searchResultsLoadingText.SetText(loadingText + periods);

                yield return new WaitForSeconds(0.5f);
            }
        }

        #endregion

        #region Actions

        [UIAction("prev-video-action")]
        private void PrevVideoAction()
        {
            var videoDatas = VideoLoader.GetVideos(selectedLevel);
            if (videoDatas.activeVideo == 0)
            {
                videoDatas.activeVideo = videoDatas.Count - 1;
            }
            else
            {
                --videoDatas.activeVideo;
            }

            ChangeView(false);
            Plugin.logger.Info($"Video {videoDatas.activeVideo} {videoDatas.Count}");
            LoadVideoSettings(videoDatas.ActiveVideo);
            Save();
        }

        [UIAction("next-video-action")]
        private void NextVideoAction()
        {
            VideoDatas videoDatas = VideoLoader.GetVideos(selectedLevel);
            if (videoDatas.activeVideo == videoDatas.Count - 1)
            {
                videoDatas.activeVideo = 0;
            }
            else
            {
                ++videoDatas.activeVideo;
            }

            ChangeView(false);
            Plugin.logger.Info($"Video {videoDatas.activeVideo} {videoDatas.Count}");
            LoadVideoSettings(videoDatas.ActiveVideo);
            Save();
        }

        [UIAction("on-cut-video-action")]
        private void OnCutVideoActionWrapper()
        {
            Plugin.logger.Info("Cutting Video");
            StartCoroutine(OnCutVideoActionDirect(selectedVideo));
        }

        private IEnumerator OnCutVideoActionPowershell(VideoData videoData)
        {
            if ((selectedVideo == null ||
                          (!string.IsNullOrEmpty(selectedVideo.cutCommand)) && !selectedVideo.needsCut))
            {
                Plugin.logger.Info("No Cut Needed");
                if(videoData == selectedVideo) downloadStateText.SetText("No Cut Needed");
                yield break;
            }

            if (selectedVideo.HasBeenCut)
            {
                Plugin.logger.Info("Already Been Cut");
                if(videoData == selectedVideo) downloadStateText.SetText("Already Been Cut");
                yield break;
            }

            if(videoData == selectedVideo) downloadStateText.text = "Cutting Song";
            Plugin.logger.Info("Cutting Song");
            string levelFolder = VideoLoader.GetLevelPath(selectedLevel);
            if (string.IsNullOrEmpty(selectedVideo.cutCommand))
            {
                StartCoroutine(GuessLoading(selectedVideo));
                yield return StartCoroutine(OnGuessOffsetAction(videoData));
                yield return new WaitUntil(() => !videoData.isGuessing);
            }

            var cutProcess = new Process
            {
                StartInfo =
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoProfile -ExecutionPolicy unrestricted -file \"{Environment.CurrentDirectory}/Youtube-dl/concat.ps1\" {selectedVideo.cutCommand} \"{Environment.CurrentDirectory}/Youtube-dl/ffmpeg.exe\"",
                    WorkingDirectory = levelFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            Plugin.logger.Info(
                $"yt command: {cutProcess.StartInfo.FileName} {cutProcess.StartInfo.Arguments} @ {cutProcess.StartInfo.WorkingDirectory}");
            if(videoData == selectedVideo) downloadStateText.text = "Cutting Video";
            Plugin.logger.Info("Cutting Video");
            var fileName = "";
            try
            {
                cutProcess.OutputDataReceived += (sender, e) =>
                {
                    Plugin.logger.Info(e.Data);
                    if (e.Data.Contains("Done: "))
                        fileName = e.Data;
                };
                Plugin.logger.Info("Output");
                cutProcess.ErrorDataReceived += (sender, e) => { Plugin.logger.Error(e.Data); };
                cutProcess.Exited += (sender, e) =>
                {
                    Plugin.logger.Info("Cutting Done");
                    Plugin.logger.Info($"File at -> \"{fileName}\"");

                    // Regex regex = new Regex("\"(.*?)\"");
                    //
                    // var matches = regex.Matches(fileName);
                    //
                    // if (matches.Count > 0)
                    // {
                    //     Console.WriteLine(matches[0].Groups[1]);
                    // }
                    selectedVideo.cutVideoPath = fileName.Replace("Done: ", "").Trim('\"', ' ', '\n', '\r');
                    if(videoData == selectedVideo) downloadStateText.text = "Cutting Done";
                    selectedVideo.HasBeenCut = true;
                };
                Plugin.logger.Info("Cutting Starting");
                cutProcess.Start();
                cutProcess.BeginOutputReadLine();
                cutProcess.BeginErrorReadLine();
                Plugin.logger.Info("Cutting Started");
            }
            catch (Exception e)
            {
                Plugin.logger.Error(
                    @"Cannot Convert song from egg to mp3 -> Cannot Make Comparison | I Don't know What happened");
                Plugin.logger.Error(e);
                yield break;
            }

            // offsetProcess.PriorityBoostEnabled = true;
            Plugin.logger.Debug(cutProcess.HasExited.ToString());
            yield return new WaitUntil(() => { try { return cutProcess.HasExited; } catch { return true; }});
            Plugin.logger.Info("Disposing");
            try
            {
                cutProcess?.Dispose();
            }
            catch
            {
                // Already disposed
            }
            Plugin.logger.Info("Cutting Offset done?");
            Save(videoData);
        }

        public struct StartAndLength
        {
            public string start;
            public string length;

            public override string ToString() => $"-ss {start} -t {length}";
        }

        private IEnumerator OnCutVideoActionDirect(VideoData videoData)
        {
            // If Cut Command is not null then a guess has been made so needsCut is not just the default value
            if ((videoData == null || (!string.IsNullOrEmpty(videoData.cutCommand)) && !videoData.needsCut))
            {
                Plugin.logger.Info("No Cut Needed");
                if(videoData == selectedVideo) downloadStateText.SetText("No Cut Needed");
                yield break;
            }
            // Check if has been cut before, guessing offset clears this value
            if (videoData.HasBeenCut)
            {
                Plugin.logger.Info("Already Been Cut");
                if(videoData == selectedVideo) downloadStateText.SetText("Already Been Cut");
                yield break;
            }
            string levelFolder = VideoLoader.GetLevelPath(videoData.level);
            if (!string.IsNullOrEmpty(videoData.cutCommand))
            {
                StartCoroutine(GuessLoading(selectedVideo));
                // guess offset "synchronously" inside this coroutine
                yield return StartCoroutine(OnGuessOffsetAction(videoData));
                // Wait until GuessSucceeded or GuessFailed is done
                yield return new WaitUntil(() => !videoData.isGuessing);
                if (!videoData.needsCut) yield break;
            }

            if(videoData == selectedVideo) downloadStateText.text = "Cutting Song";
            Plugin.logger.Info("Cutting Song");

            var videoFile = videoData.cutVideoArgs[0];
            Plugin.logger.Info(videoFile);
            var outFile = videoData.cutVideoArgs[1];
            Plugin.logger.Info(outFile);
            var startsAndLengths = new List<StartAndLength>();
            foreach (var startAndLength in videoData.cutVideoArgs[2].Split('|'))
            {
                Plugin.logger.Info(startAndLength);
                var splits = startAndLength.Split(',');
                var sAndL = new StartAndLength {start = splits[0], length = splits[1]};
                startsAndLengths.Add(sAndL);
                Plugin.logger.Info(sAndL.ToString());
            }

            var concatFiles = new List<string>();
            for (var i = 0; i < startsAndLengths.Count; i++)
            {
                var startAndLength = startsAndLengths[i];
                var cutProcess = MakeFfmpegCutProcess(startAndLength.start, startAndLength.length, videoFile,
                    levelFolder, $"file{i}.mp4");
                var i1 = i;
                cutProcess.Exited += (sender, args) =>
                {
                    if (cutProcess.ExitCode != 0)
                    {
                        var message = "Ffmpeg Concat Process failed, try re-downloading file";
                        Plugin.logger.Error(message);
                        if(videoData == selectedVideo) downloadStateText.text = message;
                        return;
                    }
                    concatFiles.Add($"file{i1}.mp4");
                    try
                    {
                        cutProcess?.Dispose();
                    }
                    catch (Exception e)
                    {
                        Plugin.logger.Error(e);
                    }
                };
                cutProcess.Start();
                cutProcess.BeginErrorReadLine();
                cutProcess.BeginOutputReadLine(); 
                yield return new WaitUntil(() =>
                {
                    try
                    {
                        return cutProcess.HasExited;
                    }
                    catch (InvalidOperationException)
                    {
                        return true;
                    }
                    catch (Exception e)
                    {
                        Plugin.logger.Error(e);
                        return true;
                    }
                });
            }
            var concatFileLines = from file in concatFiles select $"file {file}";
            File.WriteAllLines(levelFolder + "/concat.txt", concatFileLines);
            if(videoData == selectedVideo) downloadStateText.text = "Cutting Video";
            Plugin.logger.Info("Cutting Video");
            var concatProcess = MakeFfmpegConcatProcess(outFile, levelFolder);
            concatProcess.Exited += (sender, args) =>
            {
                if (concatProcess.ExitCode != 0)
                {
                    Plugin.logger.Error("Ffmpeg Concat Process failed");
                    if(videoData == selectedVideo) downloadStateText.text = "Ffmpeg Concat Process failed";
                    return;
                }
                try
                {
                    concatProcess?.Dispose();
                } catch { }
                concatFiles.Add("concat.txt");
                foreach (var concatFile in concatFiles)
                {
                    if (File.Exists(concatFile))
                    {
                        File.Delete(concatFile);
                    }
                    else
                    {
                        Plugin.logger.Error($"File: {concatFile} not found");
                    }
                }
                videoData.HasBeenCut = true;
                videoData.cutVideoPath = outFile;
                if(videoData == selectedVideo) downloadStateText.text = "Cutting Offset Done";
                Plugin.logger.Info("Cutting Offset done?");
                if (selectedVideo != videoData) return;
                LoadVideoSettingsIfSameVideo(videoData);
                Plugin.logger.Info("Loaded New Video");
            };
            concatProcess.Start();
            concatProcess.BeginErrorReadLine();
            concatProcess.BeginOutputReadLine();
            yield return new WaitUntil(() =>
            {
                try
                {
                    return concatProcess.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
                catch(Exception e)
                {
                    Plugin.logger.Error(e);
                    return true;
                }
            });
        }

        private static Process MakeFfmpegCutProcess(string start, string length, string videoPath, string levelFolder, string outFile)
        {
            var proc =  new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory + "/Youtube-dl/ffmpeg.exe",
                    Arguments = $"-ss \"{start}\" -t \"{length}\" -i \"{videoPath}\" -c copy \"{outFile}\" -y",
                    WorkingDirectory = levelFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            proc.ErrorDataReceived += (sender, e) =>
            {
                Plugin.logger.Error(e.Data);
            };
            proc.OutputDataReceived += (sender, e) =>
            {
                Plugin.logger.Info(e.Data);
            };
            return proc;
        }

        private static Process MakeFfmpegConcatProcess(string outFile, string levelFolder)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory + "/Youtube-dl/ffmpeg.exe",
                    Arguments = $"-f concat -safe 0 -i ./concat.txt -c copy \"{outFile}\" -y",
                    WorkingDirectory = levelFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            proc.ErrorDataReceived += (sender, e) =>
            {
                Plugin.logger.Error(e.Data);
            };
            proc.OutputDataReceived += (sender, e) =>
            {
                Plugin.logger.Info(e.Data);
            };
            return proc;
        }

        // private string _offsetGuess;
        // private bool isGuessing = false;

        [UIAction("on-guess-offset-action")]
        private void OnGuessOffsetActionWrapper()
        {
            selectedVideo.isGuessing = true;
            StartCoroutine(GuessLoading(selectedVideo));
            StartCoroutine(OnGuessOffsetAction(selectedVideo));
        }

        [UIAction("on-reset-guess-action")]
        private void OnResetGuessAction()
        {
            selectedVideo.ResetGuess();
        }

        private IEnumerator GuessLoading(VideoData videoData)
        {
            var loadingText = "Guessing Offset";
            // downloadStateText.gameObject.SetActive(true);
            var periods = "";
            while (videoData.isGuessing)
            {
                if (periods.Length < 3)
                {
                    periods += '.';
                }
                else
                {
                    periods = "";
                }

                if(videoData == selectedVideo) downloadStateText.SetText(loadingText + periods);

                yield return new WaitForSeconds(0.5f);
            }
        }

        private bool _needsCut = false;

        private IEnumerator OnGuessOffsetAction([CanBeNull] VideoData videoData, string alignOrOffset = "align")
        {
            if (videoData == null) yield break;
            Plugin.logger.Info("Guessing Offset");
            videoData.isGuessing = true;
            var levelFolder = VideoLoader.GetLevelPath(videoData.level);
            string videoAbsolutePath;
            if (videoData.HasBeenCut)
            {
                alignOrOffset = "offset";
                videoAbsolutePath = videoData.FullCutVideoPath;
            }
            else
            {
                videoAbsolutePath = videoData.FullVideoPath;
            }

            CustomPreviewBeatmapLevel customLevel;
            try
            {
                customLevel = (CustomPreviewBeatmapLevel) videoData.level;
            }
            catch
            {
                if(videoData == selectedVideo) downloadStateText.text = "No Official Support Yet";
                Plugin.logger.Debug(videoData.level.GetType().ToString());
                GuessOffsetFailed(videoData);
                throw new NotImplementedException("No Official Support Yet");
            }

            var songAbsolutePath = Path.Combine(levelFolder,
                customLevel.standardLevelInfoSaveData.songFilename);
            var songLength = customLevel.songDuration;
            // Plugin.logger.Debug(Environment.CurrentDirectory + "\\Youtube-dl\\ffmpeg.exe" + $"-i {songAbsolutePath} {mp3SongPath}");
            // Plugin.logger.Debug(Environment.CurrentDirectory + "\\Youtube-dl\\SyncVideoWithAudio\\SyncVideoWithAudio.exe" + $"offest {songAbsolutePath} {videoAbsolutePath}");
            // Process ffmpegProcess = new Process
            // {
            //     StartInfo =
            //     {
            //         FileName = Environment.CurrentDirectory + "\\Youtube-dl\\ffmpeg.exe",
            //         Arguments = $"-i \"{songAbsolutePath}\" \"{mp3SongPath}\" -y",
            //         RedirectStandardOutput = true,
            //         RedirectStandardError = true,
            //         UseShellExecute = false,
            //         CreateNoWindow = true
            //     }
            // };
            // Plugin.logger.Info("Made Process");
            // Plugin.logger.Debug($"{ffmpegProcess.StartInfo.FileName} {ffmpegProcess.StartInfo.Arguments} inside folder {ffmpegProcess.StartInfo.WorkingDirectory}");
            // try
            // {
            //     ffmpegProcess.OutputDataReceived += (sender, e) =>
            //     {
            //         Plugin.logger.Debug(e.Data);
            //         videoData.offsetGuess = e.Data;
            //     };
            //     ffmpegProcess.ErrorDataReceived += (sender, e) =>
            //     {
            //         Plugin.logger.Error(e.Data);
            //         videoData.offsetGuess = e.Data;
            //     };
            //     ffmpegProcess.Exited += (send, eventArgs) =>
            //     {
            //         if (File.Exists(mp3SongPath) && new FileInfo(mp3SongPath).Length > 0)
            //         {
            //             Plugin.logger.Info("Converted song from egg to mp3 -> Can Make Comparison");
            //         }
            //         else
            //         {
            //             Plugin.logger.Error(
            //                 $"Cannot Convert song from egg to mp3 -> Cannot Make Comparison | Check errors by runnning {Environment.CurrentDirectory + "\\Youtube-dl\\ffmpeg.exe"} -i {songAbsolutePath} {mp3SongPath}");
            //         }
            //         Plugin.logger.Info("FFMpeg done");
            //     };
            //     ffmpegProcess.Start();
            //     Plugin.logger.Info("FFMpeg Started");
            // }
            // catch (Exception e)
            // {
            //     Plugin.logger.Error(
            //         $"Cannot Convert song from egg to mp3 -> Cannot Make Comparison | Check that FFmpeg is installed at {Environment.CurrentDirectory + "\\Youtube-dl\\ffmpeg.exe"}");
            //     Plugin.logger.Error(e);
            // }
            //
            // ffmpegProcess.PriorityBoostEnabled = true;
            // Plugin.logger.Debug(ffmpegProcess.HasExited.ToString());
            // yield return new WaitUntil(() => ffmpegProcess.HasExited);
            StartCoroutine(GuessLoading(videoData));
            Plugin.logger.Info("Guessing Offset");
            var offsetProcess = new Process
            {
                StartInfo =
                {
                    FileName = Environment.CurrentDirectory +
                               "\\Youtube-dl\\SyncVideoWithAudio\\SyncVideoWithAudio.exe",
                    Arguments = $"{alignOrOffset} \"{videoAbsolutePath}\" \"{songAbsolutePath}\" \"{songLength}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            Plugin.logger.Info("Made Guess Process");
            Plugin.logger.Debug(
                $"{offsetProcess.StartInfo.FileName} {offsetProcess.StartInfo.Arguments} inside folder {offsetProcess.StartInfo.WorkingDirectory}");
            try
            {
                // offsetProcess.OutputDataReceived += (sender, e) =>
                // {
                //     Plugin.logger.Info(e.Data);
                //     videoData.offsetGuess = e.Data;
                // };
                Plugin.logger.Info("Output");
                offsetProcess.ErrorDataReceived += (sender, e) =>
                {
                    Plugin.logger.Error(e.Data);
                    //videoData.offsetGuess = e.Data.Split(' ')[0];
                };
                Plugin.logger.Info("Error");
                offsetProcess.Exited += (sender, e) =>
                {
                    Plugin.logger.Info("Guess Offset done!");
                    if (offsetProcess.ExitCode != 0)
                    {
                        if(videoData == selectedVideo) downloadStateText.text = "Error Occurred While Guessing";
                    }

                    Plugin.logger.Info($"GO->{videoData.offsetGuess}->BS");
                    Plugin.logger.Info("Done Read");
                    var restOfOutput = offsetProcess.StandardOutput.ReadToEnd();
                    Plugin.logger.Info("Wait");
                    Plugin.logger.Info(restOfOutput);
                    var lineOutput = restOfOutput.Split('\n');
                    foreach (var line in lineOutput)
                    {
                        if (!line.Contains("Results: ")) continue;
                        var trimmedLine = line.Replace("Results: ", "");
                        trimmedLine = trimmedLine.Trim();
                        if (line == "" || line.Length < 2)
                        {
                            continue;
                        }

                        Plugin.logger.Info("line got");
                        Plugin.logger.Info(trimmedLine);
                        var results = trimmedLine.Split(' ');
                        videoData.offsetGuess = results[0];
                        if (!bool.TryParse(results[1], out _needsCut)) _needsCut = false;
                        // Only Execute if aligning (and not using the pre-cut video)
                        if (alignOrOffset == "align")
                        {
                            videoData.needsCut = _needsCut;
                            videoData.cutCommand = string.Join(" ", results.Skip(2).Take(results.Length - 2));
                            var pattern = "\"(.*?)\"";
                            Regex r = new Regex(pattern);
                            MatchCollection mc = r.Matches(videoData.cutCommand);
                            if (mc.Count > 3)
                            {
                                Plugin.logger.Error(
                                    "More than 3 matches, Shits fucked (or you got a bootleg SyncVideoWithAudio.exe)");
                            }

                            for (var i = 0; i < mc.Count && i < 3; i++)
                            {
                                var matchString = mc[i].Value.Substring(1, mc[i].Value.Length - 2);
                                Console.WriteLine(matchString);
                                videoData.cutVideoArgs[i] = matchString;
                            }
                        }

                        Plugin.logger.Info(videoData.offsetGuess);
                    }

                    // Plugin.logger.Info("Done Wait");
                    // Plugin.logger.Info(restOfOutputTask.Result);
                    Plugin.logger.Info("Disposing");
                    try {
                        offsetProcess?.Dispose();
                    } catch { }
                    StartCoroutine(GuessOffsetSucceeded(videoData));
                };
                offsetProcess.Start();
                // offsetProcess.BeginOutputReadLine();
                offsetProcess.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                GuessOffsetFailed(videoData);
                Plugin.logger.Error(
                    @"Cannot Convert song from egg to mp3 -> Cannot Make Comparison | I Don't know What happened");
                Plugin.logger.Error(e);
                yield break;
            }

            // offsetProcess.PriorityBoostEnabled = true;
            //Plugin.logger.Debug(offsetProcess.HasExited.ToString());
            //yield return new WaitUntil(() => offsetProcess.HasExited);
        }

        private void GuessOffsetFailed(VideoData videoData)
        {
            Plugin.logger.Error($"Offset Guess is: {videoData.offsetGuess}");
            videoData.isGuessing = false;
            if(selectedVideo == videoData)
                if(videoData == selectedVideo) downloadStateText.text = "Offset Guess Failed";
        }

        private IEnumerator GuessOffsetSucceeded(VideoData videoData)
        {
            Plugin.logger.Info($"GOS->{videoData.offsetGuess}->Bs");
            Plugin.logger.Info("Finished Guess");
            try
            {
                videoData.offset = (int) double.Parse(videoData.offsetGuess.Trim());
                Plugin.logger.Info(videoData.offset.ToString());
                videoData.needsCut = _needsCut;
                Save(videoData);
                if(selectedVideo == videoData)
                    currentVideoOffsetText.text = videoData.offset.ToString();
            }
            catch (Exception e)
            {
                Plugin.logger.Error(e);
                GuessOffsetFailed(videoData);
                yield break;
            }
            LoadVideoSettingsIfSameVideo(videoData);
            if(videoData == selectedVideo) downloadStateText.text = "Guess Successful and " + (_needsCut ? "Needs Cut" : "No Cut");
            videoData.isGuessing = false;
            Save(videoData);
        }

        private int offsetMagnitude = 100;

        [UIAction("on-offset-magnitude-action")]
        private void OnOffsetMagnitudeAction()
        {
            // isOffsetInSeconds = !isOffsetInSeconds;
            switch (offsetMagnitude)
            {
                case 1000:
                    offsetMagnitude = 100;
                    break;
                case 100:
                    offsetMagnitude = 20;
                    break;
                case 20:
                    offsetMagnitude = 1000;
                    break;
                // default:
                //     offsetMagnitude = 100;
                //     break;
            }

            offsetMagnitudeButtonText.text = $"+{offsetMagnitude}";
        }

        [UIAction("on-offset-decrease-action")]
        private void OnOffsetDecreaseAction()
        {
            UpdateOffset(true);
        }

        [UIAction("on-offset-increase-action")]
        private void OnOffsetIncreaseAction()
        {
            UpdateOffset(false);
        }

        [UIAction("on-delete-video-action")]
        private void OnDeleteVideoAction()
        {
            if (selectedVideo == null) return;
            Plugin.logger.Debug(selectedVideo.downloadState.ToString());
            switch (selectedVideo.downloadState)
            {
                case DownloadState.Queued:
                case DownloadState.Downloading:
                    YouTubeDownloader.Instance.DequeueVideo(selectedVideo);
                    selectedVideo.UpdateDownloadState();
                    break;
                case DownloadState.NotDownloaded:
                case DownloadState.Cancelled:
                    // Download from video.json if only video not there
                    Plugin.logger.Debug("Re-Downloading");
                    //YouTubeDownloader.Instance.EnqueueVideo(selectedVideo);
                    YouTubeDownloader.Instance.StartDownload(selectedVideo, false);
                    //VideoLoader.Instance.AddVideo(selectedVideo);
                    break;
                case DownloadState.Downloaded:
                default:
                    if (VideoLoader.DeleteVideo(selectedVideo, false))
                    {
                        Plugin.logger.Info("Deleted Video");
                    }
                    else
                    {
                        Plugin.logger.Error("Failed to Delete Video");
                    }
                    selectedVideo.UpdateDownloadState();
                    break;
            }
            LoadVideoSettings(selectedVideo);
        }

        [UIAction("on-delete-action")]
        private void OnDeleteAction()
        {
            if (selectedVideo == null) return;
            bool loadNull;
            Plugin.logger.Debug(selectedVideo.downloadState.ToString());
            switch (selectedVideo.downloadState)
            {
                case DownloadState.Downloading:
                case DownloadState.Queued:
                    YouTubeDownloader.Instance.DequeueVideo(selectedVideo);
                    goto default;

                case DownloadState.NotDownloaded:
                case DownloadState.Downloaded:
                case DownloadState.Cancelled:
                default:
                    loadNull = VideoLoader.DeleteVideo(selectedVideo);
                    break;
            }

            if (loadNull)
            {
                LoadVideoSettings(null);
            }
            else
            {
                PrevVideoAction();
            }
        }

        [UIAction("on-add-action")]
        private void OnAddAction()
        {
            if (isPreviewing)
            {
                StopPreview(true);
            }

            if (selectedVideo != null)
            {
                LoadVideoSettings(null, false);
            }
        }

        [UIAction("on-preview-action")]
        private void OnPreviewActionWrapper()
        {
            StartCoroutine(OnPreviewAction());
        }
        
        private IEnumerator OnPreviewAction()
        {
            if (isPreviewing)
            {
                yield return StopPreview(true);
            }
            else
            {
                yield return isPreviewing = true;
                if (!ScreenManager.Instance.videoPlayer.isPrepared)
                {
                    Plugin.logger.Info("Not Prepped yet");
                }
                Plugin.logger.Debug("Prepare CRT");
                yield return ScreenManager.Instance.PrepareVideoCoroutine(selectedVideo); // Prepare Synchronously? ¯\_(ツ)_/¯
                Plugin.logger.Debug("Done Prepping");
                ScreenManager.Instance.PlayVideo(true);
                Plugin.logger.Debug("Playing");
                yield return songPreviewPlayer.volume = 1;
                songPreviewPlayer.CrossfadeTo(selectedLevel.GetPreviewAudioClipAsync(new CancellationToken()).Result, 0,
                    selectedLevel.songDuration, 1f);
            }
            SetPreviewState();
        }

        [UIAction("on-search-action")]
        private void OnSearchAction()
        {
            ChangeView(true);
            searchKeyboard.SetText(selectedLevel.songName + " - " + selectedLevel.songAuthorName);
            parserParams.EmitEvent("show-keyboard");
        }

        [UIAction("on-back-action")]
        private void OnBackAction()
        {
            ChangeView(false);
        }

        [UIAction("on-select-cell")]
        private void OnSelectCell(TableView view, int idx)
        {
            if (customListTableData.data.Count > idx)
            {
                selectedCell = idx;
                downloadButton.interactable = true;
                Plugin.logger.Debug($"Selected Cell: {YouTubeSearcher.searchResults[idx].ToString()}");
            }
            else
            {
                downloadButton.interactable = false;
                selectedCell = -1;
            }
        }

        [UIAction("on-download-action")]
        private void OnDownloadAction()
        {
            Plugin.logger.Debug("Download Pressed");
            if (selectedCell < 0) return;
            downloadButton.interactable = false;
            VideoData data = new VideoData(YouTubeSearcher.searchResults[selectedCell], selectedLevel);
            ChangeView(false);
            //Queueing doesn't really work So let's just download them all simultaneously does it really matter?
            //YouTubeDownloader.Instance.EnqueueVideo(data);
            YouTubeDownloader.Instance.StartDownload(data, false);
            VideoLoader.Instance.AddVideo(data);
            LoadVideoSettings(data);
        }

        // [UIAction("on-download-by-id-action")]
        private void DownloadById(string id)
        {
            downloadButton.interactable = false;
            VideoData data = new VideoData(id, selectedLevel);
            YouTubeDownloader.Instance.StartDownload(data, false);
            VideoLoader.Instance.AddVideo(data);
            LoadVideoSettings(data);
        }

        [UIAction("on-refine-action")]
        private void OnRefineAction()
        {
            OnSearchAction();
        }

        [UIAction("on-query")]
        private void OnQueryAction(string query)
        {
            ResetSearchView();
            downloadButton.interactable = false;
            refineButton.interactable = false;
            if (query.StartsWith("v="))
            {
                DownloadById(query.Replace("v=", ""));
                return;
            }
            StartCoroutine(SearchLoading());

            YouTubeSearcher.Search(query, () =>
            {
                // Shouldn't throw InvalidOperationException but might if searchResults is changed
                updateSearchResultsCoroutine = UpdateSearchResults(YouTubeSearcher.searchResults.ToList());
                StartCoroutine(updateSearchResultsCoroutine);
            });
        }

        #endregion

        #region Youtube Downloader

        // Only use full reload once at end of download
        private void VideoDownloaderDownloadProgress(VideoData video)
        {
            VideoDatas videoDatas = VideoLoader.GetVideos(video.level);
            //check if on the same level AND not on a different video config AND not a blank video config (dumbly)
            //Check for blankness first because otherwise videoDatas can be null
            if (selectedLevel != video.level || videoTitleText.text == "No Video" ||
                videoDatas.videos.IndexOf(video) != videoDatas.activeVideo)
            {
                Plugin.logger.Debug("Not Same Video");
                return;
            }
            LoadVideoDownloadState();
        }

        #endregion

        #region BS Events

        public void HandleDidSelectLevel(LevelCollectionViewController sender, IPreviewBeatmapLevel level)
        {
            ScreenManager.Instance.PauseVideo();
            selectedLevel = level;
            selectedVideo = null;
            ChangeView(false);
            Plugin.logger.Debug($"Selected Level: {level.songName}");
            
            
            if (selectedVideo != null)
            {
                //Don't query YouTube if a video is configured
                return;
            }
            
            if (!Settings.instance.PreloadSearch)
            {
                return;
                
            }
            
            //Get Results but only pass if string is the same
            StartCoroutine(YouTubeSearcher.SearchYoutubeWithMyExeCoroutine($"{selectedLevel.songName} - {selectedLevel.songAuthorName}", null, 15));
        }

        private void GameSceneLoaded()
        {
            StopAllCoroutines();
            
            if (BS_Utils.Plugin.LevelData.Mode == Mode.Multiplayer)
            {
                Plugin.logger.Debug("Detected multiplayer, disabling");
                ScreenManager.Instance.HideScreen();
                return;
            }
            ScreenManager.Instance.TryPlayVideo();
        }

        #endregion

        #region Events

        private void MissionSelectionDidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            selectedVideo = null;
            selectedLevel = null;
            Activate();
        }

        private void StatusViewerDidEnable(object sender, EventArgs e)
        {
            Activate();
        }

        private void StatusViewerDidDisable(object sender, EventArgs e)
        {
            Deactivate();
        }

        #endregion

        #region Classes

        public class VideoMenuStatus : MonoBehaviour
        {
            public event EventHandler DidEnable;
            public event EventHandler DidDisable;

            void OnEnable()
            {
                var handler = DidEnable;

                handler?.Invoke(this, EventArgs.Empty);
            }

            void OnDisable()
            {
                var handler = DidDisable;

                handler?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}