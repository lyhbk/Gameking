using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 命令记录(回放):由玩家实体(EntityPlayer)持有实例(每个玩家一份),对局进行中按发生顺序追加"命令"。
/// record场景(由fighting场景复制:具备全部UI功能,但不跑对局逻辑)只要按命令顺序回放即可重现整局。
///
/// 一、编号规则(8位数字,便于回放定位):
///     效果(EffLogic.CardComponent):1开头      —— CardComponent.effId
///     支付(EffLogic.PayComponent) :0开头      —— PayComponent.payId
///     系统命令(抽卡/阶段切换/攻击/结束等):9开头
///     编号随机生成,只要求本局内不冲突;回放不解释编号语义,仅用于结算中"补全"数据时定位命令。
///
/// 二、记录范围:
///     记录:对局中"发生了什么"——初始手牌/抽卡/阶段切换/效果支付/效果结算/攻击/结束;
///          以及"只有结算过程中才能知晓的数据"(如特招落在第几个怪兽区,必须等玩家点完蓝格、
///          结算完成后才知道),这类数据由效果自身在结算完成后"补全"到对应命令上(见 Patch / CatchPlaceZone)。
///     不记录:判断类数据——如"是否可以发动效果"(Effection.EffCost / JudCost.IsCanPay)、
///          "是否可以连锁"、"AI决策"等。这类结果由逻辑实时算出,回放无需重放,
///          故 Record 不为它们提供任何写入入口(也没有对应的 Order)。
/// </summary>
public class Record
{
    #region 编号

    public const int CodeLength = 8;                 //编号位数
    public const char EffPrefix = '1';               //效果编号首位
    public const char PayPrefix = '0';               //支付编号首位
    public const char SysPrefix = '9';               //系统命令(非效果/支付)编号首位
    private const int CodeRandomRange = 10000000;    //后7位随机范围

    private static readonly System.Random codeRandom = new System.Random();

    public static string NewEffCode() { return NewCode(EffPrefix); }      //效果编号:8位,1开头
    public static string NewPayCode() { return NewCode(PayPrefix); }      //支付编号:8位,0开头
    public static string NewSysCode() { return NewCode(SysPrefix); }      //系统命令编号:8位,9开头

    public static string NewCode(char prefix)
    {
        int tail;
        lock (codeRandom) tail = codeRandom.Next(0, CodeRandomRange);
        return prefix.ToString() + tail.ToString("D7", CultureInfo.InvariantCulture);
    }

    public static bool IsEffCode(string code) { return !string.IsNullOrEmpty(code) && code.Length == CodeLength && code[0] == EffPrefix; }
    public static bool IsPayCode(string code) { return !string.IsNullOrEmpty(code) && code.Length == CodeLength && code[0] == PayPrefix; }
    public static bool IsSysCode(string code) { return !string.IsNullOrEmpty(code) && code.Length == CodeLength && code[0] == SysPrefix; }

    #endregion

    #region 命令类型

    public enum Order
    {
        IniHand = 0,     //初始手牌(回放起始状态)
        DrawCard = 1,    //抽卡(开局抽卡/抽卡阶段抽卡/效果抽卡)
        NextPhase = 2,   //阶段切换
        EffectPay = 3,   //效果代价支付(编号=支付编号,0开头)
        EffectAct = 4,   //效果结算(编号=效果编号,1开头)
        Attack = 5,      //攻击宣言(含伤害判定结果)
        End = 6,         //对局结束
        SetCard = 7      //里侧盖放(魔陷盖到魔陷区/怪兽里侧守备)。盖放此前没有自己的命令,
                         //落位产生的区域改变会被暂存、刷进下一条不相干的命令(如 End)
    }

