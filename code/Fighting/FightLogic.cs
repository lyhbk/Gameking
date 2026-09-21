using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CardBase;
using static EffLogic;
using static EventMintor;
using static ExternalQuestBase.UILinkLog;
using Cysharp.Threading.Tasks;

public class FightLogic : MonoBehaviour
{
    private Player player1;

    private StateMachine _stateMachine;
    private StateMachine.StateMachineEventAction stateMachineEventAction;
    private EventMintor eventMintor;
    private readonly List<StateMachine.TimePointBase> timePointBases = new List<StateMachine.TimePointBase>();

    private StateMachine stateMachine
    {
        get
        {
            if (_stateMachine == null)
                InitStateMachine();
            return _stateMachine;
        }
    }

    public event Action<StateMachine.CurrentPoint> OnCurrentPointChanged;  //转发状态机时点变化事件（UI订阅）
    public event Action<GameManage.GamePhase> OnGamePhaseChanged;          //转发状态机阶段变化事件（UI订阅）

    public EntityPlayer entityPlayer1;
    public EntityPlayer enemy;

    public EnemyAI enemyAI { get; private set; }                        //敌方AI脚本（FightLogic持有）

    [SerializeField] private bool enableEnemyAI = true;                 //是否启用敌方AI（Inspector可关，用于手动测试）

    public EntityPlayer.Deck player1Deck;
    public EntityPlayer.ExtraDeck player1ExtraDeck;
    public EntityPlayer.Hand player1Hand = new EntityPlayer.Hand();
    public EntityPlayer.Cemetery player1Cemetery = new EntityPlayer.Cemetery();
    public EntityPlayer.Banished player1Banished = new EntityPlayer.Banished();
    public EntityPlayer.Field player1Field = new EntityPlayer.Field();

    public EntityPlayer.Deck enemyDeck;
    public EntityPlayer.ExtraDeck enemyExtraDeck;
    public EntityPlayer.Hand enemyHand = new EntityPlayer.Hand();
    public EntityPlayer.Cemetery enemyCemetery = new EntityPlayer.Cemetery();
    public EntityPlayer.Banished enemyBanished = new EntityPlayer.Banished();
    public EntityPlayer.Field enemyField = new EntityPlayer.Field();


    private LogicLinkUI requestHandler;

   
    
    private void Awake()
    {
        FightAsyncScope.Begin();            //开局:启用本局异步作用域(游戏结束时统一取消)
        eventMintor = new EventMintor();
        IniEntPlaCar();
        IniEQ();
        //洗牌必须早于开局发牌:FightUI.Start(双方各抽5的初始手牌)与本组件Start的执行先后不受脚本控制,
        //若洗牌放在Start里可能晚于发牌,导致起手手牌=卡组未洗的原序、每局雷同。
        //引擎保证所有Awake先于任何Start,故卡组建好后立即在此洗牌,双方卡组在发牌前必定完成洗乱。
        player1Deck.ShuffCard();
        enemyDeck.ShuffCard();
    }

    private void OnDestroy()
    {
        FightAsyncScope.End();              //场景卸载兜底:终止本局全部在途异步
    }

    private void InitStateMachine()
    {
        if (_stateMachine != null) return;
        IniStateMachineEventAction();
        _stateMachine = new StateMachine(Factory.EntityIniQuestPlayer(requestHandler?.GetBoutChangeReback(), WaitForPlayClickAI), stateMachineEventAction, timePointBases);
        _stateMachine.OnCurrentPointChanged += (point) => OnCurrentPointChanged?.Invoke(point);   //转发时点事件
        _stateMachine.OnGamePhaseChanged += (phase) => OnGamePhaseChanged?.Invoke(phase);         //转发阶段事件
    }

    //阶段切换询问：转发给UI弹窗等待玩家确认（AI回合同样询问，玩家可在对方回合发动效果）
    private UniTask<bool> WaitForPlayClickAI()
    {
        if (requestHandler?.GetWaitForPlayClick() is Func<UniTask<bool>> wait)
            return wait.Invoke();
        return UniTask.FromResult(true);
    }

    public void GiveRequestHandler(UILinkLogic requestHandler)
    {
        this.requestHandler = requestHandler;
        enemyAI = new EnemyAI(this, requestHandler) { IsEnable = enableEnemyAI };    // 创建并持有敌方AI脚本
        IniStateMachineEventAction();   // requestHandler 注入较晚时补注册抽卡回调（覆盖式赋值，安全）
        IniEQ();                        // 使用完整的 requestHandler 重新初始化 eQ
        if (_stateMachine != null)
            _stateMachine = null;
    }

