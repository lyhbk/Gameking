using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static CardBase;

public class InsLibCard : MonoBehaviour
{
    private ObjectPool objectPool;

    [SerializeField] private Transform content;
    [SerializeField] private GameObject cardItemPrefab;
    [SerializeField] private Image CardImageBigDraw;
    private MainSceneLinkBase.ButtonLogicIn requestHandler;

    public Action PreCardGroupListChange;
    int totalCount;

    public void GiveRequestHandler(MainSceneUILink requestHandler)
    {
        this.requestHandler = requestHandler;
    }

    private void Awake()
    {
        totalCount = CardBase.monsterCards.Count + CardBase.magicCards.Count + CardBase.trapCards.Count;
        objectPool = new ObjectPool(cardItemPrefab, totalCount);
    }
    void Start()
    {
        RefreshLibraryUI();
        DetAllCardImage();
        
    }
    public void RefreshLibraryUI()                  //清晰UI并重新生成卡牌项
    {
        PreCardGroupListChange = null;
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

    }

    public void DetAllCardImage()
    {
        int i = 0;
        CreateCards(ref i, monsterCards);
        CreateCards(ref i, magicCards);
        CreateCards(ref i, trapCards);
    }

    public void CreateCards<T>(ref int i, Dictionary<string, T> Cards)where T:CardBase.Card
    {
        foreach (var card in Cards)
        {
            GameObject cardItem = objectPool.Get();
            cardItem.transform.SetParent(content, false);
            CardLibItem cardLibItem = cardItem.GetComponent<CardLibItem>();
            cardLibItem.buttonLogic = requestHandler;
            cardLibItem.CardImageBigDraw = CardImageBigDraw;
            cardLibItem.GetDataId(card.Key);
            PreCardGroupListChange += cardLibItem.PreCardGroupListChange;
            i++;
        }
    }

    public void CreateCards<T>(ref int i, List<T> Cards) where T : CardBase.Card
    {
        foreach (var card in Cards)
        {
            GameObject cardItem = objectPool.Get();
            cardItem.transform.SetParent(content, false);
            CardLibItem cardLibItem = cardItem.GetComponent<CardLibItem>();
            cardLibItem.buttonLogic = requestHandler;
            cardLibItem.CardImageBigDraw = CardImageBigDraw;
            cardLibItem.GetDataId(card.id);
            PreCardGroupListChange += cardLibItem.PreCardGroupListChange;
            i++;
        }
    }

    private void OnDestroy()
    {
        objectPool.Clear();
    }
}

