using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

public class VideoGridFullScreen : MonoBehaviour
{
    [Header("Grid Setup")]
    public GameObject gridPanel;
    public Button[] previewButtons = new Button[4];
    public Texture2D[] thumbnails = new Texture2D[4];
    public VideoClip[] videoClips = new VideoClip[4];

    [Header("Full-Screen Playback")]
    public GameObject fullScreenPanel;
    public RawImage fullScreenRawImage;
    public VideoPlayer videoPlayer;
    public Button closeButton;

    [Header("Playback Controls")]
    public Slider scrubSlider;
    public Button playPauseButton;

    private Text playPauseButtonText;
    private bool isScrubbing = false;

    private void Start()
    {
        playPauseButtonText = playPauseButton.GetComponentInChildren<Text>();

        for (int i = 0; i < previewButtons.Length; i++)
        {
            int index = i;
            var thumbImage = previewButtons[i].GetComponentInChildren<RawImage>();
            if (thumbImage != null && thumbnails.Length > index)
                thumbImage.texture = thumbnails[index];

            previewButtons[i].onClick.AddListener(() => OnPreviewClicked(index));
        }

        closeButton.onClick.AddListener(CloseFullScreen);
        playPauseButton.onClick.AddListener(TogglePlayPause);
        scrubSlider.onValueChanged.AddListener(OnScrubSliderChanged);

        var trigger = scrubSlider.gameObject.AddComponent<EventTrigger>();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((_) => isScrubbing = true);
        trigger.triggers.Add(downEntry);

        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((_) => isScrubbing = false);
        trigger.triggers.Add(upEntry);

        fullScreenPanel.SetActive(false);
    }

    private void OnPreviewClicked(int index)
    {
        gridPanel.SetActive(false);
        fullScreenPanel.SetActive(true);

        videoPlayer.Stop();
        videoPlayer.clip = (videoClips.Length > index ? videoClips[index] : null);
        videoPlayer.renderMode = VideoRenderMode.APIOnly;

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.prepareCompleted += OnVideoPrepared;

        videoPlayer.Prepare();

        scrubSlider.value = 0f;
        SetButtonText("Pause");
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        fullScreenRawImage.texture = vp.texture;
        vp.Play();
    }

    private void TogglePlayPause()
    {
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            SetButtonText("Play");
        }
        else
        {
            videoPlayer.Play();
            SetButtonText("Pause");
        }
    }

    private void SetButtonText(string label)
    {
        if (playPauseButtonText != null)
            playPauseButtonText.text = label;
    }

    private void OnScrubSliderChanged(float value)
    {
        if (isScrubbing && videoPlayer.frameCount > 0)
        {
            double targetTime = value * videoPlayer.length;
            videoPlayer.time = targetTime;
        }
    }

    private void Update()
    {
        if (videoPlayer.isPlaying && videoPlayer.length > 0 && !isScrubbing)
        {
            scrubSlider.value = (float)(videoPlayer.time / videoPlayer.length);
        }
    }

    private void CloseFullScreen()
    {
        videoPlayer.Stop();
        fullScreenPanel.SetActive(false);
        gridPanel.SetActive(true);
    }
}