    /// <summary>命令携带的数据键(结算中才能知晓的数据用同一批键"补全"进来)</summary>
    public static class Key
    {
        public const string PlayerIndex = "PlayerIndex";     //命令发出方(0我方/1对方)
        public const string Owner = "Owner";                 //回合归属(0我方/1对方)
        public const string CardId = "CardId";               //效果持有卡(卡名id:card.id,回放据此重建实体)
        public const string CurrentId = "CurrentId";         //卡牌实例id(entityCard.currentid)
        public const string CardIds = "CardIds";             //多张卡的卡名id(card.id,'|'分隔)
        public const string CurrentIds = "CurrentIds";       //多张卡的实例id(currentid,'|'分隔,与CardIds一一对应)
        public const string EffIndex = "EffIndex";           //效果序号(Effection.id,卡上从左到右0基)
        public const string Num = "Num";                     //数量(抽卡张数/丢弃张数等)
        public const string Phase = "Phase";                 //阶段(GameManage.GamePhase 名)
        public const string SpeSomZone = "SpeSomZone";       //特招落地区号(槽位码,'|'分隔多张)——结算完成后补全
        public const string MagicTrapZone = "MagicTrapZone"; //魔陷落地区号(槽位码)——结算完成后补全
        public const string IsFaceUp = "IsFaceUp";           //盖放卡就地翻面为表侧(1=本次发动把场上盖放卡翻开)。
                                                             //盖放的魔陷发动时不再落位(卡已在场上),只翻面;回放必须补这一步,
                                                             //否则盖放卡发动后会"直接消失"。
        public const string IsCover = "IsCover";             //是否里侧盖放(1=盖放)。魔陷落位有两条完全不同的分支:
                                                             //表侧发动(EffectPay/发动命令)与里侧盖放,回放必须据此选用不同的落位函数,
                                                             //否则盖放会被当成发动处理(显示成正面、位置也取错)。
        public const string SelectCards = "SelectCards";     //选卡弹窗结果(currentid,'|'分隔)——结算中补全
        public const string TargetCards = "TargetCards";     //取对象结果(currentid,'|'分隔)
        public const string AttackerId = "AttackerId";       //攻击方(currentid)
        public const string TargetId = "TargetId";           //被攻击方(currentid)
        public const string Damage = "Damage";               //伤害值
        public const string IsWin = "IsWin";                 //结束:我方是否胜利
        public const string Move = "Move";                   //卡去向:见 Record.MoveTo(送墓/除外/回手/回卡组/上场)——移动完成后补全
        public const string CurrentIdsBefore = "CurrentIdsBefore";   //移动"前"的实例id('|'分隔)——回放据此定位要移动的卡
        public const string CurrentIdsAfter = "CurrentIdsAfter";     //移动"后"的实例id('|'分隔)——回放移动完成后把id改为此值,与实际对局一致
        //逐卡移动明细:"移动前id>移动后id>去向"('|'分隔多张卡;去向为空=进入卡组/额外卡组等隐藏区,无UI可表现)。
        //Key.Move 一条命令只能存一个去向,一条命令里多张卡去往不同区域时会丢失信息,回放一律以本条明细为准。
        public const string CurrentIdsMoves = "CurrentIdsMoves";
        public const string DamageTo = "DamageTo";           //受到战斗伤害的一方(0我方/1对方)——攻击回放据此把血量变化落到正确一方
    }

    /// <summary>卡去向:回放时据此把卡预制体移到对应区域(送墓/除外/回手/回卡组)</summary>
    public static class MoveTo
    {
        public const string Cemetery = "Cemetery";           //送去墓地
        public const string Banished = "Banished";           //除外
        public const string Hand = "Hand";                   //返回手卡
        public const string Deck = "Deck";                   //返回卡组/额外卡组
        public const string Field = "Field";                 //上场(怪兽区/魔陷区)
    }

    /// <summary>
    /// 命令记录上下文:标记"当前正在执行的命令"(支付用payId / 效果结算用effId)。
    /// 支付与效果在 Effection 里是串行 await 执行的,故用静态当前值即可唯一定位到那条命令。
    /// 用途:统一的卡移动入口(EffLogic.SendCardToZone)在移动完成后,把"卡被送到哪去了"按当前命令编号补记进去,
    ///      这样回放才知道要把哪张卡的预制体移去哪里(如"白之圣女支付:将自身送去墓地")。
    /// </summary>
    public static class RecordContext
    {
        public static string code { get; private set; }              //当前命令编号
        public static EntityPlayer owner { get; private set; }       //命令归属玩家(持有 record)

        public static bool IsActive { get { return !string.IsNullOrEmpty(code) && owner != null; } }
        //是否为"明确"上下文:仅支付(CostPay)/结算(EffAction)期间为true,那时命令编号确凿。
        //Record.Add 只是把最近一条命令设为当前,属隐式,不能被它顶掉显式上下文。
        private static bool isExplicit;

