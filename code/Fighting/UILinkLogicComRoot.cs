using Cysharp.Threading.Tasks;
using UnityEngine;
using static ExternalQuestBase.UILinkLog;
public class UILinkLogicWrapper
{
    private UILinkLogic link;

    public void Initialize(UILinkLogic link)
    {
        this.link = link;
    }
    public UILinkLogic GetUILink()
    {
        return link;
    }

    public void ChangeHPRequestHandle(ExternalQuestBase.UILinkLog.ChangeHPRequest changeHPRequest) => link?.ChangeHPRequestHandle(changeHPRequest);
    public void ChangeHPToRequestHandle(ExternalQuestBase.UILinkLog.ChangeHPToRequest changeHPToRequest) => link?.ChangeHPToRequestHandle(changeHPToRequest);
    public async UniTask ChangeBoutToRequestHandle(ExternalQuestBase.UILinkLog.ChangeBout changeBout) => link?.ChangeBoutToRequestHandle(changeBout); 
    public async UniTask WaitCurChangeBout(ExternalQuestBase.UILinkLog.WaitCurChangeBout waitCurChangeBout) => link?.WaitCurChangeBout(waitCurChangeBout);

}

public class UILinkLogicComRoot:MonoBehaviour
{
    [SerializeField] private FightLogic fightLogic;
    [SerializeField] private FightUI fightUI;
    [SerializeField] private UILinkLogicWrapper logicWrapper;  // 在 Inspector 中拖拽一个空物体，并挂上此脚本
    private void Awake()
    {
        UILinkLogic uiLinkLogic = new UILinkLogic(fightLogic);
        uiLinkLogic.RegisterUIListener(fightUI);
        logicWrapper = new UILinkLogicWrapper();
        logicWrapper.Initialize(uiLinkLogic);
        var res = logicWrapper.GetUILink();
        fightLogic.GiveRequestHandler(res);     // 先注入FightLogic，确保状态机懒初始化时拿到完整的询问弹窗回调
        fightUI.GiveRequestHandler(res);        // 后注入FightUI
    }
}

