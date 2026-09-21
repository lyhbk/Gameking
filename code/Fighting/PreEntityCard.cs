using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ClickEvent;
using static EffLogic;
using static ExternalQuestBase.UILinkLog;


public class PreEntityCard : MonoBehaviour,IDeselectHandler, IPointerClickHandler
{
    [SerializeField] private EffLogic.EntityCard entityCard;
    private PECListener _PECListener;



    private Image highLight;
    private Image image;
    private Button window1;
    private Button window2;
    private Button window3;
    private Text window2Text;

    private void Awake()
    {
        highLight = transform.GetChild(0).GetComponent<Image>();
        image = transform.GetChild(1).GetComponent<Image>();
        window1 = transform.GetChild(2).GetComponent<Button>();
        window1.gameObject.SetActive(false);
        window2 = transform.GetChild(3).GetComponent<Button>();
        window2.gameObject.SetActive(false);
        window2Text = window2.GetComponentInChildren<Text>(true);   //缓存攻击按钮文本组件（预制体可能没有，允许为空）
        window3 = transform.GetChild(4).GetComponent<Button>();
        window3.gameObject.SetActive(false);
        effEctions = new List<EffLogic.Effection>();
    }

    private void OnEnable()
    {
        SetCanUsePoint(true);
    }
    #region 基础卡牌事件
    private bool CanUsePoint = true;
    public void SetCanUsePoint(bool can = true)
    {
        CanUsePoint = can;
    }
    private bool IsSelectingQuestBlocked()                       //选卡弹窗(IsWaitingQuest)打开期间拦截本卡操作(挂载/通召/盖放/发动),防等待期漂移
    {
        if (_PECListener != null && _PECListener.IsSelectingQuest())
        {
            return true;
        }
        return false;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if(!CanUsePoint)
            return;
        ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        _PECListener?.ShowCardPicRequest(entityCard.card.id, entityCard.card.effection);
        EventSystem.current.SetSelectedGameObject(gameObject);
        GameManage.GamePhase phase = _PECListener.GetCurrentGamePhase();
        if (IsSelectingQuestBlocked())                             //选卡弹窗打开期间:卡图预览已展示,但禁止挂载/发动/换位等场景操作
            return;
        //仅我方回合允许通召/盖放等"自己回合"操作：对方回合玩家只能靠下方ActionEffection发动效果
        if (entityCard.GetCurrentOwner()?.GetPlayerIndex() == 0 && _PECListener?.GetOnwerPhase() == 0)
        {

            switch (phase)
            {
                case GameManage.GamePhase.Main1Phase:                              //盖放通召                                           
                case GameManage.GamePhase.Main2Phase:
                    SumOrCov();
                    break;
                case GameManage.GamePhase.FightPhase:                               //战斗
                    if (entityCard.entityType == 0)
                    {
                       
                        //Attack();

                    }
                    
                    break;
            };
        }
        ActionEffection();
    }
    private void SumOrCov()
    {
        bool can = false;
        if (entityCard.entityType == 0)
        {
            if (!_PECListener.CheckCanSummonRequest(0))                 //通召次数检查仅针对怪兽（魔法陷阱盖放不受通召次数限制）
                return;
            var entityMonsterCard = entityCard as EffLogic.EntityMonsterCard;
            entityMonsterCard.IniOrdSummon(_PECListener?.GetFieldRequest());
            can = entityMonsterCard.ordSummon.IsCanPay();
            if (can == true)
            {
                window1.gameObject.SetActive(true);
                Image image = window1.GetComponent<Image>();
                GameManage.SetImageUI(image, "spemon");
                if (entityMonsterCard.currentGrade <= 4)
                {
                    SummonOrCover(window1, 0, true);                    //通召/盖放怪兽：确认时才增加通召次数
                    _PECListener?.SacrificeNumRequest(0);
                }
                else if (entityMonsterCard.currentGrade == 5 || entityMonsterCard.currentGrade == 6)
                {
                    _PECListener?.SacrificeNumRequest(1);
                    window1.onClick.RemoveAllListeners();
                    window1.onClick.AddListener(async () =>
                    {
                        if (IsSelectingQuestBlocked())
                            return;
                        if (_PECListener.IsWaitingForAsyncRequest())
                        {
                            return;
                        }
                        window1.gameObject.SetActive(false);
                        OnSacrificeSummonButtonClick().Forget();

                    });
                }
                else
                {
                    _PECListener?.SacrificeNumRequest(2);
                    window1.onClick.RemoveAllListeners();
                    window1.onClick.AddListener(async () =>
                    {
                        if (IsSelectingQuestBlocked())
                            return;
                        if (_PECListener.IsWaitingForAsyncRequest())
                        {
                            return;
                        }
                        window1.gameObject.SetActive(false);
                        OnSacrificeSummonButtonClick().Forget();
                    });
                }

            }
        }
        else if (entityCard.entityType == 2)
        {
            var entityTrapCard = entityCard as EffLogic.EntityTrapCard;
            entityTrapCard.cover = new Cover(entityTrapCard, _PECListener?.GetFieldRequest());   //仅创建盖放检查实例，不改变盖放状态（isCover保持0）
            can = entityTrapCard.cover.IsCanPay();

            if (can == true)
            {
                window1.gameObject.SetActive(true);
                Image image = window1.GetComponent<Image>();
                GameManage.SetImageUI(image, "cover");
                SummonOrCover(window1, 1);
                _PECListener?.SacrificeNumRequest(0);
            }
        }
        else
        {
            var entityMagicCard = entityCard as EffLogic.EntityMagicCard;
            entityMagicCard.cover = new Cover(entityMagicCard, _PECListener?.GetFieldRequest()); //仅创建盖放检查实例，不改变盖放状态（isCover保持0）
            can = entityMagicCard.cover.IsCanPay();
            if (can == true)
            {
                window1.gameObject.SetActive(true);
                Image image = window1.GetComponent<Image>();
                GameManage.SetImageUI(image, "cover");
                SummonOrCover(window1, 1);
                _PECListener?.SacrificeNumRequest(0);
            }
        }
    }
    public void SummonOrCover(Button window,int i , bool addSumNum = false)
    {
        window.onClick.RemoveAllListeners();
        window.onClick.AddListener(() =>
        {
            if (IsSelectingQuestBlocked())
                return;
            if (addSumNum)
                _PECListener?.AddCurSomNumRequest(0);                   //仅通召（怪兽出场）增加通召次数，盖放（魔法陷阱）不增加
            AwakeRegion(i);
            if (i == 1)
                _PECListener?.GetMagicTrapRequest(this.transform);      //盖放魔陷：设置待放置的魔陷卡（对应magicOrTrap）
            else
                _PECListener?.GetSumOrCoverRequest(this.transform);     //通召/盖放怪兽：设置待放置的怪兽卡（对应sumOrCover）
            window.gameObject.SetActive(false);
        });
    }