        public static void Begin(string cmdCode, EntityPlayer cmdOwner, bool explicitCmd = false)
        {
            code = cmdCode;
            owner = cmdOwner;
            isExplicit = explicitCmd;
        }

        //新建命令时调用:仅在无明确上下文时把新命令设为当前(避免顶掉支付/结算的显式上下文)
        public static void Ensure(string cmdCode, EntityPlayer cmdOwner)
        {
            if (isExplicit) return;
            code = cmdCode;
            owner = cmdOwner;
        }

        public static void End()
        {
            code = null;
            owner = null;
            isExplicit = false;
        }

        //补记"卡的去向 + 被移动的卡":由 SendCardToZone 等统一移动入口调用。
        //currentIds 须传"移动前"的实例id(EntityCard.ChangeLocation 会重生成 currentid),
        //否则回放按移动后的新id重建出的实体,与场上已有预制体里的不是同一实例 → 墓地新建一份而场上残留。
        public static void PatchMove(string move, System.Collections.Generic.List<EffLogic.EntityCard> cards,
                                     System.Collections.Generic.List<string> currentIds = null)
        {
            if (!IsActive || owner.record == null) return;
            if (cards == null || cards.Count == 0) return;
            UnityEngine.Debug.Log($"[记录] 补记去向 {move} → 命令 {code}:{cards.Count} 张卡({JoinNames(cards)})");
            owner.record.Patch(code, Key.Move, move);
            owner.record.Patch(code, Key.CardIds, EffLogic.Effection.JoinIds(cards, false));
            //移动前/后id不在此写:由 EntityCard.ChangeLocation 以"追加"方式统一登记,
            //此处若用 Patch(覆盖)会把它已经追加好的id串整条冲掉。
        }

        //区域改变的统一登记入口(由 EntityCard.ChangeLocation 调用)。
        //区域改变必然换id,故在id重生成的唯一出口登记最权威,通召/特招/送墓/除外/回手等所有路径自动覆盖。
        //归属以"卡自己的持有者"为准,不用静态owner——静态上下文可能还停留在上一位玩家的命令上。
        //hideZone=true:卡进入卡组/额外卡组等"隐藏区"——实体保持同一个id(ChangeLocation 的非重生成分支),
        //单看"换id"看不出区域改变,但回放仍必须知道它已离开可见区:否则新区域没有UI可表现、旧区域那份UI又永远留着,
        //表现就是"命令执行了、UI却什么都没变"。
        public static void RecordMove(EntityPlayer cardOwner, string beforeId, string afterId, GameManage.CardLocation location, bool hideZone = false)
        {
            if (cardOwner?.record == null) return;
            if (string.IsNullOrEmpty(beforeId) || string.IsNullOrEmpty(afterId)) return;
            if (string.Equals(beforeId, afterId))
            {
                if (!hideZone) return;                                       //id未变且不是隐藏区:无需登记
                //开局建立卡组/洗牌等"非命令期"的大量区域写入:对回放没有意义,还会挤占暂存位,一律不记
                if (!IsActive) return;
            }
            //明确上下文(支付/结算):命令编号确凿,直接落到那条命令。
            //跨玩家效果(我方效果除外/送墓"对方的卡")也必须记到本条命令:
            //落点命令以"效果发起方"为准——若按"卡自己的持有者的记录"去找,
            //对方的记录里根本没有本条命令,移动就会被暂存、刷进下一条对方命令
            //(冰剑龙在对方回合除外对方怪兽,回放里却除外了冰剑龙自己)。
            if (isExplicit && owner != null)
            {
                WriteMove(owner, code, beforeId, afterId, location);
                return;
            }
            //落位由玩家点蓝格触发(特召/通召):此刻命令早已创建且 End() 已调用,isExplicit 为 false,
            //若不重定向就会被暂存、在下一条命令 Add 时才刷入 → 移动被记到不相干的命令上。
            //回放表现为"支付送墓的命令里多出一条上场移动"、"特招命令里移动的是效果持有卡自己"。
            //只接管"落位上场"这一种区域改变:送墓/除外等由显式上下文自己登记,不能被重定向抢走
            if (redirectOwner == cardOwner && !string.IsNullOrEmpty(redirectCode)
                && location == GameManage.CardLocation.Field)
            {
                WriteMove(cardOwner, redirectCode, beforeId, afterId, location);
                //命中够张数就收工:多卡特招是逐张落位,捕获完本条命令的全部目标即可,
                //多留会让之后无关的上场被误记到本条命令上
                if (--redirectLeft <= 0) ClearRedirectMoves();
                return;
            }
            //命令尚未创建(典型:通召——数据层先上场改id,命令随后才建立):先暂存,等该玩家 Add 命令时刷入。
            //限量防止异常路径下无限累积后被刷进不相干的命令。
            if (staged.Count > 16) staged.RemoveAt(0);
            staged.Add(new StagedMove { owner = cardOwner, before = beforeId, after = afterId, location = location });
        }