    private void IniEntPlaCar()
    {
        GameObject player;
        player = GameObject.Find("player");
        player1 = player.GetComponent<Player>();
        entityPlayer1 = new EntityPlayer(player1.staticPlayer.GetPlayerId(), player1.staticPlayer.GetPlayerName(),
                                         player1Deck, player1ExtraDeck, player1Hand, player1Cemetery, player1Banished, player1Field);
        enemy = new EntityPlayer("enemy", "我太帅了",
                                         enemyDeck, enemyExtraDeck, enemyHand, enemyCemetery, enemyBanished,enemyField);
        entityPlayer1.SetPlayerIndex(0);                        //索引0:我方
        enemy.SetPlayerIndex(1);                                //索引1:对方

        Player.PreCardGroup CardGroup = player1.preCardGroupList.preCardGroups[player1.preCardGroupList.i];
        List<CardBase.Card> extraCards = CardGroup.GetExtraCards();
        List<CardBase.Card> mainCards = CardGroup.GetMainCards(extraCards);
        player1Deck = new EntityPlayer.Deck(mainCards, entityPlayer1);
        entityPlayer1.BindDeck(player1Deck);                  //玩家构造时卡组尚未创建,创建后回写
        enemyDeck = new EntityPlayer.Deck(mainCards, enemy);
        enemy.BindDeck(enemyDeck);
        player1ExtraDeck = new EntityPlayer.ExtraDeck(extraCards, entityPlayer1);
        entityPlayer1.BindExtraDeck(player1ExtraDeck);
        enemyExtraDeck = new EntityPlayer.ExtraDeck(extraCards, enemy);
        enemy.BindExtraDeck(enemyExtraDeck);

        //开局清扫:初始化阶段组卡/发牌产生的"进入区域"记录不属于任何回合,在对局首个回合开始前全部作废(此后每个回合界限由OnBoutEnd清空)
        entityPlayer1.ClearAllBoutSent();
        enemy.ClearAllBoutSent();
    }

    private void IniStateMachineEventAction()
    {
        if (stateMachineEventAction == null)
            stateMachineEventAction = new StateMachine.StateMachineEventAction();
        stateMachineEventAction.AddBoutEnd(OnBoutEnd);                          //回合结束委托：自动重置回合数据
        if (requestHandler != null)
            stateMachineEventAction.AddDrawPhaseDrawCard(requestHandler.DrawCardHandle);
    }

    //回合结束自动调用：owner=刚结束回合的玩家(由StateMachine在回合主翻转前传入)
    private void OnBoutEnd(int owner)
    {
        //回合界限:通召次数/效果发动次数是按"每个回合"累计的,双方在每次回合切换时都必须清零,
        //故未接手回合的一方也要重置:否则对方回合发动的"1回合1次"会残留到自己回合被误判为已发动
        if (owner == 0)
        {
            entityPlayer1.BoutEndFunion();               //刚结束回合的玩家:盖放时间(coverTime)/自肃/区域停留回合结算
            enemy?.ResetBoutTurnData();
        }
        else
        {
            enemy.BoutEndFunion();
            entityPlayer1?.ResetBoutTurnData();
        }
        //回合界限:对方回合开始前清空双方全部区域"本回合进入"登记表(上一回合进入记录作废,新回合从零累计)
        entityPlayer1?.ClearAllBoutSent();
        enemy?.ClearAllBoutSent();
    }


    public List<EntityCard> DrawCard(int playerID, int num)
    {
        if (playerID == 0) {
            player1Deck.DrawCard(player1Hand, num);
            return player1Hand.cards;
        }
        else
            enemyDeck.DrawCard(enemyHand, num);
        return enemyHand.cards;
    }

    public void AbandonCard(List<EntityCard> entityCards)
    {
        foreach(var item in entityCards)
        {
            RegionCard(0, false, item);
            RegionCard(3, true, item);
        }
    }

    public void ChangeHp(int i, int curHP)          //修改血量(增量,可负):掉血/回血统一入口
    {
        int oldHp = GetPlayerHP(i);
        if (i == 0)
            entityPlayer1.ChangeHP(curHP);
        else
            enemy.ChangeHP(curHP);
        int newHp = GetPlayerHP(i);
        if (oldHp != newHp)
            eventMintor?.AwakeOnHpChange(new HpChange(newHp - oldHp, newHp, i));   //血量改变总事件:任一玩家血量变化后触发
    }
    public void ChangeHpTo(int i, int curHP)         //修改血量到指定值(绝对值,如投降/回复类效果)
    {
        int oldHp = GetPlayerHP(i);
        if (i == 0)
            entityPlayer1.ChangeHPTo(curHP);
        else
            enemy.ChangeHPTo(curHP);
        int newHp = GetPlayerHP(i);
        if (oldHp != newHp)
            eventMintor?.AwakeOnHpChange(new HpChange(newHp - oldHp, newHp, i));   //血量改变总事件:任一玩家血量变化后触发
    }

    //战斗伤害事件:战斗阶段伤害判定掉血时的额外广播(区别于普通血量改变事件;对应EventMintor.HurtAttHp"受攻击伤害导致hp下降")
    //由UILinkLogic在战斗伤害落血后调用:伤害量dmg>0,playerId=受伤方,sourceCard=造成该伤害的怪兽(无来源时传null)
    public void FireBattleDamageEvent(int dmg, int playerId, EntityCard sourceCard)
    {
        if (dmg <= 0) return;
        eventMintor?.AwakeOnHurtAttHp(new HurtAttHp(dmg, playerId, sourceCard));
    }


    private int boutAdvancing;                                      //阶段推进链深度(>0=正在推进:含AI回合自动递归)
    public bool IsBoutAdvancing => boutAdvancing > 0;               //供UI判断"下一阶段"当前是否可点

