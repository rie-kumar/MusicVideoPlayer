﻿using BS_Utils.Utilities;
using MusicVideoPlayer.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ModestTree;
using MusicVideoPlayer.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using VRUIControls;

namespace MusicVideoPlayer
{
    public class ScreenManager : MonoBehaviour
    {
        // I don't care enough to use this and be safe
        // private static ScreenManager _instance;

        public static ScreenManager Instance;
        // {
        //     get
        //     {
        //         if (_instance != null)
        //         {
        //             return Instance;
        //         }
        //         else
        //         {
        //             ScreenManager.OnLoad();
        //             return _instance;
        //         }
        //     }
        //     set => _instance = value;
        // }

        public static bool showVideo = true;
        public static bool rotateIn360 = true;
        public static bool playPreviewAudio = false;
        public VideoPlacement placement;

        private VideoData currentVideo;
        private GameObject screen;
        private Renderer vsRenderer;
        private Shader glowShader;
        private Color _onColor = Color.white.ColorWithAlpha(0) * 0.85f;

        public VideoPlayer videoPlayer;
        private AudioTimeSyncController syncController;
        private float offsetSec = 0f;
        private bool hasVideo => currentVideo == null || currentVideo.downloadState != DownloadState.Downloaded;

        private EnvironmentSpawnRotation _envSpawnRot;

        public EnvironmentSpawnRotation instanceEnvironmentSpawnRotation
        {
            get
            {
                if (_envSpawnRot == null)
                    _envSpawnRot = Resources.FindObjectsOfTypeAll<EnvironmentSpawnRotation>().LastOrDefault();
                return _envSpawnRot;
            }
        }

        public static void OnLoad()
        {
            Plugin.logger.Debug("OnLoad: ScreenManager");
            if (Instance == null)
                new GameObject("VideoManager").AddComponent<ScreenManager>();
        }

        void Start()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            showVideo = Settings.instance.ShowVideoSettings;
            rotateIn360 = Settings.instance.RotateIn360;
            playPreviewAudio = Settings.instance.PlayPreviewAudio;
            placement = Settings.instance.PlacementMode;

            BSEvents.songPaused += PauseVideo;
            BSEvents.songUnpaused += ResumeVideo;
            BSEvents.lateMenuSceneLoadedFresh += OnMenuSceneLoadedFresh;
            BSEvents.menuSceneLoaded += OnMenuSceneLoaded;

            DontDestroyOnLoad(gameObject);

            CreateScreen();
            Plugin.logger.Info("Adding Rotate Thingy");
        }


        void Update()
        {
            if (screen == null) return;
            vsRenderer.material.SetTexture("_MainTex", videoPlayer.texture);
        }

        void CreateScreen()
        {
            screen = new GameObject("Screen");
            screen.AddComponent<BoxCollider>().size = new Vector3(16f / 9f + 0.1f, 1.1f, 0.1f);
            screen.transform.parent = transform;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (body.GetComponent<Collider>() != null) Destroy(body.GetComponent<Collider>());
            body.transform.parent = screen.transform;
            body.transform.localPosition = new Vector3(0, 0, 0.1f);
            body.transform.localScale = new Vector3(16f / 9f + 0.1f, 1.1f, 0.1f);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.material = new Material(Resources.FindObjectsOfTypeAll<Material>()
                .Last(x =>
                    x.name == "DarkEnvironmentSimple")); // finding objects is wonky because platforms hides them

            GameObject videoScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            if (videoScreen.GetComponent<Collider>() != null) Destroy(videoScreen.GetComponent<Collider>());
            videoScreen.transform.parent = screen.transform;
            videoScreen.transform.localPosition = Vector3.zero;
            videoScreen.transform.localScale = new Vector3(16f / 9f, 1, 1);
            vsRenderer = videoScreen.GetComponent<Renderer>();
            vsRenderer.material = new Material(GetShader()) {color = Color.clear};

            screen.transform.position = VideoPlacementSetting.Position(placement);
            screen.transform.eulerAngles = VideoPlacementSetting.Rotation(placement);
            screen.transform.localScale = VideoPlacementSetting.Scale(placement) * Vector3.one;
            screenSoftParentRotation = screen.AddComponent<SoftParent>();

            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialProperty = "_MainTex";
            videoPlayer.playOnAwake = false;
            videoPlayer.targetMaterialRenderer = vsRenderer;
            vsRenderer.material.SetTexture("_MainTex", videoPlayer.texture);
            videoPlayer.errorReceived += VideoPlayerErrorReceived;
            videoPlayer.prepareCompleted += source => source.Pause();
            videoPlayer.waitForFirstFrame = true;

            OnMenuSceneLoaded();
            BSEvents.levelQuit += unsetTransformParent;
            BSEvents.levelCleared += unsetTransformParent;
            BSEvents.levelFailed += unsetTransformParent;
            BSEvents.levelRestarted += unsetTransformParent;
        }