        //把"之后发生的区域改变"直接落到指定编号的命令上(落位前调用,落位完成后用 ClearRedirectMoves 收尾)。
        //count=本次落位的目标张数:捕获满这么多张就自动解除(多卡特招逐张落位,需全部记到本条命令)
        public static void RedirectMovesTo(EntityPlayer recOwner, string cmdCode, int count = 1)
        {
            if (recOwner?.record == null || string.IsNullOrEmpty(cmdCode)) return;
            redirectOwner = recOwner;
            redirectCode = cmdCode;
            redirectLeft = count < 1 ? 1 : count;
            redirectAge = 0;
        }
        public static void ClearRedirectMoves() { redirectOwner = null; redirectCode = null; redirectLeft = 0; redirectAge = 0; }
        //清掉暂存的移动(外层 Record 在开新局时调用;staged 是私有字段,只能由本类提供出口)
        public static void ClearStaged() { staged.Clear(); }

        //新建命令时调用:落位点击的时机不定(可能晚于下一条甚至再下一条命令),故不能一遇新命令就解除;
        //但也不能无限期挂着(之后无关的上场会被误记到旧命令上),超过若干条命令仍未命中就作废。
        public static void OnNewCommand()
        {
            if (redirectCode == null) return;
            if (++redirectAge > 3) ClearRedirectMoves();
        }
        private static EntityPlayer redirectOwner;
        private static string redirectCode;
        private static int redirectLeft;
        private static int redirectAge;

        //新建命令时把此前暂存的区域改变刷进这条命令
        public static void FlushStaged(EntityPlayer recOwner, BaseRecord rec)
        {
            if (staged.Count == 0 || recOwner == null || rec == null) return;
            for (int i = staged.Count - 1; i >= 0; i--)
            {
                if (staged[i].owner != recOwner) continue;
                WriteMove(recOwner, rec.code, staged[i].before, staged[i].after, staged[i].location);
                staged.RemoveAt(i);
            }
        }

        private class StagedMove
        {
            public EntityPlayer owner;
            public string before;
            public string after;
            public GameManage.CardLocation location;
        }
        private static readonly System.Collections.Generic.List<StagedMove> staged
            = new System.Collections.Generic.List<StagedMove>();

        private static void WriteMove(EntityPlayer o, string cmdCode, string beforeId, string afterId, GameManage.CardLocation location)
        {
            BaseRecord rec = o.record.FindLast(cmdCode);
            if (rec == null) return;
            AppendVal(rec, Key.CurrentIdsBefore, beforeId);
            AppendVal(rec, Key.CurrentIdsAfter, afterId);
            string move;
            switch (location)
            {
                case GameManage.CardLocation.Cemetery: move = MoveTo.Cemetery; break;
                case GameManage.CardLocation.Banished: move = MoveTo.Banished; break;
                case GameManage.CardLocation.Hand: move = MoveTo.Hand; break;
                case GameManage.CardLocation.Field: move = MoveTo.Field; break;
                default: move = null; break;                      //卡组/额外卡组:隐藏区,回放无UI可表现
            }
            //逐卡明细"前id>后id>去向":这是回放还原卡移动的权威数据(Key.Move 一条命令只能存一个去向,
            //一条命令里多张卡去往不同区域时会丢信息,故回放不再依赖它)。
            AppendVal(rec, Key.CurrentIdsMoves, beforeId + ">" + afterId + ">" + (move ?? ""));
            //去向:仅在该命令还没有去向时写入,避免一条命令里多张卡去往不同区域时互相覆盖
            if (!string.IsNullOrEmpty(move) && !rec.HasData(Key.Move))
                rec.AddData(Key.Move, move);
            UnityEngine.Debug.Log($"[记录] 区域改变登记 → 命令 {cmdCode}:{beforeId} → {afterId} 去向={move ?? "隐藏区"}");
        }

