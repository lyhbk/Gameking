using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ExternalQuestBase.UILinkLog;

public class UILinkLogic: UILinkLogicIn, LogicLinkUI, RegClickEvent, PECListener
{
    private readonly FightLogic fightLogic;
    private UIListener _UIListener;
    private FightUI fightUI;
    private int triggerLoopActive = 0;      //诱发询问循环互斥计数:同刻仅允许一个循环运行(多来源并发会误清对方窗口时点/改乱当前时点→诱发卡偶发不点亮/点了没反应)

    public event Action<StateMachine.CurrentPoint> OnCurrentPointChanged;  //时点变化事件(转发FightLogic)
    public event Action<GameManage.GamePhase> OnGamePhaseChanged;          //阶段变化事件(转发FightLogic)

    public UILinkLogic(FightLogic fightLogic)
    {
        this.fightLogic = fightLogic;
        //转发FightLogic的时点/阶段变化事件
        fightLogic.OnCurrentPointChanged += point => OnCurrentPointChanged?.Invoke(point);
        fightLogic.OnGamePhaseChanged += phase => OnGamePhaseChanged?.Invoke(phase);
    }

    public void RegisterUIListener(UIListener _UIListener)
    {
        this._UIListener = _UIListener;
        this.fightUI = _UIListener as FightUI;
    }


   

    #region FightUI调用
    public void ChangeHPRequestHandle(ChangeHPRequest changeHPRequest)   //通用修改血量(非战斗伤害:效果伤害/回复等)
    {
        fightLogic.ChangeHp(changeHPRequest.playerId, changeHPRequest.newHP);   //数据层已广播"血量改变总事件"
        _UIListener?.OnHPChangeHandle(changeHPRequest.playerId, changeHPRequest.newHP);
        CheckDuelResult();                                       //血量改变总事件完成之后:任一血量<=0则弹出结算
    }

    public void ChangeHPToRequestHandle(ChangeHPToRequest changeHPToRequest)
    {
        fightLogic.ChangeHpTo(changeHPToRequest.playerId, changeHPToRequest.newHP);
        _UIListener?.OnHPChangeToHandle(changeHPToRequest.playerId, changeHPToRequest.newHP);
        CheckDuelResult();                                       //同上(投降/直接设定血量等)
    }

    //战斗伤害专用入口:战斗阶段伤害判定落血时调用——先掉血(广播血量改变总事件),再额外广播战斗伤害事件(HurtAttHp),最后判定结算
    public void BattleDamageRequestHandle(int playerId, int dmg, EffLogic.EntityCard sourceCard = null)
    {
        if (dmg <= 0) return;
        fightLogic.ChangeHp(playerId, -dmg);                     //① 掉血+血量改变总事件
        fightLogic.FireBattleDamageEvent(dmg, playerId, sourceCard);   //② 额外战斗伤害事件
        _UIListener?.OnHPChangeHandle(playerId, dmg);            //③ UI血量刷新
        CheckDuelResult();                                       //④ 两事件均触发完成后判定结算
    }

    private void CheckDuelResult()                               //血量<=0结算:我方(0)胜→胜利面板,否则败北面板(结算只弹一次,FightUI内置防重入)
    {
        if (fightUI == null || fightLogic == null) return;
        int myHp = fightLogic.GetPlayerHP(0);
        int oppHp = fightLogic.GetPlayerHP(1);
        if (myHp > 0 && oppHp > 0) return;                       //双方都存活:不结算
        if (oppHp <= 0 && myHp > 0)
            fightUI.VecUI();                                     //对方血量归零且我方存活:玩家获胜
        else
            fightUI.DefUI();                                     //我方血量归零(含同归于尽):玩家败北
    }

    public async UniTask ChangeBoutToRequestHandle(ChangeBout changeBout)
    {
        //阶段推进链进行中（阶段询问面板等待玩家/AI回合自动递归推进）：忽略"下一阶段"重复点击，防止并发推进错乱
        if (fightLogic.IsBoutAdvancing) return;
        await fightLogic.BoutChange();
    }

    public async UniTask WaitCurChangeBout(WaitCurChangeBout waitCurChangeBout)
    {

    }

    public void RegionRequestHandle(RegionRequest regionRequest)
    {
        Transform transform = fightLogic.FindRealCardTransform(regionRequest.entityCard);
        fightLogic.RegionCard(regionRequest.regionNum, regionRequest.isAdd, regionRequest.entityCard);
        _UIListener?.RegionRequestHandle(regionRequest.regionNum, regionRequest.isAdd, regionRequest.entityCard, transform);
    }

