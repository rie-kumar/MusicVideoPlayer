﻿using BS_Utils.Utilities;
using MusicVideoPlayer.UI;
using MusicVideoPlayer.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ModestTree;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using VRUIControls;

namespace MusicVideoPlayer
{
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance;

        public static bool showVideo = true;
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

        private EnvironmentSpawnRotation _envSpawnRot;

        public EnvironmentSpawnRotation instanceEnvironmentSpawnRotation
        {
            get
            {
                if (_envSpawnRot == null)
                    _envSpawnRot = Resources.FindObjectsOfTypeAll<EnvironmentSpawnRotation>().FirstOrDefault();
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

            showVideo = MVPSettings.instance.ShowVideoSettings;
            playPreviewAudio = MVPSettings.instance.PlayPreviewAudio;
            placement = MVPSettings.instance.PlacementMode;

            BSEvents.songPaused += PauseVideo;
            BSEvents.songUnpaused += ResumeVideo;
            BSEvents.lateMenuSceneLoadedFresh += OnMenuSceneLoadedFresh;
            BSEvents.menuSceneLoaded += OnMenuSceneLoaded;

            DontDestroyOnLoad(gameObject);

            CreateScreen();
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
                .First(x =>
                    x.name == "DarkEnvironmentSimple")); // finding objects is wonky because platforms hides them

            GameObject videoScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            if (videoScreen.GetComponent<Collider>() != null) Destroy(videoScreen.GetComponent<Collider>());
            videoScreen.transform.parent = screen.transform;
            videoScreen.transform.localPosition = Vector3.zero;
            videoScreen.transform.localScale = new Vector3(16f / 9f, 1, 1);
            vsRenderer = videoScreen.GetComponent<Renderer>();
            vsRenderer.material = new Material(GetShader());
            vsRenderer.material.color = Color.clear;

            screen.transform.position = VideoPlacementSetting.Position(placement);
            screen.transform.eulerAngles = VideoPlacementSetting.Rotation(placement);
            screen.transform.localScale = VideoPlacementSetting.Scale(placement) * Vector3.one;

            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialProperty = "_MainTex";
            videoPlayer.playOnAwake = false;
            videoPlayer.targetMaterialRenderer = vsRenderer;
            vsRenderer.material.SetTexture("_MainTex", videoPlayer.texture);
            videoPlayer.errorReceived += VideoPlayerErrorReceived;
            //videoPlayer.prepareCompleted += VideoPrepared;

            OnMenuSceneLoaded();
        }

        //private void VideoPrepared(VideoPlayer source)
        //{
        //    source.
        //}

