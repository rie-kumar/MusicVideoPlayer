# MusicVideoPlayer
An IPA plugin for playing videos inside BeatSaber

# Usage

Select a song and difficulty and a "Mod" menu will appear on the left side of the screen on top of the modifiers tab. This takes you to the video detail view for the currently selected song. 

If a video already has a configution a "Re-Download" button will appear and allow you to download the video the Map Maker pre-configured for the map

To begin adding a video, click search. A list of videos will appear, based on the chosen song's title and author. If these results aren't satisfactory use the refine button to type your own search terms. Select a video and press download. The video will be added to a queue and you can see the download progress in both the detail view and song list.

When the video has downloaded, the preview and offset buttons will allow you to fine tune the video offset to synchronise the beatmap audio to the downloaded video.

[Demo: Downloading a video](https://streamable.com/hnmvy2)

Download quality can be adjusted, but it is not recommended to go above Medium (720p) because of the distance of the screen from you and the size of the screen any higher than this will net minimal results for 3-4x the file size (1080p). You can also select a screen position from a set of presets.


**Install Instructions:**
Extract the contents of the release zip to your Beat Saber Directory, merge folders as necessary.
Requires BSUtils & BSML.

# Features:
**Play Videos in Beat Saber**
* A custom json file `video.json` can be used in a song directory to detail a video (details below)
* Create multiple configs with different videos for the same song
* These are automatically created for downloaded files
* Compatible with a wide range of video formats (check VideoPlayer Unity docs)
* Supports manual sync, looping, thumbnails and metadata

**Download Videos in game**
* Use the provided keyboard or the shortcut buttons to fill in your search query
* Adjustable video quality settings
* `video.json` can be shared and used to download the same video as another user (mappers can share `video.json` and players can automatically download the correct video and information)
* This plugin uses the application youtube-dl to do the heavy lifting, and will keep it up to date every launch

**Sample video.json**
```json
{
  "activeVideo": 0,
  "videos": [
    {
      "title": "[MV] GFRIEND(여자친구) _ Me Gustas Tu(오늘부터 우리는) (Choreography Ver.)",
      "author": "1theK (원더케이) ",
      "description": "[MV] GFRIEND(여자친구) _ Me Gustas Tu(오늘부터 우리는) (Choreography Ver.)*****Hello, this is 1theK. We are working on ...",
      "duration": "3:44",
      "URL": "/watch?v=oixRBiOteWY",
      "thumbnailURL": "https://i.ytimg.com/vi/oixRBiOteWY/hqdefault.jpg?sqp=-oaymwEjCPYBEIoBSFryq4qpAxUIARUAAAAAGAElAADIQj0AgKJDeAE=&amp;rs=AOn4CLDJcaceiJ0EGqHJN8jgKOmzqkEiSg",
      "loop": false,
      "offset": -100,
      "videoPath": "[MV] GFRIEND(여자친구) _ Me Gustas Tu(오늘부터 우리는) (Choreography Ver.).mp4"
    },
    {
      "title": "여자친구 GFRIEND - 오늘부터 우리는 Me gustas tu M/V",
      "author": "여자친구 GFRIEND OFFICIAL ",
      "description": "여자친구 GFRIEND - 오늘부터 우리는(Me gustas tu) M/V.",
      "duration": "4:12",
      "URL": "/watch?v=YYHyAIFG3iI",
      "thumbnailURL": "https://i.ytimg.com/vi/YYHyAIFG3iI/hqdefault.jpg?sqp=-oaymwEjCPYBEIoBSFryq4qpAxUIARUAAAAAGAElAADIQj0AgKJDeAE=&amp;rs=AOn4CLDq4Cvxyo87B8rZNE2f8OYY8w9Fbg",
      "loop": false,
      "offset": 18600,
      "videoPath": "여자친구 GFRIEND - 오늘부터 우리는 Me gustas tu M-V.mp4"
    },
    {
      "title": "GFRIEND - Me gustas tu - mirrored dance practice video - 여자친구 오늘부터 우리는",
      "author": "HQVideoCentralNet",
      "description": "GFRIEND - Me gustas tu - mirrored dance practice 여자친구 오늘부터 우리는 (C) 2015 Source Music iTunes ...",
      "duration": "3:44",
      "URL": "/watch?v=huIWmnAT7BI",
      "thumbnailURL": "https://i.ytimg.com/vi/huIWmnAT7BI/hqdefault.jpg?sqp=-oaymwEjCPYBEIoBSFryq4qpAxUIARUAAAAAGAElAADIQj0AgKJDeAE=&amp;rs=AOn4CLCkdhbiD5iEjaqPEYxivGUhwGEXSw",
      "loop": false,
      "offset": -200,
      "videoPath": "GFRIEND - Me gustas tu - mirrored dance practice video - 여자친구 오늘부터 우리는.mp4"
    }
  ],
  "Count": 3
}
```