    public void DrawCardHandle(DrawCardRequest drawCardRequest)
    {
        List<EffLogic.EntityCard> entityCards = fightLogic.DrawCard(drawCardRequest.playerID, drawCardRequest.num);
        _UIListener?.DrawCardHandle(drawCardRequest.playerID,entityCards);
    }

    public async UniTask AbandonCardHandle(AbandonCard abandonCard)
    {
        List<EffLogic.EntityCard> entityCards =  await fightUI.ShowSelectionDialog(fightLogic.player1Hand.cards, abandonCard.num);
        if (entityCards == null || entityCards.Count == 0) return;      //弹窗互斥/取消:放弃丢弃,避免foreach(null)崩溃
        foreach(var item in entityCards)
        {
            Transform transform = fightLogic.FindRealCardTransform(item);
            if (transform != null)
                _UIListener?.AbandonCardHandle(transform);
            item.ChangeLocation(abandonCard.cardLocation);

        }
        fightLogic.AbandonCard(entityCards);
    }
    #endregion

    #region 回合信息/战斗查询
    public GameManage.GamePhase GetCurGamePhase()
    {
        return fightLogic.GetCurGamePhase();
    }
    public StateMachine.CurrentPoint GetCurrentPoint()
    {
        return fightLogic.GetCurrentPoint();
    }
    public int GetOnwerPhase()
    {
        return fightLogic.GetOnwerPhase();
    }
    public List<EffLogic.EntityCard> GetMonZon(bool isEnemy)
    {
        return (isEnemy ? fightLogic.enemy : fightLogic.entityPlayer1).field.GetMonZon();
    }
    public List<EffLogic.EntityCard> GetRegionCards(int playerIndex, GameManage.CardLocation location)
    {
        EntityPlayer player = playerIndex == 1 ? fightLogic.enemy : fightLogic.entityPlayer1;
        if (player == null) return null;
        List<EffLogic.EntityCard> src = null;
        switch (location)
        {
            case GameManage.CardLocation.Deck: src = player.deck?.cards; break;
            case GameManage.CardLocation.ExtraDeck: src = player.extraDeck?.cards; break;
            case GameManage.CardLocation.Hand: src = player.hand?.cards; break;
            case GameManage.CardLocation.Cemetery: src = player.cemetery?.cards; break;
            case GameManage.CardLocation.Banished: src = player.banished?.cards; break;
            case GameManage.CardLocation.Field: src = player.field?.cards; break;
        }
        //返回副本:仅供UI浏览读取,防止误改数据层区域列表
        return src == null ? null : new List<EffLogic.EntityCard>(src);
    }
    public void ResetBoutAttackFlags()
    {
        fightLogic.ResetBoutAttackFlags();
    }
    public bool IsMonsterAttackable(EffLogic.EntityCard mon)
    {
        return fightLogic.IsMonsterAttackable(mon);
    }
    public bool IsMonsterTargetable(EffLogic.EntityCard mon)
    {
        return fightLogic.IsMonsterTargetable(mon);
    }
    public int GetPlayerHP(int playerId)
    {
        return fightLogic.GetPlayerHP(playerId);
    }
    public bool IsEnemyAIEnable()
    {
        return fightLogic.enemyAI != null && fightLogic.enemyAI.IsEnable;
    }
    #endregion

    #region FightLogic调用

    public Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> GetShowSelectionDialog()
    {
        return fightUI.ShowSelectionDialog;
    }

