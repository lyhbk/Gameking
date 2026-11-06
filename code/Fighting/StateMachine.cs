using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


public class StateMachine
{

    private GameManage.GamePhase curGamePhase;
    private int stateOwner;
    private int BoutSum ;
    private IState CurIState;

    public event Action<GameManage.GamePhase> OnGamePhaseChanged;          //阶段变化事件（UI订阅，实现动态统一显示）

    private IniQuestPlayer iniQuestPlayer;
    private StateMachineEventAction stateMachineEventAction;     // 外部传入的需要调用的函数方法
    #region
    /*
    public enum TimePoint 
    {
        DrawCard,                   //抽卡时点        0
        InCurPhase,                 //进入当前阶段    1
        ExCurPhase,                 //退出当前阶段    2
        AttackDeclare,              //攻击宣言        3
        HurtJudge,                  //伤害判定        4
        InCemetery,                 //卡进入墓地      7

        InBanished,                 //卡被除外        6
        
        Summon,                     //有卡通召        9
        SpeSom,                     //有卡特招        10
        FusionSummon,               //融合召唤        11
        GetCard,                    //从卡组加入手卡  12
        LeaveEx;                    //离开额外        13
        DestroyCard,                //卡被破坏        60
        MagicEff,                   //魔法卡发动效果  997
        TrapEff,                    //陷阱卡发动效果  998
        MonsterEff,                 //怪兽发动效果    999
        
    }
    
    public enum EffType
    {
        DrawCard,                   //抽卡
        SpeSom,                     //特招
    }
     
    */
    public enum CurrentPoint
    {
        Freedom,                  //自由
        Target,                   //诱发
        Attack,                   //战斗
        Chain                     //连锁
    }
    public CurrentPoint currentPoint {  get; private set; }
    public event Action<CurrentPoint> OnCurrentPointChanged;               //时点变化事件（UI订阅，实现动态统一显示）
    public void SetCurrentPoint(CurrentPoint point)
    {
        currentPoint = point;
        OnCurrentPointChanged?.Invoke(point);
    }