        private void OnMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenesTransition)
        {
            if (currentVideo != null) PrepareVideo(currentVideo);
            PauseVideo();
            //HideScreen();
        }

        private void OnMenuSceneLoaded()
        {
            if (currentVideo != null) PrepareVideo(currentVideo);
            PauseVideo();
            //HideScreen();
        }

        public void TryPlayVideo()
        {
            StartCoroutine(WaitForAudioSync());
        }

        private void VideoPlayerErrorReceived(VideoPlayer source, string message)
        {
            if (message == "Can't play movie []") return;
            Plugin.logger.Warn("Video player error: " + message);
        }

        public void PrepareVideo(VideoData video)
        {
            currentVideo = video;
            if (video == null)
            {
                videoPlayer.url = null;
                vsRenderer.material.color = Color.clear;
                return;
            }

            if (video.downloadState != DownloadState.Downloaded) return;
            videoPlayer.isLooping = video.loop;

            string videoPath = VideoLoader.Instance.GetVideoPath(video);
            videoPlayer.Pause();
            if (videoPlayer.url != videoPath) videoPlayer.url = videoPath;
            offsetSec = video.offset / 1000f; // ms -> s
            if (video.offset >= 0)
            {
                videoPlayer.time = offsetSec;
            }
            else
            {
                videoPlayer.time = 0;
            }

            if (!videoPlayer.isPrepared) videoPlayer.Prepare();
            vsRenderer.material.color = Color.clear;
            videoPlayer.Pause();
        }

        public void PlayVideo(bool preview)
        {
            if (currentVideo == null || currentVideo.downloadState != DownloadState.Downloaded || (!showVideo && !preview)) //If the current video is null or not downloaded or show video is off AND it isn't a preview hide the screen
            {
                HideScreen();
                return;
            }

            ShowScreen();
            vsRenderer.material.color = _onColor;
            float practiceSettingsSongStart = 0;
            if (!preview)
            {
                if (instanceEnvironmentSpawnRotation != null) // will be null when previewing
                {
                    //instanceEnvironmentSpawnRotation.didRotateEvent += ChangeRotation360; // Set up event for running 360 video (will simply do nothing for regular video)
                    try // Try to get these as errors happen when only previewing (and they are unnecessary)
                    {
                        try // Try to get these as there will be a null reference if not in practice mode or only previewing
                        {
                            practiceSettingsSongStart = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.practiceSettings.startSongTime - 1;
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
                }

                for (ushort track = 0; track < videoPlayer.audioTrackCount; track++) // For Each Track -> Decrease Audio volume to 0 on that track
                {
                    // videoPlayer.SetDirectAudioMute(track, true);
                    videoPlayer.SetDirectAudioVolume(track, 0f);
                }
            }
            else
            {
                videoPlayer.time = offsetSec > 0 ? offsetSec : 0;
                videoPlayer.playbackSpeed = 1;
                //TODO: Make Left Ear Audio the Preview and Right Ear Audio the BeatMap
                //ushort videoTrack = 1;
                for (ushort track = 0; track < videoPlayer.audioTrackCount; track++) // For Each Track -> Increase Audio volume to .5 (float) on that track
                {
                    //if (track != videoTrack) { videoPlayer.SetDirectAudioVolume(track, 0f); continue;}
                    videoPlayer.SetDirectAudioVolume(track, playPreviewAudio ? .8f : 0f);
                    Plugin.logger.Info($"Track: {track}");
                    Plugin.logger.Info($"Channels: {videoPlayer.GetAudioChannelCount(track)}");

                    // videoPlayer.SetDirectAudioMute(track, false);
                }

                //int previewTracks = VideoMenu.songPreviewPlayer.GetPrivateField<int>("_channelsCount");
                //VideoMenu.songPreviewPlayer.SetPrivateField("_channelsCount", 1);
                //VideoMenu.songPreviewPlayer.Update();
            }

            Plugin.logger.Debug("Offset for video: " + offsetSec);
            StopAllCoroutines();
            StartCoroutine(StartVideoDelayed(preview ? -offsetSec : -(offsetSec + practiceSettingsSongStart), preview));
        }

        private IEnumerator WaitForAudioSync()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());
            syncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().First();

            SetPlacement(MVPSettings.instance.PlacementMode);

            if (IsVideoPlayable())
            {
                Plugin.logger.Debug("Video is playing!");

                if (videoPlayer.time != offsetSec)
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
            float timeElapsed = 0;
            Plugin.logger.Debug(videoPlayer.time.ToString());

            if (preview)
            {
                if (startTime < 0)
                {
                    videoPlayer.Play();
                    yield break;
                }
                videoPlayer.frame = 0;
                while (timeElapsed < startTime)
                {
                    timeElapsed += Time.deltaTime;
                    yield return null;
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
            if (videoPlayer == null) return;
            if (videoPlayer.isPlaying) videoPlayer.Pause();
        }

        public void ResumeVideo()
        {
            if (videoPlayer == null) return;
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
            screen.transform.position = VideoPlacementSetting.Position(placement);
            screen.transform.eulerAngles = VideoPlacementSetting.Rotation(placement);
            screen.transform.localScale = VideoPlacementSetting.Scale(placement) * Vector3.one;
        }

        public bool IsVideoPlayable()
        {
            if (currentVideo == null || currentVideo.downloadState != DownloadState.Downloaded)
                return false;

            return true;
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
            // screen.transform.eulerAngles = new Vector3(screen.transform.eulerAngles.x, quaternion.eulerAngles.y, screen.transform.eulerAngles.z); // Set screen rotation relative to itself
            // screen.transform.position = new Vector3(quaternion.eulerAngles.y, screen.transform.position.y, screen.transform.position.z); // Set screen rotation relative to you
        }
    }
}