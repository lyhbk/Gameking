using System;
using System.Collections.Generic;
using UnityEngine;

public class MainSceneUILink : MainSceneLinkBase.ButtonLogicIn, MainSceneLinkBase.InsLibCardIn
{
    private readonly ButtonLogic buttonLogic;
    private InsLibCard insLibCard;

    public MainSceneUILink(ButtonLogic buttonLogic)
    {
        this.buttonLogic = buttonLogic;
    }

    public void RegisterInsLibCard(InsLibCard insLibCard)
    {
        this.insLibCard = insLibCard;
    }

    #region ButtonLogicIn
    public Player.PreCardGroup temporaryCardGroup => buttonLogic.temporaryCardGroup;
    public void GetFeedback(string feedbackcontent)
    {
        buttonLogic.GetFeedback(feedbackcontent);
    }
    public void CurrentNumChange()
    {
        buttonLogic.CurrentNumChange();
    }
    public void ShowPictureTemporaryCard()
    {
        buttonLogic.ShowPictureTemporaryCard();
    }
    public void AddShowPictureCard(string id)
    {
        buttonLogic.AddShowPictureCard(id);
    }
    public void RemoveShowPictureCard(GameObject cardItem)
    {
        buttonLogic.RemoveShowPictureCard(cardItem);
    }
    #endregion

    #region InsLibCardIn
    public void RefreshLibraryUI()
    {
        insLibCard.RefreshLibraryUI();
    }
    public void CreateCards(ref int i, List<CardBase.Card> Cards)
    {
        insLibCard.CreateCards(ref i, Cards);
    }
    public Action PreCardGroupListChange => insLibCard.PreCardGroupListChange;
    #endregion
}
