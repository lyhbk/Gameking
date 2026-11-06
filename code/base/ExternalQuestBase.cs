using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;



public class ExternalQuestBase 
{
    public class UILinkLog                          //FightUI与FightLogic交互
    {
        #region UI与Logic部分
        public class ChangeHPRequest                 //修改血量
        {
            public int playerId { get; }
            public int newHP { get; }

            public ChangeHPRequest(int playerId, int newHP)
            {
                this.playerId = playerId;
                this.newHP = newHP;
            }
        }

        public class ChangeHPToRequest              //修改血量到
        {
            public int playerId { get; }
            public int newHP { get; }
            public ChangeHPToRequest(int playerId, int newHP)
            {
                this.playerId = playerId;
                this.newHP = newHP;
            }
        }

        public class ChangeBout                    //切换回合
        {

        }

        public class WaitCurChangeBout
        {
            public bool isCanChangeBout {  get; }
            public WaitCurChangeBout(bool isCanChangeBout)
            {
                this.isCanChangeBout= isCanChangeBout;
            }
        } 

        public class SumOrCovBaseRequest
        {
            public Transform transform;
            public SumOrCovBaseRequest(Transform transform)
            {
                this.transform = transform;
            }
        }
        public class LayOutMagicTrapRequest
        {
            public Transform transform;
            public LayOutMagicTrapRequest(Transform transform)
            {
                this.transform = transform;
            }
        }
        public class RegionRequest              //区域卡牌改变
        {
            public EffLogic.EntityCard entityCard;
            public int regionNum;
            public bool isAdd;
            public RegionRequest(EffLogic.EntityCard entityCard, int regionNum, bool isAdd)
            {
                this.entityCard = entityCard;
                this.regionNum = regionNum;
                this.isAdd = isAdd;
            }

        }

        public class ReflashAllUI               //强制刷新UI布局
        {

        }

        public class EntityEffRequest
        {
            public EffLogic.EntityCard entityCard;
            public PreEntityCard preEntityCard;
            public EntityEffRequest(EffLogic.EntityCard entityCard, PreEntityCard preEntityCard)
            {
                this.entityCard= entityCard;
                this.preEntityCard= preEntityCard;
            }
        }

        public class DrawCardRequest                                //抽卡
        {
            public int playerID;
            public int num;
            public DrawCardRequest(int playerID, int num)
            {
                this.playerID = playerID;
                this.num = num;
            }
        }

        public class AbandonCard                                    //丢卡，专指回合结束的处理
        {
            public int num;
            public GameManage.CardLocation cardLocation;
            public AbandonCard(int num, GameManage.CardLocation cardLocation = GameManage.CardLocation.Cemetery) 
            { 
                this.num = num; 
                this.cardLocation = cardLocation;
            }
        }
        #endregion

        public interface UILinkLogicIn             //UI调用事件外部接口
        {
            public void ChangeHPRequestHandle(ChangeHPRequest changeHPRequest);
            public void ChangeHPToRequestHandle(ChangeHPToRequest changeHPToRequest);
            public UniTask ChangeBoutToRequestHandle(ChangeBout changeBout);
            public UniTask WaitCurChangeBout(WaitCurChangeBout waitCurChangeBout);
            public void RegionRequestHandle(RegionRequest leaveHandRequest);
           
            public void DrawCardHandle(DrawCardRequest drawCardRequest);
            public UniTask AbandonCardHandle(AbandonCard abandonCard);

            // ===== 回合信息/战斗查询（FightUI经requestHandler间接访问FightLogic，不再直接持有FightLogic） =====
            public GameManage.GamePhase GetCurGamePhase();                //当前游戏阶段
            public StateMachine.CurrentPoint GetCurrentPoint();            //当前时点
            public int GetOnwerPhase();                                   //当前回合玩家(0自己/1对方)
            public List<EffLogic.EntityCard> GetMonZon(bool isEnemy);     //获取怪兽区卡牌列表(false=自己,true=对方)
            public List<EffLogic.EntityCard> GetRegionCards(int playerIndex, GameManage.CardLocation location);   //读取指定玩家指定区域(Cemetery/Banished/ExtraDeck等)的实体卡列表(返回副本,供区域浏览/发动效果)
            public void ResetBoutAttackFlags();                           //重置双方怪兽攻击状态(进入战斗阶段)
            public bool IsMonsterAttackable(EffLogic.EntityCard mon);     //怪兽是否可发起攻击(攻击表示且本回合未攻击)
            public bool IsMonsterTargetable(EffLogic.EntityCard mon);     //怪兽是否可被选为攻击目标(beAttacked==1)
            public int GetPlayerHP(int playerId);                         //玩家当前HP(数据层权威值)
            public bool IsEnemyAIEnable();                                //敌方AI是否启用
            public event Action<StateMachine.CurrentPoint> OnCurrentPointChanged;  //时点变化事件(转发FightLogic)
            public event Action<GameManage.GamePhase> OnGamePhaseChanged;          //阶段变化事件(转发FightLogic)
        }

        public interface LogicLinkUI
        {
            public Func<List<EffLogic.EntityCard>, int, UniTask<List<EffLogic.EntityCard>>> GetShowSelectionDialog();
            public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetEntitySpeSom();
            public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToCemeteryUI();
            public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToBanishedUI();
            public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToHandUI();            //返回手卡(融合素材回手)
            public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> GetGoToDeckUI();            //返回卡组/额外卡组(融合素材回卡组,回收预制体)
            public Func<List<EffLogic.EntityCard>, List<Transform>, UniTask> MagicActBase();
            public Transform FindCardRealTransform(EffLogic.EntityCard realCard);    //真实实体→真实UI预制体(经UI区域容器反查)
            public Action<string> GetBoutChangeReback();
            public Func<UniTask<bool>> GetWaitForPlayClick();
            public void DrawCardHandle(int playerID, int num);
            public Func<int, List<EffLogic.EntityCard>, UniTask> GetDrawCardHandleUI();   //抽卡效果专用的纯UI手牌刷新回调
        }