    private void AwakeRegion(int i)
    {
        _PECListener?.AwakeSomRegionRequest(i);
    }


    public Action<EffLogic.EntityCard> EffAction { get; set; }
    public List<Action<EffLogic.EntityCard>> EffActions { get; set; }

    public void AddEffAction(Action<EffLogic.EntityCard> action)
    {
        EffAction = action; 
    }
    #endregion

    #region 基础事件
    public void Initialize(EffLogic.EntityCard card, UILinkLogic _UILinkLogic)
    {
        entityCard = card;
        if (effEctions == null)
            effEctions = new List<EffLogic.Effection>();    //对象池回收时列表被置null，复用前需重建
        else if (effEctions.Count > 0)
        {
            //防御:对象池同帧复用/重复Initialize时列表仍残留旧实体效果,先解绑并清空,防止旧效果(含isTrigger=0无triggers的项)污染实体.effections引发诱发遍历NRE
            for (int i = 0; i < effEctions.Count; i++)
                effEctions[i]?.DeleteTriggerEff();
            effEctions.Clear();
        }
        SetCardImage(card);
        this._PECListener = _UILinkLogic;
        EntityEffRequest entityEffRequest = new EntityEffRequest(entityCard, this);
        _PECListener?.EntityEffRequestHandle(entityEffRequest);
        for (int i = 0; i < effEctions.Count; i++)                      //绑定诱发效果
        {
            effEctions[i]?.OnTriggerEff(TriggerEffection);
        }
        //实体效果列表仅由"首次绑定"视图持有:选卡弹窗/滚动浏览等对象池临时副本重复Initialize同一实体时会另建一份Effect集合并覆盖entity.effections,
        //  副本释放(OnReturnToPool清空自身effEctions并置null)会把实体真实效果列表一并掏空→真实卡片的诱发触发器(如LeaveEx)在其OnActionEff
        //  按entity.effections查找自身触发器时落空而静默失效(表现为场上圣女的LeaveEx完全不响应)。
        //  故仅当实体尚无效果列表时才写入;副本虽仍创建/订阅自身触发器,但因不在实体权威列表中永不命中,也不会再破坏真实绑定。
        if (entityCard.effections == null || entityCard.effections.Count == 0)
            entityCard.GetEffections(effEctions);
    }
    public void SetCardImage(EffLogic.EntityCard card, string path = "")
    {
        if (path == "")
        {
            GameManage.SetImage(image, card.card.id);
            return;
        }
        GameManage.SetImage(image, path);
    }
    public void OnDeselect(BaseEventData eventData)
    {
        StartCoroutine(enumerator(0.3f));
    }
    private IEnumerator enumerator(float time)
    {
        yield return new WaitForSeconds(time);
      
        window1.gameObject.SetActive(false);
        window2.gameObject.SetActive(false);
        window3.gameObject.SetActive(false);
    }
    public EffLogic.EntityCard GetEntityCard()
    {
        return entityCard;
    }
    public List<EffLogic.Effection> GetEffEctions()             //本视图当前绑定的效果列表(供特招预制体接管权威效果列表用)
    {
        return effEctions;
    }

