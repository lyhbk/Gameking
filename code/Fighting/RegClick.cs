

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ExternalQuestBase.UILinkLog;
public class RegClick : MonoBehaviour
{
    
    private int num;
    private ClickEvent.SimpleClickHandler simpleClickHandler;
    private RegClickEvent regClickEvent;
    private void Start()
    {
        simpleClickHandler = this.gameObject.AddComponent<ClickEvent.SimpleClickHandler>();
        simpleClickHandler.OnClicked =() =>
        {
            Summon();
            MagicActive();
        };
    }
    public void GetRegClickEvent(UILinkLogic _UILinkLogic)
    {
        this.regClickEvent = _UILinkLogic;
    }
    public void GiveNum(int num)
    {
        this.num = num;
    }
    private void Summon()
    {
        if (!regClickEvent.GetSumOrCoverLock())
            return;
        Image image = this.gameObject.GetComponent<Image>();
        if (regClickEvent.IsWaitingForSacrificeRequest())
        {
            PreEntityCard preEntityCard = this.transform.GetChild(0).GetComponent<PreEntityCard>();
            EffLogic.EntityCard entityCard = preEntityCard.GetEntityCard();
            if (!JudRep(entityCard.currentid, regClickEvent.GetSelSacRequest()))
            {
                regClickEvent?.AddSelSacRequest(this.transform.GetChild(0));
            }
            if (regClickEvent.GetRequiredSacrificeCountRequest() == regClickEvent.GetSelSacRequest().Count)
            {
                regClickEvent?.EndSacrificeWaitRequest();
            }
            return;
        }
        regClickEvent?.ClearSacrificesRequest();
        if (image.color != Color.white)
        {
            SumOrCovBaseRequest sumOrCovBaseRequest = new SumOrCovBaseRequest(this.transform);
            regClickEvent?.SumOrCovBaseRequest(sumOrCovBaseRequest);   //内部已按"挂载,UI清理,数据特招(诱发)"顺序执行,此处不再二次清理,避免诱发流程共享字段被误操作
        }

    }
    private bool JudRep(string currentId,List<Transform> transforms)
    {
        foreach (Transform transform in transforms)
        {
            PreEntityCard preEntityCard = transform.GetComponent<PreEntityCard>();
            if (preEntityCard.GetEntityCard().currentid == currentId)
            {
                return true;
            }
        }
        return false;
    }
    private void MagicActive()
    {
        if (!regClickEvent.GetMagicTrapLock())
            return;
        LayOutMagicTrapRequest layOutMagicTrapRequest = new LayOutMagicTrapRequest(this.transform);
        regClickEvent?.LayOutMagicTrapCardRequest(layOutMagicTrapRequest);
        regClickEvent?.ClearUIRequest();
    }
}
