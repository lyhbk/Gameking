
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static ClickEvent;
using static EffLogic;
using static ExternalQuestBase.UILinkLog;


public class FightUI : MonoBehaviour,UIListener
{

    public ObjectPool objectPool { get; private set; }
    [SerializeField] private GameObject preEntCar;

    [SerializeField] private Image cardShow;
    [SerializeField] private Text effectionShow;
    [SerializeField] private Button surrender;

    [SerializeField] private GameObject Settlement;
    [SerializeField] private Image SettlementVec;
    [SerializeField] private Image SettlementDef;
    [SerializeField] private Button ReturnMainScene;
    private bool duelResultShown;                               //结算面板是否已弹出

    [SerializeField] private Image[] PlaMonsReg;
    [SerializeField] private Image[] PlaMagReg;
    [SerializeField] private Image[] EnemyMonsReg;
    [SerializeField] private Image[] EnemyMagReg;
    [SerializeField] private Image[] ExtraMonster;
    [SerializeField] private Image venueReg;

    [SerializeField] private Image PlaCem;
    [SerializeField] private Button PlaCemBut;
    [SerializeField] private Image EneCem;
    [SerializeField] private Button EneCemBut;
    [SerializeField] private Image PlaBan;
    [SerializeField] private Button PlaBanBut;
    [SerializeField] private Image EneBan;
    [SerializeField] private Button EneBanBut;

    [SerializeField] private GameObject PlayerUI;
    [SerializeField] private GameObject EnemyUI;
    private Image PlayerHp;
    private Image EnemyHp;
    private Text PlayerHPNum;
    private Text EnemyHPNum;

    [SerializeField] GridLayoutGroup PlaHandReg;
    [SerializeField] GridLayoutGroup EneHandReg;


    [SerializeField] private Button nextPhase;

    [SerializeField] private GameObject baseRabackObj;
    [SerializeField] private Text baseReback;
    [SerializeField] private Text boutInforMation;

    [SerializeField] private GameObject ScrollAwakeCon;
    [SerializeField] private Transform content;
    [SerializeField] private Button scrollClose;

    [SerializeField] private Image EffActionQuest;
    [SerializeField] private Text PhaseEndInf;
    [SerializeField] private Button confirmNextPhase;
    [SerializeField] private Button cancelNextPhase;

    [SerializeField] private Image askOrderStackImg;
    [SerializeField] private Text askOrderStackText;
    [SerializeField] private Button ComHandle;
    [SerializeField] private Button ConAddEff;

    [SerializeField] private Image askTriggerImg;
    [SerializeField] private Text askTriggerText;
    [SerializeField] private Button ComTriggerHandle;
    [SerializeField] private Button DelTriggerAddEff;

    [SerializeField] private Button EffEctBut1;
    [SerializeField] private Text EffEctText1;
    [SerializeField] private Button EffEctBut2;
    [SerializeField] private Text EffEctText2;
    [SerializeField] private Button EffEctBut3;
    [SerializeField] private Text EffEctText3;
    [SerializeField] private GameObject EffEctObj;               //区域入口/效果选择面板整体(EffEctBut1/2等按钮所在父级)
    [SerializeField] private Button DeleteEff;               //诱发/连锁"等待点卡
    private UILinkLogicIn requestHandler;          

    private int curRegionPlayer;                                    //当前选中的区域所属玩家
    private GameManage.CardLocation curRegionLoc;                   //当前选中的区域
    private bool regionScrollActive;                                //滚动视图content正显示区域浏览/发动内容
    private readonly Dictionary<Transform, bool> browseCreatedMap = new Dictionary<Transform, bool>();   //content内浏览卡来源

    private Button[] EffEctButs;                   //效果发动选择按钮组
    private Text[] EffEctTexts;                    //效果编号文本

    [SerializeField] private Button ComPlaCem;
    [SerializeField] private Button ComPlaBan;
    [SerializeField] private Button ComPlaExt;
    [SerializeField] private Button ComEmeCem;
    [SerializeField] private Button ComEmeBan;
    [SerializeField] private Button ComEmeExt;

    [SerializeField] private Button OutRecord;                  //退出回放场景按钮(仅record场景显示)

    //回放模式:当前场景为record(回放场景)时为真——场景由fighting复制,UI全在,但不具备对局逻辑,整局由命令驱动
    public bool isRecordMode { get; private set; }
    public RecordSceneRoot recordRoot { get; private set; }     //回放器(回放模式下自动挂载)

    private void Awake()
    {
        isRecordMode = GameManage.IsRecordScene();               //场景判定:record场景=回放模式
        UnityEngine.Debug.Log($"[回放] FightUI.Awake 场景={GameManage.CurSceneName} 回放模式={isRecordMode}");
        objectPool = new ObjectPool(preEntCar, 50);

        EffEctButs = new Button[] { EffEctBut1, EffEctBut2, EffEctBut3 };
        EffEctTexts = new Text[] { EffEctText1, EffEctText2, EffEctText3 };

        PlayerHp = PlayerUI.transform.GetChild(3).GetComponent<Image>();
        PlayerHPNum = PlayerUI.transform.GetChild(4).GetComponent<Text>();
        EnemyHp = EnemyUI.transform.GetChild(3).GetComponent<Image>();
        EnemyHPNum = EnemyUI.transform.GetChild(4).GetComponent<Text>();
        ButtonBand();
        UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
        for (int i = 0; i < 5; i++)
        {

            RegClick regClick = PlaMonsReg[i].AddComponent<RegClick>();
            regClick.GiveNum(i);
            regClick.GetRegClickEvent(_UILinkLogic);

            regClick = PlaMagReg[i].AddComponent<RegClick>();
            regClick.GiveNum(i);
            regClick.GetRegClickEvent(_UILinkLogic);

            regClick = EnemyMonsReg[i].AddComponent<RegClick>();
            regClick.GiveNum(i);
            regClick.GetRegClickEvent(_UILinkLogic);

            regClick = EnemyMagReg[i].AddComponent<RegClick>();
            regClick.GiveNum(i);
            regClick.GetRegClickEvent(_UILinkLogic);
        }

        SubscribeBoutInforMation();                     //订阅状态机事件：阶段/时点动态显示
    }
    private void OnDestroy()
    {
        UnsubscribeBoutInforMation();                   //取消订阅，防止事件泄漏
    }

    private void Start()
    {
        IniUI();

        if (isRecordMode)
        {
            UnityEngine.Debug.Log("[回放] FightUI.Start 进入回放模式");
            EnterRecordMode();                  //回放场景:不向逻辑层请求初始手牌(由回放命令重建),进入即开始回放
            return;
        }

        DrawCardRequest drawCardRequest = new DrawCardRequest(0, 5);
        requestHandler?.DrawCardHandle(drawCardRequest);
        drawCardRequest = new DrawCardRequest(1, 5);
        requestHandler?.DrawCardHandle(drawCardRequest);
    }

    #region 回放模式(record场景)

    //进入回放模式:挂上回放器并即刻开始回放(不依赖手写场景配置,FightUI 自身即可自举)
    private void EnterRecordMode()
    {
        recordRoot = GetComponent<RecordSceneRoot>();
        bool created = recordRoot == null;
        if (recordRoot == null)
            recordRoot = gameObject.AddComponent<RecordSceneRoot>();
        recordRoot.BindFightUI(this);
        UnityEngine.Debug.Log($"[回放] 回放器{(created ? "自动创建" : "场景已有")} 间隔={recordRoot.PlayInterval} 秒");
        recordRoot.PlayNow();                                             //进入回放场景即刻开始回放
    }

    //退出回放场景:中断回放并回主菜单(记录保留在 RecordTransfer,可从mainscene再次进入回放)
    public void ExitRecordScene()
    {
        if (recordRoot != null) recordRoot.Stop();
        FightAsyncScope.End();                  //兜底:结束全部在途异步
        GameManage.ExitRecordScene();
    }

    #endregion

    #region 回合信息显示（当前阶段 + 当前时点，事件驱动）
    private void SubscribeBoutInforMation()
    {
        if (requestHandler == null) return;
        UnsubscribeBoutInforMation();                               //幂等：先解除再订阅，防止GiveRequestHandler补订阅时重复
        requestHandler.OnCurrentPointChanged += OnBoutInforChanged; //时点变化 → 刷新
        requestHandler.OnGamePhaseChanged += OnBoutInforChanged;    //阶段变化 → 刷新
        RefreshBoutInforMation();                                   //初始化显示一次
    }
    private void UnsubscribeBoutInforMation()
    {
        if (requestHandler == null) return;
        requestHandler.OnCurrentPointChanged -= OnBoutInforChanged;
        requestHandler.OnGamePhaseChanged -= OnBoutInforChanged;
    }
    private void OnBoutInforChanged(StateMachine.CurrentPoint _) { RefreshBoutInforMation(); }
    private void OnBoutInforChanged(GameManage.GamePhase phase)
    {
        RefreshBoutInforMation();
        if (phase == GameManage.GamePhase.FightPhase)
        {
            requestHandler.ResetBoutAttackFlags();                  //重置双方怪兽攻击状态(规则下沉FightLogic)
            EnterFightPhase();                                      //进入战斗阶段：高亮+攻击按钮
        }
        else if (phase == GameManage.GamePhase.Main2Phase)
        {
            ExitFightPhase();                                       //离开战斗阶段：清除高亮与攻击按钮
        }
    }
    private void RefreshBoutInforMation()                           //仅在阶段/时点变化时被事件触发，无每帧开销
    {
        if (boutInforMation == null || requestHandler == null) return;
        boutInforMation.text = $"阶段:{GetPhaseName(requestHandler.GetCurGamePhase())}  时点:{GetPointName(requestHandler.GetCurrentPoint())}";
    }
    private string GetPhaseName(GameManage.GamePhase phase)
    {
        switch (phase)
        {
            case GameManage.GamePhase.DrawCardPhase: return "抽卡";
            case GameManage.GamePhase.PreparePhase: return "准备";
            case GameManage.GamePhase.Main1Phase: return "主1";
            case GameManage.GamePhase.FightPhase: return "战斗";
            case GameManage.GamePhase.Main2Phase: return "主2";
            case GameManage.GamePhase.EndPhase: return "结束";
            default: return phase.ToString();
        }
    }
    private string GetPointName(StateMachine.CurrentPoint point)
    {
        switch (point)
        {
            case StateMachine.CurrentPoint.Freedom: return "自由";
            case StateMachine.CurrentPoint.Target: return "诱发";
            case StateMachine.CurrentPoint.Attack: return "战斗";
            case StateMachine.CurrentPoint.Chain: return "连锁";
            default: return point.ToString();
        }
    }
    #endregion