    public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetEntitySpeSom()
    {
        return fightUI.EntitySpeSom;
    }
    public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToCemeteryUI()
    {
        return fightUI.GotoCeCemeteryUI;
    }
    public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToBanishedUI()
    {
        return fightUI.GotoBanishedUI;
    }
    public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToHandUI()
    {
        return fightUI.GotoHandUI;
    }
    public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToDeckUI()
    {
        return fightUI.GotoDeckUI;
    }
    public Transform FindCardRealTransform(EffLogic.EntityCard realCard)      //LogicLinkUI实现:真实实体→UI真实预制体(经FightUI区域容器反查)
    {
        return fightUI?.FindRealCardTransform(realCard);
    }
    public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> MagicActBase()
    {
        return fightUI.MagicActBase;
    }
    public Action<string> GetBoutChangeReback()
    {
        return fightUI.BoutChangeReback;
    }
    public Func<UniTask<bool>> GetWaitForPlayClick()
    {
        return fightUI.WaitForPlayClick;
    }
    public void DrawCardHandle(int playerID, int num)
    {
        List<EffLogic.EntityCard> entityCards = fightLogic.DrawCard(playerID,num);
        _UIListener?.DrawCardHandle(playerID, entityCards);
    }
    public Func<int, List<EffLogic.EntityCard>, UniTask> GetDrawCardHandleUI()     //纯UI手牌刷新回调(效果抽卡数据已由数据层移动,此处只重建手牌区,避免重复抽卡)
    {
        return (whom, entityCards) =>
        {
            _UIListener?.DrawCardHandle(whom, entityCards);
            return UniTask.CompletedTask;
        };
    }
    #endregion

    #region PreEntity调用
    public void ShowCardPicRequest(string id, string effect)
    {
        fightUI.ShowCardPic(id,effect);
    }

    public void SacrificeNumRequest(int num)
    {
        fightUI.RequestSacrifice(num);
    }
    public bool IsWaitingForAsyncRequest()
    {
        return fightUI.IsWaitingForAsync;
    }
    public bool IsSelectingQuest()               //选卡弹窗(ShowSelectionDialog)等待玩家选卡期间,拒绝场景点击驱动的数据操作(挂载/召唤/盖放/发动新效果)
    {
        return fightUI != null && fightUI.GetIsWaitingQuest();
    }
    public async UniTask<List<Transform>> WaitForSacrificeSelection()
    {
        return await fightUI.WaitForSacrificeSelection();
    }

    public void GotoCeCemeteryUIRequest(Transform transform)
    {
        fightUI.GotoCeCemeteryUI(transform);
    }

    public void ShowEffChoiceRequest(List<EffLogic.Effection> effs, Action<EffLogic.Effection> onChosen)
    {
        fightUI.ShowEffChoice(effs, onChosen);
    }

    public void PlaceCardToEnemyFieldRequest(Transform transform)
    {
        fightUI.PlaceCardToEnemyField(transform);
    }

    public void RebackTextRequest(string text, int lim = 0)
    {
        fightUI.RebackText(text, lim);
    }

    public void GetSumOrCoverRequest(Transform transform)
    {
        fightUI.GetSumOrCover(transform);
    }
    public void GetMagicTrapRequest(Transform transform)
    {
        fightUI.GetMagicTrap(transform);
    }
    public void AwakeSomRegionRequest(int i)
    {
        fightUI.AwakeRegion(i);
    }

    public EntityPlayer.Field GetFieldRequest()
    {
        return fightLogic.player1Field;
    }
    public void RegionAddCardRequest(int i, EffLogic.EntityCard entityCard, bool isEnemy = false)
    {
        EntityPlayer entityPlayer;
        if (!isEnemy)
            entityPlayer = fightLogic.entityPlayer1;
        else
            entityPlayer = fightLogic.enemy;
        switch (i)
        {
            case 0:
                entityPlayer.deck.AddCard(entityCard);
                break;
            case 1:
                entityPlayer.extraDeck.AddCard(entityCard);
                break;
            case 2:
                entityPlayer.hand.AddCard(entityCard);
                break;
            case 3:
                entityPlayer.cemetery.AddCard(entityCard);
                break;
            case 4:
                entityPlayer.banished.AddCard(entityCard);
                break;
            case 5:
                entityPlayer.field.AddCard(entityCard);
                break;
            default:
                throw new System.Exception($"参数错误,i:{i}");

        }
    }
    public void RegionRemoveCardRequest(int i, EffLogic.EntityCard entityCard, bool isEnemy = false)
    {
        EntityPlayer entityPlayer;
        if (!isEnemy)
            entityPlayer = fightLogic.entityPlayer1;
        else
            entityPlayer = fightLogic.enemy;
        switch (i)
        {
            case 0:
                entityPlayer.deck.RemoveCard(entityCard);
                break;
            case 1:
                entityPlayer.extraDeck.RemoveCard(entityCard);
                break;
            case 2:
                entityPlayer.hand.RemoveCard(entityCard);
                break;
            case 3:
                entityPlayer.cemetery.RemoveCard(entityCard);
                break;
            case 4:
                entityPlayer.banished.RemoveCard(entityCard);
                break;
            case 5:
                entityPlayer.field.RemoveMonster(entityCard);
                break;
            default:
                throw new System.Exception($"参数错误,i:{i}");
        }
    }