    public void AwakeHighLight()
    {
        highLight.gameObject.SetActive(true);
    }
    public void CloseHightLight()
    {
        highLight.gameObject.SetActive(false);
    }
    /// 诱发"等待点卡"阶段点DeleteEff退出诱发时调用:关闭本卡因"可发动但未点击发动"而亮起的高亮与发动入口(window3)。
    /// 已点击发动过的诱发卡会走ActivateEff自行关闭,此处只负责清理仍留在"可发动"状态的诱发效果持有卡。
    public void CloseTriggerWaitUI()
    {
        if (window3 != null && window3.gameObject.activeSelf)
            window3.gameObject.SetActive(false);
        if (highLight != null)
            highLight.gameObject.SetActive(false);
    }
    public void SetAttackButton(bool active)                //激活攻击按钮（window2）：文本显示为"攻击"，点击进入攻击宣言流程
    {
        if (active)
        {
            if (window2Text == null)                        //预制体无文本组件：动态创建
            {
                window2Text = window2.GetComponentInChildren<Text>(true);
                if (window2Text == null)
                {
                    GameObject go = new GameObject("AttackText");
                    go.transform.SetParent(window2.transform, false);
                    window2Text = go.AddComponent<Text>();
                    window2Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (window2Text.font == null)
                        window2Text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    window2Text.color = Color.white;
                    window2Text.alignment = TextAnchor.MiddleCenter;
                    window2Text.raycastTarget = false;      //不拦截点击，保证按钮可点
                    window2Text.resizeTextForBestFit = true;
                }
            }
            window2Text.text = "攻击";
            window2.onClick.RemoveAllListeners();
            window2.onClick.AddListener(() =>
            {
                if (IsSelectingQuestBlocked())
                    return;
                var monster = entityCard as EntityMonsterCard;
                if (monster == null) return;
                window2.gameObject.SetActive(false);        //点击后先隐藏，防止攻击流程中重复点击
                _PECListener?.AttackRequest(monster).Forget();
            });
        }
        window2.gameObject.SetActive(active);
    }
    #endregion