        private static void AppendVal(BaseRecord rec, string key, string value)
        {
            string cur = rec.GetData(key, "");
            rec.AddData(key, string.IsNullOrEmpty(cur) ? value : cur + "|" + value);
        }

        private static string JoinNames(System.Collections.Generic.List<EffLogic.EntityCard> cards)
        {
            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) names.Add(cards[i].currentName);
            return string.Join("、", names.ToArray());
        }
    }

    /// <summary>槽位码:统一编码"哪个区域格",特招/落位补全与回放共用(见 FightUI.GetSlotCode)</summary>
    public static class Slot
    {
        public const int PlayerMonster = 0;      //0-4  我方怪兽区
        public const int EnemyMonster = 5;       //5-9  对方怪兽区
        public const int PlayerMagicTrap = 10;   //10-14 我方魔陷区
        public const int EnemyMagicTrap = 15;    //15-19 对方魔陷区
        public const int Unknown = -1;

        public static bool IsMonster(int code) { return code >= PlayerMonster && code < PlayerMagicTrap; }
        public static bool IsMagicTrap(int code) { return code >= PlayerMagicTrap && code <= EnemyMagicTrap + 4; }
        public static int ToPlayerIndex(int code)
        {
            if (code >= EnemyMonster && code < PlayerMagicTrap) return 1;      //5-9 对方怪兽区
            if (code >= EnemyMagicTrap) return 1;                              //15-19 对方魔陷区
            return 0;
        }
        public static int ToZoneIndex(int code)
        {
            if (code < 0) return -1;
            if (code < PlayerMagicTrap) return code % 5;                        //怪兽区:0-4 / 5-9 → 0-4
            if (code < EnemyMagicTrap) return code - PlayerMagicTrap;           //我方魔陷区
            return code - EnemyMagicTrap;                                       //对方魔陷区
        }
        public static int Make(int playerIndex, bool isMagicTrap, int zoneIndex)
        {
            int baseCode = isMagicTrap ? (playerIndex == 0 ? PlayerMagicTrap : EnemyMagicTrap)
                                       : (playerIndex == 0 ? PlayerMonster : EnemyMonster);
            return baseCode + zoneIndex;
        }
    }

    #endregion

    #region 命令

    [Serializable]
    public class RecordData
    {
        public string key;
        public string value;
        public RecordData() { }
        public RecordData(string key, string value) { this.key = key; this.value = value; }
    }

    [Serializable]
    public class BaseRecord
    {
        public Order order;                     //命令类型
        public int numth;                       //命令类型序号(与order等价,便于按旧格式读取)
        public string code;                     //8位编号(效果1开头/支付0开头/系统9开头)
        public long seconds;                    //记录时刻(Unix秒)
        public int index;                       //全局序号(双方共用计数器,保证回放时间线有序)
        public List<RecordData> datas = new List<RecordData>();

        public BaseRecord() { }
        public BaseRecord(Order order, string code, int index)
        {
            this.order = order;
            this.numth = (int)order;
            this.code = code;
            this.index = index;
            this.seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>补全/写入一条命令数据(同键覆盖)</summary>
        public BaseRecord AddData(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return this;
            if (datas == null) datas = new List<RecordData>();
            for (int i = 0; i < datas.Count; i++)
            {
                if (datas[i] != null && datas[i].key == key)
                {
                    datas[i].value = value;
                    return this;
                }
            }
            datas.Add(new RecordData(key, value));
            return this;
        }
        public BaseRecord AddData(string key, int value) { return AddData(key, value.ToString(CultureInfo.InvariantCulture)); }
        public BaseRecord AddData(string key, bool value) { return AddData(key, value ? "1" : "0"); }

        public string GetData(string key, string def = "")
        {
            if (datas == null || string.IsNullOrEmpty(key)) return def;
            for (int i = 0; i < datas.Count; i++)
                if (datas[i] != null && datas[i].key == key) return datas[i].value ?? def;
            return def;
        }
        public int GetDataInt(string key, int def = -1)
        {
            string v = GetData(key, null);
            int r;
            return v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : def;
        }
        public bool GetDataBool(string key, bool def = false)
        {
            string v = GetData(key, null);
            return v == null ? def : (v == "1" || v == "true" || v == "True");
        }
        public bool HasData(string key) { return GetData(key, null) != null; }
        public List<string> GetDataList(string key)
        {
            List<string> res = new List<string>();
            string v = GetData(key, null);
            if (string.IsNullOrEmpty(v)) return res;
            string[] arr = v.Split('|');
            for (int i = 0; i < arr.Length; i++)
                if (!string.IsNullOrEmpty(arr[i])) res.Add(arr[i]);
            return res;
        }
        public List<int> GetDataIntList(string key)
        {
            List<int> res = new List<int>();
            List<string> vs = GetDataList(key);
            for (int i = 0; i < vs.Count; i++)
            {
                int r;
                if (int.TryParse(vs[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) res.Add(r);
            }
            return res;
        }

        public bool IsEffCommand { get { return IsEffCode(code); } }
        public bool IsPayCommand { get { return IsPayCode(code); } }
        public int PlayerIndex { get { return GetDataInt(Key.PlayerIndex, 0); } }
        public string CardId { get { return GetData(Key.CardId, ""); } }

        public override string ToString()
        {
            return $"[{index}] {order}({numth}) code={code} {DumpData()}";
        }
        public string DumpData()
        {
            if (datas == null || datas.Count == 0) return "";
            List<string> parts = new List<string>();
            for (int i = 0; i < datas.Count; i++)
                if (datas[i] != null) parts.Add(datas[i].key + "=" + datas[i].value);
            return string.Join(",", parts.ToArray());
        }
    }

    #endregion

    #region 落地区号采集(结算中才能知晓的数据)

    // 特招/落位时玩家点的是"第几个怪兽区",这个数字只有点完才知道(即结算完成后才知晓),
    // 由UI在放置瞬间写入采集器,效果结算完成后由效果自身取出并"补全"到自己的命令上。
    private static readonly List<int> placeSlots = new List<int>();

    public static void BeginPlace() { placeSlots.Clear(); }
    public static void CatchPlaceSlot(int slotCode) { if (slotCode != Slot.Unknown) placeSlots.Add(slotCode); }
    public static string TakePlaceSlots()
    {
        string res = JoinInt(placeSlots);
        placeSlots.Clear();
        return res;
    }
    public static void ClearPlace() { placeSlots.Clear(); }

    private static string JoinInt(List<int> values)
    {
        List<string> parts = new List<string>();
        for (int i = 0; i < values.Count; i++) parts.Add(values[i].ToString(CultureInfo.InvariantCulture));
        return string.Join("|", parts.ToArray());
    }

    #endregion

    #region 实例

    private readonly List<BaseRecord> records = new List<BaseRecord>();

    /// <summary>持有本记录的玩家实体(由玩家实体持有实例)</summary>
    public EntityPlayer owner { get; private set; }

    public Record(EntityPlayer owner = null)
    {
        this.owner = owner;
    }

    public int Count { get { return records.Count; } }
    public List<BaseRecord> GetRecords() { return new List<BaseRecord>(records); }

    //开新局:清掉全部跨局残留的静态状态(落位采集器/暂存移动/落位重定向/最近命令入口)。
    //这些是静态的、不随场景卸载而重置,上一局的残留会被带进新一局(典型:上一局的落位重定向
    //让新一局第一条上场移动记到旧命令编号上)。
    public static void ClearAllForNewDuel()
    {
        RecordContext.ClearRedirectMoves();
        RecordContext.ClearStaged();
        ClearPlace();
        LastInstance = null;
    }

    /// <summary>把所有命令及参数按序拼成多行文本(进入回放场景、开始回放前打日志用)</summary>
    public string DumpAll()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"===== 回放命令清单 共 {records.Count} 条 =====");
        for (int i = 0; i < records.Count; i++)
            sb.AppendLine(records[i] != null ? records[i].ToString() : $"[{i}] (空命令)");
        sb.AppendLine("===== 命令清单结束 =====");
        return sb.ToString();
    }
    public BaseRecord Get(int index) { return index >= 0 && index < records.Count ? records[index] : null; }
    public BaseRecord Last { get { return records.Count > 0 ? records[records.Count - 1] : null; } }

    /// <summary>当前"待补全"命令:结算过程中才能知晓的数据默认补到最后一条命令上</summary>
    public BaseRecord pending { get; private set; }

    //最近一次 Add 出来的命令所属的记录实例。
    //UI 在"落位瞬间"要把区号补进命令,但它拿不到玩家/记录引用,故由 Add 自动登记一个全局可及的入口。
    public static Record LastInstance { get; private set; }
    //把"结算中才知晓的数据"补到最近一条命令上(UI 侧专用;已有同键则不覆盖)
    public static void PatchLastPending(string key, string value)
    {
        if (LastInstance == null || LastInstance.pending == null || string.IsNullOrEmpty(key)) return;
        if (!LastInstance.pending.HasData(key)) LastInstance.pending.AddData(key, value);
    }

    //把一次移动追加到"最近一条指定类型的命令"上。
    //用于不走 ChangeLocation 的移动(典型:战斗破坏只改区域列表、id 也不重生成),
    //若不显式登记,记录里就完全没有这次移动的信息——回放时被破坏的卡会一直留在场上。
    public static void AppendMoveToLastOrder(Order order, string beforeId, string afterId, string move)
    {
        if (LastInstance == null || string.IsNullOrEmpty(beforeId)) return;
        BaseRecord rec = LastInstance.FindLast(order);
        if (rec == null) return;
        if (string.IsNullOrEmpty(afterId)) afterId = beforeId;
        AppendMoveVal(rec, Key.CurrentIdsBefore, beforeId);
        AppendMoveVal(rec, Key.CurrentIdsAfter, afterId);
        AppendMoveVal(rec, Key.CurrentIdsMoves, beforeId + ">" + afterId + ">" + move);
        if (!rec.HasData(Key.Move)) rec.AddData(Key.Move, move);
    }

    //向命令的某个键追加一个值(已有则用'|'分隔)。
    //与 RecordContext 里的同名辅助作用相同,但那是嵌套类的私有成员,外层取不到,故在此另备一份。
    private static void AppendMoveVal(BaseRecord rec, string key, string value)
    {
        string cur = rec.GetData(key, "");
        rec.AddData(key, string.IsNullOrEmpty(cur) ? value : cur + "|" + value);
    }

    /// <summary>追加一条命令;code为空时按系统命令自动编号(9开头)</summary>
    public BaseRecord Add(Order order, string code = null)
    {
        if (string.IsNullOrEmpty(code)) code = NewSysCode();
        BaseRecord record = new BaseRecord(order, code, NextIndex());
        records.Add(record);
        pending = record;
        LastInstance = this;
        //落位点击可能晚于下一条甚至再下一条命令,故不能在此直接解除上一条命令的落位重定向,
        //只做"超期作废"(见 RecordContext.OnNewCommand):否则落位的区域改变会被暂存、刷进下一条命令。
        RecordContext.OnNewCommand();
        //新命令作为"当前命令":区域改变(id重生成)会把移动前/后id登记到它上面。
        //此前只在支付/结算期间有上下文,通召等其它命令期间的id变化全部漏记 → 回放id链断裂。
        RecordContext.Ensure(code, owner);
        //通召这类"数据层先改区域、命令后建"的路径,其区域改变在 ChangeLocation 时只能暂存,
        //此刻命令已建立,把暂存的id对刷进来(否则命令里没有before/after,回放无法定位)。
        RecordContext.FlushStaged(owner, record);
        return record;
    }

    /// <summary>追加一条"效果支付"命令(code=支付编号,0开头)</summary>
    public BaseRecord AddEffectPay(string payId, int playerIndex, string cardId, int effIndex)
    {
        return Add(Order.EffectPay, payId)
            .AddData(Key.PlayerIndex, playerIndex)
            .AddData(Key.CardId, cardId)
            .AddData(Key.EffIndex, effIndex);
    }

    /// <summary>追加一条"效果结算"命令(code=效果编号,1开头)</summary>
    public BaseRecord AddEffectAct(string effId, int playerIndex, string cardId, int effIndex)
    {
        return Add(Order.EffectAct, effId)
            .AddData(Key.PlayerIndex, playerIndex)
            .AddData(Key.CardId, cardId)
            .AddData(Key.EffIndex, effIndex);
    }

    /// <summary>直接塞入一条已构造好的命令(合成回放时间线/反序列化用,不重新编号)</summary>
    public void AddRaw(BaseRecord record)
    {
        if (record == null) return;
        records.Add(record);
        pending = record;
    }

    /// <summary>当前待补全命令是否已有该键(用于避免覆盖其它入口已追加好的数据)</summary>
    public bool HasPending(string key)
    {
        return pending != null && pending.HasData(key);
    }

    /// <summary>补全"当前待补全"命令的数据</summary>
    public void Patch(string key, string value)
    {
        if (pending == null) return;
        pending.AddData(key, value);
    }

    /// <summary>按编号补全:找到最近一条同编号命令写入数据(效果/支付用各自的编号定位)</summary>
    public void Patch(string code, string key, string value)
    {
        BaseRecord record = FindLast(code);
        if (record != null) record.AddData(key, value);
    }

    public BaseRecord FindLast(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        for (int i = records.Count - 1; i >= 0; i--)
            if (records[i] != null && records[i].code == code) return records[i];
        return null;
    }

    public BaseRecord FindLast(Order order)
    {
        for (int i = records.Count - 1; i >= 0; i--)
            if (records[i] != null && records[i].order == order) return records[i];
        return null;
    }

    public void EndPending() { pending = null; }

    /// <summary>开局清理玩家携带的回放数据(默认只保存一局)。
    /// 命名刻意避开通用的 Clear:本类型内还有同名成员,调用处会解析出二义性(CS0121)。</summary>
    public void RebackClear() { records.Clear(); pending = null; }

    #endregion

    #region 全局序号(双方共用,保证合成时间线有序)

    private static int globalIndex = 0;

    public static int NextIndex() { return globalIndex++; }
    public static void ResetGlobalIndex() { globalIndex = 0; ClearPlace(); }

    #endregion

    #region 回放

    /// <summary>
    /// 回放第 i 条命令并返回下一条下标(已到末尾返回 -1)。
    /// handler 为空时只推进游标(用于"跳过"回放)。
    /// </summary>
    public async UniTask<int> PlayAt(int i, IRecordPlayBack handler)
    {
        BaseRecord record = Get(i);
        if (record == null) return -1;
        if (handler != null) await handler.OnRecord(record);
        return i + 1 < records.Count ? i + 1 : -1;
    }

    #endregion

    #region 存取(跨场景/落盘)

    [Serializable]
    public class RecordSave
    {
        public List<BaseRecord> records = new List<BaseRecord>();
    }

    public string ToJson()
    {
        RecordSave save = new RecordSave();
        save.records = new List<BaseRecord>(records);
        return JsonUtility.ToJson(save);
    }

    public static Record FromJson(string json, EntityPlayer owner = null)
    {
        Record record = new Record(owner);
        if (string.IsNullOrEmpty(json)) return record;
        RecordSave save = JsonUtility.FromJson<RecordSave>(json);
        if (save != null && save.records != null)
            for (int i = 0; i < save.records.Count; i++)
                record.AddRaw(save.records[i]);
        return record;
    }

    #endregion
}

/// <summary>
/// 回放命令处理器:record场景里由"只做UI、不跑逻辑"的回放器实现,按命令直接驱动界面。
/// 判断类数据不在命令里,故实现中不需要做任何规则判断。
/// </summary>
public interface IRecordPlayBack
{
    UniTask OnRecord(Record.BaseRecord record);
}

/// <summary>
/// 跨场景搬运记录:对局结束(fighting)写入,record场景读取。
/// 静态成员在场景切换后仍然存活,无需 DontDestroyOnLoad。
/// </summary>
public static class RecordTransfer
{
    public static Record current { get; set; }
    public static string currentJson { get; set; }

    public static void Save(Record record)
    {
        current = record;
        currentJson = record == null ? null : record.ToJson();
    }

    public static Record Take()
    {
        if (current != null) return current;
        if (!string.IsNullOrEmpty(currentJson)) return Record.FromJson(currentJson);
        return null;
    }

    public static void Clear() { current = null; currentJson = null; }

    //是否有可回放的记录:mainscene"进入回放"按钮据此决定能否进入
    public static bool HasRecord() { return current != null || !string.IsNullOrEmpty(currentJson); }
}