        public interface UIListener                 //UI回调接口
        {
            public void OnHPChangeHandle(int playerId, int newHP);
            public void OnHPChangeToHandle(int playerId, int newHP);
            public UniTask OnBoutHandle();
            public UniTask OnWaitCurChangeBout(bool isCanChangeBout);
            public void RegionRequestHandle(int regionNum, bool isAdd, EffLogic.EntityCard entityCard, Transform transform);
            public void ClearRegionUIHandle();                  //召唤特招盖放UI清理

            public EffLogic.EntityCard SumOrCovBaseHandle(Transform transform);
            public void LayOutMagicTrapCardHandle(Transform transform);
            public void DrawCardHandle(int whom, List<EffLogic.EntityCard> entityCards);
            public void AbandonCardHandle(Transform transform);                           //丢弃卡牌，专指回合结束
        }

        public interface PECListener              //PreEntityCard回调接口
        {
            public void ShowCardPicRequest(string id, string effect);
            public void SacrificeNumRequest(int num);
            public bool IsWaitingForAsyncRequest();
            public bool IsSelectingQuest();                              //选卡弹窗(IsWaitingQuest)打开期间:禁止场景卡操作(挂载/召唤/盖放/发动),防弹窗等待期漂移
            public UniTask<List<Transform>> WaitForSacrificeSelection();
            public void GotoCeCemeteryUIRequest(Transform transform);
            public void GetSumOrCoverRequest(Transform transform);
            public void GetMagicTrapRequest(Transform transform);
            public void AwakeSomRegionRequest(int i);
            public EntityPlayer.Field GetFieldRequest();
            public void RegionAddCardRequest(int i, EffLogic.EntityCard entityCard, bool isEnemy = false);
            public void RegionRemoveCardRequest(int i, EffLogic.EntityCard entityCard, bool isEnemy = false);
            public GameManage.GamePhase GetCurrentGamePhase();
            public int GetOnwerPhase();                                  //当前回合玩家(0=我方/1=对方):对方回合玩家只能发动效果,不能通召/盖放/攻击

            public bool CheckCanSummonRequest(int playerID);
            public void AddCurSomNumRequest(int playerID);
            public void EntityEffRequestHandle(EntityEffRequest entityEffRequest);

            // 连锁栈
            public void PushOrderStack(StateMachine.EffOrder effOrder);   //效果命令压入连锁栈
            public UniTask HandleOrderStack();                            //无人连锁时结算连锁栈（结算完成后2s询问是否发动诱发）
            public UniTask<bool> AskOrderStackAsk(string tipText = null); //弹问询面板：是否继续连锁（true=连锁）
            public bool IsChainAsk();                                     //是否正处于连锁问询中
            public void SetChainAsk(bool isChain);                        //设置连锁问询状态
            public UniTask<bool> WaitForChainActivate();                  //连锁中等待玩家发动一个效果
            public void ReportChainActivate(bool success);                //玩家发动了效果/取消连锁
            public bool IsTriggerAsk();                                    //是否正处于等待诱发效果发动中
            public void SetTriggerAsk(bool isTrigger);                      //设置等待诱发效果发动状态
            public UniTask<bool> WaitForTriggerActivate();                 //等待玩家发动一个诱发效果（6s无操作超时）
            public void ReportTriggerActivate(bool success);               //玩家发动了诱发效果/取消
            public bool IsTimePointEmpty();                               //当前时点是否为空
            public int GetTopOrderSpeed();                                //连锁栈栈顶效果速度（栈空返回0）
            public int GetTopOrderStackState();                           //连锁栈栈顶效果进栈状态(连锁限制;-1正常/0禁怪兽/1禁魔法/2禁陷阱/10禁怪兽魔法/20禁怪兽陷阱/21禁魔法陷阱/3不可被连锁直接结算;栈空返回-1)
            public StateMachine.CurrentPoint GetCurrentPoint();           //当前时点（最初为自由时点）
            public void SetCurrentPointRequest(StateMachine.CurrentPoint point);   //设置当前时点
            public UniTask AttackRequest(EffLogic.EntityMonsterCard attacker);    //战斗：攻击宣言（含目标选择与伤害判定）
            public void ShowEffChoiceRequest(List<EffLogic.Effection> effs, Action<EffLogic.Effection> onChosen);   //同一时点一张卡多个可发动效果:弹出效果选择面板(几个效果激活几套UI,Text显示效果编号effection.id)
        }

        public interface RegClickEvent
        {
            public void SumOrCovBaseRequest(SumOrCovBaseRequest sumOrCovBaseRequest);
            public void LayOutMagicTrapCardRequest(LayOutMagicTrapRequest layOutMagicTrapRequest);
            public void ClearUIRequest();
            public void ClearSacrificesRequest();
            public bool IsWaitingForSacrificeRequest();
            public void AddSelSacRequest(Transform transform);
            public List<Transform> GetSelSacRequest();
            public int GetRequiredSacrificeCountRequest();
            public void EndSacrificeWaitRequest();
            public bool GetSumOrCoverLock();
            public bool GetMagicTrapLock();
        }
    }
}
