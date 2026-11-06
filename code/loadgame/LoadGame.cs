using UnityEngine.Video;
using UnityEngine;
using UnityEngine.UI;


public class LoadGame : MonoBehaviour
{
    [SerializeField] private Button startGame;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage rawImage;
    private void Awake()
    {
        if (rawImage != null)
        {
            RenderTexture renderTexture = new RenderTexture(1920, 1080, 0);
            videoPlayer.targetTexture = renderTexture;
            rawImage.texture = renderTexture;
        }
        videoPlayer.Play();
    }
    void Start()
    {
        startGame.onClick.AddListener(() =>
        {
            GameManage.ChangeScene("mainscene");
        });
    }


}
