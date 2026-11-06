using System;
using System.Collections.Generic;
using UnityEngine;

public class MainSceneLinkBase                                  //mainscene各组件间交互接口定义
{
    // 供 InsLibCard、CardLibItem、TemporaryCardPre 调用的 ButtonLogic 能力
    public interface ButtonLogicIn
    {
        Player.PreCardGroup temporaryCardGroup { get; }
        void GetFeedback(string feedbackcontent);
        void CurrentNumChange();
        void ShowPictureTemporaryCard();
        void AddShowPictureCard(string id);                 //添加卡牌:直接追加显示,不做整池重建
        void RemoveShowPictureCard(GameObject cardItem);    //移除卡牌(双击预览卡):删数据并只销毁该卡项
    }

    // 供 ButtonLogic 调用的 InsLibCard 能力
    public interface InsLibCardIn
    {
        void RefreshLibraryUI();
        void CreateCards(ref int i, List<CardBase.Card> Cards);
        Action PreCardGroupListChange { get; }
    }
}