    private async UniTask OnSacrificeSummonButtonClick()
    {
        List<Transform> selectedMonsters = await _PECListener.WaitForSacrificeSelection();
        if (selectedMonsters == null || selectedMonsters.Count == 0)
        {
            return;
        }
        foreach (var monsterTrans in selectedMonsters)
        {
            PreEntityCard preEntityCard = monsterTrans.GetComponent<PreEntityCard>();
            EffLogic.EntityCard card = preEntityCard.GetEntityCard();
            card.ChangeLocation(GameManage.CardLocation.Cemetery);

            _PECListener?.RegionRemoveCardRequest(5, card);
            _PECListener?.RegionAddCardRequest(3, card);
            _PECListener?.GotoCeCemeteryUIRequest(monsterTrans);
        }
        _PECListener?.AddCurSomNumRequest(0);                           //祭品通召成功：确认通召时增加通召次数
        _PECListener?.GetSumOrCoverRequest(this.transform);
        _PECListener?.SacrificeNumRequest(0);
        AwakeRegion(0);
    }





    public Action OnEffLaunching;               //效果发动入口(window3)点击后的收尾回调

    [SerializeField]private List<EffLogic.Effection> effEctions = new List<EffLogic.Effection>();
    private List<EffLogic.Effection> canActiveEffs = new List<EffLogic.Effection>();   //当前可发动效果候选(点window3后单发或多效果面板选择)
    public void AddEffEction(EffLogic.Effection effEctions)
    {
        this.effEctions.Add(effEctions);
    }
    public async UniTask AskOrderStack()                            //问询连锁
    {
        _PECListener?.SetChainAsk(true);                            //进入连锁问询状态
        _PECListener?.SetCurrentPointRequest(StateMachine.CurrentPoint.Chain);   //询问是否有连锁,时点改为连锁
        try
        {
            //栈顶效果orderStackState==3(不可被连锁):跳过连锁问询直接结算
            if (_PECListener != null && EffLogic.IsUnchainable(_PECListener.GetTopOrderStackState()))
                return;
            bool isFirst = true;
            while (true)
            {
                if (_PECListener != null && EffLogic.IsUnchainable(_PECListener.GetTopOrderStackState()))   //新链上栈顶不可再被连锁
                    break;
                bool go = await _PECListener.AskOrderStackAsk(isFirst ? "是否连锁？" : "是否继续连锁？");
                if (!go) break;                                     //点[否]放弃连锁,结束问询,结算
                isFirst = false;
                await _PECListener.WaitForChainActivate();
            }
        }
        finally
        {
            _PECListener?.SetChainAsk(false);                       //退出连锁问询状态
            if (_PECListener != null)
            {
                _PECListener?.SetCurrentPointRequest(StateMachine.CurrentPoint.Freedom);   
                await _PECListener.HandleOrderStack();             
            }
        }
    }
    public bool HasCanActiveEff()                                   //当前时点该卡是否存在可发动的主动效果
    {
        if (_PECListener == null || entityCard == null || effEctions == null || effEctions.Count == 0)
            return false;
        if (entityCard.GetCurrentOwner()?.GetPlayerIndex() != 0)    
            return false;
        StateMachine.CurrentPoint curPoint = _PECListener.GetCurrentPoint();
        if (curPoint != StateMachine.CurrentPoint.Freedom && curPoint != StateMachine.CurrentPoint.Chain)
            return false;
        for (int i = 0; i < effEctions.Count; i++)
        {
            EffLogic.Effection eff = effEctions[i];
            if (eff == null || eff.isTrigger == 1)
                continue;                                            
            if (_PECListener.IsChainAsk() && !EffLogic.CanChainByOrderState(_PECListener.GetTopOrderStackState(), entityCard.entityType))
                continue;
            if (eff.EffCost())
            {
                if (_PECListener.IsChainAsk() && eff.speed < _PECListener.GetTopOrderSpeed())
                    continue;
                return true;
            }
        }
        return false;
    }
    private void ActionEffection()                                  
    {
        StateMachine.CurrentPoint curPoint = _PECListener.GetCurrentPoint();
        if (curPoint != StateMachine.CurrentPoint.Freedom && curPoint != StateMachine.CurrentPoint.Chain)
            return;
        if(effEctions.Count == 0)
        {
            return;
        }
        canActiveEffs.Clear();                                       //重建主动效果候选
        for (int i = 0; i < effEctions.Count; i++)
        {
            EffLogic.Effection eff = effEctions[i];
            if (eff == null || eff.isTrigger == 1)
                continue;                                           //跳过诱发效果
            //连锁问询中
            if (_PECListener.IsChainAsk() && !EffLogic.CanChainByOrderState(_PECListener.GetTopOrderStackState(), entityCard.entityType))
                continue;
            if (eff.EffCost())
            {
                //连锁中发动速度不得低于连锁栈栈顶效果速度
                if (_PECListener.IsChainAsk() && eff.speed < _PECListener.GetTopOrderSpeed())
                    continue;
                canActiveEffs.Add(eff);
            }
        }
        if (canActiveEffs.Count == 0)
        {
            window3.gameObject.SetActive(false);
            return;
        }
        window3.gameObject.SetActive(true);                          
        BindWindow3();
    }