    public async UniTask BoutChange()
    {
        if (FightAsyncScope.IsCanceled) return;         //对局已结束:停止阶段推进(含AI递归进入新回合)
        boutAdvancing++;
        try
        {
            int ownerBefore = stateMachine.GetCurOwner();
            GameManage.GamePhase phaseBefore = stateMachine.GetCurBout();
            await stateMachine.ChangeState();
            if (FightAsyncScope.IsCanceled) return;
            //阶段询问里选"取消"=停留当前阶段：本次不推进AI行动,等玩家再点"下一阶段"继续
            if (stateMachine.GetCurOwner() == ownerBefore && stateMachine.GetCurBout() == phaseBefore) return;
            //AI回合接管：进入新阶段后由AI执行本阶段操作并自动推进下一阶段（递归直至回到玩家回合）
            if (enemyAI != null && enemyAI.IsEnable && stateMachine.GetCurOwner() == 1)
                await enemyAI.OnBoutChanged();
        }
        finally
        {
            boutAdvancing--;
        }
    }


    #region    EffLogic外部请求

    public EntityPlayer HandleEntityPlayer()
    {
        return entityPlayer1;
    }

    public List<EntityPlayer.EntityFindComponent> HandleFindComponentRequest(List<bool>bools)
    {
        //bool编码共12位:前6位(0-5)为自己区域,后6位(6-11)为对方区域;返回列表顺序与编码一致,便于按索引访问
        List<EntityPlayer.EntityFindComponent> components = new List<EntityPlayer.EntityFindComponent>();
        if (bools[0]) components.Add(player1Deck);
        if (bools[1]) components.Add(player1ExtraDeck);
        if (bools[2]) components.Add(player1Hand);
        if (bools[3]) components.Add(player1Cemetery);
        if (bools[4]) components.Add(player1Banished);
        if (bools[5]) components.Add(player1Field);
        if (bools.Count > 6)
        {
            if (bools[6]) components.Add(enemyDeck);
            if (bools[7]) components.Add(enemyExtraDeck);
            if (bools[8]) components.Add(enemyHand);
            if (bools[9]) components.Add(enemyCemetery);
            if (bools[10]) components.Add(enemyBanished);
            if (bools[11]) components.Add(enemyField);
        }
        return components;
    }

    public GameManage.GamePhase GetGamePhase()
    {
        return stateMachine.GetCurBout();
    }
    public int GetOnwerPhase()
    {
        return stateMachine.GetCurOwner();
    }
    //战斗
    public void ResetBoutAttackFlags()                      //重置双方场上怪兽的攻击状态(每回合进入战斗阶段时调用)
    {
        foreach (var c in entityPlayer1.field.GetMonZon())
            (c as EntityMonsterCard)?.ResetAttackFlag();
        foreach (var c in enemy.field.GetMonZon())
            (c as EntityMonsterCard)?.ResetAttackFlag();
    }
    public bool IsMonsterAttackable(EntityCard mon)         //怪兽是否可发起攻击(攻击表示且本回合未攻击)
    {
        return mon is EntityMonsterCard m && m.showAttOrDef == 1 && m.isattack == 1;
    }
    public bool IsMonsterTargetable(EntityCard mon)         //怪兽是否可被选为攻击目标(beAttacked==1)
    {
        return mon is EntityMonsterCard m && m.beAttacked == 1;
    }




    public int GetPlayerHP(int playerId)                    //玩家当前HP(数据层权威值)
    {
        return playerId == 0 ? entityPlayer1.GetPlayerHP() : enemy.GetPlayerHP();
    }

    public void RegionCard(int regionNum, bool isAdd,EntityCard entityCard)      //在区域中增减卡片
    {
        if (isAdd)
        {
            RegionAddCard(entityCard, regionNum);
        }
        else
        {
            RegionDeleteCard(entityCard);
        }
    }
    public void RegionAddCard(EntityCard entityCard, int regionNum)               //在区域中添加卡片
    {
        switch (regionNum)
        {
            case 0:
                player1Deck.AddCard(entityCard);
                entityCard.ChangeLocation(GameManage.CardLocation.Deck);
                break;
            case 1:
                player1ExtraDeck.AddCard(entityCard);
                entityCard.ChangeLocation(GameManage.CardLocation.ExtraDeck);
                break;
            case 2:
                player1Hand.AddCard(entityCard);
                entityCard.ChangeLocation(GameManage.CardLocation.Hand);
                break;
            case 3:
                player1Cemetery.AddCard(entityCard);
                entityCard.ChangeLocation(GameManage.CardLocation.Cemetery);
                break;
            case 4:
                player1Banished.AddCard(entityCard);
                entityCard.ChangeLocation(GameManage.CardLocation.Banished);
                break;
            case 5:
                player1Field.AddCard(entityCard);
                entityCard.ChangeLocation(GameManage.CardLocation.Field);
                break;
            default:
                throw new Exception($"输入错误 , regionNum:{regionNum}");
        }
    }
    public void RegionDeleteCard(EntityCard entityCard)                           //在区域中去除卡片
    {
        GameManage.CardLocation cardLocation = entityCard.location;
        switch(cardLocation)
        {
            case GameManage.CardLocation.Deck:
                player1Deck.RemoveCard(entityCard);
                break;
            case GameManage.CardLocation.ExtraDeck:
                player1ExtraDeck.RemoveCard(entityCard);
                break;
            case GameManage.CardLocation.Hand:
                player1Hand.RemoveCard(entityCard); 
                break;
            case GameManage.CardLocation.Cemetery:
                player1Cemetery.RemoveCard(entityCard);
                break;
            case GameManage.CardLocation.Banished:
                player1Banished.RemoveCard(entityCard);
                break;
            case GameManage.CardLocation.Field:                                     //场上:从怪兽区/魔陷区移除(旧版落入default空转,导致"location=Field的卡被特招/移动"时场上旧数据不清理→重复/残留)
                player1Field.RemoveMonster(entityCard);
                player1Field.RemoveMagicTrap(entityCard);
                break;
            default:
                break;

        }
    }

