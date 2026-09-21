
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;




public class ButtonLogic : MonoBehaviour
{
    private Player player;
    private MainSceneLinkBase.InsLibCardIn requestHandler;

    [SerializeField] private InputField searchBox;
    [SerializeField] private Button searchCard;
    [SerializeField] private Button startGame;
    [SerializeField] private Button lastCardGroup;
    [SerializeField] private Button createCardGroup;
    [SerializeField] private Button nextCardGroup;
    [SerializeField] private Button clearCardGroup;
    [SerializeField] private Button complete;
    [SerializeField] private Text cardGroupCurName;
    [SerializeField] private Button changeCardGroupName;

    [SerializeField] private InputField inputChaneName;
    [SerializeField] private Button confirmName;
    [SerializeField] private Text feedback;

    [SerializeField] private Canvas pictureTemporaryCardCanvas;
    [SerializeField] private Canvas extraCardTemporaryCardCanvas;
    [SerializeField] private GameObject pictureTemporaryCardPre;

    [SerializeField] private Image bigPic;
  
    public Player.PreCardGroup temporaryCardGroup { get; set; }

    private int temporaryCardGroupNum;


    public void GiveRequestHandler(MainSceneUILink requestHandler)
    {
        this.requestHandler = requestHandler;
    }

    void Start()
    {
        player = GameObject.Find("player").GetComponent<Player>();
        cardGroupCurName.text = player.preCardGroupList.preCardGroups[player.preCardGroupList.i].groupName;
        temporaryCardGroup = player.preCardGroupList.preCardGroups[player.preCardGroupList.i].Clone();
        temporaryCardGroupNum = player.preCardGroupList.preCardGroups.Count;
        ShowPictureTemporaryCard();
        ButtonBand();
        searchBox.text = "搜索卡牌";
        inputChaneName.text = "请输入修改名字";
    }

    private void ButtonBand()
    {
        searchCard.onClick.AddListener(() => {
            string searchName = searchBox.text;
            List<CardBase.Card> res = CardBase.NameSearchCard(searchName);
            requestHandler.RefreshLibraryUI();
            int i = 0;
            requestHandler.CreateCards(ref i, res);
        });

        startGame.onClick.AddListener(() => { 
            if(temporaryCardGroup.GetMainCards(temporaryCardGroup.GetExtraCards()).Count <= 5 && temporaryCardGroupNum != player.preCardGroupList.i)
            {
                GetFeedback("请先保存卡组");
                return;
            }

            GameManage.ChangeScene("fighting"); });

        lastCardGroup.onClick.AddListener(() => {
            player.preCardGroupList.MinusIndex();
            ChangeTemporaryCardGroup();
            ShowPictureTemporaryCard();
        });

        createCardGroup.onClick.AddListener(() => {
            GetFeedback("创建卡组成功");
            if (temporaryCardGroupNum == player.preCardGroupList.preCardGroups.Count)
            {
                temporaryCardGroup = new Player.PreCardGroup("临时卡组" + temporaryCardGroupNum.ToString());
                cardGroupCurName.text = "临时卡组" + temporaryCardGroupNum.ToString();
                temporaryCardGroupNum++;
            }
        });

        nextCardGroup.onClick.AddListener(() =>
        {
            player.preCardGroupList.PlusIndex();
            ChangeTemporaryCardGroup();
            ShowPictureTemporaryCard();
        });

        clearCardGroup.onClick.AddListener(() =>
        {
            temporaryCardGroup.Clear();
            ShowPictureTemporaryCard();                 //清空数据后整组重建:预览随之清空(增量添加模式下必须同步)
            CurrentNumChange();                         //同步卡牌库"当前"数量
        });

        complete.onClick.AddListener(() => {
            if (temporaryCardGroup.GetMainCards(temporaryCardGroup.GetExtraCards()).Count <= 5)
            {
                GetFeedback("卡牌数量不足");
                return;
            }
            if (temporaryCardGroupNum > player.preCardGroupList.preCardGroups.Count)
            {
                player.preCardGroupList.AddPreCardGroup(temporaryCardGroup);
                player.preCardGroupList.Changei(temporaryCardGroupNum - 1);
            }
            else
            {
                player.preCardGroupList.preCardGroups[player.preCardGroupList.i] = temporaryCardGroup;
            }
            GetFeedback("保存成功");
        });

        changeCardGroupName.onClick.AddListener(() => {
            inputChaneName.gameObject.SetActive(true);
            confirmName.gameObject.SetActive(true);
        });

        confirmName.onClick.AddListener(() => {
            string name = inputChaneName.text;
            if (name == "")
            {
                GetFeedback("名字不能为空");
                return;
            }
            if (player.preCardGroupList.preCardGroups.Any(preCardGroup => preCardGroup.groupName == name))
            {
                GetFeedback("已有此卡组名");
                return;
            }
            temporaryCardGroup.ChangeName(inputChaneName.text);
            cardGroupCurName.text = name;
            inputChaneName.text = "请输入修改名字";
            inputChaneName.gameObject.SetActive(false);
            confirmName.gameObject.SetActive(false);
        });
    }