    private void TriggerEffection(EffLogic.Effection effection)      
    {
        if (_PECListener.GetCurrentPoint() != StateMachine.CurrentPoint.Target)
            return;
        if (effection.isTrigger != 1)
            return;
        if (!canActiveEffs.Contains(effection))                      //累加本轮被分发的诱发
            canActiveEffs.Add(effection);
        AwakeHighLight();
        window3.gameObject.SetActive(true);                          //激活效果发动按钮
        BindWindow3();                                               //统一绑定
    }

    private void BindWindow3()                                       
    {
        window3.onClick.RemoveAllListeners();
        window3.onClick.AddListener(async () =>
        {
            try
            {
                if (IsSelectingQuestBlocked())
                    return;
                if (_PECListener != null && _PECListener.IsWaitingForAsyncRequest())
                {
                    return;
                }
                await OnWindow3Clicked();
            }
            catch (OperationCanceledException)
            {
                //对局已结束:终止该异步效果链(async void 顶层吞掉,避免打到 Unity 造成报错)
            }
        });
    }

    private async UniTask OnWindow3Clicked()                         //点window3发动:同卡同点多个可发动效果时弹出选择面板
    {
        List<EffLogic.Effection> canEffs = CollectCanActiveEffs();   //点击时按当前时点/次数重新过滤(防御点击间隔状态变化)
        if (canEffs.Count == 0)
        {
            window3.gameObject.SetActive(false);                     //通用判空:无有效效果可发动,跳过(不发动,保持浏览面板可继续选卡)
            return;
        }
        window3.gameObject.SetActive(false);                         //隐藏入口,进入发动流程
        OnEffLaunching?.Invoke();                                    //发动前收尾
        if (canEffs.Count == 1)                                      //只有1个可发动效果
        {
            await ActivateEff(canEffs[0]);
            return;
        }
        //同一时点有多个可发动效果
        _PECListener?.ShowEffChoiceRequest(canEffs, eff =>
        {
            ActivateEff(eff).Forget();
        });
    }