    public class TimePointBase
    {
        public int id { get; private set; }                    //按照类型编号
        public TimePointBase(int id)
        {
            this.id = id;
        }
    }
    public class DrawCard                                       //抽卡
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard {  get; private set; }
        public DrawCard(EffLogic.EntityCard entityCard):base(0)
        {
            this.entityCard = entityCard;
        }
    }
    public class InCemetery                                     //有卡进入墓地
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public GameManage.CardLocation fromLocation;            //从哪里进入墓地
        public InCemetery(EffLogic.EntityCard entityCard, GameManage.CardLocation fromLocation) : base(7)
        {
            this.entityCard = entityCard;
            this.fromLocation = fromLocation;
        }
    }
    public class InBanished                                     //有卡被除外(进入除外区)
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public GameManage.CardLocation fromLocation;            //从哪里被除外
        public InBanished(EffLogic.EntityCard entityCard, GameManage.CardLocation fromLocation) : base(6)
        {
            this.entityCard = entityCard;
            this.fromLocation = fromLocation;
        }
    }
    public class SpeSomTimePoint                                //特招时点
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public SpeSomTimePoint(EffLogic.EntityCard entityCard) : base(10)
        {
            this.entityCard = entityCard;
        }
    }
    public class Summon                                         //通常召唤(通召)成功
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public Summon(EffLogic.EntityCard entityCard) : base(9)
        {
            this.entityCard = entityCard;
        }
    }
    public class FusionSummon                                   //融合召唤
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public FusionSummon(EffLogic.EntityCard entityCard) : base(11)
        {
            this.entityCard = entityCard;
        }
    }
    public class DestroyCard                                    //有卡被破坏
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public DestroyCard(EffLogic.EntityCard entityCard) :base(60)
        {
            this.entityCard = entityCard;
        }
    }
    public class GetCard                                        //有卡从卡组加入手卡(检索/加入手卡)时点
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public GameManage.CardLocation fromLocation { get; private set; }    //从哪里加入手卡(检索来源=卡组)
        public GetCard(EffLogic.EntityCard entityCard, GameManage.CardLocation fromLocation) : base(12)
        {
            this.entityCard = entityCard;
            this.fromLocation = fromLocation;
        }
    }
    public class LeaveEx                                         //有卡离开额外卡组时点
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }          //离开者
        public GameManage.CardLocation cardLocation { get; private set; }    //离开后去向(墓地/除外区/场上等)
        public LeaveEx(EffLogic.EntityCard entityCard, GameManage.CardLocation cardLocation) : base(13)
        {
            this.entityCard = entityCard;
            this.cardLocation = cardLocation;
        }
    }
    public class MagicEffTimePoint                              //魔法卡发动效果时点 997
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public MagicEffTimePoint(EffLogic.EntityCard entityCard) : base(997)
        {
            this.entityCard = entityCard;
        }
    }
    public class TrapEffTimePoint                               //陷阱卡发动效果时点 998
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public TrapEffTimePoint(EffLogic.EntityCard entityCard) : base(998)
        {
            this.entityCard = entityCard;
        }
    }
    public class MonsterEffTimePoint                             //怪兽发动效果时点 999
        : TimePointBase
    {
        public EffLogic.EntityCard entityCard { get; private set; }
        public MonsterEffTimePoint(EffLogic.EntityCard entityCard) : base(999)
        {
            this.entityCard = entityCard;
        }
    }
    #endregion
    public List<TimePointBase> timePointBases {  get; private set; }
    public void AddTimePoints(TimePointBase timePoint)
    {
        timePointBases.Add(timePoint);
    }
    public bool FindCurPoints(TimePointBase timePointBase)
    {
        return timePointBases.Contains(timePointBase);
    }
    public void ClearTimePoints()
    {
        timePointBases.Clear();
    }

    public Stack<EffOrder> orderStack { get; private set; }           //连锁命令栈
    public readonly RegionAccessGate settlementGate = new RegionAccessGate();   //结算临界区:连锁链接按FIFO排队进入,替代固定延时兜底
    public void PushOrderStack(EffOrder effOrder)                     //把效果命令压入连锁栈
    {
        effOrder.RefreshOrderId(orderStack.Count + 1);                //连锁编号：后发动的编号更大，结算时先出
        orderStack.Push(effOrder);
    }
    public async UniTask HandleOrderStack()                           //结算（逆序：后发动的先结算，异步：等待每个效果执行完毕）
    {
        ClearTimePoints();
        while (orderStack != null && orderStack.Count > 0)
        {
            EffOrder effOrder = orderStack.Pop();
            //结算临界区:每条链接以await排队进入/结算完释放(FIFO)。诱发循环等多条异步流程同时走结算时,
            //链接互不穿插——上一链接(如魔法②取对象特招墓地圣女)跨await写场期间,下一链接(如落胤②收集手·卡组·墓地候选)
            //只会等上一链接真正释放临界区后才读场,不会读到"已决定特招但尚未提交"的中间态;无竞争时零开销。
            using (await settlementGate.EnterAsync(FightAsyncScope.Token))     //对局结束:等待者以取消态退出,不再进入结算
            {
                await effOrder.HandleEff();
                //连锁节奏:本条链接结算完毕后,若连锁栈还有下一条,仍持临界区缓冲0.8s再放行下一条——
                //给结算产生的诱发/点蓝格特招等异步流程留出提交窗口,防止下一条效果在上一链接"已决定特招但尚未提交"的中间态读场,产生并发/漂移。
                if (orderStack != null && orderStack.Count > 0)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(0.8f), cancellationToken: FightAsyncScope.Token);
            }
        }
        orderStack.Clear();
    }
    public bool IsTimePointEmpty()                                    //当前时点是否为空
    {
        return timePointBases == null || timePointBases.Count == 0;
    }
    public int GetTopOrderSpeed()                                     //连锁栈栈顶效果速度（栈空返回0）
    {
        if (orderStack == null || orderStack.Count == 0) return 0;
        return orderStack.Peek().speed;
    }
    public int GetTopOrderStackState()                                //连锁栈栈顶效果进栈状态(连锁限制;-1正常/0禁怪兽/1禁魔法/2禁陷阱/10禁怪兽魔法/20禁怪兽陷阱/21禁魔法陷阱/3不可被连锁直接结算;栈空返回-1)
    {
        if (orderStack == null || orderStack.Count == 0) return -1;
        return orderStack.Peek().orderStackState;
    }
    public bool IsOrderStackEmpty()                                   //连锁栈是否为空
    {
        return orderStack == null || orderStack.Count == 0;
    }
    public EffOrder GetTopEffOrder()                                  //连锁栈栈顶效果（栈空返回null,不影响栈）
    {
        if (orderStack == null || orderStack.Count == 0) return null;
        return orderStack.Peek();
    }

    public class EffOrder                                       //效果的命令形式
    {
        public int id { get; private set; }                     //在栈中位置
        public string playerID {  get; private set; }           //发动玩家ID
        public int currentCardType {  get; private set; }       //卡牌类型
        public string[] effOrderType {  get; private set; }     //命令类型
        public bool isInvalid { get; private set; }             //是否被无效
        public Func<UniTask> eff {  get; private set; }         //效果函数引用（异步：结算时等待执行完毕）
        public int speed { get; private set; }                  //效果速度：连锁中发动时不得低于栈顶效果速度
        public int orderStackState { get; private set; }        //进栈状态(连锁限制):-1正常问询/0禁怪兽连锁/1禁魔法/2禁陷阱/10禁怪兽魔法/20禁怪兽陷阱/21禁魔法陷阱/3不可被连锁直接结算
        public EffOrder(int id, string playerID, int currentCardType, string[] effOrderType, Func<UniTask> eff, int speed, int orderStackState = -1)
        {
            this.id = id;
            this.playerID = playerID;
            this.currentCardType = currentCardType;
            this.effOrderType = effOrderType;
            this.eff = eff;
            this.isInvalid = false;
            this.speed = speed;
            this.orderStackState = orderStackState;
        }
        public void InvalidFrontEff()
        {
            this.isInvalid = true;
        }
        public void RefreshOrderId(int id)
        {
            this.id = id;
        }
        public async UniTask HandleEff()
        {
            if (!isInvalid && eff != null)
                await eff.Invoke();
        }
    }

    public sealed class RegionAccessGate                                        //结算临界区:异步FIFO公平排队(Unity单线程协作式:不阻塞,等待方挂起直到轮到自己)
    {
        private readonly Queue<UniTaskCompletionSource> waiters = new Queue<UniTaskCompletionSource>();
        private bool inUse;

        public struct Scope : IDisposable                                      //using作用域:离开自动Release,异常也不泄漏
        {
            private readonly RegionAccessGate gate;
            public Scope(RegionAccessGate gate) { this.gate = gate; }
            public void Dispose() => gate?.Release();
        }

        public async UniTask<Scope> EnterAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)                //对局已结束:即使临界区空闲也不放行新结算
                throw new OperationCanceledException(cancellationToken);
            if (inUse)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                UniTaskCompletionSource tcs = new UniTaskCompletionSource();
                waiters.Enqueue(tcs);
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))    //对局结束:唤醒本排队等待者,使其以取消态退出
                {
                    await tcs.Task;                                               //临界区被占用:挂起排队,轮到自己时由Release唤醒
                }
            }
            inUse = true;
            return new Scope(this);
        }
        public void Release()
        {
            if (waiters.Count > 0)
                waiters.Dequeue().TrySetResult();                             //唤醒下一等待者(FIFO),inUse保持true由其续持
            else
                inUse = false;
        }
    }

    public class StateMachineEventAction
    {
        public Action<int,int> DrawPhaseDrawCard;                    //抽卡阶段自动抽卡
        public Action<int> BoutEnd;                                  //回合结束委托（参数：刚结束回合的玩家ID）

        public void AddDrawPhaseDrawCard(Action<int,int> DrawPhaseDrawCard)
        {
            this.DrawPhaseDrawCard = DrawPhaseDrawCard;
        }
        public void AddBoutEnd(Action<int> BoutEnd)
        {
            this.BoutEnd = BoutEnd;
        }
    }

    public StateMachine(IniQuestPlayer iniQuestPlayer, StateMachineEventAction stateMachineEventAction, List<TimePointBase> timePointBases)
    {
        this.iniQuestPlayer = iniQuestPlayer;
        this.iniQuestPlayer.SetGlobalChangePhaseCallback(GiveIsCanChangePhase);

        curGamePhase = GameManage.GamePhase.DrawCardPhase;
        stateOwner = 0;
        BoutSum = 1;
        currentPoint = CurrentPoint.Freedom;                    //最初为自由时点
        this.stateMachineEventAction = stateMachineEventAction;
        CurIState = new DrawCardState(stateOwner,iniQuestPlayer, this.stateMachineEventAction.DrawPhaseDrawCard);

        this.timePointBases = timePointBases;
        orderStack = new Stack<EffOrder>();
    }

    

    public GameManage.GamePhase GetCurBout()
    {
        return curGamePhase;
    }
    public int GetCurOwner()
    {
        return stateOwner;
    }
    public async UniTask ChangeState()
    {
        await CurIState.OnExit();
        if (!IsCanChangePhase)
            return;
        switch (curGamePhase)
        {
            case GameManage.GamePhase.DrawCardPhase:
                CurIState = new PrepareState(iniQuestPlayer);
                curGamePhase = GameManage.GamePhase.PreparePhase;
                break;
            case GameManage.GamePhase.PreparePhase:
                CurIState = new Main1State(iniQuestPlayer);
                curGamePhase = GameManage.GamePhase.Main1Phase;
                break;
            case GameManage.GamePhase.Main1Phase:
                if (BoutSum == 1)
                {
                    CurIState = new EndState(iniQuestPlayer);
                    curGamePhase = GameManage.GamePhase.EndPhase;
                }
                else
                {
                    CurIState = new FightState(iniQuestPlayer);
                    curGamePhase = GameManage.GamePhase.FightPhase;
                }
                break;
            case GameManage.GamePhase.FightPhase:
                CurIState = new Main2State(iniQuestPlayer);
                curGamePhase = GameManage.GamePhase.Main2Phase;
                break;
            case GameManage.GamePhase.Main2Phase:
                CurIState = new EndState(iniQuestPlayer);
                curGamePhase = GameManage.GamePhase.EndPhase;
                break;
            case GameManage.GamePhase.EndPhase:
                int boutEndOwner = stateOwner;                          //回合委托参数需为"刚结束回合的玩家ID",须在回合主翻转前取值
                stateOwner = -1 * stateOwner + 1;
                curGamePhase = GameManage.GamePhase.DrawCardPhase;
                BoutSum++;
                CurIState = new DrawCardState(stateOwner,iniQuestPlayer, stateMachineEventAction.DrawPhaseDrawCard);
                stateMachineEventAction.BoutEnd?.Invoke(boutEndOwner);  //回合结束自动调用：按委托约定传入刚结束回合的玩家,使该玩家自身的通召次数/发动次数/盖放回合数(coverTime)/自肃/区域停留回合数在本次回合界限结算
                break;
        }
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.8f), cancellationToken: FightAsyncScope.Token);   //对局结束:取消该延时,阶段切换链原地终止
        OnGamePhaseChanged?.Invoke(curGamePhase);           //阶段已切换，通知订阅者（UI实时刷新）
        await CurIState.OnEnter();                          //立即进入阶段逻辑（去掉0.8s定时等待，阶段推进由玩家确认点击驱动）
    }

    private bool IsCanChangePhase = true;
    public void GiveIsCanChangePhase(bool IsCanChangePhase)
    {
        this.IsCanChangePhase = IsCanChangePhase;
    }

    private UniTaskCompletionSource waitForQuestPlayer;
    private bool WaitForQuestPlayerLock = false;

    public interface IState
    {
        public UniTask OnEnter();  // 进入该状态时触发
        public UniTask OnExit();   // 离开该状态时触发
        public void OnUpdate(); // 状态持续期间每帧触发
    }

    public class IniQuestPlayer
    {
        public Action<bool> GiveIsCanChangePhase { get; private set; }    

        public Action<string> GetQuestReback { get; private set; }                            
        public Func<UniTask<bool>> QuestWaitEffUI { get; private set; }
        public void SetGlobalChangePhaseCallback(Action<bool> GiveIsCanChangePhase)
        {
            this.GiveIsCanChangePhase = GiveIsCanChangePhase;
        }
        public IniQuestPlayer(Action<string> GetQuestReback, Func<UniTask<bool>> QuestWaitEffUI)
        {
            this.GetQuestReback = GetQuestReback;
            this.QuestWaitEffUI = QuestWaitEffUI;
        }
    }


    public abstract class QuestPlayer
    {
        public Action<bool> GiveIsCanChangePhase {  get; private set; }                  //返回是否可以切换回合
        public Action<string> GetQuestReback {  get; private set; }                             //UI反馈
        public Func<UniTask<bool>> QuestWaitEffUI {  get; private set; }                        //等待玩家选择

        protected string startPhase { get; set; }
        protected string endPhase { get; set; }

        public QuestPlayer(IniQuestPlayer iniQuestPlayer)
        {
            this.GiveIsCanChangePhase = iniQuestPlayer.GiveIsCanChangePhase;
            this.GetQuestReback = iniQuestPlayer.GetQuestReback;
            this.QuestWaitEffUI = iniQuestPlayer.QuestWaitEffUI;
        }
        public async UniTask QuestStartPhaseIsEff()
        {
            GetQuestReback?.Invoke(startPhase);
            bool IsCanChangePhase = await QuestWaitEffUI.Invoke();
            GiveIsCanChangePhase?.Invoke(IsCanChangePhase);
        }
        public async UniTask QuestEndPhaseIsEff()
        {
            GetQuestReback?.Invoke(endPhase);
            bool IsCanChangePhase = await QuestWaitEffUI.Invoke();
            GiveIsCanChangePhase?.Invoke(IsCanChangePhase);
        }
    }

    public class DrawCardState : 
        QuestPlayer,IState
    {
        private Action<int,int> ActionDrawCard;
        private int stateOwner;
        public DrawCardState(int stateOwner,IniQuestPlayer iniQuestPlayer,Action<int,int> ActionDrawCard) : base(iniQuestPlayer) 
        {
            this.stateOwner = stateOwner;
            this.ActionDrawCard = ActionDrawCard;
            startPhase = "进入抽卡阶段，是否发动效果";
            endPhase = "退出抽卡阶段，是否发动效果";
        }
        public async UniTask OnEnter()
        {
            ActionDrawCard?.Invoke(stateOwner,1);
            await QuestStartPhaseIsEff();
        }
        public async UniTask OnExit()
        {
            await QuestEndPhaseIsEff();
        }
        public void OnUpdate()
        {
            return;
        }
    }

    public class PrepareState : 
        QuestPlayer,IState
    {
        public PrepareState(IniQuestPlayer iniQuestPlayer) : base(iniQuestPlayer)
        {
            startPhase = "进入准备阶段，是否发动效果";
            endPhase = "退出准备阶段，是否发动效果";
        }

        public async UniTask OnEnter()
        {
            await QuestStartPhaseIsEff();
        }
        public async UniTask OnExit()
        {
            await QuestEndPhaseIsEff();
        }
        public void OnUpdate()
        {
            return;
        }
    }

    public class Main1State : 
        QuestPlayer,IState
    {
        public Main1State(IniQuestPlayer iniQuestPlayer) : base(iniQuestPlayer)
        {
            startPhase = "进入主要阶段，是否发动效果";
            endPhase = "退出主要阶段，是否发动效果";
        }
        public async UniTask OnEnter()
        {
            await QuestStartPhaseIsEff();
        }
        public async UniTask OnExit()
        {
            await QuestEndPhaseIsEff();
        }
        public void OnUpdate()
        {
            // 主阶段1的持续逻辑
        }
    }

    public class FightState : 
        QuestPlayer,IState
    {
        public FightState(IniQuestPlayer iniQuestPlayer) : base(iniQuestPlayer)
        {
            startPhase = "进入战斗阶段，是否发动效果";
            endPhase = "退出战斗阶段，是否发动效果";
        }
        public async UniTask OnEnter()
        {
            await QuestStartPhaseIsEff();
        }
        public async UniTask OnExit()
        {
            await QuestEndPhaseIsEff();
        }
        public void OnUpdate()
        {
            // 战斗阶段的持续逻辑
        }
    }

    public class Main2State : 
        QuestPlayer,IState
    {
        public Main2State(IniQuestPlayer iniQuestPlayer) : base(iniQuestPlayer)
        {
            startPhase = "进入主要阶段，是否发动效果";
            endPhase = "退出主要阶段，是否发动效果";
        }
        public async UniTask OnEnter()
        {
            await QuestStartPhaseIsEff();
        }
        public async UniTask OnExit()
        {
            await QuestEndPhaseIsEff();
        }
        public void OnUpdate()
        {
            // 主阶段2的持续逻辑
        }
    }

    public class EndState : 
        QuestPlayer,IState
    {
        public EndState(IniQuestPlayer iniQuestPlayer) : base(iniQuestPlayer)
        {
            startPhase = "进入结束阶段，是否发动效果";
            endPhase = "退出结束阶段，是否发动效果";
        }
        public async UniTask OnEnter()
        {
            await QuestStartPhaseIsEff();
        }
        public async UniTask OnExit()
        {
            await QuestEndPhaseIsEff();
        }
        public void OnUpdate()
        {
            // 结束阶段的持续逻辑
        }
    }

}