    public bool CheckCanSummonRequest(int playerID)
    {
        if (playerID == 0)
        {
            return fightLogic.entityPlayer1.CanSummon();
        }
        else
        {
            return fightLogic.enemy.CanSummon();
        }
    }

    public void AddCurSomNumRequest(int playerID)
    {
        if (playerID == 0)
        {
            fightLogic.entityPlayer1.AddCurSomNum();
        }
        else
        {
            fightLogic.enemy.AddCurSomNum();
        }
    }

    public GameManage.GamePhase GetCurrentGamePhase()
    {
        return fightLogic.GetCurGamePhase();
    }

    public void EntityEffRequestHandle(EntityEffRequest entityEffRequest)
    {
        fightLogic.EntityEff(entityEffRequest.entityCard, entityEffRequest.preEntityCard);
    }

    public void PushOrderStack(StateMachine.EffOrder effOrder)
    {
        fightLogic.PushOrderStack(effOrder);
    }
    public async UniTask HandleOrderStack()                             //结算连锁栈，结算后立即询问是否发动诱发（无定时等待）
    {
        await fightLogic.HandleOrderStack();                            //1.逆序结算连锁栈（等待所有效果执行完毕）
        await HandleTriggerTimePoints();                                //2.诱发询问循环
    }
    public async UniTask HandleTriggerTimePoints()                      //诱发询问循环：有诱发时点才询问；无时点立即返回（通召等自由动作成功后也可调用）
    {
        //诱发流程：弹[是否发动诱发效果？是/否]→点[是]关闭面板→场上高亮诱发卡可连续点击发动(逐一入栈)，
        //直到点击DeleteEff退出诱发→(有诱发入栈时)进入连锁"逐张重弹"询问；
        //连锁流程：弹[是否连锁？是/否]→点[是]关闭面板→点击一张连锁卡入栈→再次弹询问，直到点[否]结算；
        //若误触[是](不点卡)可点DeleteEff回退本次[是]，询问面板会重新弹出。
        //即使场上无卡可发动每次也会询问，由玩家点[否]跳过。
        //并发防御:通召(SumOrCovBaseRequest对每次通召Forget启动)、效果特召、效果结算等都会调用本循环,各来源共享
        //  timePointBases/当前时点/询问TCS。若无互斥,后启动的循环会因AskTrigger互斥立即返回false→对前一个循环正等待
        //  询问的窗口执行ClearTimePoints、并把当前时点改回自由→诱发卡不点亮/点了没反应(偶发失效)。
        //  故同刻仅保留一个循环:新窗口的时点照常登记在共享表,由运行中的循环按剩余时点继续询问;重复请求直接返回。
        if (triggerLoopActive > 0)
            return;
        triggerLoopActive++;
        try
        {
            int triggerRound = 0;                                        //诊断:诱发询问轮次
            while (!fightLogic.IsTimePointEmpty())
            {
                triggerRound++;
                await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: FightAsyncScope.Token);
                //询问玩家是否发动诱发（是/否）
                fightLogic.SetCurrentPoint(StateMachine.CurrentPoint.Target);   //改为诱发时点
                //窗口快照:登记"本窗口"实际包含的时点。询问弹窗期间/点卡期间新登记到表里的时点(新召唤/进墓等)不属于本窗口,
                //分发与清除都只针对本快照,使它们保留下来由后续轮次继续询问,避免被整表清空误杀(偶发诱发不能发动)。
                List<StateMachine.TimePointBase> window = fightLogic.GetTimePointSnapshot();
                if (window.Count == 0)
                    continue;
                UnityEngine.Debug.Log($"[诱发诊断] 第{triggerRound}轮询问,本窗口时点数:{window.Count},表内总时点数:{fightLogic.GetTimePointCount()}");
                bool wantTrigger = await fightUI.AskTrigger("是否发动诱发效果？");
                if (!wantTrigger)                                    //玩家选择"否"：放弃当前窗口的诱发(仅移除本窗口时点,期间新到窗口保留)
                {
                    fightLogic.RemoveTimePoints(window);
                    continue;                                        //新窗口仍在表内则继续询问;表已空则外层while自然退出
                }
                SetTriggerAsk(true);                                     //进入等待诱发发动状态
                fightLogic.StartTriggerEff(window);                      //仅分发本窗口时点(不满足费用/次数用尽的诱发自动过滤，不点亮)
                bool anyTrigger = await fightUI.WaitForTriggerActivate();//点卡阶段(面板已关)：可连续点诱发卡入栈，点DeleteEff退出
                SetTriggerAsk(false);
                fightLogic.RemoveTimePoints(window);                     //诱发时点窗口结束(无论是否发动)即清空本窗口时点;期间新登记的时点留在列表进入下一轮询问
                if (!anyTrigger)                                         //未发动任何诱发（DeleteEff退出/无卡可点）：结束本次诱发询问
                    continue;
                //已发动诱发入栈：进入连锁问询（栈顶orderStackState==3表示不可被连锁,跳过连锁问询直接结算）
                fightLogic.SetCurrentPoint(StateMachine.CurrentPoint.Chain);    //改为连锁时点
                if (!EffLogic.IsUnchainable(fightLogic.GetTopOrderStackState()))
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: FightAsyncScope.Token);    //玩家结束诱发后等1s，再询问是否有连锁效果
                    await AskChainQuestionLoop();                        //逐张重弹:是→关面板→点一张入栈→再弹;否→结算;DeleteEff回退本次[是]
                }
                fightLogic.SetCurrentPoint(StateMachine.CurrentPoint.Freedom);   //开始结算 ,改为自由时点
                //结算前暂存"非本窗口的新到诱发窗口"(如点卡/连锁点卡间隙手动新召唤登记的表内时点)。
                //注意stateMachine.HandleOrderStack入口会清空整个时点表,若不清除这些新窗口会在此处被静默吞掉,
                //须在结算完成后补回,再由外层while继续询问。
                List<StateMachine.TimePointBase> resolvePending = fightLogic.GetTimePointSnapshot();
                await fightLogic.HandleOrderStack();                        //结算诱发效果（结算期间新产生的诱发时点在入口清空之后登记,自然保留）
                fightLogic.ReAddTimePoints(resolvePending);                 //补回结算前新到窗口→下一轮循环继续询问
            }
        }
        finally
        {
            triggerLoopActive--;
            //退出保护:若在循环收尾的同步间隙恰好有新窗口登记(未被while捕获),兜底重启一轮,避免窗口静默遗漏。
            //对局已结束时(作用域取消)不重启,否则新循环会在首个Delay处立刻抛取消异常并再次触发finally,无限空转。
            if (triggerLoopActive == 0 && !fightLogic.IsTimePointEmpty() && !FightAsyncScope.IsCanceled)
                HandleTriggerTimePoints().Forget();
        }

        fightLogic.SetCurrentPoint(StateMachine.CurrentPoint.Freedom);   //结束：恢复自由时点
    }
    //连锁"逐张重弹"询问循环：弹[是否连锁？]→[是]关闭面板→等待点一张连锁卡入栈→再次弹询问；直至点[否]结束连锁问询(由调用方结算)；
    //等待点卡时点DeleteEff=回退本次[是](未发动)，询问面板重新弹出。
    private async UniTask AskChainQuestionLoop()
    {
        SetChainAsk(true);                                      //进入连锁问询状态(卡牌点击按连锁合法性过滤)
        bool isFirst = true;
        try
        {
            while (true)
            {
                if (EffLogic.IsUnchainable(fightLogic.GetTopOrderStackState()))   //栈顶效果不可被连锁:结束问询直接结算
                    break;
                bool go = await fightUI.AskOrderStack(isFirst ? "是否连锁？" : "是否继续连锁？");
                if (!go) break;                                  //点[否]：结束连锁问询 → 结算
                isFirst = false;
                await fightUI.WaitForChainActivate();            //点[是]关面板:等待点一张连锁卡入栈(true→再次弹询问)或点DeleteEff回退(false→重新弹询问)
            }
        }
        finally
        {
            SetChainAsk(false);                                  //退出连锁问询状态
        }
    }
    public UniTask<bool> AskOrderStackAsk(string tipText = null)
    {
        return fightUI.AskOrderStack(tipText);
    }
    public bool IsChainAsk()
    {
        return fightUI.IsChainAsk;
    }
    public void SetChainAsk(bool isChain)
    {
        fightUI.SetChainAsk(isChain);
    }
    public UniTask<bool> WaitForChainActivate()
    {
        return fightUI.WaitForChainActivate();
    }
    public void ReportChainActivate(bool success)
    {
        fightUI.ReportChainActivate(success);
    }
    #region 战斗：攻击流程
    private bool isAttacking;                                               //攻击流程互斥锁（防止攻击中重复触发）

    public async UniTask AttackRequest(EffLogic.EntityMonsterCard attacker) //攻击宣言 → 目标选择 → 伤害判定
    {
        if (isAttacking) return;
        if (attacker == null || !fightLogic.IsMonsterAttackable(attacker)) return;   //可攻击规则判定统一下沉FightLogic
        if (fightLogic.GetCurGamePhase() != GameManage.GamePhase.FightPhase) return;

        isAttacking = true;
        try
        {
            //进入战斗时点：攻击过程中锁定效果发动（主动/诱发均不响应）
            fightLogic.SetCurrentPoint(StateMachine.CurrentPoint.Attack);

            int owner = fightLogic.GetOnwerPhase();                         //当前回合玩家
            bool selfIsEnemy = owner == 1;                                  //自己是否为enemy（owner==1时数据存于enemy）
            bool oppIsEnemy = owner == 0;                                   //对方是否为enemy
            EntityPlayer opp = owner == 0 ? fightLogic.enemy : fightLogic.entityPlayer1;
            int oppId = owner == 0 ? 1 : 0;

            //① 收集对方场上可被攻击的怪兽（规则判定统一下沉FightLogic）
            List<EffLogic.EntityCard> targets = new List<EffLogic.EntityCard>();
            foreach (var c in opp.field.GetMonZon())
            {
                if (fightLogic.IsMonsterTargetable(c))
                    targets.Add(c);
            }

            //② 阶段一：攻击宣言
            if (targets.Count == 0)                                         //对方无怪兽：直接攻击
            {
                fightUI.RebackText($"{attacker.currentName} 发动直接攻击！", 3);
                int dmg = attacker.currentAtt;
                if (dmg > 0)
                {
                    BattleDamageRequestHandle(oppId, dmg, attacker);   //战斗伤害:额外触发战斗伤害事件
                    fightUI.RebackText($"{attacker.currentName} 的直接攻击命中！对方受到 {dmg} 点伤害", 3);
                }
            }
            else
            {
                EffLogic.EntityMonsterCard target = null;
                bool isAI = fightLogic.enemyAI != null && fightLogic.enemyAI.IsEnable && owner == 1;   //AI回合：不弹选择UI
                if (isAI)
                {
                    //AI自动选择目标：优先攻击力最低的可被攻击怪兽（最易击破）
                    fightUI.RebackText($"{attacker.currentName} 发动攻击宣言！", 3);
                    target = targets
                        .Select(c => c as EffLogic.EntityMonsterCard)
                        .Where(m => m != null)
                        .OrderBy(m => m.currentAtt)
                        .FirstOrDefault();
                    if (target == null) return;
                }
                else
                {
                    fightUI.RebackText($"{attacker.currentName} 发动攻击宣言！请选择攻击目标", 3);
                    List<EffLogic.EntityCard> selected = await fightUI.ShowSelectionDialog(targets, 1);
                    if (selected == null || selected.Count == 0) return;        //取消攻击
                    target = selected[0] as EffLogic.EntityMonsterCard;
                    if (target == null) return;
                }

                fightUI.RebackText($"{attacker.currentName} 向 {target.currentName} 发动攻击！", 3);   //攻击宣言反馈（谁向谁攻击）

                //③ 阶段二：伤害判定（攻击力 vs 对方攻击力/守备力）
                int atk = attacker.currentAtt;
                if (target.showAttOrDef == 1)                               //对方攻击表示：攻 vs 攻
                {
                    if (atk > target.currentAtt)                            //击破目标，对方受到差值伤害
                    {
                        SendMonsterToCemetery(target, oppIsEnemy);
                        int dmg = atk - target.currentAtt;
                        if (dmg > 0) BattleDamageRequestHandle(oppId, dmg, attacker);   //战斗伤害:额外触发战斗伤害事件
                        fightUI.RebackText($"{target.currentName} 被战斗破坏！对方受到 {dmg} 点伤害", 3);
                    }
                    else if (atk < target.currentAtt)                       //攻击方被击破，自己受到差值伤害
                    {
                        SendMonsterToCemetery(attacker, selfIsEnemy);
                        int dmg = target.currentAtt - atk;
                        if (dmg > 0) BattleDamageRequestHandle(owner, dmg, target);   //战斗伤害:额外触发战斗伤害事件
                        fightUI.RebackText($"{attacker.currentName} 被战斗破坏！自己受到 {dmg} 点伤害", 3);
                    }
                    else                                                    //攻击力相等：同归于尽
                    {
                        SendMonsterToCemetery(target, oppIsEnemy);
                        SendMonsterToCemetery(attacker, selfIsEnemy);
                        fightUI.RebackText($"{attacker.currentName} 与 {target.currentName} 同归于尽！", 3);
                    }
                }
                else                                                        //对方守备表示：攻 vs 防
                {
                    if (atk > target.currentDef)                            //击破守备怪兽，对方受到贯通伤害
                    {
                        SendMonsterToCemetery(target, oppIsEnemy);
                        int dmg = atk - target.currentDef;
                        if (dmg > 0) BattleDamageRequestHandle(oppId, dmg, attacker);   //战斗伤害:额外触发战斗伤害事件
                        fightUI.RebackText($"{target.currentName}（守备表示）被击破！对方受到 {dmg} 点贯通伤害", 3);
                    }
                    else if (atk < target.currentDef)                       //攻击无法击破，自己受到差额伤害（攻击方不破坏）
                    {
                        int dmg = target.currentDef - atk;
                        if (dmg > 0) BattleDamageRequestHandle(owner, dmg, target);   //战斗伤害:额外触发战斗伤害事件
                        fightUI.RebackText($"{attacker.currentName} 的攻击被 {target.currentName} 抵挡！自己受到 {dmg} 点伤害", 3);
                    }
                    else                                                    //攻防相等：无伤害无破坏
                    {
                        fightUI.RebackText($"{attacker.currentName} 的攻击被 {target.currentName} 完全抵挡！", 3);
                    }
                }
            }
            attacker.ChangeIsAttack();                                      //攻击后本回合不能再攻击

            //④ 刷新战斗UI：已攻击的怪兽不再高亮/显示攻击按钮，被破坏的怪兽已移出
            fightUI.EnterFightPhase();
        }
        finally
        {
            fightLogic.SetCurrentPoint(StateMachine.CurrentPoint.Freedom);  //恢复自由时点
            isAttacking = false;
        }
    }

    private void SendMonsterToCemetery(EffLogic.EntityCard card, bool isEnemy)   //战斗破坏：怪兽移出场上并送入墓地
    {
        if (card == null) return;
        Transform t = fightLogic.FindRealCardTransform(card);
        if (t != null)
        {
            PreEntityCard pec = t.GetComponent<PreEntityCard>();
            pec?.SetCanUsePoint(true);                                      //恢复卡牌点击
        }
        RegionRemoveCardRequest(5, card, isEnemy);                          //从场上移除
        RegionAddCardRequest(3, card, isEnemy);                             //加入墓地
        if (t != null) GotoCeCemeteryUIRequest(t);                          //UI移动到墓地
    }
    #endregion

    public bool IsTriggerAsk()
    {
        return fightUI.IsTriggerAsk;
    }
    public void SetTriggerAsk(bool isTrigger)
    {
        fightUI.SetTriggerAsk(isTrigger);
    }
    public UniTask<bool> WaitForTriggerActivate()
    {
        return fightUI.WaitForTriggerActivate();
    }
    public void ReportTriggerActivate(bool success)
    {
        fightUI.ReportTriggerActivate(success);
    }
    public bool IsTimePointEmpty()
    {
        return fightLogic.IsTimePointEmpty();
    }
    public int GetTopOrderSpeed()
    {
        return fightLogic.GetTopOrderSpeed();
    }
    public int GetTopOrderStackState()
    {
        return fightLogic.GetTopOrderStackState();
    }
    public void SetCurrentPointRequest(StateMachine.CurrentPoint point)   //设置当前时点
    {
        fightLogic.SetCurrentPoint(point);
    }
    #endregion

    #region RegClick
    public void SumOrCovBaseRequest(SumOrCovBaseRequest sumOrCovBaseRequest)
    {
        //选卡弹窗(ShowSelectionDialog/取对象等IsWaitingQuest=true)打开期间拒绝手动召唤/特招:
        //弹窗候选等待玩家选卡时,若同卡又被蓝区手动特招上场,玩家点弹窗后会被"特招前剔场→剩余0→无反应"。
        //互斥锁让玩家只能在弹窗关闭后再操作场面,从操作层杜绝等待期漂移
        if (IsSelectingQuest())
        {
            return;
        }
        EffLogic.EntityCard entityCard = _UIListener?.SumOrCovBaseHandle(sumOrCovBaseRequest?.transform);
        if (entityCard == null)
            return;                                                             //特招预制体挂载失败:不执行数据层特招,防止误触发诱发/错误卡上场
        //先完成本次特招的UI清理(关闭蓝区高亮/解除特招等待/释放speSumOrCover引用),
        //再执行数据层特招:fightLogic.SpeSom可能同步触发诱发流程,诱发特招会复用speSom/speSumOrCover/蓝区等共享字段,
        //清理若放在SpeSom之后,会误关诱发流程的蓝区、误完成诱发等待源,导致"未召唤却触发诱发"的竞态
        _UIListener?.ClearRegionUIHandle();
        fightLogic.SpeSom(entityCard);
        //通召成功(通召/盖放模式下落场的是怪兽=手牌通常召唤):登记Summon诱发时点并进入诱发询问循环(启用诱发);
        //特招(mode==1)走效果内自己的诱发结算、魔陷盖放非召唤,均不在此广播
        if (entityCard is EffLogic.EntityMonsterCard && fightUI?.IsSumOrCovMode() == true)
        {
            fightLogic.AddSummonTimePoint(entityCard);
            HandleTriggerTimePoints().Forget();
        }
    }
    public void LayOutMagicTrapCardRequest(LayOutMagicTrapRequest layOutMagicTrapRequest)
    {
        if (IsSelectingQuest())          //选卡弹窗期间拒绝盖放/魔法发动放置(与SumOrCovBaseRequest同理)
        {
            return;
        }
        _UIListener?.LayOutMagicTrapCardHandle(layOutMagicTrapRequest.transform);
        //盖放流程(非魔法表侧发动,isMagicActBase=false)的数据同步:卡从手牌真正落入我方魔陷区,
        //使回合结束UpdateCoverTime能遍历到该盖放卡(盖放当回合coverTime=0不可发动,满一回合=1后才可发动);
        //表侧发动(MagicActBase等待放置)的数据移动已在MagicActBase.Pay完成,这里跳过避免重复加入
        if (fightUI != null && !fightUI.GetIsMagicActBase() && layOutMagicTrapRequest.transform.childCount > 0)
        {
            PreEntityCard pec = layOutMagicTrapRequest.transform.GetChild(0).GetComponent<PreEntityCard>();
            MoveHandCardToMagicTrapZone(pec?.GetEntityCard());
        }
        fightUI.EndWaitMagicActBase();
    }
    private void MoveHandCardToMagicTrapZone(EffLogic.EntityCard card)      //手牌魔陷盖放落位:数据上离手并进入魔陷区(仅支持我方;敌方AI盖放走EnemyAI数据流程)
    {
        if (card == null) return;
        EntityPlayer owner = card.GetCurrentOwner();
        if (owner == null || owner != fightLogic.entityPlayer1) return;
        if (card.location == GameManage.CardLocation.Hand)
        {
            owner.hand?.RemoveCard(card);
            card.ChangeLocation(GameManage.CardLocation.Field);
        }
        if (owner.field != null && !owner.field.GetMTZon().Contains(card))
            owner.field.AddMagicTrap(card);
    }
    public void ClearUIRequest()
    {
        _UIListener?.ClearRegionUIHandle();
    }
    public void ClearSacrificesRequest()
    {
        fightUI.IniSc();
    }
    public bool IsWaitingForSacrificeRequest()
    {
        return fightUI.GetIsWaitingForSacrifice();
    }
    public void AddSelSacRequest(Transform transform)
    {
        fightUI.AddSelSac(transform);
    }
    public List<Transform> GetSelSacRequest()
    {
        return fightUI.GetSelSac();
    }
    public int GetRequiredSacrificeCountRequest()
    {
        return fightUI.GetRequiredSacrificeCount();
    }
    public void EndSacrificeWaitRequest()
    {
        fightUI.IniSum();
        fightUI.ReportSacrificeSelectionComplete();
    }
    public bool GetSumOrCoverLock()
    {
        return fightUI.GetSumOrCoverLock();
    }
    public bool GetMagicTrapLock()
    {
        return fightUI.GetMagicTrapLock();
    }
    #endregion
}

