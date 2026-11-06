using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;




public class EQ 
{
    public static Func<EffLogic.EntityCard, Transform> OneEntityCardToTransfrom;                   //单个卡牌实体转预制体
    public static Func<List<EffLogic.EntityCard>, List<Transform>> EntityCardToTransfrom;          //批量卡牌实体转预制体
    public EffLogic.ExtraQuest SpeSumEQ;
    public EffLogic.ExtraQuest GoToCemeteryEQ;
    public EffLogic.ExtraQuest GoToBanishedEQ;
    public EffLogic.ExtraQuest MagicActBaseEQ;
    public EffLogic.ExtraQuest GetObjectEQ;
    public EffLogic.ExtraQuest DrawCardEQ;
    public EffLogic.ExtraQuest GoToHandEQ;          //返回手卡UI回调(HandleTransfrom=fightUI.GotoHandUI)
    public EffLogic.ExtraQuest GoToDeckEQ;          //返回卡组/额外卡组UI回调(HandleTransfrom=fightUI.GotoDeckUI:收回预制体)
    public EQ(Func<EffLogic.EntityCard, Transform> func0, Func<List<EffLogic.EntityCard>,List<Transform>> func1)
    {
        EffLogic.ExtraQuest extraQuest = new EffLogic.ExtraQuest(null,null,null,null);
        extraQuest.IniEntityCardToTransfrom(func0, func1);
    }

    public void IniDrawCardEQ(Func<int, List<EffLogic.EntityCard>, UniTask> DrawCardHandle,
                                List<StateMachine.TimePointBase> timePointBases)
    {
        EffLogic.ExtraQuest extraQuest = new EffLogic.ExtraQuest(null, null, timePointBases, null);
        extraQuest.DrawCardHandle = DrawCardHandle;
        DrawCardEQ = extraQuest;
    }

    public void IniEQSpeSumEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>,List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        SpeSumEQ = new EffLogic.ExtraQuest(func,HandleTransfrom, timePointBases, MapRealCard);
    }

    public void IniGoToCemeteryEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>,List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        GoToCemeteryEQ = new EffLogic.ExtraQuest(func, HandleTransfrom, timePointBases, MapRealCard);
    }
    public void IniGoToBanishedEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        GoToBanishedEQ = new EffLogic.ExtraQuest(func, HandleTransfrom, timePointBases, MapRealCard);
    }
    public void IniGoToHandEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        GoToHandEQ = new EffLogic.ExtraQuest(func, HandleTransfrom, timePointBases, MapRealCard);
    }
    public void IniGoToDeckEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        GoToDeckEQ = new EffLogic.ExtraQuest(func, HandleTransfrom, timePointBases, MapRealCard);
    }
    public void IniMagicActBaseEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        MagicActBaseEQ = new EffLogic.ExtraQuest(func, HandleTransfrom, timePointBases, MapRealCard);
    }
    public void IniGetObjectEQ(Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> func, Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> HandleTransfrom,
                                List<StateMachine.TimePointBase> timePointBases, Func<EffLogic.EntityCard, EffLogic.EntityCard> MapRealCard = null)
    {
        GetObjectEQ = new EffLogic.ExtraQuest(func, HandleTransfrom, timePointBases, MapRealCard);
    }
}