    //移除预览中所有卡牌项(直接销毁,不走对象池:失活对象不再残留在画布下干扰顺序)
    private void ClearShowPicture()
    {
        foreach (Transform child in pictureTemporaryCardCanvas.transform)
        {
            if (child.gameObject.name == "CardImageBigDraw")
                continue;
            Destroy(child.gameObject);
        }
        foreach (Transform child in extraCardTemporaryCardCanvas.transform)
        {
            Destroy(child.gameObject);
        }
    }

    //生成一张预览卡:直接实例化并排在画布末尾,保证展示顺序=卡组cards顺序(不依赖任何对象池复用顺序)
    private void CreatePreviewItem(CardBase.Card card, Canvas canvas)
    {
        GameObject cardItem = Instantiate(pictureTemporaryCardPre);
        cardItem.SetActive(true);
        cardItem.transform.SetParent(canvas.transform, false);
        cardItem.transform.SetAsLastSibling();

        TemporaryCardPre temporaryCardPre = cardItem.GetComponent<TemporaryCardPre>();
        temporaryCardPre.buttonLogic = requestHandler as MainSceneLinkBase.ButtonLogicIn;
        temporaryCardPre.UpdatePic(card.id);
        temporaryCardPre.imageBigDraw = bigPic;
        temporaryCardPre.parentBigShow = bigPic.transform.parent.gameObject;
    }

    public void ShowPictureTemporaryCard()                                  //整组重建(进入场景/切换卡组/清空时用)
    {
        List<CardBase.Card> extraCards = temporaryCardGroup.GetExtraCards();
        List<CardBase.Card> mainCards = temporaryCardGroup.GetMainCards(extraCards);
        Transform transform = bigPic.transform.parent;
        ClearShowPicture();
        for (int i = 0; i < extraCards.Count; i++)
        {
            CreatePreviewItem(extraCards[i], extraCardTemporaryCardCanvas);
        }
        for (int i = 0; i < mainCards.Count; i++)
        {
            CreatePreviewItem(mainCards[i], pictureTemporaryCardCanvas);
        }
        transform.gameObject.SetActive(false);
        transform.SetAsLastSibling();
    }

    //添加卡牌:数据已AddCard成功后,只把这张新卡直接追加到预览末尾(不做整池重建,顺序不被打乱)
    public void AddShowPictureCard(string id)
    {
        if (temporaryCardGroup == null || temporaryCardGroup.cards.Count == 0)
            return;
        CardBase.Card added = temporaryCardGroup.cards[temporaryCardGroup.cards.Count - 1];   //AddCard总是追加到末尾
        bool isExtra = temporaryCardGroup.GetExtraCards().Contains(added);
        CreatePreviewItem(added, isExtra ? extraCardTemporaryCardCanvas : pictureTemporaryCardCanvas);
    }

    //移除卡牌(双击预览卡):删数据并只销毁被点的那一项,其余卡保持原位、顺序不变
    public void RemoveShowPictureCard(GameObject cardItem)
    {
        if (cardItem == null)
            return;
        TemporaryCardPre temporaryCardPre = cardItem.GetComponent<TemporaryCardPre>();
        if (temporaryCardPre != null && temporaryCardGroup != null)
        {
            string removeId = temporaryCardPre.GetCardId();
            if (!string.IsNullOrEmpty(removeId))
                temporaryCardGroup.RemoveCard(removeId);
        }
        Destroy(cardItem);
        CurrentNumChange();                                     //同步卡牌库"当前"数量
    }

    private void ChangeTemporaryCardGroup()
    {
        GetFeedback("切换卡组");
        ChangeCardGroupCurName(player.preCardGroupList.preCardGroups[player.preCardGroupList.i].groupName);
        if (temporaryCardGroupNum > player.preCardGroupList.preCardGroups.Count)
        {
            temporaryCardGroup = null;
            temporaryCardGroupNum--;
        }
        temporaryCardGroup = player.preCardGroupList.preCardGroups[player.preCardGroupList.i].Clone();
        CurrentNumChange();
    }

    public void CurrentNumChange()
    {
        requestHandler.PreCardGroupListChange?.Invoke();
    }

    public void GetFeedback(string feedbackcontent)
    {
        feedback.gameObject.SetActive(true);
        feedback.text = feedbackcontent;
        StartCoroutine(ClearFeedback());
    }

    IEnumerator ClearFeedback()
    {
        yield return new WaitForSeconds(3f);
        feedback.text = "";
        feedback.gameObject.SetActive(false);
    }

    private void ChangeCardGroupCurName(string cardGroupCurName)
    {
        this.cardGroupCurName.text = cardGroupCurName;
    }
}