    public void SpeSom(EntityCard entityCard, bool isGiveEme = false)                                      //特招函数
    {
        UnityEngine.Debug.Log($"[FightLogic.SpeSom] 数据特招 entity={entityCard?.currentName} id={entityCard?.GetHashCode()} curId={entityCard?.currentid} loc={entityCard?.location} isGiveEme={isGiveEme}");
        RegionCard( -1, false, entityCard);
        entityCard.ChangeLocation(GameManage.CardLocation.Field);
        if(!isGiveEme)
            player1Field.AddMonster(entityCard);
        else
            enemyField.AddMonster(entityCard);
    }
    public bool IsEntityOnMyFieldMonZone(EffLogic.EntityCard entityCard)      //诊断:该实体此刻是否已在我方场上怪兽容器(重复特招/并发放置检测)
    {
        return entityCard != null && player1Field != null && player1Field.GetMonZon().Contains(entityCard);
    }

    //连锁栈
    public void PushOrderStack(StateMachine.EffOrder effOrder)
    {
        stateMachine.PushOrderStack(effOrder);
    }
    public async UniTask HandleOrderStack()
    {
        await stateMachine.HandleOrderStack();
    }
    public bool IsTimePointEmpty()
    {
        return stateMachine.IsTimePointEmpty();
    }
    public int GetTimePointCount()                       //诱发时点表当前数量(诊断用)
    {
        return timePointBases.Count;
    }
    public int GetTopOrderSpeed()
    {
        return stateMachine.GetTopOrderSpeed();
    }
    public int GetTopOrderStackState()                 //连锁栈栈顶效果进栈状态(-1正常/0禁怪兽/1禁魔法/2禁陷阱/10禁怪兽魔法/20禁怪兽陷阱/21禁魔法陷阱/3不可连锁直接结算;栈空返回-1)
    {
        return stateMachine.GetTopOrderStackState();
    }
    public bool IsOrderStackEmpty()
    {
        return stateMachine.IsOrderStackEmpty();
    }
    public StateMachine.EffOrder GetCurEffOrder()               //获取当前链顶效果(栈空返回null),供JudLastEffCost等判断连锁中上一个效果
    {
        return stateMachine.GetTopEffOrder();
    }
    public StateMachine.CurrentPoint GetCurrentPoint()          //当前时点（最初为自由时点）
    {
        return stateMachine.currentPoint;
    }
    public void SetCurrentPoint(StateMachine.CurrentPoint point) //设置当前时点
    {
        stateMachine.SetCurrentPoint(point);
    }
    public void StartTriggerEff(List<StateMachine.TimePointBase> points = null)   //发动诱发：按时点id分发AwakeOn(缺省分发全部时点;诱发询问循环按"窗口快照"只分发当前窗口)
    {
        if (points == null)
            points = timePointBases;
        for (int i = 0; i < points.Count; i++)
        {
            StateMachine.TimePointBase timePoint = points[i];
            //日志:解析时点对应实体(不同时点子类各自持有entityCard)
            var tpEntity = (timePoint as StateMachine.InCemetery)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.InBanished)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.SpeSomTimePoint)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.FusionSummon)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.Summon)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.MagicEffTimePoint)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.TrapEffTimePoint)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.MonsterEffTimePoint)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.GetCard)?.entityCard;
            if (tpEntity == null) tpEntity = (timePoint as StateMachine.LeaveEx)?.entityCard;
            switch (timePoint.id)
            {
                case 6:                                                     //除外(进入除外区)时点
                    var inBanished = timePoint as StateMachine.InBanished;
                    eventMintor.AwakeOnEnterBanished(new EventMintor.EnterBanished(inBanished.entityCard, inBanished.fromLocation));
                    break;
                case 7:                                                     //进墓时点
                    var inCemetery = timePoint as StateMachine.InCemetery;
                    eventMintor.AwakeOnEntercemtery(new Entercemtery(inCemetery.entityCard, inCemetery.fromLocation));
                    break;
                case 9:                                                     //通召成功时点
                    var summonTimePoint = timePoint as StateMachine.Summon;
                    eventMintor.AwakeOnMonsterSummon(new EventMintor.MonsterSummon(summonTimePoint.entityCard));
                    break;
                case 10:                                                    //特招时点
                    var speSomTimePoint = timePoint as StateMachine.SpeSomTimePoint;
                    eventMintor.AwakeOnMonsterSpecialSummon(new MonsterSpecialSummon(speSomTimePoint.entityCard));
                    break;
                case 11:                                                    //融合特殊召唤时点
                    var fusionTimePoint = timePoint as StateMachine.FusionSummon;
                    eventMintor.AwakeOnMonsterSpecialSummon(new MonsterSpecialSummon(fusionTimePoint.entityCard));  //兼容普通特招诱发(融合召唤仍属特殊召唤)
                    eventMintor.AwakeOnFusionSummon(new EventMintor.FusionSummon(fusionTimePoint.entityCard));      //融合召唤成功诱发(SelfFusionSummon)
                    break;
                case 12:                                                    //有卡从卡组加入手卡(检索)时点:GetCard效果结算登记,广播给"自身被从卡组加入手卡时"诱发(SelfGetCard)
                    var getCardTimePoint = timePoint as StateMachine.GetCard;
                    eventMintor.AwakeOnGetCard(new EventMintor.GetCard(getCardTimePoint.entityCard, getCardTimePoint.fromLocation));
                    break;
                case 13:                                                    //有卡离开额外卡组时点:LeaveEx移动登记后广播,给"自己·对方的卡从额外卡组离开的场合"诱发(LeaveEx触发器)
                    var leaveExTimePoint = timePoint as StateMachine.LeaveEx;
                    eventMintor.AwakeOnLeaveExtra(new EventMintor.LeaveExtra(leaveExTimePoint.entityCard, leaveExTimePoint.cardLocation));
                    break;
                case 997:                                                   //魔法卡发动时点
                    var magicEffTimePoint = timePoint as StateMachine.MagicEffTimePoint;
                    eventMintor.AwakeOnMagicEff(new MagicEff(magicEffTimePoint.entityCard));
                    break;
                case 998:                                                   //陷阱卡发动时点
                    var trapEffTimePoint = timePoint as StateMachine.TrapEffTimePoint;
                    eventMintor.AwakeOnTrapEff(new TrapEff(trapEffTimePoint.entityCard));
                    break;
                case 999:                                                   //怪兽发动时点
                    var monsterEffTimePoint = timePoint as StateMachine.MonsterEffTimePoint;
                    eventMintor.AwakeOnMonsterEff(new MonsterEff(monsterEffTimePoint.entityCard));
                    break;
                default:
                    throw new Exception($"未知诱发时点id:{timePoint.id}");
            }
        }
    }
    public void ClearTimePoints()                               //否：清空时点
    {
        stateMachine.ClearTimePoints();
    }
    public List<StateMachine.TimePointBase> GetTimePointSnapshot()               //诱发窗口快照:复制当前全部时点,使"分发/清除"只针对本窗口,不误清新到窗口
    {
        return new List<StateMachine.TimePointBase>(timePointBases);
    }
    public void RemoveTimePoints(List<StateMachine.TimePointBase> points)        //仅移除指定窗口的时点(逐个按引用移除,不误清新窗口/结算期间新到时点)
    {
        if (points == null) return;
        for (int i = 0; i < points.Count; i++)
            timePointBases.Remove(points[i]);
    }
    public void ReAddTimePoints(List<StateMachine.TimePointBase> points)         //补回暂存时点(见诱发询问循环结算前的暂存逻辑;按引用去重追加)
    {
        if (points == null) return;
        for (int i = 0; i < points.Count; i++)
            if (points[i] != null && !timePointBases.Contains(points[i]))
                timePointBases.Add(points[i]);
    }
    public void AddSummonTimePoint(EffLogic.EntityCard entityCard)          //通召成功：登记Summon诱发时点(由通召入口在数据落场后调用)
    {
        if (entityCard == null) return;
        timePointBases.Add(new StateMachine.Summon(entityCard));
    }

    private EQ eQ;
    private void IniEQ()
    {
        eQ = new EQ(EntityCardToTransfrom, EntityCardToTransfrom);
        eQ.IniEQSpeSumEQ(requestHandler?.GetShowSelectionDialog(), requestHandler?.GetEntitySpeSom(), timePointBases, MapToRealCard);
        eQ.IniGoToCemeteryEQ(requestHandler?.GetShowSelectionDialog(), requestHandler?.GetGoToCemeteryUI(), timePointBases, MapToRealCard);
        eQ.IniGoToBanishedEQ(requestHandler?.GetShowSelectionDialog(), requestHandler?.GetGoToBanishedUI(), timePointBases, MapToRealCard);
        eQ.IniGoToHandEQ(requestHandler?.GetShowSelectionDialog(), requestHandler?.GetGoToHandUI(), timePointBases, MapToRealCard);
        eQ.IniGoToDeckEQ(requestHandler?.GetShowSelectionDialog(), requestHandler?.GetGoToDeckUI(), timePointBases, MapToRealCard);
        eQ.IniMagicActBaseEQ(null, requestHandler?.MagicActBase(), timePointBases, MapToRealCard);
        eQ.IniGetObjectEQ(requestHandler?.GetShowSelectionDialog(), requestHandler?.GetGoToCemeteryUI(), timePointBases, MapToRealCard);
        eQ.IniDrawCardEQ(requestHandler?.GetDrawCardHandleUI(), timePointBases);
    }
    #endregion

    //卡牌实体(真实/快照/弹窗副本均可用)→真实预制体:先在数据层按currentid归一为真实实体(MapToRealCard/FindEntityCardById),
    //再由UI层在持久区域容器中按真实实体引用反查其真实预制体(替代原IdTransfrom字典)
    public Transform FindRealCardTransform(EntityCard entityCard)
    {
        EntityCard real = MapToRealCard(entityCard);
        if (real == null) return null;
        return requestHandler?.FindCardRealTransform(real);
    }

    private Transform EntityCardToTransfrom(EntityCard entityCard)                             //单个卡牌实体转预制体(真实实体定位→真实预制体反查)
    {
        return FindRealCardTransform(entityCard);
    }

    private List<Transform> EntityCardToTransfrom(List<EntityCard> entityCards)
    {
        List<Transform> transforms = new List<Transform>();
        if (entityCards == null) return transforms;
        foreach (var entityCard in entityCards)
            transforms.Add(FindRealCardTransform(entityCard));
        return transforms;
    }

    public EntityCard MapToRealCard(EntityCard entityCard)                     //弹窗/快照实体映射回真实实体
    {
        if (entityCard == null) return entityCard;
        EntityCard real = FindEntityCardById(entityCard.currentid);
        return real ?? entityCard;
    }

    public EntityCard FindEntityCardById(string currentid)                     //按currentid在自己与对方所有区域中查找真实实体
    {
        if (string.IsNullOrEmpty(currentid)) return null;
        List<List<EntityCard>> zones = new List<List<EntityCard>>()
        {
            player1Deck?.cards, player1ExtraDeck?.cards, player1Hand?.cards,
            player1Cemetery?.cards, player1Banished?.cards, player1Field?.cards,
            enemyDeck?.cards, enemyExtraDeck?.cards, enemyHand?.cards,
            enemyCemetery?.cards, enemyBanished?.cards, enemyField?.cards
        };
        foreach (var zone in zones)
        {
            if (zone == null) continue;
            foreach (var card in zone)
            {
                if (card != null && card.currentid == currentid)
                    return card;
            }
        }
        return null;
    }

   
    public void EntityEff(EntityCard entityCard, PreEntityCard preEntityCard)                                                               //效果的实体化
    {
        CardBase.Card card = entityCard.card;
        for (int i = 0; i < card.EffNum; i++)
        {
            StaticEff staticEff = card.staticEffs[i];
            EffLogic.JudCost judCost = null;
            EffLogic.CostPay costPay = null;
            EffLogic.Eff eff = null;
            EffLogic.Effection effection = new EffLogic.Effection(i, entityCard,entityPlayer1.FindEffNum,entityPlayer1.AddNum, eventMintor, timePointBases);   //id=卡上技能从左到右的序号(0基),对应CSV效果列读写顺序
            for (int j = 0; j < staticEff.specificCostClass.Count; j++)
            {
                //组件参数串固定尾两&段(GC|OC),装配前剥掉,剩余为功能参数原文(与旧格式一致)
                SplitContinueTail(staticEff.costParameter[j], out string funcCostPar, out List<string> gcCost, out List<string> ocCost);
                judCost = Factory.EntityCost(entityCard, staticEff.specificCostClass[j], funcCostPar,
                                             HandleEntityPlayer, HandleFindComponentRequest,
                                             GetGamePhase, GetOnwerPhase, () => enemy, GetCurEffOrder, eventMintor);
                if (judCost is EffLogic.Cost costGate)
                {
                    costGate.SetContinueGates(gcCost, ocCost);
                    costGate.SetContinueRegistry(continues);
                }
                effection.AddCost(judCost, staticEff.costParameter[j]);   //携带cost判断参数原文(含永续尾段,仅用于失败日志),供Effection.EffCost失败时输出"哪个判断失败+参数"
            }
            for (int j = 0; j < staticEff.specificCostPay.Count; j++)
            {
                costPay = Factory.EntityPay(entityCard, staticEff.specificCostPay[j], staticEff.costPayParameter[j], HandleEntityPlayer, HandleFindComponentRequest, eQ);
                effection.AddPay(costPay);
            }
            for (int j = 0; j < staticEff.specificEffectClass.Count; j++)
            {
                //组件参数串固定尾两&段(GC|OC),装配前剥掉,剩余为功能参数原文(与旧格式一致)
                SplitContinueTail(staticEff.effectParameter[j], out string funcEffPar, out List<string> gcEff, out List<string> ocEff);
                eff = Factory.EntityEff(staticEff.specificEffectClass[j], funcEffPar, entityCard, HandleEntityPlayer, HandleFindComponentRequest, () => enemy, eQ);
                if (eff is EffLogic.CardComponent effGate)
                {
                    effGate.SetContinueGates(gcEff, ocEff);
                    effGate.SetContinueRegistry(continues);
                }
                effection.AddEff(eff);
            }
            List<string> basePar = staticEff.baseEffParameter;
            int layoutOffset = (basePar != null && basePar.Count >= 15) ? 2 : 0;
            string Par(int newIdx)  
            {
                if (basePar == null) return "";
                if (layoutOffset == 0)
                {
                    int oldIdx = newIdx - 2;
                    if (newIdx < 2 || oldIdx >= basePar.Count) return "";
                    return basePar[oldIdx];
                }
                return newIdx < basePar.Count ? basePar[newIdx] : "";
            }
            int ParInt(int newIdx, int def = 0)
            {
                string v = Par(newIdx);
                return string.IsNullOrWhiteSpace(v) ? def : (int.TryParse(v.Trim(), out int r) ? r : def);
            }
            int isGetObject = ParInt(0, 0);                       //是否取对象
            int orderStackState = ParInt(1, -1);                  //进栈状态(连锁限制),缺省-1正常问询
            List<string> effType = string.IsNullOrWhiteSpace(Par(7)) ? new List<string>() : Par(7).Split('|').ToList();
            List<string> timePoint = string.IsNullOrWhiteSpace(Par(8)) ? new List<string>() : Par(8).Split('|').ToList();
            List<string> payLimition = string.IsNullOrWhiteSpace(Par(10)) ? new List<string>() : Par(10).Split('|').ToList();
            List<int> payLimTimeLength = null;   //无固有自肃时跳过时长段读取
            if (payLimition.Count > 0)
            {
                payLimTimeLength = Par(11).Split('|').Select(int.Parse).ToList();
                if (payLimTimeLength.Count != payLimition.Count)
                    throw new FormatException($"固有自肃(payLimition)与时长数量不一一对应:{payLimition.Count}条 vs {payLimTimeLength.Count}条");
            }
            List<string> effLimition = string.IsNullOrWhiteSpace(Par(12)) ? new List<string>() : Par(12).Split('|').ToList();
            List<int> effLimTimeLength = null;   //无发动后自肃时跳过时长段读取
            if (effLimition.Count > 0)
            {
                effLimTimeLength = Par(13).Split('|').Select(int.Parse).ToList();
                if (effLimTimeLength.Count != effLimition.Count)
                    throw new FormatException($"发动后自肃(effLimition)与时长数量不一一对应:{effLimition.Count}条 vs {effLimTimeLength.Count}条");
            }
            List<string> avoidLimition = string.IsNullOrWhiteSpace(Par(14)) ? new List<string>() : Par(14).Split('|').ToList();
            int isLinkEff = ParInt(15, 0);                      
            List<int> isLinkEffID = null;                        
            if (!string.IsNullOrWhiteSpace(Par(16)))
                isLinkEffID = Par(16).Split('|').Select(s => int.Parse(s.Trim())).ToList();
            int isHaveConEff = ParInt(17, 0);                  //是否有永续效果:效果基础参数&串倒数第2段(0无/1有)
            string conEffPar = Par(18);                        //永续效果参数:效果基础参数&串最后1段(如"…&数值…"规则串),无则空
            effection.AddBasePar(isGetObject, orderStackState, ParInt(2), ParInt(3), ParInt(4), ParInt(5), ParInt(6),
                                            effType, timePoint, ParInt(9, 1), payLimition, payLimTimeLength,
                                            effLimition, effLimTimeLength, avoidLimition,
                                            isLinkEff, isLinkEffID, isHaveConEff, conEffPar);
            //魔法卡发动cost通用化:魔法卡的主动效果自动带上"魔法卡发动条件"(IsCanMagic),无需CSV逐条声明;
            //   CSV已显式声明时以CSV为准(其参数可挂GC/OC永续尾段);诱发效果不接管(诱发由时点触发,并非"发动这张卡")。
            if (entityCard is EffLogic.EntityMagicCard && effection.isTrigger != 1
                && !staticEff.specificCostClass.Contains("IsCanMagic"))
            {
                EffLogic.JudCost magicCost = Factory.EntityCost(entityCard, "IsCanMagic", "",
                                             HandleEntityPlayer, HandleFindComponentRequest,
                                             GetGamePhase, GetOnwerPhase, () => enemy, GetCurEffOrder, eventMintor);
                effection.AddCost(magicCost, "IsCanMagic:");      //自动注入:无参数
            }
            preEntityCard.AddEffEction(effection);
        }
    }

    //组件参数串固定尾两&段:倒数第2段=GlobalContinues(GC),倒数第1段=OperationContinues(OC);段内|分隔永续编号,空段=无。
    //装配时剥掉尾两段,剩余为功能参数原文(与未补尾的旧格式一致)。
    private static void SplitContinueTail(string raw, out string funcPar, out List<string> gc, out List<string> oc)
    {
        gc = new List<string>();
        oc = new List<string>();
        if (string.IsNullOrEmpty(raw))
        {
            funcPar = "";
            return;
        }
        string[] segs = raw.Split('&');
        if (segs.Length < 3)
        {
            funcPar = raw;   //不足"功能参数+两空段"三段的视为未补永续尾段:整串按功能参数处理
            return;
        }
        oc = SplitContinueIds(segs[segs.Length - 1]);
        gc = SplitContinueIds(segs[segs.Length - 2]);
        funcPar = string.Join("&", segs, 0, segs.Length - 2);
    }
    private static List<string> SplitContinueIds(string seg)
    {
        List<string> res = new List<string>();
        if (string.IsNullOrWhiteSpace(seg)) return res;
        foreach (string part in seg.Split('|'))
            if (!string.IsNullOrWhiteSpace(part)) res.Add(part.Trim());
        return res;
    }

    #region

    public GameManage.GamePhase GetCurGamePhase()
    {
        return stateMachine.GetCurBout();
    }
    #endregion


    #region 永续效果
    /*
     
    public enum Continue
    {
        OnlyOne_InField = 0              //场上只能存在一张
    }

    */
    public List<Continue> continues {  get; private set; } = new List<Continue>();          //加入场上的效果(全局登记处):仅登记"对双方都生效"的永续,结算前其他效果经GC(GlobalContinues)在此查询
    public void AddContinues(Continue _continue)
    {
        if (_continue == null) return;
        if (!continues.Contains(_continue))
            continues.Add(_continue);
    }
    public bool RemoveContinues(Continue _continue)     //永续不再适用时移出登记处(对双方生效期间由持有方负责登记/注销)
    {
        if (_continue == null) return false;
        return continues.Remove(_continue);
    }
    public bool CheckContinues(List<string> ids)                        //查询全局登记处:任一编号对应永续存在且生效(IsHaveEff()==true)→true
    {
        if (ids == null || ids.Count == 0 || continues == null || continues.Count == 0)
            return false;
        foreach (string id in ids)
            foreach (Continue con in continues)
                if (con != null && con.id == id && con.IsHaveEff())
                    return true;
        return false;
    }
    //内置永续编号"0"(OnlyOne_InField):同名卡在自己场上只能表侧存在1张。候选卡自身携带该永续且生效时,
    //在召唤/融合/取为被操作对象等候选池中会被自动拦截(见EffLogic基类IsCardOperationBlocked),OC尾段无需重复声明。
    public const string OnlyOneContinueId = "0";

    //同名表侧永续"静态判定+场上生效判定":不依赖实例继续(实例继续须经PreEntityCard.Initialize实体化后才生成,
    //额外卡组/墓地等隐藏区的候选在cost结算前可能尚未实体化,故以此静态声明兜底,保证"场上已表侧同名→候选被剔除"对任何来源候选都成立)
    public static bool IsOnlyOneRestricted(EntityCard entityCard)
    {
        if (entityCard == null || entityCard.card == null || entityCard.card.staticEffs == null) return false;
        bool declared = false;
        foreach (StaticEff se in entityCard.card.staticEffs)
        {
            List<string> bp = se?.baseEffParameter;
            if (bp == null || bp.Count == 0) continue;
            int layoutOffset = bp.Count >= 15 ? 2 : 0;                                    //与EffLogic基础参数布局一致
            string Par(int idx) => layoutOffset == 2
                ? (idx < bp.Count ? bp[idx] : "")
                : (idx < 2 || idx - 2 >= bp.Count ? "" : bp[idx - 2]);
            if (Par(17) == "1" && (Par(18) ?? "").Split(':')[0] == OnlyOneContinueId)     //isHaveConEff=1 且 conEffPar 首段为 OnlyOne 编号
            {
                declared = true;
                break;
            }
        }
        if (!declared) return false;
        EntityPlayer owner = entityCard.GetCurrentOwner();
        if (owner?.field == null) return false;
        foreach (EntityCard m in owner.field.GetMonZon())
        {
            if (m == null || m == entityCard || m.card == null) continue;
            if (m.isCover != 0) continue;                                                 //仅表侧表示计入"只能有1张表侧表示存在"
            if (m.card.name == entityCard.card.name)
                return true;
        }
        return false;
    }
    public class Continue
    {
        public string id {  get;private set; }
        public EntityCard entityCard { get; set; }
        public Continue(string id,EntityCard entityCard)
        {
            this.id = id;
            this.entityCard = entityCard;
        }
        public virtual bool IsHaveEff()        //是否生效
        {
            return false;
        }
    }
    public class OnlyOne_InField
        : Continue
    {
        
        public OnlyOne_InField(EntityCard EntityCard) : base(OnlyOneContinueId, EntityCard)
        {
        }

        public override bool IsHaveEff()
        {
            //生效判定:当前控制者场上已表侧存在"另一张同卡名的卡"时,本卡(含其余同名复制)的表侧出场受限。
            //用于拦截"同名已在场上表侧→不能通过融合/特招等手段再使第2张表侧出现"(候选池自动剔除,见EffLogic基类IsCardOperationBlocked内置拦截)。
            if (entityCard == null || entityCard.card == null) return false;
            EntityPlayer owner = entityCard.GetCurrentOwner();
            if (owner?.field == null) return false;
            foreach (EntityCard m in owner.field.GetMonZon())
            {
                if (m == null || m == entityCard || m.card == null) continue;
                if (m.isCover != 0) continue;                               //仅表侧表示计入"只能有1张表侧表示存在"
                if (m.card.name == entityCard.card.name)
                    return true;
            }
            return false;
        }
    }
    #endregion
}