    private List<EffLogic.Effection> CollectCanActiveEffs()          //按当前时点/连锁限制/次数过滤候选
    {
        List<EffLogic.Effection> res = new List<EffLogic.Effection>();
        if (canActiveEffs == null || canActiveEffs.Count == 0)
            return res;
        StateMachine.CurrentPoint curPoint = _PECListener != null ? _PECListener.GetCurrentPoint() : StateMachine.CurrentPoint.Freedom;
        bool isTriggerPoint = curPoint == StateMachine.CurrentPoint.Target;
        for (int i = 0; i < canActiveEffs.Count; i++)
        {
            EffLogic.Effection eff = canActiveEffs[i];
            if (eff == null) continue;
            if (isTriggerPoint ? eff.isTrigger != 1 : eff.isTrigger == 1)
                continue;                                            //诱发候选只在诱发时点,主动候选只在自由/连锁时点
            if (!eff.EffCost())
                continue;                                            //cost/次数/卡名限制校验
            if (!isTriggerPoint && _PECListener != null && _PECListener.IsChainAsk())
            {
                if (!EffLogic.CanChainByOrderState(_PECListener.GetTopOrderStackState(), entityCard.entityType))
                    continue;
                if (eff.speed < _PECListener.GetTopOrderSpeed())
                    continue;
            }
            res.Add(eff);
        }
        return res;
    }

    private async UniTask ActivateEff(EffLogic.Effection eff)        //效果发动:支付cost->压入连锁栈->问询/结算
    {
        await eff.CostPay();                                         
        canActiveEffs.Remove(eff);                                   //已发动效果移出候选(防同窗口重复发动)
        await AskChain(eff);                                        
        CloseHightLight();
    }
    
    private async UniTask AskChain(EffLogic.Effection eff)
    {
        StateMachine.EffOrder effOrder = new StateMachine.EffOrder(  // 把EffAction包成命令
        0,                                                      
        entityCard.GetCurrentOwner()?.GetPlayerIndex().ToString() ?? "0",   
        entityCard.entityType,                                   
        //effOrderType：携带效果实际类型,供JudLastEffCost等判断连锁中上一个效果;未配置则回退"active"
        eff.effType != null && eff.effType.Count > 0 ? eff.effType.ToArray() : new string[] { "active" },
        () => eff.EffAction(),                                   
        eff.speed,                                               
        eff.orderStackState                                     
    );
        _PECListener?.PushOrderStack(effOrder);
        window3.gameObject.SetActive(false);

        if (_PECListener.IsChainAsk())                               //已在连锁问询中
        {
            _PECListener.ReportChainActivate(true);                  //通知正在问询的循环：效果已发动
        }
        else if (_PECListener.IsTriggerAsk())                         //正在等待诱发效果发动
        {
            _PECListener.ReportTriggerActivate(true);                 //通知诱发问询循环：诱发已发动
        }
        else
        {
            await AskOrderStack();                                   //完整问询 ,无人连锁则结算
        }
    }
    private void OnReturnToPool()
    {
        SimpleClickHandler simpleClickHandler = this.gameObject.GetComponent<SimpleClickHandler>();
        if (simpleClickHandler != null)
        {
            DestroyImmediate(simpleClickHandler);   //立即销毁:Destroy延迟到帧末,同帧被Get复用时会残留旧组件(AddComponent后形成双handler,点击重复触发)
        }
        if (this.entityCard == null && (effEctions == null || effEctions.Count == 0))
            return;                                                     //幂等:延迟销毁/重复回收时对象已被复用并重新Initialize,跳过避免误清复用后的绑定
        if (effEctions != null)
        {
            for (int i = 0; i < effEctions.Count; i++)                  //去除绑定的诱发效果
            {
                effEctions[i]?.DeleteTriggerEff();
            }
            effEctions.Clear();
            effEctions = null;
        }
        SetCanUsePoint(true);
        CloseHightLight();
        window2.gameObject.SetActive(false);
        window3.gameObject.SetActive(false);
        if (canActiveEffs != null)
            canActiveEffs.Clear();
        OnEffLaunching = null;                          //归还对象池时清掉外部注入的发动收尾回调,防止复用后残留
    }


}