    private void ButtonBand()
    {
        surrender.onClick.AddListener(() =>
        {
            ChangeHPToRequest changeHPRequest = new ChangeHPToRequest(0,0);
            requestHandler?.ChangeHPToRequestHandle(changeHPRequest);
        });


        nextPhase.onClick.AddListener(() =>
        {
            if (FightAsyncScope.IsCanceled) return;     //对局已结束:不再推进阶段
            ChangeBout changeBout = new ChangeBout();
            requestHandler?.ChangeBoutToRequestHandle(changeBout);
        });

        scrollClose.onClick.AddListener(() =>
        {
            if (regionScrollActive)                 //区域浏览/发动内容在滚动区内:按区域归位并关闭(真实区域卡不能走对象池Release)
            {
                RegionBrowseClose();
                return;
            }
            RefreshContent();
            scrollClose.gameObject.SetActive(false);
        });

        //区域按钮:点选浏览目标区域(Cem墓地/Ban除外/Ext额外卡组),随后激活"确认区域卡牌/确认区域效果"入口(EffEctBut1/2)
        BindComRegionButton(ComPlaCem, 0, GameManage.CardLocation.Cemetery);
        BindComRegionButton(ComPlaBan, 0, GameManage.CardLocation.Banished);
        BindComRegionButton(ComPlaExt, 0, GameManage.CardLocation.ExtraDeck);
        BindComRegionButton(ComEmeCem, 1, GameManage.CardLocation.Cemetery);
        BindComRegionButton(ComEmeBan, 1, GameManage.CardLocation.Banished);
        BindComRegionButton(ComEmeExt, 1, GameManage.CardLocation.ExtraDeck);

        confirmNextPhase.onClick.AddListener(() => { 
            GiveTrue();
            HideEffActionQuest();
        });
        cancelNextPhase.onClick.AddListener(() => { 
            GiveFalse(); 
            HideEffActionQuest(); });

        ReturnMainScene.onClick.AddListener(() => 
        {
            FightAsyncScope.End();              //返回主场景兜底:结束对局全部在途异步(结算时已触发,重复安全)
            GameManage.ChangeScene(GameManage.MainSceneName);                //返回主菜单场景(mainscene.unity,场景名全小写)
        });
        //回放场景:已配好 OutRecord 时隐藏"返回主菜单",统一走 OutRecord(会先中断回放再切场景)。
        //未配 OutRecord 时保留"返回主菜单"作为退路,避免回放场景无处可退。
        if (isRecordMode && OutRecord != null && ReturnMainScene != null)
            ReturnMainScene.gameObject.SetActive(false);
        ComHandle.onClick.AddListener(() =>
        {
            if (waitAskOrderStack != null)
            {
                GetWaitAskOrderStack(false);                //问询中：放弃连锁 → 返回false → 结算
                return;
            }
            ReportChainActivate(false);                     //连锁等待中：点【否】放弃 → 结束等待并结算
        });
        ConAddEff.onClick.AddListener(() =>
        {
            GetWaitAskOrderStack(true);     //继续连锁 → 返回true → 等待玩家发动其他效果
        });
        ComTriggerHandle.onClick.AddListener(() =>
        {
            if (waitAskTrigger != null)
                GetWaitAskTrigger(true);    //发动诱发 → 执行StartTriggerEff（诱发等待中该按钮隐藏，无作用）
        });
        DelTriggerAddEff.onClick.AddListener(() =>
        {
            if (waitAskTrigger != null)
            {
                GetWaitAskTrigger(false);   //不发动 → 清空timePointBases
                return;
            }
            ReportTriggerActivate(false);   //诱发等待中：点【否】放弃 → 结束等待
        });
        if (DeleteEff != null)
        {
            DeleteEff.onClick.AddListener(() =>      //诱发/连锁"等待点卡(面板已关)"专用：诱发阶段点它=退出诱发；连锁阶段点它=回退本次[是]重新询问；询问中可充当[否]
            {
                if (triggerActivateTcs != null)
                {
                    ClearTriggerWaitCardUI();                       //退出诱发:关闭可发动而未点击发动的诱发卡高亮与window3
                    triggerActivateTcs.TrySetResult(false);
                    triggerActivateTcs = null;
                    return;
                }
                if (chainActivateTcs != null)
                {
                    chainActivateTcs.TrySetResult(false);
                    chainActivateTcs = null;
                    return;
                }
                if (waitAskTrigger != null)
                {
                    GetWaitAskTrigger(false);
                    return;
                }
                if (waitAskOrderStack != null)
                {
                    GetWaitAskOrderStack(false);
                    return;
                }
                DeleteEff.gameObject.SetActive(false);
                isTriggerAsk = false;
                isChainAsk = false;
            });
            DeleteEff.gameObject.SetActive(false);   //默认隐藏:仅诱发/连锁等待点卡阶段由SetDeleteEffVisible亮出
        }

        if (OutRecord != null)
        {
            //退出回放场景按钮:仅在回放场景(record)亮出,对局场景(fighting)隐藏
            OutRecord.onClick.AddListener(() => ExitRecordScene());
            OutRecord.gameObject.SetActive(isRecordMode);
        }
        else if (isRecordMode)
            Debug.LogWarning("[FightUI] 回放场景未指定退出按钮:请在 Inspector 里把退出按钮拖到 FightUI 的 OutRecord");
    }
    #region HP变化
    public void VecUI()                                        //玩家胜利结算:血量改变总事件完成后对方血量<=0时弹出
    {
        if (duelResultShown) return;
        duelResultShown = true;
        FightAsyncScope.End();              //对局结束:终止本局全部在途异步
        DuelFinishedCleanup();
        if (Settlement == null) return;
        Settlement.SetActive(true);
        if (SettlementDef != null) SettlementDef.gameObject.SetActive(false);
        if (SettlementVec != null) SettlementVec.gameObject.SetActive(true);
    }
    public void DefUI()                                        //玩家败北结算:我方血量<=0(含同归于尽/投降)时弹出
    {
        if (duelResultShown) return;
        duelResultShown = true;
        FightAsyncScope.End();              //对局结束:终止本局全部在途异步
        DuelFinishedCleanup();
        if (Settlement == null) return;
        Settlement.SetActive(true);
        if (SettlementVec != null) SettlementVec.gameObject.SetActive(false);
        if (SettlementDef != null) SettlementDef.gameObject.SetActive(true);
    }

    //对局结束的UI清理:隐藏仍在等待/询问中的残留面板并复位,避免其悬停在结算界面
    private void DuelFinishedCleanup()
    {
        if (EffActionQuest != null) EffActionQuest.gameObject.SetActive(false);
        if (askOrderStackImg != null) askOrderStackImg.gameObject.SetActive(false);
        if (askTriggerImg != null) askTriggerImg.gameObject.SetActive(false);
        if (DeleteEff != null) DeleteEff.gameObject.SetActive(false);
        if (ScrollAwakeCon != null) ScrollAwakeCon.SetActive(false);
        CloseAllHighLight();
    }

    public void IniUI()
    {
        OnHPChangeToHandle(0,8000);
        OnHPChangeToHandle(1, 8000);
    }

    #endregion

    #region 通召盖放
    private UniTaskCompletionSource<List<Transform>> sacrificeTcs;
    [SerializeField] private Transform sumOrCover;          //通召/盖放怪兽：待放置预制体（仅通召/盖放流程读写）
    private Transform speSumOrCover;                        //特招专用：待放置预制体（与通召/盖放完全隔离，互不污染）
    private int sumOrCovMode = 0;                           //当前待放置模式：0=通召/盖放，1=特招
    [SerializeField] private Transform magicOrTrap;
    private int requiredSacrificeCount;                                     //需要的祭品数量（通召）
    private bool isWaitingForAsyncOperation = false;
    public bool IsWaitingForAsync => isWaitingForAsyncOperation;

