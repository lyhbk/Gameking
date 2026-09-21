
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static ClickEvent;

public class TemporaryCardPre : MonoBehaviour
{
    [SerializeField] private Image cardImage;
    public Image imageBigDraw { get;  set; }
    public GameObject parentBigShow;

    private string cardId;

    public MainSceneLinkBase.ButtonLogicIn buttonLogic { get; set; }

    private void Awake()
    {
        cardImage = this.transform.GetChild(0).GetComponent<Image>();
       
    }

    private void Start()
    {



        CardClickHandler clickHandler = cardImage.GetComponent<CardClickHandler>();
        if (clickHandler == null)
        {
            clickHandler = cardImage.gameObject.AddComponent<CardClickHandler>();
        }

        clickHandler.OnSingleClicked += () => {
            GameManage.SetImage(imageBigDraw, cardId);
            parentBigShow.SetActive(true);
        };
        clickHandler.OnDoubleClicked += () => {
            buttonLogic.RemoveShowPictureCard(this.gameObject);      //双击移除:删数据并只销毁本卡项,其余卡顺序不变
        };
    }

    public string GetCardId()
    {
        return cardId;
    }

    public void UpdatePic(string id)
    {
        cardId = id;
        GameManage.SetImage(cardImage, cardId);
    }


}