        private void unsetTransformParent(StandardLevelScenesTransitionSetupDataSO slstsd, LevelCompletionResults lcr)
        {
            screen.transform.parent = null;
        }

        private void OnMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenesTransition)
        {
            if (currentVideo != null) PrepareVideo(currentVideo);
            PauseVideo();
            HideScreen();
        }

        private void OnMenuSceneLoaded()
        {
            if (currentVideo != null) PrepareVideo(currentVideo);
            PauseVideo();
            HideScreen();
        }

        public void TryPlayVideo()
        {
            StartCoroutine(WaitForAudioSync());
        }

        private static void VideoPlayerErrorReceived(VideoPlayer source, string message)
        {
            if (message == "Can't play movie []") return;
            Plugin.logger.Debug("Video player error: " + message);
            // File.AppendAllText("video errors.log", "\n" + message);
        }

        private IEnumerator prepareVideoCoroutine;
        public void PrepareVideo(VideoData video)
        {
            if(prepareVideoCoroutine != null)
                StopCoroutine(prepareVideoCoroutine);
            prepareVideoCoroutine = PrepareVideoCoroutine(video);
            StartCoroutine(prepareVideoCoroutine);
        }

        public void PrepareVideoSync(VideoData video)
        {
            if(prepareVideoCoroutine != null)
                StopCoroutine(prepareVideoCoroutine);
            prepareVideoCoroutine = PrepareVideoCoroutine(video);
            while (prepareVideoCoroutine.MoveNext())
            {
            }
        }

        // private VideoPlayer.EventHandler videoPlayerOnprepareCompleted;
        public IEnumerator PrepareVideoCoroutine(VideoData video)
        {
            // if (videoPlayerOnprepareCompleted != null) videoPlayer.prepareCompleted -= videoPlayerOnprepareCompleted;
            currentVideo = video;
            if (video == null)
            {
                videoPlayer.url = null;
                vsRenderer.material.color = Color.clear;
                try
                {
                    videoPlayer.Prepare();
                    // Plugin.logger.Notice("Prepared null video");
                }
                catch
                {
                    Plugin.logger.Notice("Oops guess I can't prepare null video");
                }

                yield break;
            }

            videoPlayer.source = UnityEngine.Video.VideoSource.Url;
            videoPlayer.Pause();
            if (video.isLocal)
            {
                Plugin.logger.Info($"Video is Local file");
                if (video.downloadState != DownloadState.Downloaded) yield break;
                videoPlayer.isLooping = video.loop;

                Plugin.logger.Info($"Video has been cut: {video.HasBeenCut}");
                string videoPath;
                yield return
                    (videoPath = video.CorrectVideoPath); // VideoLoader.GetVideoPath(video, video.HasBeenCut));
                Plugin.logger.Info($"Loading video: {videoPath}");
                videoPlayer.Pause();
                var lockTimer = new Stopwatch();
                lockTimer.Start();
                var videoFileInfo = new FileInfo(videoPath);
                var lockWaitTicks = 6 * TimeSpan.TicksPerSecond;
                if (videoPlayer.url != videoPath)
                {
                    yield return new WaitUntil(() =>
                        !IsFileLocked(videoFileInfo) || lockTimer.ElapsedTicks > lockWaitTicks);
                    yield return (videoPlayer.url = videoPath);
                }

                lockTimer.Stop();
                if (lockTimer.ElapsedTicks > lockWaitTicks && IsFileLocked(videoFileInfo))
                {
                    var exception = new Exception("File Locked");
                    Plugin.logger.Error(exception);
                    throw exception;
                }

                yield return offsetSec = video.offset / 1000f; // ms -> s
                var correctVideoTime = video.offset >= 0 ? offsetSec : 0;

                var correctVideoFrame = (int) Math.Round(videoPlayer.frameRate * correctVideoTime);
                yield return (vsRenderer.material.color = Color.clear);
                videoPlayer.Prepare();
                // videoPlayerOnprepareCompleted = source => SeekVideoToTime(correctVideoTime);
                // videoPlayer.prepareCompleted += videoPlayerOnprepareCompleted;
                Plugin.logger.Debug("Preparing");
                yield return new WaitUntil(() =>
                {
                    // Plugin.logger.Debug($"{(videoPlayer.isPrepared ? "Prepared" : "Not Prepared")}\t{(videoPlayer.canSetTime ? "canSetTime" : "Not canSetTime")}");
                    return videoPlayer.isPrepared;
                });
                Plugin.logger.Info("Prepared video");
                // Plugin.logger.Debug($"{(videoPlayer.isPrepared ? "Prepared" : "Not Prepared")}\t{(videoPlayer.canSetTime ? "canSetTime" : "Not canSetTime")}");
                // Plugin.logger.Debug("Seeking");
                // while (Math.Abs(videoPlayer.time - correctVideoTime) > .050 || (videoPlayer.frame - correctVideoFrame) > 2)
                // {
                yield return SeekVideoToTime(correctVideoTime);
                // }

                Plugin.logger.Info(
                    $"Times are {videoPlayer.time}:{correctVideoTime}\tFrames are {videoPlayer.frame}:{correctVideoFrame}\t{(videoPlayer.isPrepared ? "Prepared" : "Not Prepared")}\t{(videoPlayer.isPaused ? "Paused" : "Not Paused")}");
            }
            else
            {
                Plugin.logger.Info($"Video is located at {video.videoPath}");
                videoPlayer.url = video.videoPath; // set url to the non-local path
                yield return offsetSec = video.offset / 1000f; // ms -> s
                var correctVideoTime = video.offset >= 0 ? offsetSec : 0;

                var correctVideoFrame = (int)Math.Round(videoPlayer.frameRate * correctVideoTime);
                yield return (vsRenderer.material.color = Color.clear);
                videoPlayer.Prepare();
                // videoPlayerOnprepareCompleted = source => SeekVideoToTime(correctVideoTime);
                // videoPlayer.prepareCompleted += videoPlayerOnprepareCompleted;
                Plugin.logger.Debug("Preparing");
                yield return new WaitUntil(() =>
                {
                    // Plugin.logger.Debug($"{(videoPlayer.isPrepared ? "Prepared" : "Not Prepared")}\t{(videoPlayer.canSetTime ? "canSetTime" : "Not canSetTime")}");
                    return videoPlayer.isPrepared;
                });
                Plugin.logger.Info("Prepared video");
                // Plugin.logger.Debug($"{(videoPlayer.isPrepared ? "Prepared" : "Not Prepared")}\t{(videoPlayer.canSetTime ? "canSetTime" : "Not canSetTime")}");
                // Plugin.logger.Debug("Seeking");
                // while (Math.Abs(videoPlayer.time - correctVideoTime) > .050 || (videoPlayer.frame - correctVideoFrame) > 2)
                // {
                yield return SeekVideoToTime(correctVideoTime);
                // }

                Plugin.logger.Info(
                    $"Times are {videoPlayer.time}:{correctVideoTime}\tFrames are {videoPlayer.frame}:{correctVideoFrame}\t{(videoPlayer.isPrepared ? "Prepared" : "Not Prepared")}\t{(videoPlayer.isPaused ? "Paused" : "Not Paused")}");

            }
        }

        private IEnumerator SeekVideoToTime(float correctVideoTime)
        {
            // int correctVideoFrame = (int)Math.Round(videoPlayer.frameRate * correctVideoTime);
            // Plugin.logger.Debug($"Times are {videoPlayer.time}:{correctVideoTime}\tFrames are {videoPlayer.frame}:{correctVideoFrame}\t{(videoPlayer.isPrepared ? "Prepared" : "NotPrepared")}\t{(videoPlayer.canSetTime ? "canSetTime" : "Not canSetTime")}");
            yield return videoPlayer.time = correctVideoTime;
            // if (!videoPlayer.isPrepared) videoPlayer.Prepare();
            videoPlayer.Pause();
        }

        private static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                stream?.Close();
            }

            return false;
        }

        private SoftParent screenSoftParentRotation;
        public void PlayVideo(bool preview)
        {
            if (hasVideo || (!showVideo && !preview)) //If the current video is null or not downloaded or show video is off AND it isn't a preview hide the screen
            {
                HideScreen();
                return;
            }

            ShowScreen();
            vsRenderer.material.color = _onColor;
            float practiceSettingsSongStart = 0;
            if (!preview)
            {

                try // Try to get these, as errors happen when only previewing (and they are unnecessary)
                {
                    try // Try to get these as there will be a null reference if not in practice mode or only previewing
                    {
                        practiceSettingsSongStart =
                            BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings.startSongTime - 1;
                        if (practiceSettingsSongStart < 0)
                        {
                            practiceSettingsSongStart = 0;
                        }
                    }
                    catch (NullReferenceException)
                    {
                        practiceSettingsSongStart = 0;
                    }

                    float songSpeed = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.gameplayModifiers.songSpeedMul;
                    videoPlayer.playbackSpeed =
                        BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings != null
                            ? BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings.songSpeedMul
                            : songSpeed; // Set video speed to practice or non-practice speed

                    if (offsetSec + practiceSettingsSongStart > 0)
                    {
                        videoPlayer.time = offsetSec + practiceSettingsSongStart;
                    }
                    else
                    {
                        videoPlayer.time = 0;
                    }
                }
                catch (Exception e)
                {
                    Plugin.logger.Debug("Probably cause previews don't have speed mults");
                    Plugin.logger.Error(e.ToString());
                }

                for (ushort track = 0;
                    track < videoPlayer.audioTrackCount;
                    track++) // For Each Track -> Decrease Audio volume to 0 on that track
                {
                    // videoPlayer.SetDirectAudioMute(track, true);
                    videoPlayer.SetDirectAudioVolume(track, 0f);
                }

                if (rotateIn360)
                {
                    try
                    {
                        Plugin.logger.Debug(BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.environmentInfo.environmentType.name);
                        CoreGameHUDController cgh = Resources.FindObjectsOfTypeAll<CoreGameHUDController>()
                            .LastOrDefault(x => x.isActiveAndEnabled);
                        screenSoftParentRotation.AssignParent(cgh.transform);
                    }
                    catch { }
                    //Null reference if done in preview
                    screenSoftParentRotation.enabled = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.environmentInfo.environmentType.name == "CircleEvironmentType";
                }
            }
            else
            {
                videoPlayer.time = offsetSec > 0 ? offsetSec : 0;
                videoPlayer.playbackSpeed = 1;
                //TODO: Make Left Ear Audio the Preview and Right Ear Audio the BeatMap
                //ushort videoTrack = 1;
                for (ushort track = 0;
                    track < videoPlayer.audioTrackCount;
                    track++) // For Each Track -> Increase Audio volume to .5 (float) on that track
                {
                    //if (track != videoTrack) { videoPlayer.SetDirectAudioVolume(track, 0f); continue;}
                    bool failed = false;
                    try
                    {
                        videoPlayer.SetDirectAudioVolume(track, playPreviewAudio ? .8f : 0f);
                        Plugin.logger.Debug($"Channels: {videoPlayer.GetAudioChannelCount(track)}");
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                    Plugin.logger.Debug($"Track: {track} {(failed ? "Failed" : "Succeeded")}");
                }
                Plugin.logger.Debug("Done lower volume");
            }
            Plugin.logger.Debug("Offset for video: " + offsetSec);
            StopAllCoroutines();
            StartCoroutine(StartVideoDelayed(preview ? -offsetSec : -(offsetSec + practiceSettingsSongStart), preview));
        }

        private IEnumerator WaitForAudioSync()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());
            syncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Last();

            if (IsVideoPlayable())
            {
                Plugin.logger.Debug("Video is playing!");

                if (Math.Abs(videoPlayer.time - offsetSec) > 0.050)
                {
                    // game was restarted
                    if (currentVideo.offset >= 0)
                    {
                        videoPlayer.time = offsetSec;
                    }
                    else
                    {
                        videoPlayer.time = 0;
                    }
                }

                ShowScreen();
                SetPlacement(Settings.instance.PlacementMode);
                PlayVideo(false);
            }
            else
            {
                Plugin.logger.Debug("Video could not be found!");
                HideScreen();
            }
        }

        private IEnumerator StartVideoDelayed(float startTime, bool preview)
        {
            // Wait
            if (preview)
            {
                if (startTime >= 0)
                {
                    Stopwatch startStopwatch = new Stopwatch();
                    startStopwatch.Start();
                    videoPlayer.frame = 0;
                    var startTicks = startTime * TimeSpan.TicksPerSecond;
                    yield return new WaitUntil(() => startStopwatch.ElapsedTicks >= startTicks);
                }
            }
            else
            {
                yield return new WaitUntil(() => syncController.songTime >= startTime);
            }

            // Time has elapsed, start video
            // frames are short enough that we shouldn't notice imprecise start time
            videoPlayer.Play();
        }

        public void PauseVideo()
        {
            StopAllCoroutines();
            if (hasVideo) return;
            if (videoPlayer.isPlaying) videoPlayer.Pause();
        }

        public void ResumeVideo()
        {
            if (hasVideo) return;
            if (!videoPlayer.isPlaying) videoPlayer.Play();
        }

        public void ShowScreen()
        {
            screen.SetActive(true);
        }

        public void HideScreen()
        {
            screen.SetActive(false);
        }

        public void SetScale(Vector3 scale)
        {
            if (Instance.screen == null) return;
            screen.transform.localScale = scale;
        }

        public void SetPosition(Vector3 pos)
        {
            if (Instance.screen == null) return;
            screen.transform.position = pos;
        }

        public void SetRotation(Vector3 rot)
        {
            if (Instance.screen == null) return;
            screen.transform.eulerAngles = rot;
        }

        public void SetPlacement(VideoPlacement placement)
        {
            this.placement = placement;
            if (Instance.screen == null) return;
             // screen.GetComponent<SoftParent>() != 
            screen.transform.position = VideoPlacementSetting.Position(placement);
            screen.transform.eulerAngles = VideoPlacementSetting.Rotation(placement);
            screen.transform.localScale = VideoPlacementSetting.Scale(placement) * Vector3.one;
        }

        public bool IsVideoPlayable()
        {
            return currentVideo != null && currentVideo.downloadState == DownloadState.Downloaded;
        }

        public Shader GetShader()
        {
            if (glowShader != null) return glowShader;
            // load shader

            var myLoadedAssetBundle = AssetBundle.LoadFromMemory(
                UIUtilities.GetResource(Assembly.GetExecutingAssembly(), "MusicVideoPlayer.Resources.mvp.bundle"));

            Shader shader = myLoadedAssetBundle.LoadAsset<Shader>("ScreenGlow");
            myLoadedAssetBundle.Unload(false);
            glowShader = shader;
            return shader;
        }

        //Function to run when rotation changes in 360/90 mode
        //Will never be called if not in those modes
        //Changes video rotation to match where you are looking in 360/90
        public void ChangeRotation360(Quaternion quaternion)
        {
            // Plugin.logger.Debug($"Song Time: {syncController.songTime}");
            // screen.transform.rotation = Quaternion.Slerp(transform.rotation, quaternion, Time.deltaTime * 5.0f);
            // screen.transform.eulerAngles = new Vector3(screen.transform.eulerAngles.x, quaternion.eulerAngles.y, screen.transform.eulerAngles.z); // Set screen rotation relative to itself
            // screen.transform.position = new Vector3(quaternion.eulerAngles.y, screen.transform.position.y, screen.transform.position.z); // Set screen rotation relative to you
        }
    }
}