    private bool IsWaitingForSacrifice;
    public List<Transform> selectedSacrifices = new List<Transform>();
    public void GetSumOrCover(Transform sumOrCover)
    {
        this.sumOrCover = sumOrCover;
        sumOrCovMode = 0;                                   //标记为通召/盖放模式
    }
    public bool IsSumOrCovMode()                            //当前待放置模式是否为通召/盖放(0=通召/盖放,1=特招)——通召成功广播判定用
    {
        return sumOrCovMode == 0;
    }
    public void GetMagicTrap(Transform magicOrTrap)
    {
        this.magicOrTrap = magicOrTrap;
    }
    //回放专用:取 IniSpeSom 刚建好的特招预制体。魔陷落位走 LayOutMagicTrapCardHandle,
    //它用的是 magicOrTrap(不是 speSumOrCover),回放必须手工把两者对上。
    public Transform GetSpeSumOrCover() { return speSumOrCover; }
    public void PlaceCardToEnemyField(Transform transform)                      //AI通召：将怪兽卡放置到敌方怪兽区第一个空位
    {
        foreach (var reg in EnemyMonsReg)
        {
            if (reg.transform.childCount == 0)
            {
                transform.SetParent(reg.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                return;
            }
        }
    }
    public bool GetIsWaitingForSacrifice()
    {
        return IsWaitingForSacrifice;
    }
    public void RequestSacrifice(int count)
    {
        requiredSacrificeCount = count;
        if (requiredSacrificeCount == 0)
            IsWaitingForSacrifice = false;
        else
            IsWaitingForSacrifice = true;
    }
    public async UniTask<List<Transform>> WaitForSacrificeSelection()
    {

        isWaitingForAsyncOperation = true;
        try
        {
            sacrificeTcs = new UniTaskCompletionSource<List<Transform>>();
            List<Transform> result = await sacrificeTcs.Task.AttachExternalCancellation(FightAsyncScope.Token);
            RestoreHight();
            sacrificeTcs = null;

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;                                  //对局已结束:终止该异步链
        }
        catch (System.Exception)
        {
            return new List<Transform>();
        }
        finally
        {
            isWaitingForAsyncOperation = false;
        }
    }
    public void ReportSacrificeSelectionComplete()
    {
        if (sacrificeTcs != null)
        {
            sacrificeTcs.TrySetResult(selectedSacrifices);
            sacrificeTcs = null;
        }
    }

    public void ReportSacrificeSelectionCanceled()
    {
        if (sacrificeTcs != null)
        {
            sacrificeTcs.TrySetResult(new List<Transform>());
            sacrificeTcs = null;
        }
    }
    public int GetRequiredSacrificeCount()
    {
        return requiredSacrificeCount;
    }
    public void AddSelSac(Transform transform)
    {
        selectedSacrifices.Add(transform);
    }
    public List<Transform> GetSelSac()
    {
        return selectedSacrifices;
    }
    public void IniSc()
    {
        selectedSacrifices.Clear();
    }
    public void IniSum()
    {
        requiredSacrificeCount = 0;
        RequestSacrifice(0);
    }


    public void AwakeHight()
    {
        for (int i = 0; i < 5; i++)
        {
            if (PlaMonsReg[i].transform.childCount != 0)
            {
                PreEntityCard preEntityCard = PlaMonsReg[i].transform.GetChild(0).GetComponent<PreEntityCard>();
                preEntityCard.AwakeHighLight();
            }
        }
    }

    public void RestoreHight()
    {
        foreach (var reg in PlaMonsReg)
        {
            if (reg.transform.childCount != 0)
            {
                PreEntityCard preEntityCard = reg.transform.GetChild(0).GetComponent<PreEntityCard>();
                preEntityCard.CloseHightLight();
            }
        }
    }

    #region 战斗阶段：攻击高亮与攻击按钮
    public void EnterFightPhase()                                   //进入战斗阶段：自己可攻击怪兽高亮+激活攻击按钮；对方可被攻击怪兽高亮
    {
        if (requestHandler == null) return;
        int owner = requestHandler.GetOnwerPhase();                 //当前回合玩家
        Image[] myReg = owner == 0 ? PlaMonsReg : EnemyMonsReg;
        Image[] eneReg = owner == 0 ? EnemyMonsReg : PlaMonsReg;
        //自己可攻击怪兽（规则判定已下沉FightLogic.IsMonsterAttackable）：高亮 + 激活攻击按钮
        //AI回合只高亮、不给攻击按钮：玩家不能替AI的怪兽宣言攻击
        foreach (var reg in myReg)
        {
            if (reg.transform.childCount == 0) continue;
            PreEntityCard pre = reg.transform.GetChild(0).GetComponent<PreEntityCard>();
            if (owner == 0 && requestHandler.IsMonsterAttackable(pre.GetEntityCard()))
            {
                pre.AwakeHighLight();
                pre.SetAttackButton(true);
            }
            else
            {
                pre.CloseHightLight();
                pre.SetAttackButton(false);
            }
        }
        //对方可被攻击怪兽（规则判定已下沉FightLogic.IsMonsterTargetable）：高亮（提示可选攻击目标）
        foreach (var reg in eneReg)
        {
            if (reg.transform.childCount == 0) continue;
            PreEntityCard pre = reg.transform.GetChild(0).GetComponent<PreEntityCard>();
            if (requestHandler.IsMonsterTargetable(pre.GetEntityCard()))
                pre.AwakeHighLight();
            else
                pre.CloseHightLight();
        }
    }
    public void ExitFightPhase()                                    //离开战斗阶段：清除高亮与攻击按钮
    {
        ClearBattleUI(PlaMonsReg);
        ClearBattleUI(EnemyMonsReg);
    }
    private void ClearBattleUI(Image[] regs)
    {
        foreach (var reg in regs)
        {
            if (reg.transform.childCount == 0) continue;
            PreEntityCard pre = reg.transform.GetChild(0).GetComponent<PreEntityCard>();
            pre.CloseHightLight();
            pre.SetAttackButton(false);
        }
    }
    #endregion

    #endregion


    #region 基础UI

    public void ShowCardPic(string id, string effect)
    {
        GameManage.SetImage(cardShow, id);
        effectionShow.text = effect;
    }
    public void RebackText(string text, int lim = 0)
    {
        baseReback.text = text;
        baseRabackObj.SetActive(true);
        if (lim != 0)
            StartCoroutine(enumerator());
    }
    private void ClearReback()
    {
        baseReback.text = "";
        baseRabackObj.SetActive(false);
    }
    private IEnumerator enumerator()
    {
        yield return new WaitForSeconds(3f);
        ClearReback();
    }
    private bool sumOrCoverLock = false;
    public bool GetSumOrCoverLock() 
    { 
        return sumOrCoverLock; 
    }
    private bool magicTrapLock = false;
    public bool GetMagicTrapLock()
    {
        return magicTrapLock;
    }

    public void AwakeRegion(int i)
    {
        switch (i)
        {
            case 0:                                                     //通召/盖放怪兽：仅开启通召锁
                sumOrCoverLock = true;
                foreach (Image item in PlaMonsReg)
                {
                    if (item.transform.childCount == 0)
                    {
                        Image image = item.GetComponent<Image>();
                        image.color = Color.blue;
                    }
                }
                break;
            case 1:                                                     //盖放/发动魔陷：仅开启魔陷锁
                magicTrapLock = true;
                foreach (Image item in PlaMagReg)
                {
                    if (item.transform.childCount == 0)
                    {
                        Image image = item.GetComponent<Image>();
                        image.color = Color.blue;
                    }
                }
                break;

        }
    }






    #region 槽位码(命令记录/回放共用)

    //区域格 → 槽位码(见 Record.Slot):0-4我方怪兽区 / 5-9对方怪兽区 / 10-14我方魔陷区 / 15-19对方魔陷区
    public int GetSlotCode(Transform region)
    {
        if (region == null) return Record.Slot.Unknown;
        int code = IndexOfRegion(PlaMonsReg, region, Record.Slot.PlayerMonster);
        if (code >= 0) return code;
        code = IndexOfRegion(EnemyMonsReg, region, Record.Slot.EnemyMonster);
        if (code >= 0) return code;
        code = IndexOfRegion(PlaMagReg, region, Record.Slot.PlayerMagicTrap);
        if (code >= 0) return code;
        code = IndexOfRegion(EnemyMagReg, region, Record.Slot.EnemyMagicTrap);
        return code;
    }

    //槽位码 → 区域格(回放时按命令里的槽位码直接取到那一格)
    public Transform GetSlotTransform(int slotCode)
    {
        if (slotCode == Record.Slot.Unknown) return null;
        if (slotCode < Record.Slot.EnemyMonster) return RegionAt(PlaMonsReg, slotCode - Record.Slot.PlayerMonster);
        if (slotCode < Record.Slot.PlayerMagicTrap) return RegionAt(EnemyMonsReg, slotCode - Record.Slot.EnemyMonster);
        if (slotCode < Record.Slot.EnemyMagicTrap) return RegionAt(PlaMagReg, slotCode - Record.Slot.PlayerMagicTrap);
        return RegionAt(EnemyMagReg, slotCode - Record.Slot.EnemyMagicTrap);
    }

    //回放专用:按卡类型找该玩家对应区域的第一个空槽位。
    //通召/魔法陷阱发动上场这类命令没有"特招落位区号"(它只在特招时由玩家点蓝格产生),
    //但统一登记的前后id表明卡确实上了场,故由回放按卡类型选区:怪兽→怪兽区,魔法/陷阱→魔陷区。
    public int GetEmptySlotCode(EffLogic.EntityCard card, int playerIndex)
    {
        bool enemy = playerIndex == 1;
        bool monster = card is EffLogic.EntityMonsterCard;
        Image[] regs = monster ? (enemy ? EnemyMonsReg : PlaMonsReg) : (enemy ? EnemyMagReg : PlaMagReg);
        int baseCode = monster ? (enemy ? Record.Slot.EnemyMonster : Record.Slot.PlayerMonster)
                               : (enemy ? Record.Slot.EnemyMagicTrap : Record.Slot.PlayerMagicTrap);
        if (regs == null) return Record.Slot.Unknown;
        for (int i = 0; i < regs.Length; i++)
            if (regs[i] != null && regs[i].transform.childCount == 0)
                return baseCode + i;
        return Record.Slot.Unknown;                //对应区域已满:无空位可落
    }

    private static int IndexOfRegion(Image[] regs, Transform region, int baseCode)
    {
        if (regs == null) return Record.Slot.Unknown;
        for (int i = 0; i < regs.Length; i++)
            if (regs[i] != null && regs[i].transform == region) return baseCode + i;
        return Record.Slot.Unknown;
    }
    private static Transform RegionAt(Image[] regs, int index)
    {
        return regs != null && index >= 0 && index < regs.Length && regs[index] != null ? regs[index].transform : null;
    }

    //回放专用:直接刷新"阶段/回合"显示(不经逻辑层)
    public void ShowPhase(GameManage.GamePhase phase, int owner)
    {
        if (boutInforMation == null) return;
        boutInforMation.text = $"阶段:{GetPhaseName(phase)}  回合:{(owner == 0 ? "我方" : "对方")}";
    }

    //回放专用:按实体卡找到它在场上的预制体 Transform(手牌/怪兽区/魔陷区/墓地/除外),找不到返回null。
    //回放要把卡"送去墓地"时,必须先拿到它当前的预制体才能把同一个物体移进墓地容器(否则只能在墓地新建)。
    public Transform FindCardPrefab(EffLogic.EntityCard card)
    {
        if (card == null) return null;
        Transform t = FindCardPrefabIn(PlaHandReg != null ? PlaHandReg.transform : null, card)
                   ?? FindCardPrefabIn(EneHandReg != null ? EneHandReg.transform : null, card);
        if (t != null) return t;
        t = FindCardPrefabInRegions(PlaMonsReg, card) ?? FindCardPrefabInRegions(PlaMagReg, card)
         ?? FindCardPrefabInRegions(EnemyMonsReg, card) ?? FindCardPrefabInRegions(EnemyMagReg, card);
        if (t != null) return t;
        return FindCardPrefabIn(PlaCem != null ? PlaCem.transform : null, card)
            ?? FindCardPrefabIn(EneCem != null ? EneCem.transform : null, card)
            ?? FindCardPrefabIn(PlaBan != null ? PlaBan.transform : null, card)
            ?? FindCardPrefabIn(EneBan != null ? EneBan.transform : null, card);
    }
    //回放专用:取墓地/除外容器(送墓/除外落位目标),便于回放核对落位目标是否存在(为null会静默跳过移动)
    public Transform GetCemeteryReg(bool enemy)
    {
        return enemy ? (EneCem != null ? EneCem.transform : null) : (PlaCem != null ? PlaCem.transform : null);
    }
    public Transform GetBanishedReg(bool enemy)
    {
        return enemy ? (EneBan != null ? EneBan.transform : null) : (PlaBan != null ? PlaBan.transform : null);
    }

    //回放专用:卡的持有者是否为对方(决定进哪个墓地/除外容器)
    public bool IsOwnerEnemyPublic(EffLogic.EntityCard card)
    {
        return IsOwnerEnemy(card);
    }

    //回放专用:取手牌区容器(回放登记"卡→预制体"映射用)
    public Transform GetHandReg(int playerIndex)
    {
        GridLayoutGroup reg = playerIndex == 1 ? EneHandReg : PlaHandReg;
        return reg != null ? reg.transform : null;
    }

    //回放专用:打印手牌区实际存在的"卡名@id",用于核对回放登记的id与UI里的实体id是否对得上
    public string DumpHandIds()
    {
        List<string> parts = new List<string>();
        AppendHandIds(PlaHandReg != null ? PlaHandReg.transform : null, "我方", parts);
        AppendHandIds(EneHandReg != null ? EneHandReg.transform : null, "对方", parts);
        return parts.Count == 0 ? "空" : string.Join(" / ", parts.ToArray());
    }
    private static void AppendHandIds(Transform reg, string label, List<string> parts)
    {
        if (reg == null) return;
        for (int i = 0; i < reg.childCount; i++)
        {
            PreEntityCard pec = reg.GetChild(i).GetComponent<PreEntityCard>();
            EffLogic.EntityCard c = pec != null ? pec.GetEntityCard() : null;
            parts.Add($"{label}:{(c == null ? "?" : c.currentName)}@{c?.currentid ?? "?"}");
        }
    }

    //回放专用:按"实例id"找到卡的预制体(遍历手牌/怪兽区/魔陷区/墓地/除外)。
    //区域改变时旧id对应的那份UI必须能被精确定位并销毁,故以实体自带的 currentid 为准,不依赖外部映射。
    public Transform FindCardPrefabById(string currentId)
    {
        if (string.IsNullOrEmpty(currentId)) return null;
        Transform t = FindCardPrefabIn(PlaHandReg != null ? PlaHandReg.transform : null, currentId)
                   ?? FindCardPrefabIn(EneHandReg != null ? EneHandReg.transform : null, currentId);
        if (t != null) return t;
        t = FindCardPrefabInRegions(PlaMonsReg, currentId) ?? FindCardPrefabInRegions(PlaMagReg, currentId)
         ?? FindCardPrefabInRegions(EnemyMonsReg, currentId) ?? FindCardPrefabInRegions(EnemyMagReg, currentId);
        if (t != null) return t;
        return FindCardPrefabIn(PlaCem != null ? PlaCem.transform : null, currentId)
            ?? FindCardPrefabIn(EneCem != null ? EneCem.transform : null, currentId)
            ?? FindCardPrefabIn(PlaBan != null ? PlaBan.transform : null, currentId)
            ?? FindCardPrefabIn(EneBan != null ? EneBan.transform : null, currentId);
    }
    private Transform FindCardPrefabIn(Transform parent, string currentId)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            PreEntityCard pec = parent.GetChild(i).GetComponent<PreEntityCard>();
            EffLogic.EntityCard card = pec != null ? pec.GetEntityCard() : null;
            if (card != null && card.currentid == currentId) return parent.GetChild(i);
        }
        return null;
    }
    private Transform FindCardPrefabInRegions(Image[] regs, string currentId)
    {
        if (regs == null) return null;
        for (int i = 0; i < regs.Length; i++)
        {
            Transform t = FindCardPrefabIn(regs[i] != null ? regs[i].transform : null, currentId);
            if (t != null) return t;
        }
        return null;
    }

    //回放专用:解绑并归还预制体到对象池。
    //区域改变(送墓/除外/特招等)时,旧区域的那个UI必须彻底消失——不搬走、不留残留,
    //新区域改用"转移后id产生的新实体"另行新建,从根上杜绝同一张卡出现两份UI。
    public void ReleaseCardPrefab(Transform item)
    {
        if (item == null) return;
        item.SetParent(this.transform, false);      //先移出原区域容器(场上/手牌/墓地),防对象池复用残留
        objectPool.Release(item.gameObject);
    }

    //兜底:实例id对不上时按"卡名id + 是否为同一玩家方"找一份预制体。
    //宁可匹配到同名的另一份也不能在墓地新建——否则会变成场上残留一份、墓地多一份。
    public Transform FindCardPrefabByName(EffLogic.EntityCard card)
    {
        if (card?.card == null) return null;
        bool enemy = IsOwnerEnemy(card);
        Transform t = FindCardPrefabIn(PlaHandReg != null ? PlaHandReg.transform : null, card, enemy, true)
                   ?? FindCardPrefabIn(EneHandReg != null ? EneHandReg.transform : null, card, enemy, true);
        if (t != null) return t;
        t = FindCardPrefabInRegions(PlaMonsReg, card, true) ?? FindCardPrefabInRegions(PlaMagReg, card, true)
         ?? FindCardPrefabInRegions(EnemyMonsReg, card, true) ?? FindCardPrefabInRegions(EnemyMagReg, card, true);
        return t;
    }
    private Transform FindCardPrefabIn(Transform parent, EffLogic.EntityCard card)
    {
        return FindCardPrefabIn(parent, card, false, false);
    }
    private Transform FindCardPrefabIn(Transform parent, EffLogic.EntityCard card, bool enemy, bool byName)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            PreEntityCard pec = parent.GetChild(i).GetComponent<PreEntityCard>();
            if (pec == null) continue;
            EffLogic.EntityCard target = pec.GetEntityCard();
            if (target == null || target.card == null) continue;
            if (!byName)
            {
                if (target == card) return parent.GetChild(i);
            }
            else if (target.card.id == card.card.id && IsOwnerEnemy(target) == enemy)
                return parent.GetChild(i);
        }
        return null;
    }
    private Transform FindCardPrefabInRegions(Image[] regs, EffLogic.EntityCard card)
    {
        return FindCardPrefabInRegions(regs, card, false);
    }
    private Transform FindCardPrefabInRegions(Image[] regs, EffLogic.EntityCard card, bool byName)
    {
        if (regs == null) return null;
        bool enemy = IsOwnerEnemy(card);
        for (int i = 0; i < regs.Length; i++)
        {
            Transform t = FindCardPrefabIn(regs[i] != null ? regs[i].transform : null, card, enemy, byName);
            if (t != null) return t;
        }
        return null;
    }

    //回放专用:显示回放进度与当前间隔("第几条/共几条 + 间隔秒数"),便于核对每条命令之间确实停顿了设定秒数
    public void ShowRecordProgress(int played, int total, float interval)
    {
        if (boutInforMation == null) return;
        boutInforMation.text = $"回放 {played}/{total}  间隔 {interval:0.##}s";
    }

    #endregion

    public void RestoreRegion()
    {
        foreach (Image item in PlaMonsReg)
        {
            Image image = item.GetComponent<Image>();
            image.color = Color.white;
        }
        foreach (Image item in PlaMagReg)
        {
            Image image = item.GetComponent<Image>();
            image.color = Color.white;
        }
        sumOrCoverLock = false;
        magicTrapLock = false;
    }
    #region 状态机控制回合切换

    public void BoutChangeReback(string s)
    {
        //对方回合同样弹出阶段询问：玩家可在此窗口发动效果（取消=停留当前阶段，确认=继续推进）
        bool oppTurn = requestHandler != null && requestHandler.IsEnemyAIEnable() && requestHandler.GetOnwerPhase() == 1;
        PhaseEndInf.text = oppTurn ? $"[对方回合] {s}" : s;
        AwakeEffActionQuest();
    }

    public void AwakeEffActionQuest()
    {
        EffActionQuest.gameObject.SetActive(true);
    }
    public void HideEffActionQuest()
    {
        EffActionQuest.gameObject.SetActive(false);
    }
    private UniTaskCompletionSource<bool> waitForPlaCor;
    private bool waitForPlaCorLock = false;
    
    public async UniTask<bool> WaitForPlayClick()
    {
        if (waitForPlaCorLock)
        {
            return false;
        }
        try
        {
            waitForPlaCorLock = true;
            waitForPlaCor = new UniTaskCompletionSource<bool>();
            bool res = await waitForPlaCor.Task.AttachExternalCancellation(FightAsyncScope.Token);
            return res;
        }
        catch (OperationCanceledException)
        {
            throw;                                  //对局已结束:终止该异步链
        }
        catch
        {
            return false;
        }
        finally
        {
            waitForPlaCorLock = false;
        }
    }

    private void GiveTrue()
    {
        if (waitForPlaCor != null)
        {
            waitForPlaCor.TrySetResult(true);
            waitForPlaCor = null;
        }
        waitForPlaCorLock = false;
    }

    private void GiveFalse()
    {
        if (waitForPlaCor != null)
        {
            waitForPlaCor.TrySetResult(false);
            waitForPlaCor = null;
        }
        waitForPlaCorLock = false;
    }
    #endregion

    #endregion


    #region 滚动查询显示与点击事件
    public void RefreshContent()
    {
        if (regionScrollActive)                                  //滚动区正显示区域浏览/发动内容(含真实墓地/除外卡预制体):先按区域归位归还,禁止对象池Release真实卡
        {
            RegionBrowseClose();
            return;
        }
        List<Transform> children = new List<Transform>();       //先快照:避免释放过程中content子物体列表变化导致迭代遗漏/越界
        foreach (Transform child in content)
        {
            children.Add(child);
        }
        foreach (Transform child in children)
        {
            objectPool.Release(child.gameObject);
            child.SetParent(null, false);                       //移出content:防止残留卡片被下一次RefreshContent再次遍历回收(配合Release去重双保险)
        }
        ScrollAwakeCon.SetActive(false);
    }
    private UniTaskCompletionSource<List<Transform>> waitEventClick;
    private int needClicknum;
    private List<Transform> needClick = new List<Transform>();
    public void GetneedClicknum(int i = 0)
    {
        needClicknum = i;
    }
    public void AddneedClick(Transform transform)
    {
        needClick.Add(transform);
    }
    public void IniEvrntClick()
    {
        needClicknum = 0;
        needClick.Clear();
    }
    public void InsContent(List<EntityCard> entityCards, int needClickEvent = 0)                        //要进行逻辑  攻击选择逻辑
    {
        RefreshContent();
        ScrollAwakeCon.SetActive(true);
        foreach (var entityCard in entityCards)
        {
            GameObject obj = objectPool.Get();
            obj.transform.SetParent(content, false);
            obj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            PreEntityCard preEntityCard = obj.GetComponent<PreEntityCard>();
            UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
            preEntityCard.Initialize(entityCard, _UILinkLogic);
            if (needClickEvent != 0)
            {
                SimpleClickHandler oldHandler = obj.GetComponent<SimpleClickHandler>();
                if (oldHandler != null)
                    DestroyImmediate(oldHandler);   //防御:复用卡片可能残留旧点击组件,须同帧清除,否则点击事件重复触发(素材重复/计数错乱)
                SimpleClickHandler simpleClickHandler = obj.AddComponent<SimpleClickHandler>();
                simpleClickHandler.OnClicked = () =>
                {
                    EntityCard card = entityCard;
                    needRequst.Add(card);
                    if (needRequst.Count == needClickEvent)
                    {
                        Destroy(simpleClickHandler);
                        preEntityCard.AwakeHighLight();         //先高亮(选中反馈),再整体清理
                        RefreshContent();                       //关闭面板前清空content:弹窗内卡均为对象池临时副本,全部Release回池并隐藏面板,防止残留卡被首次区域浏览/下次弹窗误处理(重复展示/重复入列表)
                        GetQuestClick();                        //清完再唤醒等待方,避免同步恢复的下个弹窗被覆盖
                    }
                    else
                        return;
                };
            }

        }
    }



    public void GetWaitForClickContntCard()
    {
        if (waitEventClick != null)
        {
            waitEventClick.TrySetResult(needClick);
        }
    }
    public void ReportClickContntCardCanceled()
    {
        if (waitEventClick != null && !waitEventClick.Task.Status.IsCompleted())
        {
            waitEventClick.TrySetResult(new List<Transform>());
        }
    }
    #endregion

    #region  专供EffLogic进行外部请求
    private bool IsWaitingQuest = false;
    public bool GetIsWaitingQuest() => IsWaitingQuest;                   //查询选卡弹窗(ShowSelectionDialog)是否打开:等待期互斥锁(UILinkLogic.IsSelectingQuest)

    private UniTaskCompletionSource<List<EntityCard>> waitRequest;
    private List<EntityCard> needRequst = new List<EntityCard>();

    public async UniTask<List<EntityCard>> ShowSelectionDialog(List<EntityCard> entityCards, int needSelectCount)
    {
        if (IsWaitingQuest)
        {
            return null;
        }
        IsWaitingQuest = true;
        try
        {
            needRequst.Clear();                                     //新弹窗开始前清空已选列表:防止上次弹窗异常/残留计数导致本次点击一次即提前达标误关弹窗
            QuestInsContent(entityCards, needSelectCount);          //移入try块:内部异常时finally仍会复位IsWaitingQuest,避免后续所有弹窗被永久屏蔽

            if (waitRequest != null)
            {
                waitRequest.TrySetCanceled();
            }
            waitRequest = new UniTaskCompletionSource<List<EntityCard>>();
            ScrollAwakeCon.SetActive(true);
            List<EntityCard> result = await waitRequest.Task.AttachExternalCancellation(FightAsyncScope.Token);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;                                  //对局已结束:终止该异步链
        }
        catch (Exception e)
        {
            return new List<EntityCard>();
        }
        finally
        {
            IsWaitingQuest = false;
        }
    }
    public void GetQuestClick()
    {
        if (waitRequest != null)
        {
            List<EntityCard> snapshot = new List<EntityCard>(needRequst);   //快照:TrySetResult传引用会被下方Clear清空(异步恢复时返回空列表)
            UniTaskCompletionSource<List<EntityCard>> w = waitRequest;
            waitRequest = null;        //先置空:同步恢复时下一个弹窗新建的waitRequest不会被本方法覆盖
            needRequst.Clear();
            w.TrySetResult(snapshot);
        }
        else
        {
        }
    }
    public void QuestInsContent(List<EntityCard> entityCards, int needSelectCount)              //弹出选择框
    {
        RefreshContent();
        ScrollAwakeCon.SetActive(true);
        foreach (var entityCard in entityCards)
        {
            GameObject obj = objectPool.Get();
            obj.transform.SetParent(content, false);
            obj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

            PreEntityCard preEntityCard = obj.GetComponent<PreEntityCard>();
            UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
            try
            {
                preEntityCard.Initialize(entityCard, _UILinkLogic);
            }
            catch (Exception e)
            {
                //初始化中断:该卡不挂点击组件(避免点了无响应),继续创建其余卡;堆栈见Console,重点排查该卡的代价/效果组件解析
            }
            preEntityCard.SetCanUsePoint(false);
            preEntityCard.CloseHightLight();
            if (needSelectCount != 0)
            {
                SimpleClickHandler oldHandler = obj.GetComponent<SimpleClickHandler>();
                if (oldHandler != null)
                    DestroyImmediate(oldHandler);   //防御:复用卡片可能残留旧点击组件,须同帧清除,否则点击事件重复触发(素材重复/计数错乱)
                SimpleClickHandler simpleClickHandler = obj.AddComponent<SimpleClickHandler>();
                simpleClickHandler.OnClicked = () =>
                {
                    EntityCard card = entityCard;
                    needRequst.Add(card);
                    if (needRequst.Count == needSelectCount)
                    {
                        Destroy(simpleClickHandler);
                        preEntityCard.AwakeHighLight();         //先高亮(选中反馈),再整体清理
                        RefreshContent();                       //关闭面板前清空content:弹窗内卡均为对象池临时副本,全部Release回池并隐藏面板,防止残留卡被首次区域浏览/下次弹窗误处理(重复展示/重复入列表)
                        GetQuestClick();                        //清完再唤醒等待方,避免同步恢复的下个弹窗被覆盖
                    }
                    else
                        return;
                };
            }
        }
    }




    public void IniSpeSom(EntityCard entityCard)
    {
        GameObject obj = objectPool.Get();
        PreEntityCard preEntityCard = obj.GetComponent<PreEntityCard>();
        UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
        preEntityCard.Initialize(entityCard, _UILinkLogic);
        //特招新建的预制体接管该实体的权威效果列表:原区域(手牌/墓地/额外卡组/除外区等)预制体会在EntitySpeSom里被RegionRequestHandle回收,
        //其OnReturnToPool会Clear并把effEctions置Null;而Initialize的"首次绑定"守卫(见PreEntityCard)不会把新预制体的列表写回实体,
        //若此处不主动接管,回收时掏空的就是entity.effections→特招后该卡所有诱发触发器(如SelfSpeSom/SelfSummon)在OnActionEff判空处静默失效。
        entityCard.GetEffections(preEntityCard.GetEffEctions());
        speSumOrCover = obj.transform;                      //特招专用变量：不再污染 sumOrCover
        sumOrCovMode = 1;                                   //标记为特招模式
    }

    

    #endregion

    #region  额外外部请求装载

    private UniTaskCompletionSource speSom;

    public async UniTask EntitySpeSom(List<EntityCard> entityCards,List<Transform> transforms)                               //UI特招     
    {
        UnityEngine.Debug.Log($"[EntitySpeSom] 特招UI入口 targets={entityCards?.Count} [{string.Join(",", (entityCards ?? new List<EntityCard>()).ConvertAll(c => c?.currentName + "@" + c?.location))}] transforms={(transforms == null ? "null" : transforms.Count.ToString())} speSom置空?={speSom == null}");
        Record.BeginPlace();                                                //命令记录:开一次落位采集(玩家接下来点的每个格子都会被记下)
        //落位由玩家点蓝格触发,晚于本条命令的 End():把随后的区域改变重定向到当前命令。
        //融合召唤(冰剑龙等)不走 EffectLogic 里那几处特招补全点,漏了这一步它的上场移动就会被暂存、
        //刷进下一条命令——冰剑龙于是被记到了 End 命令上,回放结束才出现。
        Record.RecordContext.RedirectMovesTo(
            entityCards != null && entityCards.Count > 0 ? entityCards[0]?.GetCurrentOwner() : null,
            Record.LastInstance?.pending?.code, entityCards?.Count ?? 1);
        //补记"上场卡的卡名id":融合召唤不走 EffectLogic 的补全点,不补的话命令里只有
        //CardId(效果持有卡=烙印融合),回放据此建实体就会再把魔法卡生成一份
        //(表现为"场上出现两张烙印融合")。
        if (entityCards != null && entityCards.Count > 0)
        {
            List<string> staticIds = new List<string>();
            for (int i = 0; i < entityCards.Count; i++)
                if (entityCards[i] != null && entityCards[i].card != null) staticIds.Add(entityCards[i].card.id);
            if (staticIds.Count > 0)
                Record.PatchLastPending(Record.Key.CardIds, string.Join("|", staticIds.ToArray()));
        }
        for (int i = 0; i < entityCards.Count; i++)
        {
            EntityCard entityCard = entityCards[i];
            IniSpeSom(entityCards[i]);                              //创建特招预制体并记录为speSumOrCover
            speSom = new UniTaskCompletionSource();                 //每张卡独立等待源,避免多卡特招时speSom复用/置空崩溃
            AwakeRegion(0);
            await speSom.Task.AttachExternalCancellation(FightAsyncScope.Token);
            UnityEngine.Debug.Log($"[EntitySpeSom] 第{i}张特招放置完成(已挂场上)");
            //特招确认完成(SumOrCovBaseHandle已把新预制体挂到场上并完成数据层移动):释放原区域预制体
            //(手牌/额外卡组/墓地/除外区等所有来源,保证墓地、除外区的UI随特招移除)
            if (transforms != null && i < transforms.Count && transforms[i] != null)
                RegionRequestHandle(0, false, entityCard, transforms[i]);
        }
    }
    public void GetSpeSomRes()
    {
        if (speSom != null)
        {
            speSom.TrySetResult();
            speSom = null;
        }
    }
    public void CloseAllHighLight()                                     //关闭场上全部高亮：攻击/可攻击高亮 + 放置蓝色高亮
    {
        ExitFightPhase();                                               //关闭攻击高亮与攻击按钮
        RestoreRegion();                                                //关闭放置区域蓝色高亮
    }

    private void BeforeGoToCemetery()                                   //送去墓地前的总体操作（统一时机：战斗/效果破坏等一切送墓场景）
    {
        // TODO: 在此统一处理，如"被破坏时"诱发效果、代破、连锁时点等
    }

    public async UniTask GotoCeCemeteryUI(List<EntityCard> entityCards,List<Transform> transforms)                                                     //UI送去墓地
    {
        CloseAllHighLight();                                            //战斗阶段怪兽被破坏（战斗/效果）：先关闭高亮
        BeforeGoToCemetery();                                           //送去墓地前总体操作
        UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
        for (int i = 0; i < entityCards.Count && i < transforms.Count; i++)
        {
            Transform item = transforms[i];
            EntityCard card = entityCards[i];
            //落位区域按卡原持有者分边:敌方卡进对方墓地EneCem,我方卡进我方墓地PlaCem(修复AI的卡被破坏/效果送墓后显示进玩家墓地的问题)
            Transform reg = (card != null && IsOwnerEnemy(card))
                ? (EneCem != null ? EneCem.transform : null)
                : (PlaCem != null ? PlaCem.transform : null);
            if (reg == null) continue;
            if (item == null)                                              //素材原无预制体(来自卡组/额外等隐藏区):按墓地新建展示
            {
                GameObject obj = objectPool.Get();
                obj.transform.SetParent(reg, false);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                PreEntityCard pec = obj.GetComponent<PreEntityCard>();
                if (pec != null) pec.Initialize(card, _UILinkLogic);
                continue;
            }
            item.SetParent(reg,false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
            item.localScale = Vector3.one;
        }
    }
    public void GotoCeCemeteryUI(Transform item)
    {
        CloseAllHighLight();                                            //战斗阶段怪兽被破坏（战斗/效果）：先关闭高亮
        BeforeGoToCemetery();                                           //送去墓地前总体操作
        //落位区域按卡原持有者分边:经预制体取真实实体判定(战斗破坏/祭品等走单卡版送墓的场景,含AI怪兽);取不到卡时回退我方墓地
        PreEntityCard pec = item != null ? item.GetComponent<PreEntityCard>() : null;
        EntityCard card = pec != null ? pec.GetEntityCard() : null;
        Transform reg = (card != null && IsOwnerEnemy(card))
            ? (EneCem != null ? EneCem.transform : null)
            : (PlaCem != null ? PlaCem.transform : null);
        if (reg == null) return;
        item.SetParent(reg, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        item.localScale = Vector3.one;
    }

    public async UniTask GotoBanishedUI(List<EntityCard> entityCards, List<Transform> transforms)    //UI返回除外区(按原持有者放入己方/对方除外容器)
    {
        CloseAllHighLight();
        for (int i = 0; i < entityCards.Count && i < transforms.Count; i++)
        {
            EntityCard card = entityCards[i];
            bool isEnemy = IsOwnerEnemy(card);
            Transform reg = isEnemy ? (EneBan != null ? EneBan.transform : null) : (PlaBan != null ? PlaBan.transform : null);
            if (reg == null) continue;
            Transform item = transforms[i];
            if (item == null)                                              //素材原无预制体(回放按"转移后id的新实体"新建):按除外区新建展示
            {
                GameObject obj = objectPool.Get();
                obj.transform.SetParent(reg, false);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                PreEntityCard pec = obj.GetComponent<PreEntityCard>();
                if (pec != null) pec.Initialize(card, requestHandler as UILinkLogic);
                continue;
            }
            item.SetParent(reg, false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
            item.localScale = Vector3.one;
        }
    }

    public async UniTask GotoHandUI(List<EntityCard> entityCards, List<Transform> transforms)        //UI返回手卡:素材预制体移回原持有者手牌区(无预制体的隐藏区来源按手牌新建)
    {
        for (int i = 0; i < entityCards.Count && i < transforms.Count; i++)
        {
            EntityCard card = entityCards[i];
            bool isEnemy = IsOwnerEnemy(card);
            GridLayoutGroup reg = isEnemy ? EneHandReg : PlaHandReg;
            if (card == null || reg == null) continue;
            UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
            Transform item = transforms[i];
            if (item == null)                                              //素材原无预制体(来自卡组/额外等隐藏区):按手牌新建预制体
            {
                GameObject obj = objectPool.Get();
                obj.transform.SetParent(reg.transform, false);
                obj.transform.localScale = new Vector3(1, 1, 1);
                PreEntityCard pec = obj.GetComponent<PreEntityCard>();
                if (pec != null) pec.Initialize(card, _UILinkLogic);
                continue;
            }
            item.SetParent(reg.transform, false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
            item.localScale = Vector3.one;
        }
        RefreshHandSpacing(PlaHandReg);                                    //移入后重算手牌区重叠间距(与DrawCardHandle规则一致)
        RefreshHandSpacing(EneHandReg);
    }

    public async UniTask GotoDeckUI(List<EntityCard> entityCards, List<Transform> transforms)        //UI返回卡组/额外卡组:收回预制体(卡组/额外为隐藏区,预制体释放回对象池,不新建展示)
    {
        for (int i = 0; i < entityCards.Count && i < transforms.Count; i++)
        {
            Transform item = transforms[i];
            if (item == null) continue;                                    //隐藏区来源素材无预制体,数据已归位,无需UI
            item.SetParent(this.transform, false);                         //移出原区域容器(场上/墓地/除外),防对象池复用残留
            objectPool.Release(item.gameObject);
        }
    }
    private bool IsOwnerEnemy(EntityCard card)                             //卡的原持有者是否为对方(playerIndex==1)
    {
        EntityPlayer owner = card?.GetOwner();
        return owner != null && owner.GetPlayerIndex() == 1;
    }

    private void RefreshHandSpacing(GridLayoutGroup reg)                   //按手牌数调整手牌区重叠间距(<=6张不重叠,>6张负间距收拢)
    {
        if (reg == null) return;
        int n = reg.transform.childCount;
        reg.spacing = n <= 6 ? new Vector2(0, 0) : new Vector2(-30 * (n - 6) / (n - 1), 0);
    }

    private bool isMagicActBase = false;
    private UniTaskCompletionSource waitMagicActBase;
    public bool GetIsMagicActBase()
    {
        return isMagicActBase;
    }
    public async UniTask MagicActBase(List<EntityCard> entityCards, List<Transform> transforms)
    {
        if (entityCards.Count != transforms.Count)
            throw new System.Exception("错误");
        if (isMagicActBase)
            return;
        try
        {
            isMagicActBase = true;
            waitMagicActBase = new UniTaskCompletionSource();
            Record.BeginPlace();                                //命令记录:开一次落位采集(玩家点的魔陷区格子会被记下)
            for (int i = 0; i < entityCards.Count; i++)
            {
                int isCover;                                    //魔陷发动统一处理：已盖放则翻面显示正面，未盖放则等待放置
                if (entityCards[i] is EntityMagicCard magicCard)
                    isCover = magicCard.isCover;
                else if (entityCards[i] is EntityTrapCard trapCard)
                    isCover = trapCard.isCover;
                else
                    throw new System.Exception($"entityCards[i]错误:{entityCards[i].currentName}+{entityCards[i].currentid}");
                if (isCover == 1)
                {
                    GameManage.SetImage(transforms[i].GetChild(1).GetComponent<Image>(), entityCards[i].card.id);
                    //命令记录:盖放的魔陷发动时不再落位(卡已在场上),只"就地翻面"为表侧。
                    //回放必须补这一步,否则盖放卡发动后会直接消失(没有翻面、也没有任何移动)。
                    Record.PatchLastPending(Record.Key.IsFaceUp, "1");
                    Record.PatchLastPending(Record.Key.CurrentId, entityCards[i].currentid);
                }
                else
                {
                    AwakeRegion(1);
                    magicOrTrap = transforms[i];
                    await waitMagicActBase.Task.AttachExternalCancellation(FightAsyncScope.Token);
                }
            }
            //落位完成:卡落在"第几个魔陷区"只有玩家点完格子这一刻才知晓,补记进当前命令,
            //否则回放无从得知该放哪一格,只能取第一个空位(表现为魔法卡总是被放到魔法区一号位)。
            Record.PatchLastPending(Record.Key.MagicTrapZone, Record.TakePlaceSlots());
        }
        finally
        {
            isMagicActBase = false;
            waitMagicActBase = null;        //清理等待源:防止场上盖放发动(翻面分支)等不等待放置的调用残留,误判后续盖放为表侧发动
        }
    }
    //回放专用:让魔陷以"表侧发动"状态落位。
    //落位判定见 SumOrCovBaseHandle:waitMagicActBase 为空时一律按"盖放"处理(把卡图换成卡背),
    //真实流程由 MagicActBase 开启等待源;回放不经过点击等待,若不预先置入等待源,
    //魔法卡发动就会被显示成盖放,直到下一条命令重建才翻正面。
    public void BeginWaitMagicActForReplay()
    {
        waitMagicActBase = new UniTaskCompletionSource();
    }
    public void EndWaitMagicActForReplay()
    {
        waitMagicActBase = null;
    }
    public void EndWaitMagicActBase()
    {
        if(waitMagicActBase != null)
        {
            waitMagicActBase.TrySetResult();
            waitMagicActBase = null;
        }
    }
    #endregion




    public async UniTask<List<Transform>> WaitForClickContntCard()
    {
        isWaitingForAsyncOperation = true;
        RebackText("请选择卡牌");
        try
        {
            if (waitEventClick != null && !waitEventClick.Task.Status.IsCompleted())
            {
                waitEventClick.TrySetCanceled();
            }
            waitEventClick = new UniTaskCompletionSource<List<Transform>>();
            List<Transform> result = await waitEventClick.Task.AttachExternalCancellation(FightAsyncScope.Token);
            ClearReback();
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;                                  //对局已结束:终止该异步链
        }
        catch (System.Exception)
        {
            return new List<Transform>();
        }
        finally
        {
            isWaitingForAsyncOperation = false;
            ScrollAwakeCon.SetActive(false);
            RefreshContent();
        }
    }

 

    #region  外部请求接口实现
    public void GiveRequestHandler(UILinkLogic requestHandler)
    {
        this.requestHandler = requestHandler;
        SubscribeBoutInforMation();                                 //requestHandler注入较晚时补订阅阶段/时点事件（内部已幂等）
    }
    public void OnHPChangeHandle(int playerId, int newHP)
    {
        //HP累计纯逻辑已在数据层(FightLogic.ChangeHp→EntityPlayer.ChangeHP):此处只做UI刷新,以数据层权威值为准,不再从UI文本回读累加
        if (requestHandler == null) return;
        OnHPChangeToHandle(playerId, requestHandler.GetPlayerHP(playerId));
    }
    public void OnHPChangeToHandle(int playerId, int newHP)
    {
        if (playerId == 0)
        {
            PlayerHPNum.text = newHP.ToString();
            if (newHP >= 8000)
            {
                PlayerHp.rectTransform.sizeDelta = new Vector2(80, 15);
                return;
            }
            PlayerHp.rectTransform.sizeDelta = new Vector2(80 / 8000 * newHP, 15);
            return;
        }
        EnemyHPNum.text = newHP.ToString();
        if (newHP >= 8000)
        {
            EnemyHp.rectTransform.sizeDelta = new Vector2(80, 15);
            return;
        }
        EnemyHp.rectTransform.sizeDelta = new Vector2(80f / 8000 * newHP, 15);
    }
    public async UniTask OnBoutHandle()
    {

    }
    public async UniTask OnWaitCurChangeBout(bool isCanChangeBout)
    {

    }
    //真实实体→其UI真实预制体:在全部持久区域容器(场上/魔陷/手牌/墓地/额外区)中按实体引用反查绑定它的PreEntityCard
    //(替代原IdTransfrom字典:不再依赖currentid的注册表,杜绝id变化/弹窗副本/对象池复用的映射漂移)
    public Transform FindRealCardTransform(EffLogic.EntityCard realCard)
    {
        if (realCard == null) return null;
        List<Transform> regs = new List<Transform>();
        AddRegTransforms(regs, PlaMonsReg);
        AddRegTransforms(regs, PlaMagReg);
        AddRegTransforms(regs, EnemyMonsReg);
        AddRegTransforms(regs, EnemyMagReg);
        AddRegTransforms(regs, ExtraMonster);
        if (PlaHandReg != null) regs.Add(PlaHandReg.transform);
        if (EneHandReg != null) regs.Add(EneHandReg.transform);
        if (PlaCem != null) regs.Add(PlaCem.transform);
        if (EneCem != null) regs.Add(EneCem.transform);
        if (PlaBan != null) regs.Add(PlaBan.transform);
        if (EneBan != null) regs.Add(EneBan.transform);
        if (venueReg != null) regs.Add(venueReg.transform);
        return FindCardInRegs(realCard, regs);
    }
    private void AddRegTransforms(List<Transform> list, Image[] regs)
    {
        if (regs == null) return;
        foreach (var reg in regs)
            if (reg != null) list.Add(reg.transform);
    }
    private Transform FindCardInRegs(EffLogic.EntityCard realCard, List<Transform> regs)
    {
        foreach (var reg in regs)
        {
            if (reg == null || reg.childCount == 0) continue;
            for (int i = 0; i < reg.childCount; i++)
            {
                PreEntityCard pec = reg.GetChild(i).GetComponent<PreEntityCard>();
                if (pec != null && pec.GetEntityCard() == realCard)
                    return reg.GetChild(i);
            }
        }
        return null;
    }
    /// 诱发"等待点卡"阶段点DeleteEff退出诱发:遍历全部持久区域容器(场上/魔陷/手牌/墓地/除外/额外/竞技场),
    /// 关闭"可发动但未点击发动"的诱发效果持有卡的高亮与发动入口(window3)。
    private void ClearTriggerWaitCardUI()
    {
        List<Transform> regs = new List<Transform>();
        AddRegTransforms(regs, PlaMonsReg);
        AddRegTransforms(regs, PlaMagReg);
        AddRegTransforms(regs, EnemyMonsReg);
        AddRegTransforms(regs, EnemyMagReg);
        AddRegTransforms(regs, ExtraMonster);
        if (PlaHandReg != null) regs.Add(PlaHandReg.transform);
        if (EneHandReg != null) regs.Add(EneHandReg.transform);
        if (PlaCem != null) regs.Add(PlaCem.transform);
        if (EneCem != null) regs.Add(EneCem.transform);
        if (PlaBan != null) regs.Add(PlaBan.transform);
        if (EneBan != null) regs.Add(EneBan.transform);
        if (venueReg != null) regs.Add(venueReg.transform);
        foreach (var reg in regs)
        {
            if (reg == null || reg.childCount == 0) continue;
            for (int i = 0; i < reg.childCount; i++)
            {
                PreEntityCard pec = reg.GetChild(i).GetComponent<PreEntityCard>();
                if (pec != null)
                    pec.CloseTriggerWaitUI();
            }
        }
    }
    public void RegionRequestHandle(int regionNum, bool isAdd, EntityCard entityCard, Transform transform)
    {
        if (!isAdd)
        {
            transform.SetParent(this.transform,false);
            objectPool.Release(transform.gameObject);
            return;
        }
        switch (regionNum)                   // Deck, ExtralDeck, Hand, Cemetery, Banished, Field
        {
            case 2:

                break;
            case 3:

                break;
            case 5:

                break;
            default:
                break;
        }
    }
    public EntityCard SumOrCovBaseHandle(Transform transform)                     //通召盖放特招基本函数
    {
        //命令记录:卡落在"第几个怪兽区"只有玩家点完蓝格这一刻才知晓,先采集下来,等效果结算完成后由效果自身补全进命令
        Record.CatchPlaceSlot(GetSlotCode(transform));
        Transform card = sumOrCovMode == 1 ? speSumOrCover : sumOrCover;         //按当前模式取对应预制体：特招用speSumOrCover，通召/盖放用sumOrCover，互不污染
        if (card == null)
            return null;                                                        //预制体不存在(如被诱发流程误清空):直接失败,上层判空后不再误执行数据层特招
        card.SetParent(transform, false);
        card.localPosition = Vector3.zero;
        card.localRotation = Quaternion.identity;
        card.localScale = new Vector3(1, 1, 1);
        PreEntityCard preEntityCard = card.GetComponent<PreEntityCard>();
        preEntityCard.CloseHightLight();
        EntityCard entityCard = preEntityCard.GetEntityCard();
        if (entityCard.entityType != 0)
        {
            //走到这里的魔陷一律是"里侧盖放"(表侧发动走 LayOutMagicTrapCardHandle)。
            //① 补记盖放标记:回放据此选用盖放落位分支,否则会被当成发动显示成正面;
            //② 补记魔陷区号:盖放也要还原玩家点的那一格。区号已由开头 CatchPlaceSlot 采集,
            //   但盖放不是特招、没人来 TakePlaceSlots,故这里直接补记并清掉采集器,避免残留污染后续命令。
            Record.PatchLastPending(Record.Key.IsCover, "1");
            Record.PatchLastPending(Record.Key.MagicTrapZone,
                GetSlotCode(transform).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Record.ClearPlace();
            //换卡背与盖放计时:只属于魔陷,必须留在魔陷分支内
            preEntityCard.SetCardImage(entityCard, "0");
            if (entityCard.entityType == 1)
            {
                var entityMagicCard = entityCard as EntityMagicCard;
                entityMagicCard.SetCoverTime();
            }
            else
            {
                var entityTrapCard = entityCard as EntityTrapCard;
                entityTrapCard.SetCoverTime();
            }
        }
        else
        {
            //怪兽:落位区号也要直接补记到当前命令。
            //融合召唤不经过 EffectLogic 的 CompleteSpeSomRecord,没人来 TakePlaceSlots,
            //区号就丢了——回放只能取第一个空位(表现为融合怪兽落在1号位而不是实际点的2号位)。
            Record.PatchLastPending(Record.Key.SpeSomZone,
                GetSlotCode(transform).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return entityCard;
    }
    public void LayOutMagicTrapCardHandle(Transform transform)
    {
        //命令记录:卡落在第几个魔陷区,只有玩家点完格子这一刻才知晓(与 SumOrCovBaseHandle 同一机制)
        Record.CatchPlaceSlot(GetSlotCode(transform));
        magicOrTrap.SetParent(transform, false);
        magicOrTrap.localPosition = Vector3.zero;
        magicOrTrap.localRotation = Quaternion.identity;
        magicOrTrap.localScale = new Vector3(1, 1, 1);
        PreEntityCard preEntityCard = magicOrTrap.GetComponent<PreEntityCard>();
        EffLogic.EntityCard entityCard = preEntityCard.GetEntityCard();
        if (entityCard.entityType != 0)                                 //魔陷成功放置到魔陷区（盖放或表侧发动落位）
        {
            if (waitMagicActBase == null)
            {
                //里侧盖放流程(waitMagicActBase为空,区别于表侧发动):
                //盖放此前没有自己的命令,落位产生的区域改变会被暂存、在下一条命令建立时才刷进去
                //(于是盖放被记到 End 之类不相干的命令上,回放既找不到区号也认不出是盖放)。
                //故在此先建一条盖放命令承载它,并把随后的落位移动重定向过来。
                if (Record.LastInstance != null)
                {
                    EntityPlayer coverOwner = entityCard.GetCurrentOwner();
                    Record.BaseRecord cmd = Record.LastInstance.Add(Record.Order.SetCard);
                    cmd.AddData(Record.Key.IsCover, "1");
                    //落位的区域改变随后才发生(命令已 End()),重定向到这条盖放命令
                    Record.RecordContext.RedirectMovesTo(coverOwner, cmd.code);
                }
                //标记盖放状态并重置盖放计时(盖放当回合不可发动)
                if (entityCard is EffLogic.EntityMagicCard entityMagicCard)
                {
                    entityMagicCard.isCover = 1;
                    entityMagicCard.SetCoverTime();
                }
                else if (entityCard is EffLogic.EntityTrapCard entityTrapCard)
                {
                    entityTrapCard.isCover = 1;
                    entityTrapCard.SetCoverTime();
                }
                preEntityCard.SetCardImage(entityCard, "0");            //盖放:卡图换成卡背(Resources/card_img/0)
            }
            //魔法发动流程(MagicActBase等待放置,waitMagicActBase非空):表侧发动落位,保持正面显示(isCover保持0,不算盖放)
            //两种落位都要记录魔陷区号:没记录的话回放只能取第一个空位(表现为盖放总是落在魔法区一号位)
            Record.PatchLastPending(Record.Key.MagicTrapZone,
                GetSlotCode(transform).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    public void ClearRegionUIHandle()
    {
        RestoreRegion();
        GetSpeSomRes();
        speSumOrCover = null;                               //特招放置结束：释放引用，避免残留影响后续通召/盖放或特招
    }
    public void DrawCardHandle(int whom, List<EntityCard> entityCards)
    {
        if (whom == 0)
        {
            ReflashAllUI(PlaHandReg.transform);
            foreach (var entityCard in entityCards)
            {
                GameObject obj = objectPool.Get();
                obj.transform.SetParent(PlaHandReg.transform);
                obj.transform.localScale = new Vector3(1, 1, 1);
                PreEntityCard preEntityCard = obj.GetComponent<PreEntityCard>();
                UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
                preEntityCard.Initialize(entityCard, _UILinkLogic);
            }
            int i = entityCards.Count;
            if (i <= 6)
            {
                PlaHandReg.spacing = new Vector2(0, 0);
            }
            else
            {
                PlaHandReg.spacing = new Vector2(-30 * (i - 6) / (i - 1), 0);
            }
            return;
        }
        ReflashAllUI(EneHandReg.transform);
        foreach (var entityCard in entityCards)
        {
            GameObject obj = objectPool.Get();
            obj.transform.SetParent(EneHandReg.transform);
            obj.transform.localScale = new Vector3(1, 1, 1);
            PreEntityCard preEntityCard = obj.GetComponent<PreEntityCard>();
            UILinkLogic _UILinkLogic = requestHandler as UILinkLogic;
            preEntityCard.Initialize(entityCard, _UILinkLogic);
        }
        int j = entityCards.Count;
        if (j <= 6)
        {
            EneHandReg.spacing = new Vector2(0, 0);
        }
        else
        {
            EneHandReg.spacing = new Vector2(-30 * (j - 6) / (j - 1), 0);
        }
    }
    public void AbandonCardHandle(Transform transform)
    {
        GotoCeCemeteryUI(transform);
    }
    private UniTaskCompletionSource<bool> waitAskOrderStack;
    public async UniTask<bool> AskOrderStack(string tipText = null)      //连锁询问(是/否)：点[是]返回true并关闭面板,由调用方切入"等待点一张连锁卡"阶段(DeleteEff亮出);点[否]返回false
    {
        if (waitAskOrderStack != null)
            return false;
        SetDeleteEffVisible(false);                                  //询问期:隐藏DeleteEff(仅等待点卡阶段亮出)
        askOrderStackText.text = tipText ?? "是否继续连锁？";
        askOrderStackImg.raycastTarget = true;                       //询问期:面板拦截射线,须先作答
        askOrderStackImg.gameObject.SetActive(true);
        waitAskOrderStack = new UniTaskCompletionSource<bool>();
        bool res = await waitAskOrderStack.Task.AttachExternalCancellation(FightAsyncScope.Token);
        askOrderStackImg.gameObject.SetActive(false);
        return res;
    }
    public void GetWaitAskOrderStack(bool isChain)
    {
        if (waitAskOrderStack != null)
        {
            waitAskOrderStack.TrySetResult(isChain);
            waitAskOrderStack = null;
        }
    }
    private UniTaskCompletionSource<bool> waitAskTrigger;
    public async UniTask<bool> AskTrigger(string tipText = null)        //询问是否发动诱发效果(是/否)：无论选是还是否都关闭面板;选[是]后由调用方原地切入"等待点诱发卡"阶段(DeleteEff亮出)
    {
        if (waitAskTrigger != null)
            return false;
        SetDeleteEffVisible(false);                                  //询问期:隐藏DeleteEff(仅等待点卡阶段亮出)
        askTriggerText.text = tipText ?? "是否发动诱发效果？";
        askTriggerImg.gameObject.SetActive(true);
        askTriggerImg.raycastTarget = true;                          //询问期:面板拦截射线,须先作答
        waitAskTrigger = new UniTaskCompletionSource<bool>();
        bool res = await waitAskTrigger.Task.AttachExternalCancellation(FightAsyncScope.Token);
        waitAskTrigger = null;
        askTriggerImg.gameObject.SetActive(false);
        return res;
    }
    public void GetWaitAskTrigger(bool isTrigger)                       //诱发询问按钮回调：是/否
    {
        if (waitAskTrigger != null)
        {
            waitAskTrigger.TrySetResult(isTrigger);
            waitAskTrigger = null;
        }
    }
    private void SetDeleteEffVisible(bool show)                         //DeleteEff仅在诱发/连锁"等待点卡(询问面板已关)"阶段亮出:诱发点它=退出诱发;连锁点它=回退本次[是];询问期/空闲隐藏
    {
        if (DeleteEff == null)
        {
            return;
        }
        if (show)
        {
            DeleteEff.transform.SetAsLastSibling();                 //亮出时置顶,避免被同层其他面板(如全屏Image)盖住导致点击不到而退不出等待
            DeleteEff.gameObject.SetActive(show);
        }
        else
            DeleteEff.gameObject.SetActive(false);
    }


    #region 同一时点多效果发动选择(先点卡上window3发动入口,再弹此面板选具体效果)
    public void ShowEffChoice(List<EffLogic.Effection> effs, System.Action<EffLogic.Effection> onChosen)
    {
        HideEffChoice();
        if (effs == null || effs.Count == 0 || EffEctButs == null)
            return;
        if (EffEctObj != null)
            EffEctObj.SetActive(true);                                  //同一组效果选择按钮若挂在此面板下,需一并亮出(与区域入口共用面板)
        int show = Mathf.Min(effs.Count, EffEctButs.Length);
        for (int i = 0; i < show; i++)
        {
            EffLogic.Effection eff = effs[i];
            EffEctTexts[i].text = eff.id.ToString();                    //文本显示效果编号(Effection.id)
            EnsureVisibleInHierarchy(EffEctButs[i].gameObject);         //父级未接线EffEctObj时兜底沿链激活
            EffEctButs[i].gameObject.SetActive(true);                   //有几个效果激活几套UI组件
            EffEctButs[i].onClick.RemoveAllListeners();
            EffEctButs[i].onClick.AddListener(() =>                     //按钮负责发动对应效果
            {
                CloseEffEctObj();                                       //选定发动:整体关闭效果选择面板EffEctObj(与区域入口一致)
                onChosen?.Invoke(eff);
            });
        }
    }
    public void HideEffChoice()
    {
        if (EffEctButs == null) return;
        for (int i = 0; i < EffEctButs.Length; i++)
        {
            if (EffEctButs[i] != null)
                EffEctButs[i].gameObject.SetActive(false);
        }
    }
    #endregion

    #region 连锁问询状态
    private bool isChainAsk;
    public bool IsChainAsk => isChainAsk;
    public void SetChainAsk(bool isChain)
    {
        isChainAsk = isChain;
    }
    private UniTaskCompletionSource<bool> chainActivateTcs;
    public async UniTask<bool> WaitForChainActivate(string tipText = null)   //连锁"等待点一张卡"阶段(询问面板已关)：等待一张连锁卡点击入栈;点DeleteEff回退本次[是](未发动,由调用方重新弹询问)。返回true=已发动一张(调用方应再次弹询问)；false=回退(调用方重新弹询问)
    {
        if (chainActivateTcs != null)
            return false;
        SetDeleteEffVisible(true);                                   //等待点卡阶段:亮出DeleteEff作为"回退本次[是]"入口
        try
        {
            chainActivateTcs = new UniTaskCompletionSource<bool>();
            return await chainActivateTcs.Task.AttachExternalCancellation(FightAsyncScope.Token);
        }
        finally
        {
            chainActivateTcs = null;
            SetDeleteEffVisible(false);                              //结束等待:关闭DeleteEff
        }
    }
    public void ReportChainActivate(bool success)               //玩家发动了效果或点DeleteEff回退（任一结果都会结束本次等待）
    {
        //与ReportTriggerActivate同理:先置null再完成,防止内联续体重建的新tcs被误清
        UniTaskCompletionSource<bool> tcs = chainActivateTcs;
        chainActivateTcs = null;
        if (tcs != null)
            tcs.TrySetResult(success);
    }
    #endregion

    #region 诱发发动等待状态
    private bool isTriggerAsk;                                   //是否正处于等待诱发效果发动中
    public bool IsTriggerAsk => isTriggerAsk;
    public void SetTriggerAsk(bool isTrigger)
    {
        isTriggerAsk = isTrigger;
    }
    private UniTaskCompletionSource<bool> triggerActivateTcs;
    public async UniTask<bool> WaitForTriggerActivate(string tipText = null)   //诱发"等待点卡"阶段(询问面板已关)：可连续点击多张诱发卡入栈;点DeleteEff退出诱发。返回本阶段是否发动过诱发
    {
        if (triggerActivateTcs != null)
        {
            return false;
        }
        SetDeleteEffVisible(true);                                   //等待点卡阶段:亮出DeleteEff作为"退出诱发"入口
        bool any = false;                                            //本阶段是否至少发动过一张诱发
        try
        {
            while (true)
            {
                triggerActivateTcs = new UniTaskCompletionSource<bool>();
                bool activated = await triggerActivateTcs.Task.AttachExternalCancellation(FightAsyncScope.Token);
                triggerActivateTcs = null;
                if (!activated) break;                              //点DeleteEff → 退出诱发点卡阶段
                any = true;                                         //已发动一张诱发(已入栈)：可继续点其他诱发卡入栈
            }
        }
        finally
        {
            triggerActivateTcs = null;
            SetDeleteEffVisible(false);                             //退出诱发:关闭DeleteEff
        }
        return any;
    }
    public void ReportTriggerActivate(bool success)              //玩家发动了诱发效果或点DeleteEff退出（任一结果都会结束等待）
    {
        //关键:必须"先置null、再TrySetResult"。UniTaskCompletionSource续体是内联同步恢复的：
        //若先TrySetResult,诱发等待循环会在内联续体里立刻重建新的tcs并挂起等待,返回后本方法再置null会把新tcs误清,
        //导致诱发等待挂在字段已找不到的tcs上,之后点DeleteEff(诱发tcs=False)永远退不出诱发时点。
        UniTaskCompletionSource<bool> tcs = triggerActivateTcs;
        triggerActivateTcs = null;
        if (tcs != null)
            tcs.TrySetResult(success);
    }
    #endregion
    public void ReflashAllUI(Transform transform)
    {
        if (transform == null) return;
        //先把子物体收进列表:归还池时要把它们移出容器,不能边遍历边改父物体(会漏项/抛异常)
        List<GameObject> olds = new List<GameObject>();
        foreach (Transform item in transform)
            if (item != null) olds.Add(item.gameObject);
        for (int i = 0; i < olds.Count; i++)
        {
            if (olds[i] == null) continue;
            //必须先移出区域容器再归还池:归还池只是SetActive(false),物体仍留在容器的子物体列表里,
            //于是容器里堆着"已回收的僵尸子物体"——子物体数量/顺序与实际卡牌对不上,
            //之后按实例id/GUID扫描容器就可能命中僵尸(把真卡当成找不到、或移错了对象),重绘手牌也会一直把它算进去。
            olds[i].transform.SetParent(this.transform, false);
            objectPool.Release(olds[i]);
        }
    }
    #endregion

    #region 区域浏览与效果发动(ComPlaCem/ComEmeExt选择区域 → 激活EffEctBut1确认区域卡牌 / EffEctBut2确认区域效果)
    private void BindComRegionButton(Button btn, int playerIndex, GameManage.CardLocation loc)
    {
        if (btn == null)
        {
            return;
        }
        btn.onClick.AddListener(() => ActivateRegionActionEntry(playerIndex, loc));
    }

    //点击区域按钮:记录所选区域,激活两组动作入口UI(EffEctText1=确认区域卡牌 / EffEctText2=确认区域效果)
    private void ActivateRegionActionEntry(int playerIndex, GameManage.CardLocation loc)
    {
        //异步选卡弹窗(ShowSelectionDialog, IsWaitingQuest)等待期间:整条区域浏览入口拦截——Com区域按钮点击直接无效,
        //不再激活"确认区域卡牌/效果"入口面板。区域浏览与选卡弹窗共用ScrollAwakeCon/content滚动区,等待期若打开列表
        //会把正在等待的选卡弹窗候选卡清出/顶掉(真实卡被归位、临时副本被Release回池),导致等待方永久挂起,故选卡期间整条拦截。
        if (IsWaitingQuest)
        {
            return;
        }
        if (EffEctBut1 == null || EffEctBut2 == null)
        {
            return;
        }
        curRegionPlayer = playerIndex;
        curRegionLoc = loc;
        HideEffChoice();                                         //先清场:避免与连锁"同一时点多效果选择"残留的监听/显示互相污染
        if (EffEctText1 != null) EffEctText1.text = "确认区域卡牌";
        if (EffEctText2 != null) EffEctText2.text = "确认区域效果";
        EffEctBut1.onClick.RemoveAllListeners();
        EffEctBut1.onClick.AddListener(RegionBrowseCards);
        EffEctBut2.onClick.RemoveAllListeners();
        EffEctBut2.onClick.AddListener(RegionBrowseEffects);
        //亮出入口面板EffEctObj(若赋值);否则退回沿父链激活按钮自身(兼容EffEctObj未接线时旧有层级)
        if (EffEctObj != null)
            EffEctObj.SetActive(true);
        else
        {
            EnsureVisibleInHierarchy(EffEctBut1.gameObject);
            EnsureVisibleInHierarchy(EffEctBut2.gameObject);
        }
        EffEctBut1.gameObject.SetActive(true);
        EffEctBut2.gameObject.SetActive(true);
    }
    private void EnsureVisibleInHierarchy(GameObject go)           //把目标从自身沿父链逐层激活,确保即使父级曾隐藏也能整链显示
    {
        if (go == null) return;
        Transform t = go.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    public void RegionBrowseCards()                              //确认区域卡牌:关闭入口面板,滚动列出该区域全部卡
    {
        if (IsWaitingQuest)                                      //异步选卡等待期间拦截:入口面板若在选卡开始前已亮出,等待期点击同样不得打开滚动列表(防顶掉选卡弹窗)
        {
            return;
        }
        CloseEffEctObj();
        OpenRegionContent(false);
    }
    public void RegionBrowseEffects()                            //确认区域效果:关闭入口面板,对区域内卡执行一次cost判定,仅列出当前可发动的卡
    {
        if (IsWaitingQuest)                                      //异步选卡等待期间拦截:同确认区域卡牌,整条入口在选卡期间一律不得打开滚动列表
        {
            return;
        }
        CloseEffEctObj();
        OpenRegionContent(true);
    }
    private void CloseEffEctObj()                                //点击EffEctBut1/2(确认区域卡牌/效果)后:整面板关闭(滚动展示期间入口不再显示)
    {
        if (EffEctObj != null)
            EffEctObj.SetActive(false);
        HideEffChoice();                                         //兜底:入口按钮自身也隐藏,防止面板未接线时按钮残留
    }

    private void OpenRegionContent(bool effectFilter)
    {
        if (requestHandler == null || content == null) return;
        RegionBrowseClose();                                     //兜底:先清理可能残留的浏览内容(防御)
        HideEffChoice();                                         //进入内容展示:关闭区域动作入口(滚动区/发动期间不显示,下次点区域按钮重新激活)
        List<EntityCard> cards = requestHandler.GetRegionCards(curRegionPlayer, curRegionLoc);
        if (cards == null || cards.Count == 0)
        {
            return;
        }
        UILinkLogic link = requestHandler as UILinkLogic;
        int kept = 0;
        foreach (var card in cards)
        {
            if (card == null) continue;
            Transform tr = FindRealCardTransform(card);          //真实预制体:直接移入content排列(不生成副本),返回时归位原区域
            bool created = false;
            if (tr == null)
            {
                GameObject obj = objectPool.Get();               //该卡无现成UI(如额外卡组隐藏区/尚未生成UI的卡):新建临时展示
                PreEntityCard createdPec = obj.GetComponent<PreEntityCard>();
                if (createdPec != null) createdPec.Initialize(card, link);
                tr = obj.transform;
                created = true;
            }
            tr.SetParent(content, false);
            ResetBrowseCardLocal(tr);
            PreEntityCard pec = tr.GetComponent<PreEntityCard>();
            if (pec != null)
                pec.OnEffLaunching = RegionBrowseClose;          //内容卡点发动按钮(window3)时:先关闭本滚动面板并归还内容卡,再走正常效果发动询问/连锁/结算
            browseCreatedMap[tr] = created;
            if (effectFilter)
            {
                if (pec == null || !pec.HasCanActiveEff())       //可发动判定:当前时点存在cost/次数可行的主动效果
                {
                    ReturnBrowseCardToRegion(tr);                //不可发动:不显示,立即归还
                    continue;
                }
            }
            kept++;
        }
        if (kept == 0)
        {
            ScrollAwakeCon.SetActive(false);
            browseCreatedMap.Clear();
            return;
        }
        regionScrollActive = true;
        EnsureVisibleInHierarchy(ScrollAwakeCon);                //沿父链激活:滚动面板若挂在隐藏父级下,仅SetActive自身不会显示
        ScrollAwakeCon.SetActive(true);
        if (scrollClose != null)
        {
            EnsureVisibleInHierarchy(scrollClose.gameObject);
            scrollClose.gameObject.SetActive(true);              //区域浏览/发动需要scrollClose可关闭(可能此前被点击隐藏)
        }
    }

    private void RegionBrowseClose()                             //关闭区域浏览/发动:把content内卡全部归还(或释放临时副本),并隐藏滚动面板
    {
        if (!regionScrollActive) return;
        List<Transform> childs = new List<Transform>();
        foreach (Transform child in content)
            childs.Add(child);
        foreach (Transform child in childs)
            ReturnBrowseCardToRegion(child);
        browseCreatedMap.Clear();
        regionScrollActive = false;
        ScrollAwakeCon.SetActive(false);
    }

    private void ReturnBrowseCardToRegion(Transform tr)          //归还单张浏览卡:真实预制体回原区域容器;浏览新建的临时展示进对应容器或释放回池
    {
        if (tr == null) return;
        PreEntityCard pec = tr.GetComponent<PreEntityCard>();
        if (pec != null) pec.OnEffLaunching = null;              //归还即解除发动收尾回调,防复用残留
        bool created = browseCreatedMap.TryGetValue(tr, out bool isCreated) && isCreated;
        browseCreatedMap.Remove(tr);
        EntityCard card = pec != null ? pec.GetEntityCard() : null;
        if (created)
        {
            //浏览新建的临时展示:区域有UI容器(墓地/除外)则落为区域卡UI,否则(额外卡组/卡组等隐藏区)释放回池
            Transform reg = card != null ? GetRegionContainer(IsOwnerEnemy(card) ? 1 : 0, card.location) : null;
            if (reg != null)
            {
                tr.SetParent(reg, false);
                ResetBrowseCardLocal(tr);
                return;
            }
            tr.SetParent(this.transform, false);
            objectPool.Release(tr.gameObject);
            return;
        }
        if (card == null)
        {
            tr.SetParent(this.transform, false);
            objectPool.Release(tr.gameObject);
            return;
        }
        Transform realReg = GetRealCardContainer(card);          //真实预制体:按数据当前所在区域放回对应容器
        if (realReg != null)
        {
            tr.SetParent(realReg, false);
            ResetBrowseCardLocal(tr);
            return;
        }
        tr.SetParent(this.transform, false);
        objectPool.Release(tr.gameObject);
    }

    private Transform GetRegionContainer(int playerIndex, GameManage.CardLocation loc)     //区域→UI容器(仅墓地/除外有容器,其余无返回null)
    {
        switch (loc)
        {
            case GameManage.CardLocation.Cemetery: return (playerIndex == 1 ? EneCem : PlaCem)?.transform;
            case GameManage.CardLocation.Banished: return (playerIndex == 1 ? EneBan : PlaBan)?.transform;
            default: return null;
        }
    }
    private Transform GetRealCardContainer(EntityCard card)      //真实卡当前所在数据区域→其UI容器
    {
        if (card == null) return null;
        bool enemy = IsOwnerEnemy(card);
        switch (card.location)
        {
            case GameManage.CardLocation.Cemetery: return (enemy ? EneCem : PlaCem)?.transform;
            case GameManage.CardLocation.Banished: return (enemy ? EneBan : PlaBan)?.transform;
            case GameManage.CardLocation.Hand: return (enemy ? EneHandReg : PlaHandReg)?.transform;
            default: return null;                                 //场上/卡组/额外卡组等区域UI由其各自流程管理,不作为归还容器
        }
    }
    private void ResetBrowseCardLocal(Transform tr)
    {
        if (tr == null) return;
        tr.localPosition = Vector3.zero;
        tr.localRotation = Quaternion.identity;
        tr.localScale = Vector3.one;
    }
    #endregion

    #region 回放专用(血量UI同步)
    //回放用:读取当前界面上显示的血量。
    //回放侧以它作为扣血初值——OnHPChangeToHandle 收到的是"变化量"而不是新血量,
    //若不先取真实初值,连续两次战斗伤害会被当成"把血量设成这次的伤害值"(血量只减一次,第二次不变)。
    public int GetShownHP(int playerIndex)
    {
        var label = playerIndex == 1 ? EnemyHPNum : PlayerHPNum;
        if (label == null || string.IsNullOrEmpty(label.text)) return 8000;
        int v;
        return int.TryParse(label.text.Trim(), out v) ? v : 8000;
    }
    #endregion
}


