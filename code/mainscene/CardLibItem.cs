
using UnityEngine;
using UnityEngine.UI;
using static ClickEvent;

public class CardLibItem : MonoBehaviour
{
    [SerializeField] private string dataId;
    [SerializeField] private Image cardImage;
    [SerializeField] private Text cardname;
    [SerializeField] private Text currentNum;
    [SerializeField] private Text limition;
    [SerializeField] private Button addCard;

    public Image CardImageBigDraw;
    public MainSceneLinkBase.ButtonLogicIn buttonLogic {  get; set; }


    private void Awake()
    {
        cardImage = this.transform.GetChild(1).GetComponent<Image>();
        cardname = this.transform.GetChild(2).GetComponent<Text>();
        currentNum = this.transform.GetChild(3).GetComponent<Text>();
        limition = this.transform.GetChild(4).GetComponent<Text>();
        addCard = this.transform.GetChild(5).GetComponent<Button>();
        addCard.onClick.AddListener(() => {
            if (!buttonLogic.temporaryCardGroup.AddCard(dataId))
            {
                buttonLogic.GetFeedback("添加失败");
                return;
            }
            buttonLogic.CurrentNumChange();
            buttonLogic.AddShowPictureCard(dataId);     //直接追加到预览末尾,不再整池回收重建(顺序稳定)
        });

        
    }

    private void Start()
    {
        GameObject parentBigShow = CardImageBigDraw.transform.parent.gameObject;
        SimpleClickHandler simpleClickHandler = parentBigShow.GetComponent<SimpleClickHandler>();
        if (simpleClickHandler == null)
            simpleClickHandler = parentBigShow.AddComponent<SimpleClickHandler>();
        
        simpleClickHandler.OnClicked += () =>
        {
            parentBigShow.SetActive(false);
        };

        SimpleClickHandler simpleClickHandler1 = cardImage.GetComponent<SimpleClickHandler>();
        if (simpleClickHandler1 == null)
            simpleClickHandler1 = cardImage.gameObject.AddComponent<SimpleClickHandler>();
        simpleClickHandler1.OnClicked += () =>
        {
            GameManage.SetImage(CardImageBigDraw, dataId);
            parentBigShow.SetActive(true);
        };
    }

    public void GetDataId(string dataId)
    {
        this.dataId = dataId;
        GameManage.SetImage(cardImage, dataId);
        cardname.text = CardBase.IdFindCard(dataId).name;
        if (dataId == "73819701")
            cardname.text = "白龙之落胤";
        currentNum.text = "当前：" + CruCardNum(dataId).ToString();
        limition.text = "限制：" + LimitionNum(dataId).ToString();
    }

    private int CruCardNum(string dataId)
    {
        return buttonLogic.temporaryCardGroup.GetCardCount(dataId);
    }

    public void PreCardGroupListChange()                        //索引改变时访问
    {
        currentNum.text = "当前：" + CruCardNum(dataId).ToString();
    }

    private int LimitionNum(string dataId)
    {
        return CardBase.IdFindCard(dataId).limition;
    }

    private void OnDestroy()
    {
        Destroy(cardImage.gameObject);
    }
}
