
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static CardBase;
using Cysharp.Threading.Tasks;
using static EntityPlayer;
using Unity.VisualScripting;
using UnityEngine.UIElements;



public class EffLogic
{
    //组件索引顺序:0=Deck,1=ExtraDeck,2=Hand,3=Cemetery,4=Banished,5=Field(对方区域对应整体+6)
    public const int RegionCodeLength = 12;

    public static List<bool> NewRegionCode()
    {
        return new List<bool>(new bool[RegionCodeLength]);
    }

    //自肃形式->编码int;解析失败返回false
    public static bool TryParseLimition(string form, out int code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(form)) return false;
        if (int.TryParse(form, out code)) return code != 0;                 //已直接填数字编码(0=None视为无效)
        if (Enum.TryParse(form, true, out Limition lc) && (int)lc != 0)     //填自肃类名称(忽略大小写)
        {
            code = (int)lc;
            return true;
        }
        return false;
    }

    //连锁限制判定
    //0=禁怪兽 1=禁魔法 2=禁陷阱 10=禁怪兽·魔法 20=禁怪兽·陷阱 21=禁魔法·陷阱
    public static bool CanChainByOrderState(int orderStackState, int cardType)
    {
        if (orderStackState == -1) return true;
        switch (orderStackState)
        {
            case 0: return cardType != 0;
            case 1: return cardType != 1;
            case 2: return cardType != 2;
            case 10: return cardType != 0 && cardType != 1;
            case 20: return cardType != 0 && cardType != 2;
            case 21: return cardType != 1 && cardType != 2;
            default: return true;
        }
    }
    //不可被连锁,不再询问玩家是否连锁,改为直接结算
    public static bool IsUnchainable(int orderStackState)
    {
        return orderStackState == 3;
    }

    //CardLocation(对方区域6-11)-> 0-11组件索引
    public static int RegionCodeIndexOf(GameManage.CardLocation cardLocation)
    {
        bool isEnemy = GameManage.IsEnemyLocation(cardLocation);
        GameManage.CardLocation local = GameManage.ToLocalLocation(cardLocation);
        int idx;
        switch (local)
        {
            case GameManage.CardLocation.Deck: idx = 0; break;
            case GameManage.CardLocation.ExtraDeck: idx = 1; break;
            case GameManage.CardLocation.Hand: idx = 2; break;
            case GameManage.CardLocation.Cemetery: idx = 3; break;
            case GameManage.CardLocation.Banished: idx = 4; break;
            case GameManage.CardLocation.Field: idx = 5; break;
            default: throw new Exception($"输入错误cardLocation：{cardLocation}");
        }
        return isEnemy ? idx + 6 : idx;
    }

    //位置列表->12位bool编码(位置可包含对方区域,如e_Hand编码到第8位)
    public static List<bool> RegionLocationsToCode(IEnumerable<GameManage.CardLocation> findComponent)
    {
        List<bool> bools = NewRegionCode();
        if (findComponent == null) return bools;
        foreach (var location in findComponent)
            bools[RegionCodeIndexOf(location)] = true;
        return bools;
    }

    //0-11组件索引->CardLocation位置种类
    public static GameManage.CardLocation RegionCodeIndexToLocation(int componentIndex)
    {
        switch (componentIndex % 6)
        {
            case 0: return GameManage.CardLocation.Deck;
            case 1: return GameManage.CardLocation.ExtraDeck;
            case 2: return GameManage.CardLocation.Hand;
            case 3: return GameManage.CardLocation.Cemetery;
            case 4: return GameManage.CardLocation.Banished;
            case 5: return GameManage.CardLocation.Field;
            default: throw new Exception($"输入错误组件索引：{componentIndex}");
        }
    }


    public static class Condition2D                              //二维选卡条件
    {
        //cardCondition大组下标约定其匹配维度:
        //  大组0=卡种类  大组1=卡名  大组2=字段  大组3=怪兽基础类型  大组4=怪兽类型  大组5=怪兽特殊类型
        //  大组6=是否盖放  大组7=卡名记述(currentHavKey:效果文本记载过的卡名列表,元素精确命中任一即算包含)
        //  大组8=除外卡名(与其它"命中"大组相反,为"排除"语义:当前卡名命中该名单任一项即整体不算命中;
        //        排除在入口统一先行过滤,不受命中链影响;组内多项用*分隔)
        //  大组9=怪兽属性(currentAttribute:值与怪兽属性字符串精确匹配,如 光/暗/地/水/炎/风/神 等;仅怪兽有效,组内多项用*分隔=命中任一项)
        //默认语义(无hitList):每个非空条件大组都要命中,大组内任一子项命中即可(组内"或",组间"且")
        //hitList非空:每条"或"分支(CSV配置时元素间用|分隔)内所有叶子引用同时命中即满足(分支间"或",分支内"且")
        public const int ExcludeNameGi = 8;                         //除外卡名大组下标(追加于卡名记述之后,与大组对齐一致)
        public const int AttributeGi = 9;                          //怪兽属性大组下标(追加于除外卡名之后;值=属性字符串光/暗/地/水/炎/风/神等)
        public static bool IsMatch(EntityCard card, List<List<string>> cardCondition, List<string> hitList, List<EntityCard> poolCards = null)
        {
            if (cardCondition == null || cardCondition.Count == 0) return true;
            //除外卡名大组(8)先行过滤:卡名命中名单中任一项即整体排除(故不参与下方默认"须命中"或命中链的命中判定)
            if (cardCondition.Count > ExcludeNameGi)
            {
                List<string> excludeGroup = cardCondition[ExcludeNameGi];
                if (excludeGroup != null)
                    foreach (var ex in excludeGroup)
                        if (!string.IsNullOrWhiteSpace(ex) && card.currentName == ex.Trim()) return false;
            }
            if (hitList == null || hitList.Count == 0)
            {
                for (int gi = 0; gi < cardCondition.Count; gi++)
                {
                    if (gi == ExcludeNameGi) continue;             //空大组或除外卡名大组:不参与"须命中"(排除已先行处理)
                    List<string> group = cardCondition[gi];
                    if (group == null || group.Count == 0) continue;          //空大组:不限制
                    bool groupHit = false;
                    for (int j = 0; j < group.Count; j++)
                        if (IsLeafHit(card, cardCondition, gi, j, poolCards)) { groupHit = true; break; }
                    if (!groupHit) return false;
                }
                return true;
            }
            foreach (var branch in hitList)                                    //命中链:任一条分支内所有叶子引用同时命中即满足(分支间"或",分支内"且")
            {
                if (string.IsNullOrWhiteSpace(branch)) continue;
                bool branchHit = true;
                foreach (var leafRef in branch.Split('*'))
                {
                    if (!TryParseLeafRef(leafRef, out int gi, out int j, cardCondition)) { branchHit = false; break; }  //引用非法:该分支视为未命中(配置错误保守处理)
                    if (!IsLeafHit(card, cardCondition, gi, j, poolCards)) { branchHit = false; break; }
                }
                if (branchHit) return true;
            }
            return false;
        }

        private static bool TryParseLeafRef(string leafRef, out int gi, out int j, List<List<string>> cardCondition)   //解析叶子引用"大组下标/子项下标",含越界校验
        {
            gi = -1;
            j = -1;
            if (string.IsNullOrWhiteSpace(leafRef)) return false;
            string[] ab = leafRef.Split('/');
            if (ab.Length != 2) return false;
            if (!int.TryParse(ab[0].Trim(), out gi) || gi < 0 || gi >= cardCondition.Count) return false;
            List<string> group = cardCondition[gi];
            if (group == null || !int.TryParse(ab[1].Trim(), out j) || j < 0 || j >= group.Count) return false;
            return true;
        }

        private static bool IsLeafHit(EntityCard card, List<List<string>> cardCondition, int gi, int j, List<EntityCard> poolCards)   //单叶子求值:子项内容空白=不限制(恒命中)
        {
            string val = cardCondition[gi][j];
            if (string.IsNullOrWhiteSpace(val)) return true;
            switch (gi)
            {
                case 0: //卡种类:值=0怪兽/1魔法/2陷阱
                    return int.TryParse(val.Trim(), out int typeLim) && typeLim == card.entityType;
                case 1: //卡名:currentName精确匹配
                    return card.currentName == val.Trim();
                case 2: //字段:currentKey包含该字段
                    return card.currentKey != null && card.currentKey.Contains(val.Trim());
                case 3: //怪兽基础类型(currentBaseType)
                case 4: //怪兽类型(currentType)
                case 5: //怪兽特殊类型(currentSpecialType)
                    if (card.entityType != 0 || poolCards == null || poolCards.Count == 0) return false;
                    EntityMonsterCard monster = EntityCard.GetMonsterSpecialFind(poolCards, card.currentid);
                    if (monster == null) return false;
                    int mv = gi == 3 ? monster.currentBaseType : (gi == 4 ? monster.currentType : monster.currentSpecialType);
                    return int.TryParse(val.Trim(), out int typeVal) && mv == typeVal;
                case 6: //是否盖放:值=0否(表侧)/1是(里侧盖放),匹配card.isCover(基类字段,所有卡类型通用)
                    return int.TryParse(val.Trim(), out int coverLim) && coverLim == card.isCover;
                case 7: //卡名记述:currentHavKey任一元素精确等于该值即可(卡牌实体文本记载过该卡名,仅需实体包含,不解析效果文本原始全文)
                    return card.currentHavKey != null && card.currentHavKey.Contains(val.Trim());
                case ExcludeNameGi: //大组8除外卡名:叶子命中=当前卡名等于该名单项(整组排除已在IsMatch入口统一先行过滤,此处供命中链显式引用)
                    return card.currentName == val.Trim();
                case AttributeGi: //大组9怪兽属性:值=属性字符串(光/暗/地/水/炎/风/神等)精确匹配;仅怪兽,经候选池currentid回查怪兽实体(同大组3/4/5)
                    if (card.entityType != 0 || poolCards == null || poolCards.Count == 0) return false;
                    EntityMonsterCard attrMonster = EntityCard.GetMonsterSpecialFind(poolCards, card.currentid);
                    return attrMonster != null && attrMonster.currentAttribute == val.Trim();
                default: //预留扩展维度:新增大组下标时在此追加对应求值
                    return false;
            }
        }
    }
    [System.Serializable]
    public class EntityCard
    {
        public Card card { get; protected set; }                           //指向静态数据

        private static List<int> ints = new List<int>(Enumerable.Range(0, 300));
        private static readonly Dictionary<int, int> idCoolDowns = new Dictionary<int, int>();  //id归还冷却表:归还的id先进此表(不直接入池),值=还需经历的分配次数
        private const int idCoolDownNum = 5;                                                    //冷却值初始为5:每次分配全表值-1,减到0才放回静态池,防止刚归还就被重新取到

        [field: SerializeField] public string currentid { get; private set; }                      //实体id
        [field: SerializeField] public string currentName { get; private set; }                   //当前名称，可能被效果修改
        public string[] currentKey { get; private set; }                    //当前字段
        public string[] currentHavKey { get; protected set; }    //卡名记述
        public int entityType { get; private set; }                      //当前实体卡片类型
        public EntityPlayer owner { get; private set; }                  //原本所有者（卡牌最终归属方,决定送墓/回卡组方向等）
        public EntityPlayer currentOwner { get; private set; }           //当前所有者（控制权,初始与原本所有者相同,可因控制权转移而变化）
        public List<Effection> effections {  get; private set; }        //效果集
        public int bout { get; private set; }                            //在相应区域回合数
        public int isGiveEff { get; private set; }                       //是否加载效果
        [field: SerializeField] public GameManage.CardLocation location { get; private set; }    //位置

        public int isCover;            //是否盖放:  0为否,1为是
        public int isNoEff;            //效果是否被无效化
        public int regTime {  get; private set; }           //进入某区域的回合数:进入时初始化为0,每过一个回合结束+1

        public List<FightLogic.Continue> continues { get; private set; }

        public EntityCard(Card card, EntityPlayer owner)
        {
            this.card = card;
            currentName = card.name;
            currentKey = card.key == null ? null : (string[])card.key.Clone();   //深拷贝
            currentHavKey = card.havingKey == null ? null : (string[])card.havingKey.Clone();
            entityType = card.card_type;
            location = GameManage.CardLocation.Deck;
            isGiveEff = 0;
            this.owner = owner;                       //原本所有者:构造函数中一次性确定
            currentOwner = owner;                     //当前所有者初始与原本所有者相同(控制权)
            isCover = 0;
            isNoEff = 0;
            regTime = 0;
            continues = new List<FightLogic.Continue>();
        }

        //镜像快照复制构造
        protected EntityCard(EntityCard src)
        {
            card = src.card;
            currentid = src.currentid;
            currentName = src.currentName;
            currentKey = src.currentKey == null ? null : (string[])src.currentKey.Clone();
            currentHavKey = src.currentHavKey == null ? null : (string[])src.currentHavKey.Clone();
            entityType = src.entityType;
            location = src.location;
            isGiveEff = src.isGiveEff;
            owner = src.owner;
            currentOwner = src.currentOwner;
            bout = src.bout;
            regTime = src.regTime;
            effections = src.effections == null ? null : new List<Effection>(src.effections);
            continues = new List<FightLogic.Continue>();   //快照不复制永续(UI镜像),仅保证不空引用
        }
        public virtual EntityCard CreateSnapshot()                          //生成自身状态快照副本(子类override以包含扩展字段)
        {
            return new EntityCard(this);
        }

        public void GenerateId()
        {
            //每次分配前:冷却表中所有id剩余次数-1,减到0的id放回静态池等待随机分配
            if (idCoolDowns.Count > 0)
            {
                foreach (int id in idCoolDowns.Keys.ToList())
                {
                    int left = idCoolDowns[id] - 1;
                    if (left <= 0)
                    {
                        idCoolDowns.Remove(id);
                        ints.Add(id);
                    }
                    else
                    {
                        idCoolDowns[id] = left;
                    }
                }
            }
            if (ints.Count <= 0)
                throw new InvalidOperationException("实体id池已耗尽:请检查是否存在id未归还的流程(卡牌每次换区域需1换1)");
            int randomIndex = UnityEngine.Random.Range(0, ints.Count);
            currentid = ints[randomIndex].ToString();
            ints.RemoveAt(randomIndex);
        }
        public void ReleaseId()                             //归还当前id:不直接放回静态池,先进入冷却字典(值初始为冷却次数),防止刚归还又被取到
        {
            if (string.IsNullOrEmpty(currentid)) return;
            if (int.TryParse(currentid, out int id))
                idCoolDowns[id] = idCoolDownNum;
        }
        public void ChangeLocation(GameManage.CardLocation location)
        {
            //被除外/送墓/返回手卡/到场上:归还旧id到静态池,并重新取池中剩余一个值作为实体id
            if (location == GameManage.CardLocation.Hand || location == GameManage.CardLocation.Field
                || location == GameManage.CardLocation.Cemetery || location == GameManage.CardLocation.Banished)
            {
                ReleaseId();
                GenerateId();
            }
            this.location = location;
        }

        public void ResetRegTime()              //进入区域初始化:卡加入任一区域(AddCard/上场)时由区域组件调用,重置进入该区域的回合计数
        {
            regTime = 0;
        }
        public void AddRegTime(int value = 1)   //回合结束递增:由回合结束流程调用,表示卡在当前区域停留过回合数+1
        {
            regTime += value;
        }

        public EntityPlayer GetOwner()                  //获取原本所有者
        {
            return owner;
        }
        public EntityPlayer GetCurrentOwner()           //获取当前所有者（控制权）
        {
            return currentOwner;
        }
        public void SetCurrentOwner(EntityPlayer player)   //控制权转移:仅改变当前所有者,原本所有者不变
        {
            currentOwner = player;
        }

        public void GetEffections(List<Effection> effections)
        {
            this.effections = effections;
        }
        public static EntityMonsterCard GetMonsterSpecialFind(List<EntityCard> entityCards, string currentId)       //根据id查找怪兽实体卡片
        {
            if (entityCards == null || string.IsNullOrEmpty(currentId))
            {
                return null;
            }

            var card = entityCards.FirstOrDefault(c => c.currentid == currentId) as EntityMonsterCard;

            if (card != null && card.entityType == 0)
            {
                return card;
            }

            return null;
        }           

        public void AddContinues(FightLogic.Continue _continue)
        {
            continues.Add(_continue);
        }

    }

    public class EntityMonsterCard : EntityCard
    {
        public int currentAtt { get; set; }                              //当前攻击力
        public int currentDef { get; set; }                              //当前防御力
        public int currentGrade { get; set; }                           //当前等级
        public string currentAttribute { get; set; }                    //当前属性
        public string currentNative { get; set; }                       //当前种族
        public string inGroundWay { get; set; }                        //出场方式（通召，特招，额外正规，额外特招，墓地特招，除外特招，卡组特招）
        public int currentBaseType { get; set; }               //基本类型（通常，效果)
        public int currentType { get; set; }                  //基本类型（主卡组，仪式，融合，同调，超量，连接） 
        public int currentSpecialType { get; set; }          //特殊类型（无，调整，衍生，灵摆, 调整灵摆）
        public OrdSummon ordSummon { get; set; }
        public int showAttOrDef {  get; private set; }      //表示形式:1攻击，0防御
        public int isattack { get; private set; }          //1可以攻击，0不能攻击
        public int beAttacked { get; private set; }          //1可以被攻击，0不能被攻击
        public int changeShow {  get; private set; }        //是否可以更改表示形式
        public EntityMonsterCard(MonsterCard monsterCard, EntityPlayer owner) : base(monsterCard, owner)
        {
            currentAtt = monsterCard.attack;
            currentDef = monsterCard.defense;
            currentGrade = monsterCard.grade;
            currentAttribute = monsterCard.attribute;
            currentNative = monsterCard.native;
            currentBaseType = monsterCard.type;
            currentType = monsterCard.type;
            currentSpecialType = monsterCard.specialType;
            showAttOrDef = 1;
            isattack = 1;
            beAttacked = 1;
        }

        public EntityMonsterCard(EntityMonsterCard src) : base(src)         //怪兽镜像快照复制构造(覆盖怪兽可变字段)
        {
            currentAtt = src.currentAtt;
            currentDef = src.currentDef;
            currentGrade = src.currentGrade;
            currentAttribute = src.currentAttribute;
            currentNative = src.currentNative;
            inGroundWay = src.inGroundWay;
            currentBaseType = src.currentBaseType;
            currentType = src.currentType;
            currentSpecialType = src.currentSpecialType;
            ordSummon = src.ordSummon;
            showAttOrDef = src.showAttOrDef;
            isattack = src.isattack;
            beAttacked = src.beAttacked;
            changeShow = src.changeShow;
        }
        public override EntityCard CreateSnapshot()
        {
            return new EntityMonsterCard(this);
        }

        public void IniOrdSummon(EntityPlayer.Field field)
        {
            ordSummon = new OrdSummon(this, field);
        }
        public void ShowAttOrDef(int showAttOrDef = 1)
        {
            this.showAttOrDef = showAttOrDef;
            beAttacked = 1;
            isattack = 1;
        }
        public void ChangeShow()
        {
            changeShow = -1 * changeShow + 1;

        }
        public void ChangeBeAttacked()
        {
            beAttacked = -1 * beAttacked + 1;
        }
        public void ChangeIsAttack()
        {
            isattack = -1 * isattack + 1;
        }
        public void ResetAttackFlag()                          //重置攻击状态
        {
            isattack = 1;
            beAttacked = 1;
        }
    }

    public class EntityMagicCard : EntityCard
    {
        public int magictype;
        public Cover cover;
        
        public int coverTime;
        public EntityMagicCard(MagicCard magicCard, EntityPlayer owner) : base(magicCard, owner)
        {
            isCover = 0;
        }

        public EntityMagicCard(EntityMagicCard src) : base(src)             //魔法镜像快照复制构造(覆盖魔法可变字段)
        {
            magictype = src.magictype;
            cover = src.cover;
            isCover = src.isCover;
            coverTime = src.coverTime;
        }
        public override EntityCard CreateSnapshot()
        {
            return new EntityMagicCard(this);
        }

        public void IniCover(EntityPlayer.Field field)
        {
            isCover = 1;
            cover = new Cover(this, field);
        }
        public void SetCoverTime()
        {
            coverTime = 0;
        }
    }

    public class EntityTrapCard : EntityCard
    {
        public Cover cover;             
        public int coverTime;          //盖放回合
        public EntityTrapCard(TrapCard trapCard, EntityPlayer owner) : base(trapCard, owner)
        {
            isCover = 0;
        }

        public EntityTrapCard(EntityTrapCard src) : base(src)               //陷阱镜像快照复制构造(覆盖陷阱可变字段)
        {
            cover = src.cover;
            isCover = src.isCover;
            coverTime = src.coverTime;
        }
        public override EntityCard CreateSnapshot()
        {
            return new EntityTrapCard(this);
        }

        public void IniCover(EntityPlayer.Field field)
        {
            isCover = 1;
            cover = new Cover(this, field);
        }
        public void SetCoverTime()
        {
            coverTime = 0;
        }
    }


    [System.Serializable]
    public class Effection
    {
        public EntityCard entityCard { get; private set; }         //指向挂载卡牌实体
        public Func<EntityCard, bool, int, int> Find {  get; private set; }   //查找效果发动次数函数(实体/是否卡名限制(即isCardNameLim==1)/效果序号)
        public Action<EntityCard, bool, int> Update {  get; private set; }    //发动效果后记录发动次数(实体/是否卡名限制/效果序号)

        public List<JudCost> judCosts { get; private set; }        //判断cost列表，满足所有cost才可以发动效果
        public List<CostPay> costPays { get; private set; }        //支付cost列表，发动效果时执行
        public List<Eff> effs { get; private set; }                //效果执行

        [field: SerializeField] public int id { get; private set; } //技能编号:按实例化读写顺序自动填入
        [field: SerializeField] public int judCostsNum {  get; private set; }
        private readonly List<string> judCostParamLogs = new List<string>();   //与judCosts平行的cost判断参数原文(仅用于cost判断失败日志),顺序与judCosts一致
        public Effection(int id, EntityCard entityCard, Func<EntityCard, bool, int, int> Find, Action<EntityCard, bool, int> Update, EventMintor eventMintor, List<StateMachine.TimePointBase> timePointBases = null)
        {
            this.id = id;
            judCosts = new List<JudCost>();
            costPays = new List<CostPay>(); 
            effs = new List<Eff>();
            this.entityCard = entityCard;
            this.Find = Find;
            this.Update = Update;
            this.eventMintor = eventMintor;
            this.timePointBases = timePointBases;

            judCostsNum = judCosts.Count;
        }

        public void AddCost(JudCost judCost, string paramLog = null)   //paramLog=cost判断参数原文(如"JudTime:2&Main1Phase"的":后部分"),供cost判断失败日志输出;非CSV装配(不带原文)场景可省略
        {
            this.judCosts.Add(judCost);
            if (judCostParamLogs != null) judCostParamLogs.Add(paramLog);
            judCostsNum++;
        }                   //取对象cost默认为最后一个
        public void AddPay(CostPay costPay)
        {
            
            this.costPays.Add(costPay);
        }                   
        public void AddEff(Eff eff)
        {
            this.effs.Add(eff);
        }

        public bool EffCost()
        {
            //自肃避免:控制者(entityCard.currentOwner)已处于avoidLimition中任一自肃时,本效果不能发动
            EntityPlayer curOwner = entityCard?.GetCurrentOwner();
            if (avoidLimition != null && avoidLimition.Count > 0 && curOwner != null)
            {
                for (int a = 0; a < avoidLimition.Count; a++)
                {
                    if (TryParseLimition(avoidLimition[a], out int avoidId) && curOwner.HasLimition(avoidId))
                    {
                        return false;
                    }
                }
            }
            if (judCosts == null) return true;
            for (int i = 0; i < judCosts.Count; i++)
            {
                if (!judCosts[i].IsCanPay())
                {
                    return false;
                }
            }
            //发动次数限制:isCardNameLim=1时按原始卡名id累计(同名共享),否则按当前卡id累计(仅本实例);effNumLim>0才启用
            //粘连效果(isLinkEff==1):本效果与isLinkEffID组员共享计数,Find按整组已发动次数合计(判定前需先使组内所有效果各自EffCost均不可再发动)
            if (effNumLim > 0 && Find.Invoke(entityCard, isCardNameLim == 1, id) >= effNumLim)
            {
                return false;
            }
            return true;
        }

        public async UniTask CostPay()
        {
            int costCount = costPays.Count;
            for (int i = 0; i < costCount; i++)
            {
                if (costPays[i] != null)
                {
                    await costPays[i].Pay();
                }
            }
            //取对象结果落库:只要成本末位是GetObject(取对象成本)即把结果存入Objects,供结算时注入IEffGetObject效果。
            //不依赖效果基础参数isGetObject标记(该标记仅用于启动取对象流程,CSV个别效果漏标=1会导致弹窗取了对象但Objects未落库→结算注入null静默失效)
            if (costCount > 0 && costPays[costCount - 1] is GetObject lastGetObject)
            {
                Objects = lastGetObject.GetResults();
            }
            Update?.Invoke(entityCard, isCardNameLim == 1, id);   //记录发动次数(维度与Find一致:卡名限制按卡名id,否则按当前id)
            AddSelfLimition(payLimition, payLimTimeLength);     //固有自肃:支付代价后对控制者(entityCard.currentOwner)产生
        }

        public async UniTask EffAction()                            //效果执行（异步：必须等待所有OnTrigger执行完毕）
        {
            if (entityCard.isNoEff == 1) return ;
            for (int i = 0; i < effs.Count; i++)
            {
                if (effs[i] != null)
                {
                    CardComponent component = effs[i] as CardComponent;
                    //永续拦截(GC):动作入口自查——全局登记处(fightLogic.continues)命中本组件GC编号且生效 → 该动作直接跳过(return)
                    if (component != null && component.IsBlockedByGlobalContinue())
                        continue;
                    //取对象效果:发动阶段(CostPay)已弹窗取好对象存入Objects;结算前注入具体效果类(效果类不弹UI,直接操作注入对象)。
                    //注1:注入对象为快照副本(不带continues),OC剔除须在组件内部"映射回真实实体"后对真实卡逐张进行(见各OnTrigger)
                    //注2:不再按isGetObject标记决定注入(与CostPay落库对称):对象由GetObject成本是否在场决定,Objects为null=未取到/无需对象;组件内部按自身isGetObject参数判断是否消费objects
                    if (effs[i] is IEffGetObject getObjectEff)
                        getObjectEff.SetObjects(Objects);
                    await effs[i].OnTrigger();
                }
            }
            AddLaunchTimePoint();               //效果结算完成：按卡牌类型登记“发动”诱发时点(魔法997/陷阱998/怪兽999)
            AddSelfLimition(effLimition, effLimTimeLength);         //发动后产生的自肃:对控制者(entityCard.currentOwner)产生
        }
        private EntityPlayer GetEffectOwner()               //通过卡牌当前所有者(控制者)找到玩家
        {
            return entityCard?.GetCurrentOwner();
        }
        private void AddSelfLimition(List<string> limitionCodes, List<int> timeLengths)     //给当前控制者添加自肃,limitionCodes为自肃类名称或数字编码
        {
            EntityPlayer owner = GetEffectOwner();
            if (owner == null || limitionCodes == null) return;
            for (int i = 0; i < limitionCodes.Count; i++)
            {
                if (!TryParseLimition(limitionCodes[i], out int limId))
                {
                    continue;
                }
                int time = (timeLengths != null && i < timeLengths.Count) ? timeLengths[i] : 1;   //未配时长默认持续1回合
                owner.AddLimition(limId, time);
            }
        }
        private void AddLaunchTimePoint()                            //登记本次效果发动的诱发时点
        {
            if (timePointBases == null || entityCard == null) return;
            switch (entityCard.entityType)
            {
                case 0:                                             //怪兽发动效果
                    timePointBases.Add(new StateMachine.MonsterEffTimePoint(entityCard));
                    break;
                case 1:                                             //魔法卡发动效果
                    timePointBases.Add(new StateMachine.MagicEffTimePoint(entityCard));
                    break;
                case 2:                                             //陷阱卡发动效果
                    timePointBases.Add(new StateMachine.TrapEffTimePoint(entityCard));
                    break;
            }
        }
        [field: SerializeField] public int isGetObject {  get; private set; }              //是否取对象
        [field: SerializeField] public int orderStackState {  get; private set; }          //进栈状态, 0为无法被怪兽连锁,1为无法被魔法连锁,2为无法被陷阱连锁
                                                                                           //10表示无法被怪兽魔法连锁,20表示无法被怪兽陷阱连锁,21表示无法被魔法陷阱连锁
                                                                                           //3表示无法被连锁,及不再询问玩家是否连锁,改为直接结算,-1为正常询问
        [field: SerializeField] public int effNumLim { get; private set; }                 //单回合发动次数限制
        [field: SerializeField] public int boutFlash { get; private set; }                 //回合更新数
        [field: SerializeField] public int onlyNum { get; private set; }                   //是否全局唯一次数发动
        [field: SerializeField] public int isCardNameLim { get;private set; }              //是否是卡名限制
        [field: SerializeField] public int isTrigger {  get; private set; }                //是否是诱发
        [field: SerializeField] public List<string> effType { get; private set; }          //效果类型
        [field: SerializeField] public List<string> timePoint {  get; private set; }       //需要发动的时点
        [field: SerializeField] public int speed { get; private set; }                     //效果速度
        [field: SerializeField] public List<string> payLimition {  get; private set; }     //固有自肃,在支付代价(在CostPay之间添加)时产生
        [field: SerializeField] public List<int> payLimTimeLength { get; private set; }         //固有自肃时长,与payLimition一一对应
        [field: SerializeField] public List<string> effLimition {  get; private set; }        //产生自肃,发动效果(在EffAction之间添加)之后产生的自肃
        [field: SerializeField] public List<int> effLimTimeLength {  get; private set; }         //产生自肃时长,与effLimition一一对应
        [field: SerializeField] public List<string> avoidLimition {  get; private set; }     //避免自肃,有当前自肃不能发动
        [field: SerializeField] public int isLinkEff { get; private set; }                //是否有粘连效果
        [field: SerializeField] public List<int> isLinkEffID { get; private set; }        //粘连效果编号

        [field: SerializeField] public int isHaveConEff {  get; private set; }           //是否有永续效果
        [field: SerializeField] public string conEffPar { get;private set; }            //永续效果参数
        public List<EntityCard> Objects { get; private set; }                             //CostPay获取到对象
        public void AddBasePar(int isGetObject, int orderStackState, int effNumLim, int boutFlash, int onlyNum, int isCardNameLim, int isTrigger, List<string> effType, List<string> timePoint, int speed,
            List<string> payLimition, List<int> payLimTimeLength, List<string> effLimition, List<int> effLimTimeLength, List<string> avoidLimition,
            int isLinkEff, List<int> isLinkEffID, int isHaveConEff, string conEffPar)
        {
            //前两个参数为新增:isGetObject(是否取对象)/orderStackState(进栈状态,连锁限制,见字段注释),csv从效果基础参数第0/1段读入(旧13段数据缺省0/-1)
            this.isGetObject = isGetObject;
            this.orderStackState = orderStackState;
            this.effNumLim = effNumLim;
            this.boutFlash = boutFlash;
            this.onlyNum = onlyNum;
            this.isCardNameLim = isCardNameLim;
            this.isTrigger = isTrigger;
            this.effType = effType;
            this.timePoint = timePoint;
            this.speed = speed;
            this.payLimition = payLimition ?? new List<string>();
            this.payLimTimeLength = payLimTimeLength ?? new List<int>();
            this.effLimition = effLimition ?? new List<string>();
            this.effLimTimeLength = effLimTimeLength ?? new List<int>();
            this.avoidLimition = avoidLimition ?? new List<string>();
            this.isLinkEff = isLinkEff;
            //防御:非粘连效果(isLinkEff!=1)时isLinkEffID一律为null(即使csv误填也丢弃);仅粘连效果接受组编号,且缺省为空列表
            this.isLinkEffID = isLinkEff == 1 ? (isLinkEffID ?? new List<int>()) : null;
            //末两位:isHaveConEff(是否有永续效果)/conEffPar(永续效果参数),由CSV效果基础参数列&串倒数两段读入(旧数据缺省0/空)
            this.isHaveConEff = isHaveConEff;
            this.conEffPar = conEffPar ?? "";
            if(isHaveConEff == 1)
            {
                string[] ContinueEffs = conEffPar.Split(':');
                for(int i=0;i<ContinueEffs.Length;i=i+2)
                {
                    FightLogic.Continue _continue = Factory.EntityContiue(entityCard, ContinueEffs[i], ContinueEffs[i+1]);
                    entityCard.AddContinues(_continue);
                }
            }
        }

        public List<TriggerBase> triggers { get; private set; }
        public EventMintor eventMintor {  get; private set; }
        public List<StateMachine.TimePointBase> timePointBases { get; private set; }       //诱发时点收集表(结算后由FightLogic.StartTriggerEff统一分发广播)
        public void OnTriggerEff(Action<Effection> UIActionTrigger)                   //绑定诱发效果
        {
            if(isTrigger==0)
                return;
            triggers = Factory.EntityTrigger(timePoint,this);
            for (int i = 0; i < triggers.Count; i++)
            {
                triggers[i].OnTriggerEff();
            }
            this.UIActionTrigger = UIActionTrigger;
        }

        public Action<Effection> UIActionTrigger;            //外部UI调用

        public void GetActionTrigger()
        {
            bool costOk = EffCost();
            if (!costOk) return;
            UIActionTrigger?.Invoke(this);
        }
        public void DeleteTriggerEff()              //去除绑定的诱发效果
        {
            if (isTrigger == 0)
                return;
            for (int i = 0; i < triggers.Count; i++)
            {
                triggers[i].DeleteTriggerEff();
            }
        }
    }

    #region   诱发效果触发器
    public interface TriggerBase
    {
        public void OnTriggerEff();
        public void DeleteTriggerEff();
    }
    public class SelfSpeSom                        //自身特招触发
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public EventMintor.MonsterSpecialSummon monsterSpecialSummon;
        public SelfSpeSom(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
            monsterSpecialSummon = new EventMintor.MonsterSpecialSummon(entityCard);
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnMonsterSpecialSummon(OnActionEff);  
        }
        public void OnActionEff(EventMintor.MonsterSpecialSummon monsterSpecialSummon)
        {

            if (monsterSpecialSummon == null || !monsterSpecialSummon.entityCards.Contains(entityCard)) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null) continue;          //防御:对象池复用/回收时序可能残留无效效果项
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                    {
                        e.GetActionTrigger();
                    }
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnMonsterSpecialSummon(OnActionEff);
        }
    }
    public class SelfSummon                             //自身通召触发
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public SelfSummon(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnMonsterSummon(OnActionEff);
        }
        public void OnActionEff(EventMintor.MonsterSummon monsterSummon)
        {
            if (monsterSummon == null || monsterSummon.entityCards == null || !monsterSummon.entityCards.Contains(entityCard)) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null) continue;          //防御:对象池复用/回收时序可能残留无效效果项
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                    {
                        e.GetActionTrigger();
                    }
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnMonsterSummon(OnActionEff);
        }
    }
    public class SelfInCemetery                        //自身送墓触发
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public EventMintor.Entercemtery entercemtery;
        public SelfInCemetery(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
            entercemtery = new EventMintor.Entercemtery(entityCard, GameManage.CardLocation.Field);
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnEntercemtery(OnActionEff);
        }
        public void OnActionEff(EventMintor.Entercemtery entercemtery)
        {
            if (entercemtery == null || entercemtery.entityCard != entityCard) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null) continue;          //防御:对象池复用/回收时序可能残留无效效果项
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                    {
                        e.GetActionTrigger();
                    }
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnEntercemtery(OnActionEff);
        }
    }
    public class SelfInBanished                       //自身除外触发(仿照SelfInCemetery,自身被除外时诱发)
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public EventMintor.EnterBanished enterBanished;
        public SelfInBanished(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
            enterBanished = new EventMintor.EnterBanished(entityCard, GameManage.CardLocation.Field);
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnEnterBanished(OnActionEff);
        }
        public void OnActionEff(EventMintor.EnterBanished enterBanished)
        {
            if (enterBanished == null || enterBanished.entityCard != entityCard) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null) continue;          //防御:对象池复用/回收时序可能残留无效效果项
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                    {
                        e.GetActionTrigger();
                    }
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnEnterBanished(OnActionEff);
        }
    }
    public class SelfFusionSummon                      //自身融合召唤触发(补全:订阅EventMintor融合召唤成功事件,被融合召唤成功时诱发自身效果)
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public EventMintor.FusionSummon fusionSummon;
        public SelfFusionSummon(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
            fusionSummon = new EventMintor.FusionSummon(entityCard);
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnFusionSummon(OnActionEff);
        }
        public void OnActionEff(EventMintor.FusionSummon fusionSummon)
        {
            bool matched = fusionSummon != null && fusionSummon.entityCards != null && fusionSummon.entityCards.Contains(entityCard);
            if (!matched) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null)
                {
                    continue;                                           //防御:对象池复用/回收时序可能残留无效效果项(isTrigger=0无triggers或空项)
                }
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                    {
                        e.GetActionTrigger();
                    }
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnFusionSummon(OnActionEff);
        }
    }
    public class SelfGetCard                           //自身被从卡组加入手卡触发(检索时点,仿照SelfInCemetery:自身从卡组加入手卡时诱发)
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public EventMintor.GetCard getCard;
        public SelfGetCard(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
            getCard = new EventMintor.GetCard(entityCard, GameManage.CardLocation.Deck);
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnGetCard(OnActionEff);
        }
        public void OnActionEff(EventMintor.GetCard getCard)
        {
            if (getCard == null || getCard.entityCard != entityCard) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null) continue;          //防御:对象池复用/回收时序可能残留无效效果项
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                    {
                        e.GetActionTrigger();
                    }
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnGetCard(OnActionEff);
        }
    }
    public class LeaveEx                              //"自己·对方的卡从额外卡组离开的场合"触发(订阅EventMintor.LeaveExtra事件:该事件由移动结算登记LeaveEx时点→FightLogic分发广播)
        : TriggerBase
    {
        private EventMintor eventMintor;
        private EntityCard entityCard;
        public EventMintor.LeaveExtra leaveExtra;
        public LeaveEx(EventMintor eventMintor, EntityCard entityCard)
        {
            this.eventMintor = eventMintor;
            this.entityCard = entityCard;
        }

        public void OnTriggerEff()
        {
            if (eventMintor == null)
            {
                return;
            }
            eventMintor.AddOnLeaveExtra(OnActionEff);
        }
        public void OnActionEff(EventMintor.LeaveExtra leaveExtra)
        {
            //自己·对方的卡从额外卡组离开:事件本身已限定"离开者来自额外卡组",任一方离开均触发;具体能否发动由GetActionTrigger内的代价判断(如JudLocation自身在场)把关
            if (leaveExtra == null || leaveExtra.entityCard == null) return;
            if (entityCard == null || entityCard.effections == null) return;      //UI已回收且实体效果列表已清理,无效果可触发
            foreach (var e in entityCard.effections)
            {
                if (e == null || e.triggers == null) continue;          //防御:对象池复用/回收时序可能残留无效效果项
                for (int i = 0; i < e.triggers.Count; i++)
                {
                    if (this == e.triggers[i])
                        e.GetActionTrigger();
                }
            }
        }
        public void DeleteTriggerEff()
        {
            eventMintor.DesOnLeaveExtra(OnActionEff);
        }
    }
    #endregion
    /* 区域组件索引: Deck, ExtralDeck, Hand, Cemetery, Banished, Field
       0             1         2       3         4        5
       bool编码12位:前6位(0-5)为自己区域,后6位(6-11)为对方区域,顺序一致 */
    #region cost判断实现
    //永续拦截门:装配时由FightLogic.EntityEff对每个继承Cost/CardComponent的组件注入GC/OC(取组件参数串固定尾两&段)与全局登记处引用
    public interface JudCost                    //判断cost
    {
        public bool IsCanPay();
    }

    public class OrdSummon                                          //通常召唤判断
        : JudCost
    {
        public Func<List<bool>, List<EntityFindComponent>> QuestBools { get; set; }
        public Func<EntityPlayer> QuestEntityPlayer { get; set; }
        private EntityMonsterCard entityMonsterCard;
        private Field field;
        public OrdSummon(EntityMonsterCard EntityCard, EntityPlayer.Field field)
        {
            this.entityMonsterCard = EntityCard;
            this.field = field;
        }
        public bool IsCanPay()
        {
            if (entityMonsterCard.location != GameManage.CardLocation.Hand)
                return false;
            bool level0 = field.GetMonsterZoneCount() < 5 && entityMonsterCard.currentGrade <= 4;
            bool level1 = field.GetMonsterZoneCount() > 0 && (entityMonsterCard.currentGrade == 5 || entityMonsterCard.currentGrade == 6);
            bool level2 = field.GetMonsterZoneCount() > 1 && entityMonsterCard.currentGrade >= 7;
            return level0 || level1 || level2;
        }
        public void PayCost() { }
    }

    public class Cover                                              //盖放判断
        : JudCost
    {
        public Func<List<bool>, List<EntityFindComponent>> QuestBools { get; set; }
        public Func<EntityPlayer> QuestEntityPlayer { get; set; }
        private EntityCard card;
        private EntityPlayer.Field field;
        public Cover(EntityCard card, EntityPlayer.Field field)
        {
            this.card = card;
            this.field = field;
        }
        public bool IsCanPay()
        {
            if (card.location != GameManage.CardLocation.Hand)
                return false;
            return field.GetMagicTrapZoneCount() < 5;
        }
        public void PayCost() { }
    }

    public class IsCanMagic                                         //判断魔法卡发动(魔法卡的通用发动cost:从手卡直接发动/盖放后发动统一在此判定)
        : Cost,JudCost
    {
        public EntityMagicCard entityMagicCard { get;private set; }
        public int magictype {  get; private set; }
        private List<EntityFindComponent> entityFindComponents;
        public IsCanMagic(EntityCard entityCard, Func<List<bool>, List<EntityFindComponent>> QuestBools, Func<int> QuestOwnerPhase)
        {
            EntityMagicCard magicCard = entityCard as EntityMagicCard;
            this.entityMagicCard = magicCard;
            CardBase.MagicCard magic = magicCard.card as CardBase.MagicCard;
            this.magictype = magic.magictype;
            this.QuestBools = QuestBools;
            this.QuestOwnerPhase = QuestOwnerPhase;      //回合归属:从手卡直接发动只允许发生在控制者自己的回合
            bools = NewRegionCode();
            bools[5] = true;                              //需要自己场上
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            //规则1(从手卡直接发动):只允许在控制者自己的回合——对方回合不能从手卡直接发动魔法卡(无回合归属则放行);
            //  已在场上(盖放/表侧在魔陷区/场地)的魔法不受此限,可在对方回合按其自身条件发动
            if (IsActFromHand() && !IsOwnTurn(entityMagicCard))
            {
                return false;
            }
            //规则2(盖放后发动):已盖放在场上的魔法(速攻等)必须盖放满一回合(coverTime>=1,由回合结束递增)后才能发动,盖放当回合不可发动
            if (entityMagicCard.isCover == 1 && entityMagicCard.coverTime < 1)
            {
                return false;
            }
            Value();
            EntityPlayer.Field field = entityFindComponents[0] as EntityPlayer.Field;
            if (field == null) return false;
            //已在魔陷区(场上表侧/盖放满回合后发动自己的效果)不额外占位;从手卡发动需魔陷区有空位(<5)
            if (!field.GetMTZon().Contains(entityMagicCard) && field.GetMagicTrapZoneCount() == 5)
            {
                return false;
            }
            return true;
        }
        protected override void Value()
        {
            entityFindComponents = QuestBools.Invoke(bools);
        }
        private bool IsActFromHand()                          //本次发动是否属于"从手卡直接发动"
        {
            if (entityMagicCard.location != GameManage.CardLocation.Hand) return false;
            //容器为权威:location字段跨区移动/镜像残留时可能滞后为旧值(如Hand),
            //  卡实际已在控制者场上容器内时不按"从手卡发动"处理(与JudLocation的场上容错同策略)
            EntityPlayer curOwner = entityMagicCard.GetCurrentOwner();
            if (curOwner?.field?.cards != null && curOwner.field.cards.Contains(entityMagicCard))
                return false;
            return true;
        }
    }

    public class IsCanTrap                                        //判断陷阱卡发动
        : Cost,JudCost
    {
        public EntityTrapCard entityTrapCard { get; private set; }
        private List<EntityFindComponent> entityFindComponents;
        public IsCanTrap(EntityCard entityCard, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            EntityTrapCard trapCard = entityCard as EntityTrapCard;
            this.entityTrapCard = trapCard;
            this.QuestBools = QuestBools;
            bools = NewRegionCode();
            bools[5] = true;                              //需要自己场上
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            //陷阱卡规则:不能从手卡直接发动,只能先盖放在场上;且盖放当回合不可发动,须盖放满一回合(coverTime>=1,由回合结束递增)后才能发动。
            //(魔法与陷阱的根本区别正在于此——手卡直接发动/盖放当回合发动是魔法卡的发动方式)
            if (entityTrapCard.isCover != 1 || entityTrapCard.coverTime < 1)
            {
                return false;
            }
            Value();
            EntityPlayer.Field field = entityFindComponents[0] as EntityPlayer.Field;
            if (field == null) return false;
            //已在魔陷区(盖放满一回合后发动自己的效果)不额外占位;从手卡发动需魔陷区有空位(<5)——陷阱已盖放,正常都在魔陷区内
            if (!field.GetMTZon().Contains(entityTrapCard) && field.GetMagicTrapZoneCount() == 5)
            {
                return false;
            }
            return true;
        }
        protected override void Value()
        {
            entityFindComponents = QuestBools.Invoke(bools);
        }
    }

    public abstract class Cost
    {
        protected EntityCard entityCard;
        public Func<List<bool>, List<EntityFindComponent>> QuestBools { get; set; }     //通过bool返回区域
        public Func<EntityPlayer> QuestEntityPlayer { get; set; }                       //获取玩家
        public Func<GameManage.GamePhase> QuestGamePhase { get; set; }                  //获取回合阶段
        public Func<int> QuestOwnerPhase { get; set; }                                  //获取谁的回合

        protected Cost(EntityCard entityCard = null)
        {
            this.entityCard = entityCard;
        }

        protected List<bool> bools;                            // 动态读取区域编码

        public List<bool> DeckRead()
        {
            List<bool> bools = NewRegionCode();
            bools[0] = true;
            return bools;
        }

        public List<bool> LocationToBools(List<GameManage.CardLocation> findComponent)
        {
            return RegionLocationsToCode(findComponent);
        }

        protected abstract void Value();

        //组件参数串固定尾两&段:倒数第2段=GC(全局登记处编号,|分隔),倒数第1段=OC(被操作候选卡自身编号,|分隔);空段表示无
        protected List<string> globalContinueIds;            //GC:查fightLogic.continues全局登记处
        protected List<string> operationContinueIds;         //OC:逐个查被操作候选卡自身EntityCard.continues
        public List<FightLogic.Continue> continueRegistry { get; set; }    //全局登记处引用,装配时注入
        public void SetContinueGates(List<string> gc, List<string> oc)
        {
            globalContinueIds = gc;
            operationContinueIds = oc;
        }
        public void SetContinueRegistry(List<FightLogic.Continue> registry)
        {
            continueRegistry = registry;
        }
        //GC:全局登记处存在任一"编号命中且生效(IsHaveEff()==true)"的永续 → 本cost被拦截
        public bool IsBlockedByGlobalContinue()
        {
            if (globalContinueIds == null || globalContinueIds.Count == 0) return false;
            if (continueRegistry == null || continueRegistry.Count == 0) return false;
            foreach (string id in globalContinueIds)
                foreach (FightLogic.Continue con in continueRegistry)
                    if (con != null && con.id == id && con.IsHaveEff()) return true;
            return false;
        }
        //通用判定(回合归属):传入卡牌是否正处于其控制者(当前所有者)的回合。
        //供"仅自己回合可发动/仅自己回合可从手卡发动"一类规则复用(魔法/陷阱等通用);QuestOwnerPhase未注入时放行,避免误拦。
        protected bool IsOwnTurn(EntityCard card)
        {
            if (QuestOwnerPhase == null) return true;
            EntityPlayer cardOwner = card?.GetCurrentOwner();
            return cardOwner == null || cardOwner.GetPlayerIndex() == QuestOwnerPhase.Invoke();
        }
        //OC:单卡命中(候选卡自身持OC编号对应且生效的永续 → 该卡不能作为本次被操作卡,从候选剔除)
        //内置自动拦截:候选卡自身携带 OnlyOne_InField(编号FightLogic.OnlyOneContinueId="0",同名卡在自己场上仅可表侧存在1张)且生效时,
        //无论本组件OC尾段是否声明该编号都会剔除——保证"同名表侧已在场上时,不可再经融合/特招/被操作等方式让第2张出现"全局自动生效
        protected bool IsCardOperationBlocked(EntityCard card)
        {
            //内置拦截:同名表侧永续(静态声明判定,不依赖实例继续是否已实体化,额外/墓地等隐藏区候选同样生效)
            if (FightLogic.IsOnlyOneRestricted(card)) return true;
            if (card?.continues == null) return false;
            foreach (FightLogic.Continue con in card.continues)
            {
                if (con == null || !con.IsHaveEff()) continue;
                if (con.id == FightLogic.OnlyOneContinueId) return true;
                if (operationContinueIds != null && operationContinueIds.Contains(con.id)) return true;
            }
            return false;
        }
        //OC:候选池逐个剔除被拦截卡,返回剩余可用候选(即使无OC声明也执行内置OnlyOne检查)
        protected List<EntityCard> FilterOperationCandidates(List<EntityCard> cards)
        {
            if (cards == null) return null;
            List<EntityCard> res = new List<EntityCard>();
            foreach (EntityCard c in cards)
                if (c != null && !IsCardOperationBlocked(c)) res.Add(c);
            return res;
        }
    } 

    public class JudCostHp                                      //判断生命值cost
        : Cost,JudCost
    {
        private int hpCost;
        private EntityPlayer player;

        public JudCostHp(int hpCost, Func<EntityPlayer> QuestEntityPlayer)
        {
            this.hpCost = hpCost;
            this.QuestEntityPlayer = QuestEntityPlayer;
        }
        protected override void Value()
        {
            player = QuestEntityPlayer?.Invoke();
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            return player.GetPlayerHP() >= hpCost;
        }
    }

    public class JudCostCard                                    //判断手牌数量cost
        : Cost,JudCost
    {
        private int cardHandCost;
        private EntityPlayer player; 
        public JudCostCard(int cardHandCost, Func<EntityPlayer> QuestEntityPlayer)
        {
            this.cardHandCost = cardHandCost;
            this.QuestEntityPlayer = QuestEntityPlayer;
        }
        protected override void Value()
        {
            player = QuestEntityPlayer?.Invoke();
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            bool ok = player != null && player.GetPlayerHandNum() >= cardHandCost;
            return ok;
        }
    }

    public class JudExist                                   //判断存在cost
        : Cost,JudCost
    {
        private List<EntityFindComponent> entityFindComponents;
        private List<GameManage.CardLocation> cardLocations;   //查找地点(为空=全部区域)
        private List<List<string>> cardCondition;              //二维选卡条件
        private string excludeName;                            //排除卡名(该卡名不算命中;留空=不排除)
        private List<string> hitList;                          //命中链

        public JudExist(IniExist iniJudExist, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            this.cardLocations = iniJudExist.cardLocations;
            this.cardCondition = iniJudExist.cardCondition ?? new List<List<string>>();
            this.excludeName = iniJudExist.excludeName;
            this.hitList = iniJudExist.hitList;
            bools = NewRegionCode();
            if (cardLocations != null && cardLocations.Count > 0)
                bools = LocationToBools(cardLocations);
            else
                for (int i = 0; i < RegionCodeLength; i++) bools[i] = true;   //未配置地点=全部区域
            this.QuestBools = QuestBools;
        }
        protected override void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            if (entityFindComponents == null)
            {
                return false;
            }
            foreach (var region in entityFindComponents)                       //任一区域中存在1张满足条件的卡即成立
            {
                if (region?.cards == null || region.cards.Count == 0) continue;
                foreach (var card in region.cards)
                {
                    if (card == null) continue;
                    if (IsCardOperationBlocked(card)) continue;   //OC:候选卡自身持生效永续(本cost的OperationContinues编号)→该卡不能作为本次被操作卡,逐个剔除
                    if (!string.IsNullOrEmpty(excludeName) && card.currentName == excludeName)
                    {
                        continue;   //排除名不算命中
                    }
                    if (Condition2D.IsMatch(card, cardCondition, hitList, region.cards))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public class JudDrawCard                                       //抽卡cost
        : Cost,JudCost
    {
        private Deck deck;
        private int cardNum;

        public JudDrawCard(IniDrawCard iniDrawCard, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            this.cardNum = iniDrawCard.cardNum;
            bools = DeckRead();
            this.QuestBools = QuestBools;
        }
        protected override void Value()
        {
            deck = QuestBools?.Invoke(bools)[0] as Deck;
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            return deck.GetCardNum() >= cardNum;
        }
    }

    public class JudTime                                    //是否可以在当前阶段发动
        :Cost,JudCost
    {
        private int ownerPhase;                             // 0自己回合，1对方回合，2双方回合
        private int currentOwner;

        List<GameManage.GamePhase> gamePhase;
        GameManage.GamePhase currentGamePhase;
        public JudTime(IniTime iniTime, Func<GameManage.GamePhase> QuestGamePhase, Func<int> QuestOwnerPhase)
        {
            ownerPhase = iniTime.ownerPhase;
            gamePhase = iniTime.gamePhase;
            this.QuestGamePhase = QuestGamePhase;
            this.QuestOwnerPhase = QuestOwnerPhase;
        }

        protected override void Value() 
        {
            currentOwner = QuestOwnerPhase.Invoke();
            currentGamePhase = QuestGamePhase.Invoke();
        }

        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            bool isOwner = false;
            if (ownerPhase == 2) isOwner = true;
            else isOwner = ownerPhase == currentOwner;
            return gamePhase.Contains(currentGamePhase) && isOwner;
        }
    }

    public class JudLocation                                //判断卡牌发动地点
        : Cost,JudCost
    {
        private List<GameManage.CardLocation> CardLocation;
        public JudLocation(EntityCard entityCard, IniLocation iniLocation) : base(entityCard)
        {
            CardLocation = iniLocation.cardLocation;
        }
        protected override void Value()
        {

        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            //"场上"地点容错:区域容器为数据权威,location字段在跨区移动/镜像残留时可能滞后为旧值(如Deck/Hand),
            //  此时若实体实际已在当前所有者/原所有者的场上容器(怪兽区/魔陷区/场地)内,仍判为满足"在场上",避免JudLocation误拦已在场的诱发效果。
            if (CardLocation.Contains(GameManage.CardLocation.Field)
                && entityCard != null
                && entityCard.location != GameManage.CardLocation.Field)
            {
                bool curOwnerHit = false;
                EntityPlayer curOwner = entityCard.GetCurrentOwner();
                if (curOwner?.field?.cards != null && curOwner.field.cards.Contains(entityCard))
                    curOwnerHit = true;
                bool originOwnerHit = false;
                EntityPlayer originOwner = entityCard.GetOwner();
                if (originOwner?.field?.cards != null && originOwner.field.cards.Contains(entityCard))
                    originOwnerHit = true;
                if (curOwnerHit || originOwnerHit)
                    return true;
            }
            return CardLocation.Contains(entityCard.location);
        }
        private string DumpSameNameCopies()
        {
            if (entityCard == null || string.IsNullOrEmpty(entityCard.currentName)) return "";
            List<string> lines = new List<string>();
            List<EntityPlayer> players = new List<EntityPlayer>();
            EntityPlayer orig = entityCard.GetOwner();
            EntityPlayer cur = entityCard.GetCurrentOwner();
            if (orig != null && !players.Contains(orig)) players.Add(orig);
            if (cur != null && !players.Contains(cur)) players.Add(cur);
            foreach (var p in players)
            {
                string tag = p == orig ? "原owner" : "现owner";
                ScanSameNameRegion(p.deck, "卡组", tag, lines);
                ScanSameNameRegion(p.extraDeck, "额外卡组", tag, lines);
                ScanSameNameRegion(p.hand, "手牌", tag, lines);
                ScanSameNameRegion(p.cemetery, "墓地", tag, lines);
                ScanSameNameRegion(p.banished, "除外", tag, lines);
                ScanSameNameRegion(p.field, "场上", tag, lines);
            }
            return lines.Count == 0 ? "  (任何区域都无同名实体)" : "\n" + string.Join("\n", lines);
        }
        private void ScanSameNameRegion(EntityFindComponent region, string kind, string ownerTag, List<string> lines)
        {
            if (region == null || region.cards == null || region.cards.Count == 0) return;
            foreach (var c in region.cards)
            {
                if (c == null || c.currentName != entityCard.currentName) continue;
                string sub = "";
                if (region is Field f)
                {
                    if (f.GetMonZon().Contains(c)) sub = "[怪兽区]";
                    else if (f.GetMTZon().Contains(c)) sub = "[魔陷区]";
                }
                lines.Add($"    {kind}({ownerTag})#{c.GetHashCode()}{(c == entityCard ? "[★本绑定实例]" : "")} loc={c.location}{sub}");
            }
        }
    }

    public class JudLastEffCost                             //判断连锁中上一个效果类型
        :Cost, JudCost
    {
        private List<string> effType;                       //允许的效果类型(链顶效果的effOrderType与其中任一一致即通过)
        private Func<StateMachine.EffOrder> QuestEffOrder;  //获取当前连锁命令(链顶效果),连锁栈为空返回null
        private StateMachine.EffOrder curEffOrder;          //Value()刷新得到的链顶效果
        public JudLastEffCost(IniJudLastEffCost iniJudLastEffCost, Func<StateMachine.EffOrder> QuestEffOrder)
        {
            this.effType = iniJudLastEffCost?.effType;
            this.QuestEffOrder = QuestEffOrder;
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            if (curEffOrder?.effOrderType == null)          //连锁栈为空:无上一效果可匹配
                return false;
            if (effType == null || effType.Count == 0)      //未配置允许类型:不限制
                return true;
            foreach (var t in effType)                      //任一配置类型与链顶效果类型一致即通过
                foreach (var ot in curEffOrder.effOrderType)
                    if (ot == t)
                        return true;
            return false;
        }
        protected override void Value()
        {
            curEffOrder = QuestEffOrder?.Invoke();          //读取链顶效果(连锁问询时链顶=上一个已发动且未结算的效果)
        }
    }

    public class FusionSummonCost                          //判断融合
        : Cost, JudCost
    {
        private List<GameManage.CardLocation> fusionSumLoc;              //特招地点
        private List<GameManage.CardLocation> materialLoc;               //融合素材
        private List<bool> fusionSumCode;                                //特招地点编码
        private List<bool> materialCode;                                 //融合素材编码
        private List<EntityFindComponent> fusionSumLocReg;               //特招地点对应区域
        private List<EntityFindComponent> materialLocReg;                //融合素材对应区域

        private List<string> speNameKey;                                       //特殊名字,字段,代表素材必须包含这个名字(卡牌实体名称,而非静态数据)/字段
                                                                               // >=*表示怪兽等级大于等于 , <=*表示怪兽等级小于等于
        private List<int> Name_Key;                                            //与 speNameKey 一一对应: 0=代表卡牌名称,1=代表字段,-1=除外卡名(该卡名的融合怪兽不能被本效果融合召唤);等级条目对应位填0或空格占位
                                                                               // 对应>=* , <=* 表示融合怪兽应大于等于,小于等于对应值
        private int includeSelf;                                        //是否包含自身
        private int maxMaterialCount;                                   //融合素材至多选多少个(>0:目标怪兽素材条件数<=该值才可融;0=不限制)
        public FusionSummonCost(IniFusionSummonCost iniFusionSummonCost, Func<List<bool>, List<EntityFindComponent>> QuestBools, EntityCard entityCard = null) : base(entityCard)
        {
            this.speNameKey = iniFusionSummonCost.speNameKey;
            this.Name_Key = iniFusionSummonCost.Name_Key;
            this.fusionSumLoc = iniFusionSummonCost.fusionSumLoc;
            this.materialLoc = iniFusionSummonCost.materialLoc;
            this.fusionSumCode = LocationToBools(fusionSumLoc);
            this.materialCode = LocationToBools(materialLoc);
            this.includeSelf = iniFusionSummonCost.includeSelf;
            this.maxMaterialCount = iniFusionSummonCost.maxMaterialCount;
            this.QuestBools = QuestBools;
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            if (fusionSumLocReg == null || materialLocReg == null)
                return false;
            List<EntityCard> fusionZoneCards = new List<EntityCard>();
            foreach (var region in fusionSumLocReg)
                if (region?.cards != null)
                    fusionZoneCards.AddRange(region.cards);
            List<EntityCard> materialCards = new List<EntityCard>();
            foreach (var region in materialLocReg)
                if (region?.cards != null)
                    materialCards.AddRange(region.cards);
            if (includeSelf == 1 && entityCard != null && !materialCards.Exists(c => c.currentid == entityCard.currentid))
                materialCards.Add(entityCard);                            //包含自身:效果持有卡自身并入素材池(其所在区域可不在materialLoc中,如"自身+对方场上"类效果)
            //永续拦截(OC):被操作候选逐个剔除——融合目标池/素材池中"自身持OC编号对应且生效永续"(如不能作为融合素材等)的卡不可用;全被剔光则融合判断失败
            fusionZoneCards = FilterOperationCandidates(fusionZoneCards);
            materialCards = FilterOperationCandidates(materialCards);
            if (fusionZoneCards.Count == 0 || materialCards.Count == 0)
                return false;

            foreach (var card in fusionZoneCards)
            {
                EntityMonsterCard monster = card as EntityMonsterCard;
                if (monster == null || monster.currentType != 2)         
                    continue;
                if (!MeetFusionGrade(monster)) continue;                 //不满足等级的融合怪兽不参与融合判断
                if (IsBannedFusionMonster(monster)) continue;            //除外卡名(Name_Key=-1):该怪兽不能被本效果融合召唤,不参与支付判断
                MonsterCard staticMonster = monster.card as MonsterCard;
                if (staticMonster?.SpeMatter == null || staticMonster.SpeMatter.Count == 0)
                    continue;
                if (CanSatisfyMaterial(staticMonster.SpeMatter, materialCards))
                    return true;
            }
            return false;
        }

        private bool CanSatisfyMaterial(Dictionary<string, string[]> conditions, List<EntityCard> materials)
        {
            if (maxMaterialCount > 0 && conditions.Count > maxMaterialCount)
                return false;                                                 //素材张数上限:目标怪兽所需素材条件数超过上限则不可融合(0=不限制)
            if (includeSelf == 1 && (entityCard == null || !materials.Contains(entityCard)))
                return false;                                                 //包含自身:效果持有卡不在素材区则不可能凑齐素材
            KeyValuePair<string, string[]>[] conds = conditions.ToArray();
            List<EntityCard> used = new List<EntityCard>();
            return BackTrackMatch(conds, 0, materials, used);
        }
        private bool BackTrackMatch(KeyValuePair<string, string[]>[] conds, int index, List<EntityCard> materials, List<EntityCard> used)
        {
            if (index == conds.Length)
            {
                if (includeSelf == 1 && !used.Contains(entityCard))           //包含自身:该组合必须用到效果持有卡,否则不通过
                    return false;
                return AnySpecialUsed(used);                                  //已使用的素材满足任一特殊名字/字段条件即可
            }
            for (int i = 0; i < materials.Count; i++)
            {
                EntityCard m = materials[i];
                if (used.Contains(m))
                    continue;
                if (!IsMaterialMatch(conds[index], m))
                    continue;
                used.Add(m);
                if (BackTrackMatch(conds, index + 1, materials, used))
                    return true;
                used.Remove(m);
            }
            return false;
        }
        // 解析 speNameKey 等级条目:">=N" / "<=N"; 是则返回 true(ge=true表示大于等于,false表示小于等于;bound=等级阈值)
        private static bool TryParseGradeKey(string key, out bool ge, out int bound)
        {
            ge = false;
            bound = 0;
            if (string.IsNullOrEmpty(key)) return false;
            if (key.StartsWith(">=")) { if (int.TryParse(key.Substring(2), out bound)) { ge = true; return true; } return false; }
            if (key.StartsWith("<=")) { if (int.TryParse(key.Substring(2), out bound)) { ge = false; return true; } return false; }
            return false;
        }
        // 融合怪兽是否满足 speNameKey 中全部等级约束(>=N / <=N); 无等级条目恒真(等级约束作用于融合怪兽,素材仍走AnySpecialUsed的名字/字段匹配)
        private bool MeetFusionGrade(EntityMonsterCard monster)
        {
            if (monster == null || speNameKey == null) return true;
            foreach (var key in speNameKey)
            {
                if (!TryParseGradeKey(key, out bool ge, out int bound)) continue;
                if (ge ? monster.currentGrade < bound : monster.currentGrade > bound) return false;
            }
            return true;
        }
        private bool AnySpecialUsed(List<EntityCard> used)                    //已使用素材是否满足任一特殊名字/字段条件
        {
            if (speNameKey == null || speNameKey.Count == 0)
                return true;                                                  //未配置特殊条件,不限制
            bool hasNameCondition = false;                                    //是否存在真正要求素材匹配的名字/字段条件
            for (int i = 0; i < speNameKey.Count; i++)
            {
                string key = speNameKey[i];
                if (string.IsNullOrEmpty(key) || TryParseGradeKey(key, out _, out _))
                    continue;                                                 //等级条目(>=N/<=N)作用于融合怪兽等级,不约束素材
                int mode = (Name_Key != null && i < Name_Key.Count) ? Name_Key[i] : 0;
                if (mode == -1) continue;                                                //除外卡名(Name_Key=-1):不要求素材包含,仅用于排除对应融合怪兽
                hasNameCondition = true;                                      //存在真实的素材名字/字段要求
                foreach (var m in used)
                {
                    if (mode == 0)
                    {
                        if (m.currentName == key)                             //按卡名匹配
                            return true;
                    }
                    else if (m.currentKey != null && m.currentKey.Contains(key))  //按字段匹配
                        return true;
                }
            }
            if (!hasNameCondition) return true;                               //素材无任何名字/字段要求(仅限制目标等级/排除卡名时),素材不受约束
            return false;                                                     //所有特殊条件均未被满足
        }
        private bool IsBannedFusionMonster(EntityMonsterCard monster)         //除外卡名(Name_Key对应-1):该卡名的融合怪兽不能被本效果融合召唤
        {
            if (monster == null || speNameKey == null) return false;
            for (int i = 0; i < speNameKey.Count; i++)
            {
                string key = speNameKey[i];
                if (string.IsNullOrEmpty(key) || TryParseGradeKey(key, out _, out _)) continue;  //等级条目跳过
                int mode = (Name_Key != null && i < Name_Key.Count) ? Name_Key[i] : 0;
                if (mode != -1) continue;                                    //仅"除外卡名"(-1)参与排除
                if (monster.currentName == key) return true;                 //按卡名精确排除:该融合怪兽不能经本效果融合召唤
            }
            return false;
        }
        private bool IsMaterialMatch(KeyValuePair<string, string[]> condition, EntityCard m)
        {
            EntityMonsterCard monster = m as EntityMonsterCard;
            switch (condition.Key)
            {
                case "name":                                   //卡名精确匹配
                    return condition.Value.Contains(m.currentName);
                case "type":                                   //卡类型（主卡组0/仪式1/融合2/同调3/超量4/连接5）
                    return monster != null && condition.Value.Contains(monster.currentType.ToString());
                case "attribute":                              //属性
                    return monster != null && condition.Value.Contains(monster.currentAttribute);
                case "native":                                 //种族
                    return monster != null && condition.Value.Contains(monster.currentNative);
                case "baseType":                               //基本类型（通常/效果）
                    return monster != null && condition.Value.Contains(monster.currentBaseType.ToString());
                case "grade":                                  //星级
                    return monster != null && condition.Value.Contains(monster.currentGrade.ToString());
                default:
                    return false;
            }
        }

        protected override void Value()
        {
            fusionSumLocReg = QuestBools.Invoke(fusionSumCode);
            materialLocReg = QuestBools.Invoke(materialCode);
        }
    }

    public class SelfJudregTime                             //判断自身效果:判定"效果持有卡(自身)"当前所在位置+在该位置的停留回合数——只针对自身卡,不扫全区域
        : Cost, JudCost
    {
        private List<GameManage.CardLocation> cardLocations;   //自身允许所在位置(为空=全部区域不限制;对方区域用e_前缀)
        private int needTurns;                                 //回合门槛:0=本回合进入模式(regTime==0,自身进入该区域后尚未经过自身回合结束);>=1=停留回合模式(regTime>=needTurns)

        public SelfJudregTime(IniSelfJudregTime iniSelfJudregTime, EntityCard entityCard = null) : base(entityCard)
        {
            if (iniSelfJudregTime == null)
                throw new ArgumentException("SelfJudregTime:初始化数据IniSelfJudregTime为空,请检查cost参数配置");
            this.needTurns = Math.Max(0, iniSelfJudregTime.needTurns);   //负数防错按0(本回合进入模式)
            this.cardLocations = iniSelfJudregTime.cardLocations;
        }
        protected override void Value()
        {
            //自身卡为实时实体,location/regTime字段已随换区实时维护,无需区域查询
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            if (entityCard == null) return false;                       //无自身卡可判定:不满足
            //1 位置对比:未配置位置(空)=全部区域不限制;配置后自身当前location须命中其一
            if (cardLocations != null && cardLocations.Count > 0 && !cardLocations.Contains(entityCard.location)) return false;
            //2 回合数对比:0=本回合进入该位置(进入区域regTime置0,每过自身回合结束+1);>=1=在该位置已停留达到needTurns个回合
            return needTurns <= 0 ? entityCard.regTime == 0 : entityCard.regTime >= needTurns;
        }
    }

    public class JudregTime                                //判断"本回合进入过某区域的卡"或"已存在达到回合数的卡"
        : Cost, JudCost
    {
        private List<EntityFindComponent> entityFindComponents;
        private int existTurns;                                 //0=本回合登记模式(查该区域"本回合进入"登记表);>0=停留回合模式(regTime>=existTurns才算命中,regTime=进入后经历的回合结束数,进入=0每过回合结束+1)
        private List<GameManage.CardLocation> cardLocations;    //存在地点(为空=全部区域,含对方区域)
        private List<List<string>> cardCondition;               //二维选卡条件(大组下标/编码与JudExist/Condition2D共用,留空=不限制任何卡)
        public JudregTime(IniJudregTime iniJudregTime, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            if (iniJudregTime == null)
                throw new ArgumentException("JudregTime:初始化数据IniJudregTime为空,请检查cost参数配置");
            this.existTurns = Math.Max(0, iniJudregTime.existTurns);   //0=本回合进入登记模式;>=1=停留回合门槛(负数防错按0处理)
            this.cardLocations = iniJudregTime.cardLocations;
            this.cardCondition = iniJudregTime.cardCondition ?? new List<List<string>>();
            bools = NewRegionCode();
            if (cardLocations != null && cardLocations.Count > 0)
                bools = LocationToBools(cardLocations);
            else
                for (int i = 0; i < RegionCodeLength; i++) bools[i] = true;   //未配置地点=全部区域
            this.QuestBools = QuestBools;
        }
        protected override void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }
        public bool IsCanPay()
        {
            //永续拦截(GC):全局登记处命中本cost GC编号且生效 → 判断失败
            if (IsBlockedByGlobalContinue())
                return false;
            Value();
            if (entityFindComponents == null)
            {
                return false;
            }
            return existTurns == 0 ? IsCanPayBoutSent() : IsCanPayStayTurns();
        }

        //本回合登记模式(existTurns=0):查匹配区域的"本回合进入"登记表(登记表已上提至区域基类,故任意区域均可配置)。
        //登记表=本回合(当前进行中的回合,随回合界限在对方开始前清空)真正进入过该区域的卡牌快照副本,卡进入后即使离开该区域记录仍保留,
        //故可表达"本回合有[满足选卡条件]的卡进入过该区域"(如墓地"这个回合有融合怪兽被送去自己墓地");条件大组3/4/5回查怪兽类型时以登记表自身作候选池(快照currentid自洽)
        private bool IsCanPayBoutSent()
        {
            foreach (var region in entityFindComponents)                       //任一登记卡满足选卡条件即成立
            {
                if (region == null) continue;
                List<EntityCard> boutSent = region.GetBoutSentSnapshots();
                if (boutSent == null || boutSent.Count == 0) continue;
                foreach (var card in boutSent)
                {
                    if (card == null) continue;
                    if (IsCardOperationBlocked(card)) continue;   //OC:候选卡自身持生效永续(本cost的OperationContinues编号)→不能作为本次被操作卡,逐个剔除
                    if (Condition2D.IsMatch(card, cardCondition, null, boutSent))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //停留回合模式(existTurns>=1):任一区域中存在1张"停留达到回合数且满足选卡条件"的卡即成立
        private bool IsCanPayStayTurns()
        {
            foreach (var region in entityFindComponents)
            {
                if (region?.cards == null || region.cards.Count == 0) continue;
                foreach (var card in region.cards)
                {
                    if (card == null || card.regTime < existTurns) continue;   //进入该区域未达到所需回合数:不算命中
                    if (IsCardOperationBlocked(card)) continue;   //OC:候选卡自身持生效永续(本cost的OperationContinues编号)→不能作为本次被操作卡,逐个剔除
                    if (Condition2D.IsMatch(card, cardCondition, null, region.cards))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }


    #endregion


    #region  执行支付
    public interface CostPay
    {
        public UniTask Pay();
        public void Value();
    }

    public abstract class PayComponent
    {
        public EntityCard entityCard;
        protected List<bool> bools;                            // 动态读取区域编码

        public Func<List<bool>, List<EntityFindComponent>> QuestBools { get; set; }
        public Func<EntityPlayer> QuestEntityPlayer { get; set; }

        public ExtraQuest extraQuest {  get; set; }
        protected PayComponent(EntityCard entityCard = null)
        {
            this.entityCard = entityCard;
        }

        public List<bool> DeckRead()
        {
            List<bool> bools = NewRegionCode();
            bools[0] = true;
            return bools;
        }

        public List<bool> CemeteryRead()
        {
            List<bool> bools = NewRegionCode();
            bools[3] = true;
            return bools;
        }
        public List<bool> FeildRead()
        {
            List<bool> bools = NewRegionCode();
            bools[5] = true;
            return bools;
        }
        public List<bool> AllRead()
        {
            List<bool> bools = NewRegionCode();
            for (int i = 0; i < RegionCodeLength; i++)
            {
                bools[i] = true;
            }
            return bools;
        }
        public List<bool> LocationToBools(List<GameManage.CardLocation> findComponent)
        {
            return RegionLocationsToCode(findComponent);
        }
        public int LocToInt(GameManage.CardLocation cardLocation)
        {
            switch(cardLocation)
            {
                case GameManage.CardLocation.Deck:
                    return 0;
                case GameManage.CardLocation.ExtraDeck:
                    return 1;
                case GameManage.CardLocation.Hand:
                    return 2;
                case GameManage.CardLocation.Cemetery:
                    return 3;
                case GameManage.CardLocation.Banished:
                    return 4;
                case GameManage.CardLocation.Field:
                    return 5;
                default:
                    throw new Exception($"输入错误cardLocation：{cardLocation}");
            }
        }
    }

    public class MagicActBase
        : PayComponent, CostPay
    {
        private List<EntityFindComponent> entityFindComponents;
        public MagicActBase(EntityCard entityCard, Func<List<bool>, List<EntityFindComponent>> QuestBools, ExtraQuest extraQuest):base(entityCard)
        {
            bools = FeildRead();
            this.QuestBools = QuestBools;
            this.extraQuest = extraQuest;
        }
        public async UniTask Pay()
        {
            Value();
            Transform transform = ExtraQuest.OneEntityCardToTransfrom(entityCard);
            EntityPlayer.Field field = entityFindComponents[0] as EntityPlayer.Field;
            //从手卡发动魔法:卡先离开手牌并落入自己魔陷区(发动动作即放置在魔陷区);
            //已在魔陷区(场上盖放满一回合后发动等):不重复加入,仅保留一次
            if (field != null && !field.GetMTZon().Contains(entityCard))
            {
                if (entityCard.location == GameManage.CardLocation.Hand)
                {
                    EntityPlayer owner = entityCard.GetCurrentOwner();
                    if (owner?.hand != null) owner.hand.RemoveCard(entityCard);      //离手
                    entityCard.ChangeLocation(GameManage.CardLocation.Field);        //标记已上场(发动中先占位,结算后按效果送墓或留场)
                }
                field.AddMagicTrap(entityCard);
            }
            await extraQuest.HandleTransfrom(new List<EntityCard>() { entityCard }, new List<Transform>() { transform });
        }
        public void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }
    }

    public class TrapActBase                                    //陷阱卡发动放置
        : PayComponent, CostPay
    {
        private List<EntityFindComponent> entityFindComponents;
        public TrapActBase(EntityCard entityCard, Func<List<bool>, List<EntityFindComponent>> QuestBools, ExtraQuest extraQuest):base(entityCard)
        {
            bools = FeildRead();
            this.QuestBools = QuestBools;
            this.extraQuest = extraQuest;
        }
        public async UniTask Pay()
        {
            Value();
            Transform transform = ExtraQuest.OneEntityCardToTransfrom(entityCard);
            EntityPlayer.Field field = entityFindComponents[0] as EntityPlayer.Field;
            if (!field.GetMTZon().Contains(entityCard))         //已盖放在场上则不重复加入魔陷区
                field.AddMagicTrap(entityCard);
            await extraQuest.HandleTransfrom(new List<EntityCard>() { entityCard }, new List<Transform>() { transform });
        }
        public void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }
    }

    public class GoToCemetery                                   //进入墓地,自己
        : PayComponent, CostPay
    {
        private List<EntityFindComponent> entityFindComponents;

        private List<EntityCard> entities;                       //送去墓地等卡列表

        private int isEntityCard;                               //是否是卡牌本身

        public GoToCemetery(EntityCard entityCard,IniGoToCemetery iniGoToCemetery,Func<List<bool>, List<EntityFindComponent>> QuestBools, ExtraQuest extraQuest) :base(entityCard)
        {
            isEntityCard = iniGoToCemetery.isEntityCard;
            this.QuestBools = QuestBools;
            this.extraQuest = extraQuest;
            bools = AllRead();
        }
        public void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }

        public async UniTask Pay()
        {
            Value();
            if (isEntityCard >= 1)
            {
                entities = new List<EntityCard>();
                entities.Add(entityCard);
            }
            if (entities == null || entities.Count == 0) return;
            //统一走公共送墓入口:数据层区域移动+InCemetery/LeaveEx诱发时点登记+预制体UI同步(事件广播订阅机制不变)
            await SendCardToCemetery(entities, entityFindComponents, extraQuest);
        }

        public void GetEntities(List<EntityCard> entities)
        {
            this.entities = entities;
        }
    }

    public class GoToCemeteries                                 //其他卡进入墓地
        : PayComponent, CostPay
    {
        private GoToCemetery goToCemetery;                      //持有送墓(统一执行数据层移动与UI同步)
        private List<EntityFindComponent> entityFindComponents; //从哪里选卡
        private List<EntityCard> entities;                       //送去墓地等卡列表

        private int isEntityCard;                               //是否连效果持有卡自身也送去墓地(>=1:自身不经选择直接加入送墓列表)
        private List<List<string>> cardCondition;                     //选卡条件(二维,为空不做限制)

        public List<string> hitList {  get; private set; }         //命中链
        private int num;                                        //需选择并送墓的张数

        public GoToCemeteries(EntityCard entityCard, IniGoToCemeteries iniGoToCemeteries, Func<List<bool>, List<EntityFindComponent>> QuestBools, ExtraQuest extraQuest) : base(entityCard)
        {
            if (iniGoToCemeteries == null)
                throw new ArgumentException("GoToCemeteries:初始化数据IniGoToCemeteries为空,请检查costpay参数配置");
            this.num = Math.Max(1, iniGoToCemeteries.num);
            this.isEntityCard = iniGoToCemeteries.isEntityCard;
            this.cardCondition = iniGoToCemeteries.cardCondition ?? new List<List<string>>();
            this.hitList = iniGoToCemeteries.hitList;           //命中链:null=默认(大组间"且",组内任一),非null=元素间"或"
            this.QuestBools = QuestBools;
            this.extraQuest = extraQuest;
            bools = (iniGoToCemeteries.findComponent == null || iniGoToCemeteries.findComponent.Count == 0)
                ? AllRead()                                   //未配置区域:全部区域(含对方0-11)可作为送墓对象来源
                : LocationToBools(iniGoToCemeteries.findComponent);
            //持有送墓组件:自身是否送墓(isEntityCard)由本类在Pay中处理,故此处GoToCemetery不自动含自身,统一经GetEntities注入选中卡后执行送墓与UI移动
            goToCemetery = new GoToCemetery(entityCard, new IniGoToCemetery(0), QuestBools, extraQuest);
        }

        public void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }

        public async UniTask Pay()
        {
            Value();
            if (entityFindComponents == null || entityFindComponents.Count == 0) return;

            entities = new List<EntityCard>();
            if (isEntityCard >= 1 && entityCard != null)                //效果持有卡自身也送去墓地:不经选择直接加入送墓列表
                entities.Add(entityCard);

            //1 收集候选:配置区域内的卡(排除自身,自身已由isEntityCard决定),再经IsMatchCondition(cardCondition+hitList)过滤
            List<EntityCard> candidates = new List<EntityCard>();
            foreach (var region in entityFindComponents)
            {
                if (region?.cards == null) continue;
                foreach (var card in region.cards)
                {
                    if (card == null || card == entityCard) continue;
                    if (!Condition2D.IsMatch(card, cardCondition, hitList, region.cards)) continue;   //region.cards作poolCards供怪兽维度3/4/5查对应怪兽实体
                    if (!candidates.Contains(card)) candidates.Add(card);   //跨区域同实例防御
                }
            }
            if (candidates.Count < num) return;                             //候选不足需求数:无法完成送墓(不支付该代价)

            //2 弹UI选卡:即使候选数量恰好等于需求数(如仅1张)也弹出UI,由玩家亲自点击确认后才作为送墓对象
            List<EntityCard> chooseRes = (extraQuest != null && extraQuest.GetEntityCard != null)
                ? await extraQuest.GetEntityCard.Invoke(candidates, num)
                : candidates.GetRange(0, num);
            if (chooseRes == null || chooseRes.Count < num) return;         //玩家取消或未选满:不送墓

            //3 弹窗实体映射回真实实体(数据一致但实例可能不同,保证区域移除与UI移动命中),连同自身注入持有GoToCemetery执行送墓
            foreach (var card in chooseRes)
            {
                EntityCard real = (extraQuest != null && extraQuest.MapRealCard != null) ? (extraQuest.MapRealCard(card) ?? card) : card;
                if (real != null && !entities.Contains(real)) entities.Add(real);
            }
            if (entities.Count == 0) return;
            goToCemetery.GetEntities(entities);
            await goToCemetery.Pay();
        }

        //二维匹配逻辑已上移为共享静态求值器 EffLogic.Condition2D.IsMatch(card, cardCondition, hitList, poolCards),
        //本类在Pay收集候选时按所在区域传入poolCards(供怪兽维度3/4/5按currentid查对应怪兽实体),语义与维度注释见Condition2D
    }

    public class CostCard                                      //丢弃手卡costPay
        : PayComponent, CostPay
    {
        private int num;                                        //需选择并丢弃的手牌张数(>=1)
        private List<List<string>> cardCondition;               //二维选卡条件(大组下标/编码与GoToCemeteries、JudExist共用,见EffLogic.Condition2D;留空=不限制任何卡)
        private List<string> hitList;                           //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        private GoToCemetery goToCemetery;                      //丢弃送墓(统一执行数据层移动与UI同步,与GoToCemeteries共用)

        public CostCard(EntityCard entityCard, IniCostCard iniCostCard, Func<EntityPlayer> QuestEntityPlayer, Func<List<bool>, List<EntityFindComponent>> QuestBools, ExtraQuest extraQuest) : base(entityCard)
        {
            if (iniCostCard == null)
                throw new ArgumentException("CostCard:初始化数据IniCostCard为空,请检查costpay参数配置");
            this.num = Math.Max(1, iniCostCard.num);
            this.cardCondition = iniCostCard.cardCondition ?? new List<List<string>>();
            this.hitList = iniCostCard.hitList;
            this.QuestEntityPlayer = QuestEntityPlayer;      //丢弃手牌的所有者=当前操作玩家(与JudCostCard手牌数判断同源)
            this.QuestBools = QuestBools;
            this.extraQuest = extraQuest;
            goToCemetery = new GoToCemetery(entityCard, new IniGoToCemetery(0), QuestBools, extraQuest);
        }

        public void Value()
        {
            //无自有区域组件:候选直接取自QuestEntityPlayer(当前操作玩家)手牌,丢弃移动由持有GoToCemetery按卡location执行
        }

        public async UniTask Pay()
        {
            EntityPlayer player = QuestEntityPlayer?.Invoke();          //丢弃手牌的所有者=当前操作玩家(与JudCostCard手牌数判断同源)
            if (player?.hand?.cards == null || player.hand.cards.Count == 0)
            {
                return;
            }

            //1 收集候选:玩家手牌中的卡(排除效果持有卡自身与不满足二维条件);候选不足需求数则无法支付该代价
            List<EntityCard> candidates = new List<EntityCard>();
            foreach (var card in player.hand.cards)
            {
                if (card == null || card == entityCard) continue;
                if (!Condition2D.IsMatch(card, cardCondition, hitList, player.hand.cards)) continue;   //hand.cards作poolCards供怪兽维度3/4/5查对应怪兽实体
                if (!candidates.Contains(card)) candidates.Add(card);   //同实例防御
            }
            if (candidates.Count < num)
            {
                return;
            }

            //2 弹UI选卡:即使候选数量恰好等于需求数也弹出UI,由玩家亲自点击确认后才作为丢弃对象
            List<EntityCard> chooseRes = (extraQuest != null && extraQuest.GetEntityCard != null)
                ? await extraQuest.GetEntityCard.Invoke(candidates, num)
                : candidates.GetRange(0, num);
            if (chooseRes == null || chooseRes.Count < num)
            {
                return;     //玩家取消或未选满:不丢弃
            }

            //3 弹窗实体映射回真实实体(数据一致但实例可能不同,保证区域移除与UI移动命中),注入持有GoToCemetery执行丢弃送墓
            List<EntityCard> entities = new List<EntityCard>();
            foreach (var card in chooseRes)
            {
                EntityCard real = (extraQuest != null && extraQuest.MapRealCard != null) ? (extraQuest.MapRealCard(card) ?? card) : card;
                if (real != null && !entities.Contains(real)) entities.Add(real);
            }
            if (entities.Count == 0)
            {
                return;
            }
            goToCemetery.GetEntities(entities);
            await goToCemetery.Pay();
        }
    }

    public class GetObject                                      //取对象的costPay
        : PayComponent,CostPay
    {
        private int num;                                        //取对象数量(需在弹窗中选择的张数)
        private List<EntityFindComponent> entityFindComponents; //取对象的区域
        private List<List<string>> cardCondition;               //二维选卡条件(大组下标/编码与GoToCemeteries、JudExist共用,见Condition2D;留空=不限制任何卡)
        private int excludeSelf;                                //>=1排除效果持有卡自身,0允许选择自身
        private List<string> hitList;                           //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)

        private List<EntityCard> results;                       //选卡的结果,要保存副本,以免判断对象是否变化

        public GetObject(EntityCard entityCard, IniGetObject iniGetObject, Func<List<bool>, List<EntityFindComponent>> QuestBools, ExtraQuest extraQuest) : base(entityCard)
        {
            if (iniGetObject == null)
                throw new ArgumentException("GetObject:初始化数据IniGetObject为空,请检查costpay参数配置");
            this.num = iniGetObject.num;
            this.cardCondition = iniGetObject.cardCondition ?? new List<List<string>>();
            this.excludeSelf = iniGetObject.excludeSelf;
            this.hitList = iniGetObject.hitList;
            this.QuestBools = QuestBools;
            this.extraQuest = extraQuest;
            bools = (iniGetObject.findComponent == null || iniGetObject.findComponent.Count == 0)
                ? AllRead()                                   //未配置区域:全部区域可被取为对象
                : LocationToBools(iniGetObject.findComponent);
        }

        public void Value()
        {
            entityFindComponents = QuestBools?.Invoke(bools);
        }

        public async UniTask Pay()
        {
            Value();
            results = new List<EntityCard>();
            if (entityFindComponents == null || entityFindComponents.Count == 0)
                return;

            //1 收集候选:配置区域内的卡 + 种类限制 + 排除自身
            List<EntityCard> candidates = new List<EntityCard>();
            foreach (var region in entityFindComponents)
            {
                if (region?.cards == null) continue;
                foreach (var card in region.cards)
                {
                    if (card == null) continue;
                    if (excludeSelf >= 1 && card == entityCard) continue;                              //排除自身
                    if (!Condition2D.IsMatch(card, cardCondition, hitList, region.cards)) continue;    //二维选卡条件(cardCondition为空=不限制)
                    if (!candidates.Contains(card)) candidates.Add(card);                              //跨区域同实例防御
                }
            }
            if (candidates.Count < num) return;                                 //候选不足需求数:无法完成取对象,结果留空

            //2 弹UI选择:即使候选数量恰好等于需求数(如仅1张)也弹出UI,由玩家亲自点击确认后才作为结果
            List<EntityCard> chooseRes = (extraQuest != null && extraQuest.GetEntityCard != null)
                ? await extraQuest.GetEntityCard.Invoke(candidates, num)
                : candidates.GetRange(0, num);
            if (chooseRes == null || chooseRes.Count < num)                     //玩家取消或未选满:视为未取到对象,结果留空
            {
                results = new List<EntityCard>();
                return;
            }
            //3 保存结果为真实实体的快照副本:先把弹窗实体归一到真实实体作为基线(保证副本与真实卡currentid一致,便于回溯对照),
            //   再生成独立镜像副本——之后真实实体移动/数值变化不影响副本,反向修改副本也不污染真实实体
            results = new List<EntityCard>();
            foreach (var card in chooseRes)
            {
                EntityCard real = extraQuest?.MapRealCard != null ? (extraQuest.MapRealCard(card) ?? card) : card;
                if (real != null)
                    results.Add(real.CreateSnapshot());
            }
        }

        public List<EntityCard> GetResults()                                   //外部(后续效果/对象变化判定)读取本次取到的对象(返回副本)
        {
            return results == null ? new List<EntityCard>() : new List<EntityCard>(results);
        }
    }

    #endregion

    #region 效果实现

    public interface Eff
    {
        public void IniCardCompent();   // 初始化效果属性，参数为工厂模式初始化类和委托
        public UniTask OnTrigger();     // 触发效果（异步：可能包含等待，必须等待执行完毕）
        public List<EntityCard> GetObjects();       //获取对象
    }
    public interface IEffGetObject                                     //取对象效果标记
    {
        public void SetObjects(List<EntityCard> objects);
    }

    public abstract class CardComponent 
    {
        protected int num;                                             //选择数量
        protected List<EntityCard> entityCards;                        //UI卡片数据
        protected List<bool> entityFindComponentsCode;                 //动态读取区域编
        protected EntityCard entityCard;                               //效果持有卡(效果来源实体:用于排除自身等判定/决定抽卡方等,由子类构造函数注入)

        public Func<List<bool>, List<EntityFindComponent>> QuestBools { get; set; }
        public Func<EntityPlayer> QuestEntityPlayer { get; set; }

        public ExtraQuest extraQuest {  get; set; }

        public CardComponent(ExtraQuest extraQuest = null)
        {
            entityFindComponentsCode = NewRegionCode();
           this.extraQuest = extraQuest;
        }



        public List<bool> DeckRead()
        {
            List<bool> bools = NewRegionCode();
            bools[0] = true;
            return bools;
        }
        public List<bool> AllRead()
        {
            List<bool> bools = NewRegionCode();
            for (int i = 0; i < RegionCodeLength; i++)
            {
                bools[i] = true;
            }
            return bools;
        }
        public List<bool> LocationToBools(List<GameManage.CardLocation> findComponent)
        {
            return RegionLocationsToCode(findComponent);
        }


        public int LocToInt(GameManage.CardLocation cardLocation)
        {
            switch (cardLocation)
            {
                case GameManage.CardLocation.Deck:
                    return 0;
                case GameManage.CardLocation.ExtraDeck:
                    return 1;
                case GameManage.CardLocation.Hand:
                    return 2;
                case GameManage.CardLocation.Cemetery:
                    return 3;
                case GameManage.CardLocation.Banished:
                    return 4;
                case GameManage.CardLocation.Field:
                    return 5;
                default:
                    throw new Exception($"输入错误cardLocation：{cardLocation}");
            }
        }

        //取对象模式对象变化判定:注入副本与真实实体共用currentid,在给定区域中按currentid回溯对照真实实体
        protected EntityCard FindRealCardById(List<EntityFindComponent> regions, string currentid)
        {
            if (regions == null || string.IsNullOrEmpty(currentid)) return null;
            foreach (var region in regions)
            {
                if (region?.cards == null) continue;
                foreach (var card in region.cards)
                {
                    if (card != null && card.currentid == currentid) return card;   //找到同id真实实体=对象未改变
                }
            }
            return null;
        }

        //======== 永续拦截(GC/OC):见cost判断实现区段头注释(尾两&段注入:GC/OC) ========
        protected List<string> globalContinueIds;            //GC:查fightLogic.continues全局登记处
        protected List<string> operationContinueIds;         //OC:逐个查被操作候选卡自身EntityCard.continues
        public List<FightLogic.Continue> continueRegistry { get; set; }    //全局登记处引用,装配时注入
        public void SetContinueGates(List<string> gc, List<string> oc)
        {
            globalContinueIds = gc;
            operationContinueIds = oc;
        }
        public void SetContinueRegistry(List<FightLogic.Continue> registry)
        {
            continueRegistry = registry;
        }
        //GC:全局登记处存在任一"编号命中且生效(IsHaveEff()==true)"的永续 → 本动作被拦截(动作入口直接return)
        public bool IsBlockedByGlobalContinue()
        {
            if (globalContinueIds == null || globalContinueIds.Count == 0) return false;
            if (continueRegistry == null || continueRegistry.Count == 0) return false;
            foreach (string id in globalContinueIds)
                foreach (FightLogic.Continue con in continueRegistry)
                    if (con != null && con.id == id && con.IsHaveEff()) return true;
            return false;
        }
        //OC:单卡命中(候选卡自身持OC编号对应且生效的永续 → 该卡不能作为本次被操作卡,从候选剔除)
        //内置自动拦截:候选卡自身携带 OnlyOne_InField(编号FightLogic.OnlyOneContinueId="0",同名卡在自己场上仅可表侧存在1张)且生效时,
        //无论本组件OC尾段是否声明该编号都会剔除——保证"同名表侧已在场上时,不可再经融合/特招/被操作等方式让第2张出现"全局自动生效
        protected bool IsCardOperationBlocked(EntityCard card)
        {
            //内置拦截:同名表侧永续(静态声明判定,不依赖实例继续是否已实体化,额外/墓地等隐藏区候选同样生效)
            if (FightLogic.IsOnlyOneRestricted(card)) return true;
            if (card?.continues == null) return false;
            foreach (FightLogic.Continue con in card.continues)
            {
                if (con == null || !con.IsHaveEff()) continue;
                if (con.id == FightLogic.OnlyOneContinueId) return true;
                if (operationContinueIds != null && operationContinueIds.Contains(con.id)) return true;
            }
            return false;
        }
        //OC:候选池逐个剔除被拦截卡,返回剩余可用候选(即使无OC声明也执行内置OnlyOne检查);public供Effection.EffAction结算前对注入对象做OC剔除
        public List<EntityCard> FilterOperationCandidates(List<EntityCard> cards)
        {
            if (cards == null) return null;
            List<EntityCard> res = new List<EntityCard>();
            foreach (EntityCard c in cards)
                if (c != null && !IsCardOperationBlocked(c)) res.Add(c);
            return res;
        }
    }

    public class ExtraQuest                                         //额外外部请求
    {
        public List<StateMachine.TimePointBase> timePointBases { get; private set; }
        public static Func<EntityCard, Transform> OneEntityCardToTransfrom;                        //卡牌实体转预制体
        public static Func<List<EntityCard>, List<Transform>> EntityCardToTransfrom;                        //卡牌实体转预制体
        public Func<List<EntityCard>, int, UniTask<List<EntityCard>>> GetEntityCard { get; set; }          //用于获取玩家选择卡片列表
        public Func<List<EntityCard>,List<Transform>,UniTask> HandleTransfrom { get; set; }                //为选择的卡片预制体执行操作
        public Func<EntityCard, EntityCard> MapRealCard { get; set; }                                       //弹窗选中实体,真实实体映射（数据一致但实例不同，用于诱发匹配）
        public Func<int, List<EntityCard>, UniTask> DrawCardHandle { get; set; }                           //抽卡效果完成后刷新手牌UI(whom=玩家索引0/1,参数=该玩家完整手牌)
        public void IniEntityCardToTransfrom(Func<EntityCard, Transform> func0,Func<List<EntityCard>, List<Transform>> func1)
        {
            OneEntityCardToTransfrom = func0;
            EntityCardToTransfrom = func1;
        }
        public ExtraQuest(Func<List<EntityCard>, int, UniTask<List<EntityCard>>> GetEntityCard,  Func<List<EntityCard>,List<Transform>, UniTask> HandleTransfrom, 
                          List<StateMachine.TimePointBase> timePointBases, Func<EntityCard, EntityCard> MapRealCard)
        {
            this.timePointBases = timePointBases;
            this.GetEntityCard = GetEntityCard;
            this.HandleTransfrom = HandleTransfrom;
            this.MapRealCard = MapRealCard;
        }
    }
    
    #region 公共送墓/除外入口:进入墓地/除外区效果的诱发时点统一登记
    //所有"卡进入墓地/除外区"路径(效果送墓EffGoToCemetery、效果除外EffGoToBanished、破坏送墓DestoryCard、
    //代价送墓/丢弃GoToCemetery/GoToCemeteries/CostCard等)一律汇聚到下面两个公共方法,统一执行"数据层移动+诱发时点登记+预制体UI同步"。
    //从各效果组件上移除各自零散的登记代码,避免遗漏/重复;事件广播订阅机制保持不变,诱发仍由FightLogic.StartTriggerEff
    //按登记的InCemetery(7)/InBanished(6)/LeaveEx(13)时点统一分发广播,SelfInCemetery/SelfInBanished/LeaveEx触发器订阅不变。
    public static async UniTask<List<EntityCard>> SendCardToCemetery(List<EntityCard> cards, List<EntityFindComponent> regions, ExtraQuest extraQuest)   //公共送墓:统一送入墓地
    {
        return await SendCardToZone(cards, regions, extraQuest, GameManage.CardLocation.Cemetery, 3);
    }
    public static async UniTask<List<EntityCard>> SendCardToBanished(List<EntityCard> cards, List<EntityFindComponent> regions, ExtraQuest extraQuest)  //公共除外:统一送入除外区
    {
        return await SendCardToZone(cards, regions, extraQuest, GameManage.CardLocation.Banished, 4);
    }
    private static async UniTask<List<EntityCard>> SendCardToZone(List<EntityCard> cards, List<EntityFindComponent> regions, ExtraQuest extraQuest,
        GameManage.CardLocation targetLocation, int destIdx)
    {
        List<EntityCard> moved = new List<EntityCard>();
        if (cards == null || cards.Count == 0) return moved;
        for (int k = 0; k < cards.Count; k++)
        {
            EntityCard item = cards[k];
            if (item == null) continue;
            GameManage.CardLocation fromLocation = item.location;             //进入前的来源区域(送墓/除外前登记时点用)
            //宿主定位:目标可能位于双方任意区域(己方/对方场上·手牌·墓地·卡组等),按实际持有列表查找宿主再移除,
            //避免按location索引误从同序己方区域移除导致目标残留在原区域
            EntityFindComponent srcRegion = null;
            if (regions != null)
                foreach (var r in regions)
                    if (r?.cards != null && r.cards.Contains(item)) { srcRegion = r; break; }
            if (srcRegion == null) continue;                                 //真实实体已不在任何区域(如对象变化后被其它效果移动):跳过
            if (srcRegion is EntityPlayer.Field field)                       //场上卡:须同时从怪兽区/魔陷区移除,避免怪兽区残留导致后续占位/计数错误
            {
                if (field.GetMonZon().Contains(item)) field.RemoveMonster(item);
                else if (field.GetMTZon().Contains(item)) field.RemoveMagicTrap(item);
                else field.RemoveCard(item);
            }
            else
                srcRegion.RemoveCard(item);
            item.ChangeLocation(targetLocation);
            if (regions != null && destIdx < regions.Count && regions[destIdx] != null)
                regions[destIdx].AddCard(item);                              //送入己方对应区域组件(AddCard自动按原本所有者纠偏到对应区域,含对方区域)
            moved.Add(item);
            //诱发时点统一登记:进入墓地/除外区登记InCemetery/InBanished;来源为额外卡组的卡离开额外卡组时顺带登记LeaveEx,供对应诱发响应
            if (extraQuest?.timePointBases != null)
            {
                if (targetLocation == GameManage.CardLocation.Cemetery)
                {
                    extraQuest.timePointBases.Add(new StateMachine.InCemetery(item, fromLocation));
                    if (fromLocation == GameManage.CardLocation.ExtraDeck)
                        extraQuest.timePointBases.Add(new StateMachine.LeaveEx(item, GameManage.CardLocation.Cemetery));
                }
                else if (targetLocation == GameManage.CardLocation.Banished)
                {
                    extraQuest.timePointBases.Add(new StateMachine.InBanished(item, fromLocation));
                    if (fromLocation == GameManage.CardLocation.ExtraDeck)
                        extraQuest.timePointBases.Add(new StateMachine.LeaveEx(item, GameManage.CardLocation.Banished));
                }
            }
        }
        //预制体UI同步:将被移动卡移动到对应区域(送墓→GotoCeCemeteryUI/除外→GotoBanishedUI),与原各效果组件末尾同步时机一致
        if (moved.Count > 0 && extraQuest != null && extraQuest.HandleTransfrom != null)
            await extraQuest.HandleTransfrom(moved, ExtraQuest.EntityCardToTransfrom?.Invoke(moved));
        return moved;
    }
    #endregion

    public class EffGoToCemetery                                    //效果：送去墓地
        : CardComponent, Eff, IEffGetObject
    {
        private List<EntityFindComponent> entityFindComponents;
        private List<EntityCard> entities;                           //送去墓地的卡列表
        private List<EntityCard> objects;                            //Effection.EffAction注入的取对象结果(快照副本)
        private int isEntityCard;                                    //是否包含自身（=1 表示将自身送去墓地）
        public int isGetObject { get; private set; }                 //是否取对象(>=1=发动时先经弹窗选取对象,再以选中的对象作为送墓目标)

        public EffGoToCemetery(EntityCard entityCard, ExtraQuest extraQuest = null)
        {
            this.entityCard = entityCard;
            this.extraQuest = extraQuest;                       //注入UI同步回调(仿照GoToCemetery代价支付),否则OnTrigger中无法移动卡牌预制体到墓地
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniEffGoToCemetery = cardComponent as IniEffGoToCemetery;
            if (iniEffGoToCemetery == null)
                throw new Exception("Invalid card component type for EffGoToCemetery");
            this.isGetObject = iniEffGoToCemetery.isGetObject;
            this.isEntityCard = iniEffGoToCemetery.isEntityCard;
            entityFindComponentsCode = AllRead();
            this.QuestBools = QuestBools;
        }

        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }

        public void GetEntities(List<EntityCard> entities)          //组合模式注入:外部选定送墓目标后(如EffGoToCemeteries选卡后GetEntities(targets)再调用本方法),存为本类送墓列表
        {
            this.entities = entities;
        }

        public async UniTask OnTrigger()
        {
            IniCardCompent();
            if (isGetObject == 1)
            {
                //取对象模式:直接操作注入的对象
                if (objects == null || objects.Count == 0) return;      //未取到对象:不处理
                entities = new List<EntityCard>();
                foreach (var obj in objects)
                {
                    //对象变化判定:注入副本与真实实体共用currentid,以id回溯对照——当前全部区域找不到同id真实实体,视为对象已改变
                    EntityCard real = FindRealCardById(entityFindComponents, obj.currentid);
                    if (real == null) return;                           //对象已改变:直接返回,不操作
                    if (!entities.Contains(real)) entities.Add(real);
                }
            }
            else if (isEntityCard == 1)
            {
                //包含自身模式:效果持有卡自身送墓(重建目标列表)
                entities = new List<EntityCard>();
                entities.Add(entityCard);
            }
            //组合模式(0&0):目标已由外部经GetEntities注入(如EffGoToCemeteries选卡后GetEntities(targets)再调用本方法),entities保留注入列表直接执行送墓
            if (entities == null || entities.Count == 0) return;        //无目标(未注入/非自身/非取对象):不执行
            //永续拦截(OC):结算前对最终目标列表(自身/取对象对象已映射回真实实体/组合注入)统一逐一剔除——目标自身持OC编号对应且生效的永续(本组件OperationContinues)→剔除;全被剔光则效果不执行
            entities = FilterOperationCandidates(entities);
            if (entities == null || entities.Count == 0) return;
            if (entities == null || entities.Count == 0) return;
            //统一走公共送墓入口:数据层区域移动+InCemetery/LeaveEx诱发时点登记+预制体UI同步(事件广播订阅机制不变)
            await SendCardToCemetery(entities, entityFindComponents, extraQuest);
        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }
    }

    public class EffGoToCemeteries                                  //效果:其他卡送墓(经二维选卡/取对象选定其他卡后,持有EffGoToCemetery统一执行送墓,仿照cost送墓GoToCemeteries持有GoToCemetery)
        : CardComponent, Eff, IEffGetObject
    {
        private EffGoToCemetery effGoToCemetery;                //持有的送墓动作组件:统一执行数据层移动+InCemetery时点+预制体UI(本类选定目标后经GetEntities注入,再委托其OnTrigger执行)
        private List<EntityCard> objects;                       //Effection.EffAction注入的取对象结果(快照副本)
        private List<GameManage.CardLocation> findComponent;    //送墓候选来源区域(为空=全部区域含对方)
        private List<EntityFindComponent> entityFindComponents;
        //送墓条件(二维选卡条件,大组下标/编码与GoToCemeteries、JudExist共用,求值见Condition2D):
        private List<List<string>> cardCondition;               //cardCondition(留空=不限制任何卡)
        private int isEntityCard;                               //是否连效果持有卡自身也送去墓地(>=1:自身不经选择直接加入送墓列表;候选收集始终排除自身)
        private List<string> hitList;                           //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public int isGetObject { get; private set; }            //是否取对象(>=1=发动时先经弹窗选取对象,结算时直接送墓对象,不自行收集/弹窗)

        public EffGoToCemeteries(EntityCard entityCard, ExtraQuest extraQuest = null)
        {
            this.entityCard = entityCard;
            this.extraQuest = extraQuest;                       //注入UI同步回调(eQ.GoToCemeteryEQ,其GetEntityCard=选卡弹窗、HandleTransfrom=fightUI.GotoCeCemeteryUI),否则OnTrigger中无法弹窗选卡/移动卡牌预制体到墓地
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniEffGoToCemeteries = cardComponent as IniEffGoToCemeteries;
            if (iniEffGoToCemeteries == null)
                throw new Exception("Invalid card component type for EffGoToCemeteries");
            this.isGetObject = iniEffGoToCemeteries.isGetObject;
            this.num = Math.Max(1, iniEffGoToCemeteries.num);   //最小1
            this.findComponent = iniEffGoToCemeteries.findComponent;
            this.cardCondition = iniEffGoToCemeteries.cardCondition ?? new List<List<string>>();
            this.isEntityCard = iniEffGoToCemeteries.isEntityCard;
            this.hitList = iniEffGoToCemeteries.hitList;
            entityFindComponentsCode = (findComponent == null || findComponent.Count == 0) ? AllRead() : LocationToBools(findComponent);   //未配置区域=全部区域(含对方0-11)可作为送墓对象来源
            this.QuestBools = QuestBools;
            //持有送墓动作组件:自身是否送墓(isEntityCard)由本类在OnTrigger中处理,故EffGoToCemetery按(0,0)配置(不取对象/不含自身),
            //本类选定目标后经GetEntities注入,由EffGoToCemetery.OnTrigger执行数据层移动+InCemetery时点+预制体UI同步
            effGoToCemetery = new EffGoToCemetery(entityCard, extraQuest);
            effGoToCemetery.IniCardCompent(new IniEffGoToCemetery(0, 0), QuestBools);
        }

        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }

        public async UniTask OnTrigger()
        {
            List<EntityCard> targets = new List<EntityCard>();
            if (isGetObject == 1)
            {
                //取对象模式:直接以注入的对象为送墓目标(不自行收集候选/不弹窗),结算时先校验对象是否已改变
                if (objects == null || objects.Count == 0) return;      //未取到对象:不处理
                List<EntityFindComponent> allRegions = QuestBools?.Invoke(AllRead());   //对象可能来自全部区域,用全区域回溯对照
                if (allRegions == null || allRegions.Count == 0) return;
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    //对象变化判定:注入副本与真实实体共用currentid,以id回溯对照——全部区域找不到同id真实实体,视为对象已改变
                    EntityCard real = FindRealCardById(allRegions, obj.currentid);
                    if (real == null) return;                           //对象已改变:直接返回,不操作
                    if (!targets.Contains(real)) targets.Add(real);
                }
            }
            else
            {
                //不取对象模式:仿照cost送墓(GoToCemeteries)收集候选并弹UI(即使候选数量恰好等于需求数也弹出UI,由玩家亲自点击确认后才送墓)
                IniCardCompent();
                if (entityFindComponents == null || entityFindComponents.Count == 0) return;
                if (isEntityCard >= 1 && entityCard != null) targets.Add(entityCard);   //效果持有卡自身也送去墓地:不经选择直接加入送墓列表
                List<EntityCard> candidates = new List<EntityCard>();
                foreach (var item in entityFindComponents)
                {
                    if (item?.cards == null) continue;
                    foreach (var card in item.cards)
                    {
                        if (card == null || card == entityCard) continue;               //候选始终排除效果持有卡自身(自身是否送墓由isEntityCard决定)
                        if (IsCardOperationBlocked(card)) continue;   //永续拦截(OC):候选自身持OC编号对应且生效的永续(本组件OperationContinues)→ 该卡不能作为本次被操作候选,逐个剔除
                        if (!Condition2D.IsMatch(card, cardCondition, hitList, item.cards)) continue;   //item.cards作poolCards供怪兽维度3/4/5查对应怪兽实体
                        if (!candidates.Contains(card)) candidates.Add(card);           //跨区域同实例防御
                    }
                }
                if (candidates.Count < num) return;                                     //候选不足需求数:无法完成送墓(不执行效果)
                List<EntityCard> chooseRes = (extraQuest != null && extraQuest.GetEntityCard != null)
                    ? await extraQuest.GetEntityCard.Invoke(candidates, num)
                    : candidates.GetRange(0, num);
                if (chooseRes == null || chooseRes.Count < num) return;                 //玩家取消或未选满:不送墓
                //弹窗选中的实体与宿主区域挂载的真实实体可能不是同一实例(数据一致),送墓前映射回真实实体,保证区域移除与诱发匹配
                foreach (var card in chooseRes)
                {
                    EntityCard real = (extraQuest != null && extraQuest.MapRealCard != null) ? (extraQuest.MapRealCard(card) ?? card) : card;
                    if (real != null && !targets.Contains(real)) targets.Add(real);
                }
            }
            //永续拦截(OC):结算前对最终目标列表统一逐一剔除——含取对象对象(已映射回真实实体)/自身(isEntityCard)/弹窗选定等全部被操作目标,
            //目标自身持OC编号对应且生效的永续(本组件OperationContinues)→剔除;全被剔光则效果不执行
            targets = FilterOperationCandidates(targets);
            if (targets.Count == 0) return;

            //注入选定目标,委托持有的EffGoToCemetery执行:数据层移除原区域/送入墓地(RemoveCard自动清理怪兽/魔陷区)+InCemetery时点+预制体UI同步
            effGoToCemetery.GetEntities(targets);
            await effGoToCemetery.OnTrigger();
        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }
    }

    public class EffGoToBanished                                   //效果：除外(将卡从原区域移除并送入除外区,仿照EffGoToCemetery)
        : CardComponent, Eff, IEffGetObject
    {
        private List<EntityFindComponent> entityFindComponents;
        private List<EntityCard> entities;                           //被除外的卡列表
        private List<EntityCard> objects;                            //Effection.EffAction注入的取对象结果(快照副本)
        private int isEntityCard;                                    //是否包含自身（=1 表示将效果持有卡自身除外）
        public int isGetObject { get; private set; }                 //是否取对象(>=1=发动时先经弹窗选取对象,再以选中的对象作为除外目标)

        public EffGoToBanished(EntityCard entityCard, ExtraQuest extraQuest = null)
        {
            this.entityCard = entityCard;
            this.extraQuest = extraQuest;                       //注入UI同步回调(eQ.GoToBanishedEQ,其HandleTransfrom=fightUI.GotoBanishedUI),否则OnTrigger中无法移动卡牌预制体到除外区
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniEffGoToBanished = cardComponent as IniEffGoToBanished;
            if (iniEffGoToBanished == null)
                throw new Exception("Invalid card component type for EffGoToBanished");
            this.isGetObject = iniEffGoToBanished.isGetObject;
            this.isEntityCard = iniEffGoToBanished.isEntityCard;
            entityFindComponentsCode = AllRead();
            this.QuestBools = QuestBools;
        }

        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }
        public void GetEntities(List<EntityCard> entities)
        {
            this.entities = entities;
        }
        public async UniTask OnTrigger()
        {
            IniCardCompent();
            if (isEntityCard == 1)
            {
                //包含自身模式:以效果持有卡自身为除外目标(重建目标列表)
                entities = new List<EntityCard>();
                entities.Add(entityCard);
            }
            //组合模式(0&0):目标已由外部经GetEntities注入(如BanishedCard选卡后GetEntities(targets)再调用本方法),entities保留注入列表直接执行除外
            if (entities == null || entities.Count == 0) return;        //无目标(未注入/非自身/非取对象):不执行
            //永续拦截(OC):结算前对最终目标列表(自身/组合注入)统一逐一剔除——目标自身持OC编号对应且生效的永续(本组件OperationContinues)→剔除;全被剔光则效果不执行
            entities = FilterOperationCandidates(entities);
            if (entities == null || entities.Count == 0) return;
            if (entities == null || entities.Count == 0) return;
            //统一走公共除外入口:数据层区域移动+InBanished/LeaveEx诱发时点登记+预制体UI同步(事件广播订阅机制不变)
            await SendCardToBanished(entities, entityFindComponents, extraQuest);
        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }
    }

    public class BanishedCard                                   //效果：除外(效果持有卡保留,通过二维选卡/取对象将其他卡送入除外区,仿照DestoryCard)
        : CardComponent, Eff, IEffGetObject
    {
        private EffGoToBanished effGoToBanished;                //持有的除外动作组件:统一执行数据层移动+时点+UI(本类选定目标后经GetEntities注入,再委托其OnTrigger执行)
        private List<EntityCard> objects;                            //Effection.EffAction注入的取对象结果(快照副本)
        private List<GameManage.CardLocation> findComponent;        //除外候选来源区域(为空=全部区域)
        private List<EntityFindComponent> entityFindComponents;
        //除外条件(二维选卡条件,大组下标/编码与DestoryCard、JudExist共用,求值见Condition2D):
        private List<List<string>> cardCondition;                   //cardCondition(留空=不限制任何卡)
        private string excludeName;                                 //排除卡名(该卡名不算除外候选;留空=不排除)
        private List<string> hitList;                               //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        private int excludeSelf;                                    //>=1候选/对象排除效果持有卡自身(自身不被除外),0允许选择自身
        public int isGetObject { get; private set; }                 //是否取对象(>=1=发动时先经弹窗选取对象,结算时直接除外对象,不自行收集/弹窗)

        public BanishedCard(EntityCard entityCard, ExtraQuest extraQuest)
        {
            this.entityCard = entityCard;
            this.extraQuest = extraQuest;                       //注入UI同步回调(eQ.GoToBanishedEQ,其GetEntityCard=选卡弹窗、HandleTransfrom=fightUI.GotoBanishedUI),否则OnTrigger中无法弹窗选卡/移动卡牌预制体到除外区
        }
        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniBanishedCard = cardComponent as IniBanishedCard;
            if (iniBanishedCard == null) throw new Exception("Invalid card component type for BanishedCard");
            this.num = iniBanishedCard.num;
            this.findComponent = iniBanishedCard.findComponent;
            this.cardCondition = iniBanishedCard.cardCondition ?? new List<List<string>>();
            this.excludeName = iniBanishedCard.excludeName;
            this.hitList = iniBanishedCard.hitList;
            this.excludeSelf = iniBanishedCard.excludeSelf;
            this.isGetObject = iniBanishedCard.isGetObject;
            entityFindComponentsCode = (findComponent == null || findComponent.Count == 0) ? AllRead() : LocationToBools(findComponent);   //未配置区域=全部区域
            this.QuestBools = QuestBools;
            //持有除外动作组件:目标选择/排除自身(excludeSelf)由本类负责,EffGoToBanished按(0,0)配置(不取对象/不含自身),
            //统一经GetEntities注入选定目标后,由EffGoToBanished.OnTrigger执行数据层移动+InBanished时点+预制体UI同步
            effGoToBanished = new EffGoToBanished(entityCard, extraQuest);
            effGoToBanished.IniCardCompent(new IniEffGoToBanished(0, 0), QuestBools);
        }

        public void IniCardCompent()
        {
            entityCards = new List<EntityCard>();
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }

        public async UniTask OnTrigger()
        {
            List<EntityCard> targets = new List<EntityCard>();
            if (isGetObject == 1)
            {
                //取对象模式:直接取注入的对象为目标(不自行收集候选/不弹窗),结算时先校验对象是否已改变
                if (objects == null || objects.Count == 0) return;      //未取到对象:不处理
                List<EntityFindComponent> allRegions = QuestBools?.Invoke(AllRead());   //对象可能来自全部区域,用全区域回溯对照
                foreach (var obj in objects)
                {
                    //对象变化判定:注入副本与真实实体共用currentid,以id回溯对照——全部区域找不到同id真实实体,视为对象已改变
                    EntityCard real = FindRealCardById(allRegions, obj.currentid);
                    if (real == null) return;                           //对象已改变:直接返回,不操作
                    if (excludeSelf >= 1 && real == entityCard) continue;   //排除效果持有卡自身
                    if (!targets.Contains(real)) targets.Add(real);
                }
            }
            else
            {
                //不取对象模式:二维读取候选并弹UI(即使候选数量恰好等于需求数,也弹出由玩家点击确认后才执行除外)
                IniCardCompent();
                if (entityFindComponents == null || entityFindComponents.Count == 0) return;
                List<EntityCard> candidates = new List<EntityCard>();
                foreach (var item in entityFindComponents)
                {
                    if (item?.cards == null) continue;
                    foreach (var card in item.cards)
                    {
                        if (card == null) continue;
                        if (IsCardOperationBlocked(card)) continue;   //永续拦截(OC):候选自身持OC编号对应且生效的永续(本组件OperationContinues)→ 该卡不能作为本次被操作候选,逐个剔除
                        if (excludeSelf >= 1 && card == entityCard) continue;            //排除效果持有卡自身
                        if (!string.IsNullOrEmpty(excludeName) && card.currentName == excludeName) continue;   //排除名不算候选
                        if (!Condition2D.IsMatch(card, cardCondition, hitList, item.cards)) continue;           //二维选卡条件(cardCondition为空=不限制)
                        if (!candidates.Contains(card)) candidates.Add(card);                                  //跨区域同实例防御
                    }
                }
                if (candidates.Count < num) return;                     //候选不足需求数:无法完成除外
                List<EntityCard> chooseRes = (extraQuest != null && extraQuest.GetEntityCard != null)
                    ? await extraQuest.GetEntityCard.Invoke(candidates, num)
                    : candidates.GetRange(0, num);
                if (chooseRes == null || chooseRes.Count < num) return; //玩家取消或未选满:不除外
                //弹窗选中的实体与宿主区域挂载的真实实体可能不是同一实例(数据一致),除外前映射回真实实体,保证区域移除与诱发匹配
                foreach (var card in chooseRes)
                {
                    EntityCard real = (extraQuest != null && extraQuest.MapRealCard != null) ? (extraQuest.MapRealCard(card) ?? card) : card;
                    if (real != null && !targets.Contains(real)) targets.Add(real);
                }
            }
            //永续拦截(OC):结算前对最终目标列表统一逐一剔除——含取对象对象(已映射回真实实体)/弹窗选定等全部被操作目标,
            //目标自身持OC编号对应且生效的永续(本组件OperationContinues)→剔除;全被剔光则效果不执行
            targets = FilterOperationCandidates(targets);
            if (targets.Count == 0) return;

            //注入选定目标,委托持有的EffGoToBanished执行:数据层移除原区域/送入除外区(Field.RemoveCard自动清理怪兽/魔陷区)+InBanished时点+预制体UI同步
            effGoToBanished.GetEntities(targets);
            await effGoToBanished.OnTrigger();
        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }
    }

    public class SelfSpeSomEff                                  //自身特招(无参数):效果结算时把效果持有卡自身从当前所在区域特殊召唤上场
        : CardComponent, Eff                                    //仅特招自身:不收集候选/不弹窗/不取对象/不接收外部数据,目标固定=持有本效果的卡
    {
        private List<EntityFindComponent> entityFindComponents;
        public SelfSpeSomEff(EntityCard entityCard, ExtraQuest extraQuest = null)
        {
            this.entityCard = entityCard;                        //效果持有卡=自身特招目标
            this.extraQuest = extraQuest;                        //注入UI回调(SpeSumEQ:其HandleTransfrom=实体特招UI,等待玩家点蓝格放置并完成数据移动)
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            if (cardComponent != null && !(cardComponent is IniSelfSpeSomEff))
                throw new Exception("Invalid card component type for SelfSpeSomEff");
            entityFindComponentsCode = AllRead();               //自身可能位于任意区域(手牌/墓地/除外等),校验/查找需读全部区域(含对方)
            this.QuestBools = QuestBools;
        }
        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(AllRead());
        }

        public async UniTask OnTrigger()
        {
            if (entityCard == null) return;
            //永续拦截(OC):自身持OC编号对应且生效的永续(本组件OperationContinues,如同名限定/不能特招自身)→ 不执行自身特招
            if (IsCardOperationBlocked(entityCard))
                return;
            IniCardCompent();
            EntityCard self = entityCard;
            if (entityFindComponents != null)
            {
                //按id回溯对照区域容器,取容器内同id真实实体:防止持有卡实例在结算期间已离开容器(效果对象移动/镜像残留)却仍被误特招
                EntityCard real = FindRealCardById(entityFindComponents, self.currentid);
                if (real == null)
                {
                    return;
                }
                //已在场上则不可再特招(自身特招语义=从非场区域出场)
                if (IsCurrentIdOnFieldAny(entityFindComponents, real.currentid))
                {
                    return;
                }
                self = real;
            }
            else if (self.location == GameManage.CardLocation.Field)    //纯数据环境(无区域容器):按location字段兜底判定已在场
                return;

            List<EntityCard> targets = new List<EntityCard> { self };
            if (extraQuest == null || extraQuest.HandleTransfrom == null)
            {
                return;
            }
            List<Transform> targetTransforms = ExtraQuest.EntityCardToTransfrom?.Invoke(targets);
            await extraQuest.HandleTransfrom(targets, targetTransforms);

            //特招完成后,登记SpeSomTimePoint时点,供诱发效果响应
            if (extraQuest?.timePointBases != null)
                extraQuest.timePointBases.Add(new StateMachine.SpeSomTimePoint(self));
        }

        public List<EntityCard> GetObjects()
        {
            return null;                                        //自身特招不产生对象/候选
        }

        //权威"已在场"判定:以数据层全区域容器按currentid判断(与SpeSomEff同标准),不依赖实例location字段(镜像快照location可能是生成时的旧值)
        private static bool IsCurrentIdOnFieldAny(List<EntityFindComponent> allZones, string currentid)
        {
            if (allZones == null || string.IsNullOrEmpty(currentid)) return false;
            //区域索引:0-5=我方Deck/ExtraDeck/Hand/Cemetery/Banished/Field,6-11=对方对应区域(Field索引5/11)
            if (allZones.Count > 5 && ZoneContainsId(allZones[5], currentid)) return true;    //我方场上
            if (allZones.Count > 11 && ZoneContainsId(allZones[11], currentid)) return true;  //对方场上
            return false;
        }
        private static bool ZoneContainsId(EntityFindComponent zone, string currentid)
        {
            if (zone?.cards == null) return false;
            for (int i = 0; i < zone.cards.Count; i++)
                if (zone.cards[i] != null && zone.cards[i].currentid == currentid) return true;
            return false;
        }
    }

    public class SpeSomEff                                     //选择特招效果
        : CardComponent, Eff, IEffGetObject                 //entityCards对应UI卡片数据，满足特招条件的卡片列表
    {
        private List<EntityCard> objects;                            //Effection.EffAction注入的取对象结果(快照副本)
        private List<GameManage.CardLocation> findComponent;        //特招来源区域(为空=全部区域)
        private List<EntityFindComponent> entityFindComponents;
        //特招条件(二维选卡条件,大组下标/编码与GoToCemeteries、JudExist共用,求值见Condition2D):
        private List<List<string>> cardCondition;                   //cardCondition(留空=不限制任何卡)
        private string excludeName;                                 //排除卡名(该卡名不算特招候选;留空=不排除)
        private List<string> hitList;                               //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public int isGetObject { get; private set; }                 //是否取对象(>=1=发动时先经弹窗选取对象,再以选中的对象作为特招目标)

        public SpeSomEff(ExtraQuest extraQuest)
        {
            this.extraQuest = extraQuest;
        }
        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniSpeSumEff = cardComponent as IniSpeSomEff;
            if (iniSpeSumEff == null) throw new Exception("Invalid card component type for SpeSumEff");
            this.num = iniSpeSumEff.num;
            this.findComponent = iniSpeSumEff.findComponent;
            this.cardCondition = iniSpeSumEff.cardCondition ?? new List<List<string>>();
            this.excludeName = iniSpeSumEff.excludeName;
            this.hitList = iniSpeSumEff.hitList;
            this.isGetObject = iniSpeSumEff.isGetObject;
            entityFindComponentsCode = (findComponent == null || findComponent.Count == 0) ? AllRead() : LocationToBools(findComponent);   //未配置区域=全部区域
            this.QuestBools = QuestBools;
        }


        public void IniCardCompent()
        {
            entityCards = new List<EntityCard>();
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }

        public async UniTask OnTrigger()
        {
            if (isGetObject == 1)
            {
                //取对象模式:特招列表=注入的对象(不按名字收集候选/不弹窗),结算时先校验对象是否已改变
                UnityEngine.Debug.Log($"[特招SpeSomEff] 取对象模式进入 isGetObject={isGetObject} objects={(objects == null ? "null" : objects.Count.ToString())}");
                if (objects == null || objects.Count == 0) { UnityEngine.Debug.Log("[特招SpeSomEff] objects为空→返回"); return; }      //未取到对象:不处理
                List<EntityFindComponent> allRegions = QuestBools?.Invoke(AllRead());   //对象可能来自全部区域,用全区域回溯对照
                UnityEngine.Debug.Log($"[特招SpeSomEff] allRegions={allRegions?.Count} 首个对象={objects[0]?.currentName} id={objects[0]?.currentid} loc={objects[0]?.location}");
                List<EntityCard> targets = new List<EntityCard>();
                foreach (var obj in objects)
                {
                    //对象变化判定:注入副本与真实实体共用currentid,以id回溯对照——全部区域找不到同id真实实体,视为对象已改变
                    EntityCard real = FindRealCardById(allRegions, obj.currentid);
                    UnityEngine.Debug.Log($"[特招SpeSomEff] 对象映射 obj={obj?.currentName} id={obj?.currentid} loc={obj?.location} → real={(real == null ? "null(对象已改变→返回)" : real?.currentName + " id=" + real.currentid + " loc=" + real.location)}");
                    if (real == null) return;                           //对象已改变:直接返回,不操作
                    if (!targets.Contains(real)) targets.Add(real);
                }
                if (targets.Count == 0) { UnityEngine.Debug.Log("[特招SpeSomEff] 映射后targets为空→返回"); return; }
                //弹窗选对象与结算之间,对象可能已被并发/前置效果特招上场:按currentid在Field容器权威剔除已在场对象,
                //不依赖location字段(对象为快照副本,按id回溯归一可能命中非场残留副本使location失真),避免"再次特招已在场的卡"+误加SpeSomTimePoint诱发
                int onFieldObjectCnt = targets.RemoveAll(t => IsCurrentIdOnFieldAny(allRegions, t.currentid));
                UnityEngine.Debug.Log($"[特招SpeSomEff] 剔除已在场 {onFieldObjectCnt} 张,剩余 {targets.Count} 张:{string.Join(",", targets.ConvertAll(t => t.currentName))}");
                if (onFieldObjectCnt > 0)
                {
                    if (targets.Count == 0) return;
                }
                //永续拦截(OC):结算前对最终特招目标(取对象对象已映射回真实实体)统一逐一剔除——目标自身持OC编号对应且生效的永续(本组件OperationContinues)→剔除;全被剔光则特招不执行
                targets = FilterOperationCandidates(targets);
                UnityEngine.Debug.Log($"[特招SpeSomEff] OC过滤后剩余 {targets.Count} 张:{string.Join(",", targets.ConvertAll(t => t.currentName))}");
                if (targets.Count == 0) return;
                if (extraQuest != null && extraQuest.HandleTransfrom != null)
                {
                    List<Transform> targetTransforms = ExtraQuest.EntityCardToTransfrom(targets);
                    UnityEngine.Debug.Log("[特招SpeSomEff] 调用EntitySpeSom特招UI...");
                    await extraQuest.HandleTransfrom(targets, targetTransforms);
                    UnityEngine.Debug.Log("[特招SpeSomEff] EntitySpeSom特招UI完成");
                }
                //特招完成后，将每张特招卡牌作为SpeSomTimePoint时点加入时点队列，供诱发效果响应
                if (extraQuest?.timePointBases != null)
                {
                    foreach (var card in targets)
                        extraQuest.timePointBases.Add(new StateMachine.SpeSomTimePoint(card));
                }
                return;
            }

            IniCardCompent();
            entityCards = new List<EntityCard>();
            //收集阶段即排除"已在场上的卡":特招语义=从非场区域(手卡/卡组/墓地/除外)出场,弹窗UI不应显示已在场上的卡。
            //双标准判定已在场——①location字段==Field(残留副本/容器与字段不同步的脏数据,字段已标记上场);
            //②数据层Field容器含同currentid(该实体已被前置效果真实特招上场)。
            //候选区域容器中理论不应出现这两种卡(它们不属于手卡/卡组/墓地),出现即数据不一致,一律不收集进候选弹窗。
            List<EntityFindComponent> allZonesForCollect = QuestBools?.Invoke(AllRead());   //null=纯数据环境,退化为仅按location字段判
            foreach (var item in entityFindComponents)
            {
                if (item?.cards == null) continue;
                foreach (var card in item.cards)
                {
                    if (card == null) continue;
                    if (IsCardOperationBlocked(card)) continue;   //永续拦截(OC):候选自身持OC编号对应且生效的永续(本组件OperationContinues,如"同名仅可在场上存在1张"等)→ 该卡不能作为本次被操作候选,逐个剔除
                    if (!string.IsNullOrEmpty(excludeName) && card.currentName == excludeName) continue;   //排除名不算候选
                    if (!Condition2D.IsMatch(card, cardCondition, hitList, item.cards)) continue;           //二维选卡条件(cardCondition为空=不限制)
                    if (card.location == GameManage.CardLocation.Field
                        || (allZonesForCollect != null && IsCurrentIdOnFieldAny(allZonesForCollect, card.currentid)))   //已在场上的卡不进候选弹窗
                    {
                        continue;
                    }
                    if (!entityCards.Contains(card)) entityCards.Add(card);                                //跨区域同实例防御
                }
            }
            //弹窗前权威过滤:候选若混入"已在场的卡"(残留副本/脏数据/前置效果已特招),一律在弹窗弹出前剔除——
            //副本location是生成时的旧值,仅按location剔场会漏判;按currentid回溯归一又会先命中非场残留副本而非场上真卡。
            //故以数据层全区域容器中Field区域为准,按currentid权威判定"同id此刻是否已在场",命中即剔除;
            //过滤后的候选才是"显示瞬间真正可特招"的卡,从根源避免"UI弹出已在场卡、点击后无反应/无法结算"。
            List<EntityFindComponent> allZonesForFieldCheck = QuestBools?.Invoke(AllRead());   //null=纯数据环境(无弹窗),跳过权威过滤
            if (allZonesForFieldCheck != null)
            {
                for (int ci = entityCards.Count - 1; ci >= 0; ci--)
                {
                    if (IsCurrentIdOnFieldAny(allZonesForFieldCheck, entityCards[ci].currentid))
                    {
                        entityCards.RemoveAt(ci);
                    }
                }
            }
            //候选归一为真实实体:区域容器理论上只存真实实体,但取对象/弹窗流程曾生成过镜像副本(CreateSnapshot)并可能在并发/撤销路径下残留进候选区域容器,
            //副本的location是生成时的旧值(如仍为Hand/Cemetery),与真实实体当前location不一致,会导致"按location剔场"漏判——
            //已在场的同id卡已由上方"弹窗前权威过滤"(按Field容器currentid判定)提前剔除;此处仅把幸存候选归一为真实实体(去重),再兜底剔场。
            if (extraQuest?.MapRealCard != null)
            {
                for (int ci = 0; ci < entityCards.Count; ci++)
                {
                    EntityCard real = extraQuest.MapRealCard(entityCards[ci]) ?? entityCards[ci];
                    if (real != entityCards[ci])
                    {
                        entityCards[ci] = real;
                    }
                }
                List<EntityCard> dedup = new List<EntityCard>();
                foreach (var c in entityCards)
                    if (!dedup.Contains(c)) dedup.Add(c);
                if (dedup.Count != entityCards.Count) entityCards = dedup;   //多个残留副本归一为同一真实实体时去重
            }
            //特招语义=从"非场上"区域(手卡/卡组/墓地/除外等)特殊召唤:候选若混入已在场上的卡(区域残留/同名牌映射错位等脏数据),剔除并告警,
            //避免"已上场的卡被再次当作特招目标"(会引发无意义换位+误触发SpeSomTimePoint诱发)。已在场的取对象特招走objects注入,不经本收集分支,不受影响。
            List<EntityCard> fieldCands = entityCards.Where(c => c.location == GameManage.CardLocation.Field).ToList();
            if (fieldCands.Count > 0)
            {
                entityCards = entityCards.Where(c => c.location != GameManage.CardLocation.Field).ToList();
            }
            if (entityCards.Count < num)
            {
                return;
            }
            List<EntityCard> chooseRes;
            if (entityCards.Count > num)
            {
                if (extraQuest == null || extraQuest.GetEntityCard == null) return;     //未注入弹窗回调(纯数据环境):候选多于需求数时无法人工指定特招目标,直接放弃
                chooseRes = await extraQuest.GetEntityCard.Invoke(entityCards, num);
            }
            else
                chooseRes = entityCards;
            //弹窗可能因互斥(上一个取对象/选卡弹窗未关闭)返回null,或玩家取消返回空:与GetObject/送墓一致,放弃本次特招而非崩溃
            if (chooseRes == null || chooseRes.Count == 0)
            {
                return;
            }
            //弹窗选中的实体与手卡/场上挂载的真实实体可能不是同一实例（数据一致），特招前映射回真实实体，保证诱发匹配成功
            chooseRes = chooseRes.Select(card => extraQuest.MapRealCard(card) ?? card).ToList();
            //特招前兜底剔场:与弹窗前权威过滤采用同一标准——按数据层Field容器currentid判定"是否已在场",
            //不能按实体location字段判——ChangeLocation迁移到Hand/Field/墓地/除外时会归还旧id并重生成新id,
            //若期间残留副本/容器不同步,location字段会失真(不在Field容器却标记Field,或反之)。
            //按字段剔会误杀"实际在非场容器、可正常特招"的候选,表现为"弹窗候选点击后无反应/特招目标均已在场被静默放弃"
            List<EntityFindComponent> preAllZones = QuestBools?.Invoke(AllRead());
            if (preAllZones != null)
            {
                int onFieldTargetCnt = 0;
                List<EntityCard> keepTargets = new List<EntityCard>();
                foreach (var c in chooseRes)
                {
                    if (IsCurrentIdOnFieldAny(preAllZones, c.currentid))
                    {
                        onFieldTargetCnt++;
                    }
                    else
                        keepTargets.Add(c);
                }
                if (onFieldTargetCnt > 0)
                {
                    chooseRes = keepTargets;
                    if (chooseRes.Count == 0)
                    {
                        return;
                    }
                }
            }
            else   //纯数据环境(无区域容器/无弹窗):退化为按location字段判剔,保留原兜底
            {
                int fieldCnt = chooseRes.Count(c => c.location == GameManage.CardLocation.Field);
                if (fieldCnt > 0)
                {
                    chooseRes = chooseRes.Where(c => c.location != GameManage.CardLocation.Field).ToList();
                    if (chooseRes.Count == 0)
                    {
                        return;
                    }
                }
            }
            //永续拦截(OC):结算前对最终特招目标兜底统一剔除(候选收集阶段已剔过,此处对弹窗选定并映射回真实实体的最终列表复检),全被剔光则特招不执行
            chooseRes = FilterOperationCandidates(chooseRes);
            if (chooseRes.Count == 0) return;
            List<Transform> transforms = ExtraQuest.EntityCardToTransfrom(chooseRes);
            await extraQuest.HandleTransfrom(chooseRes, transforms);

            //特招完成后，将每张特招卡牌作为SpeSomTimePoint时点加入时点队列，供诱发效果响应
            foreach (var card in chooseRes)
            {
                extraQuest.timePointBases.Add(new StateMachine.SpeSomTimePoint(card));
            }

        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }

        //权威"已在场"判定:以数据层全区域容器按currentid判断同id实体此刻是否已在场上(Field区域)。
        //不依赖候选实例的location字段——残留副本/镜像快照(CreateSnapshot)的location是生成时的旧值(如仍为Cemetery),
        //且MapToRealCard/FindEntityCardById按"Deck→…→Cemetery→…→Field"区域顺序回溯时会先命中非场残留副本而漏掉场上真卡,
        //导致"已在场的卡以非场区域身份混入特招候选→UI错误弹出、点击后无反应"。特招语义=从非场区域出场,同id已在场即不可再作目标。
        private static bool IsCurrentIdOnFieldAny(List<EntityFindComponent> allZones, string currentid)
        {
            if (allZones == null || string.IsNullOrEmpty(currentid)) return false;
            //区域索引:0-5=我方Deck/ExtraDeck/Hand/Cemetery/Banished/Field,6-11=对方对应区域(Field索引5/11)
            if (allZones.Count > 5 && ZoneContainsId(allZones[5], currentid)) return true;
            if (allZones.Count > 11 && ZoneContainsId(allZones[11], currentid)) return true;
            return false;
        }
        private static bool ZoneContainsId(EntityFindComponent zone, string currentid)
        {
            if (zone?.cards == null) return false;
            for (int i = 0; i < zone.cards.Count; i++)
            {
                if (zone.cards[i] != null && zone.cards[i].currentid == currentid)
                    return true;
            }
            return false;
        }
    }

    public class DrawCardEff                                       //抽卡效果 
        : CardComponent, Eff
    {
        private int number;                            //抽卡数量
        private List<EntityFindComponent> entityFindComponents;

        public DrawCardEff(EntityCard entityCard, IniDrawCardEff iniDrawCard, ExtraQuest extraQuest = null)
        {
            this.entityCard = entityCard;
            this.number = iniDrawCard?.number ?? 0;    //从IniDrawCardEff类读取抽卡数量
            this.extraQuest = extraQuest;              //注入抽卡UI回调(extraQuest.DrawCardHandle=刷新该玩家手牌区)
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniDrawCard = cardComponent as IniDrawCardEff;   //从CSV解析的效果参数(IniDrawCardEff)读取抽卡数量
            if (iniDrawCard == null)
                throw new Exception("Invalid card component type for DrawCardEff");
            this.number = iniDrawCard.number;
            entityFindComponentsCode = AllRead();      //读取全部区域(含对方0-11),便于按控制者索引抽取
            this.QuestBools = QuestBools;
        }

        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public async UniTask OnTrigger()
        {
            IniCardCompent();
            //抽卡方=效果卡当前控制者(0我方/1对方);区域列表前6位为己方,后6位为对方
            int playerIndex = entityCard?.GetCurrentOwner()?.GetPlayerIndex() ?? 0;
            int offset = playerIndex == 0 ? 0 : 6;
            if (entityFindComponents == null || offset + 2 >= entityFindComponents.Count)
                return;
            EntityFindComponent deck = entityFindComponents[offset + 0];    //卡组
            EntityFindComponent hand = entityFindComponents[offset + 2];    //手牌
            if (deck == null || hand == null || deck.cards == null)
                return;
            for (int i = 0; i < number; i++)
            {
                if (deck.cards.Count == 0) break;      //卡组抽空:停止抽取
                EntityCard topCard = deck.cards[0];
                topCard.ChangeLocation(GameManage.CardLocation.Hand);
                deck.cards.RemoveAt(0);
                hand.AddCard(topCard);
                //抽卡完成后，将每张抽到的卡作为DrawCard时点加入时点队列，供诱发效果响应
                if (extraQuest?.timePointBases != null)
                    extraQuest.timePointBases.Add(new StateMachine.DrawCard(topCard));
            }
            //抽卡完成:同步刷新抽卡方手牌UI(FightUI.DrawCardHandle按完整手牌列表重建手牌区)
            if (extraQuest?.DrawCardHandle != null)
                await extraQuest.DrawCardHandle.Invoke(playerIndex, hand.cards);
        }

        public List<EntityCard> GetObjects()
        {
            return null;
        }
    }

    public class FusionSummon                                  //融合召唤效果
        : CardComponent, Eff
    {
        // 区域编号:       Deck, ExtraDeck, Hand, Cemetery, Banished, Field
        // 素材区域索引:    0       1         2       3         4        5
        // 素材处理方式:    materialHandleWay[素材区域索引]=目标区域编号(0-5), 例如materialHandleWay[2]=0 表示将手牌中的素材返回卡组; 缺省送墓地(3)
        //                 归属对方的素材(playerIndex==1)自动从对方区域移除并送往对方对应区域(如对方墓地),无需在materialHandleWay中特判
        private List<int> materialHandleWay;
        private int includeSelf;                                         //是否包含自身(>=1:融合素材必须包含效果持有卡)
        private int maxMaterialCount;                                    //融合素材至多选多少个(>0:目标怪兽素材条件数<=该值才可融合;0=不限制)

        private List<GameManage.CardLocation> fusionSumLoc;              //特招地点(融合怪兽所在区域)
        private List<GameManage.CardLocation> materialLoc;               //融合素材所在区域
        private List<bool> fusionSumCode;                                //特招地点编码
        private List<bool> materialCode;                                 //融合素材编码
        private List<EntityFindComponent> fusionSumLocReg;               //特招地点对应区域
        private List<EntityFindComponent> materialLocReg;                //融合素材对应区域
        private List<EntityFindComponent> entityFindComponents;          //全部区域(索引0-5为己方,6-11为对方),用于移动素材与特招

        private List<string> speNameKey;                                       //特殊名字,字段列表,素材满足其中任一名字/字段即可(卡牌实体名称,而非静态数据)/字段
                                                                               // >=N表示融合怪兽等级大于等于N, <=N表示融合怪兽等级小于等于N(等级约束与素材约束协同生效)
        private List<int> Name_Key;                                            //与 speNameKey 一一对应: 0=代表卡牌名称,1=代表字段,-1=除外卡名(该卡名的融合怪兽不能被本效果融合召唤);等级条目对应位填0或空格占位(该位不参与素材匹配)

        private ExtraQuest cemeteryQuest;                                      //素材送墓UI回调(GoToCemeteryEQ,其HandleTransfrom=fightUI.GotoCeCemeteryUI)
        private ExtraQuest banishedQuest;                                     //素材除外UI回调(GoToBanishedEQ,其HandleTransfrom=fightUI.GotoBanishedUI)
        private ExtraQuest handQuest;                                         //素材回手UI回调(GoToHandEQ,其HandleTransfrom=fightUI.GotoHandUI)
        private ExtraQuest deckQuest;                                         //素材回卡组/额外卡组UI回调(GoToDeckEQ,其HandleTransfrom=fightUI.GotoDeckUI:回收预制体)

        public FusionSummon(EntityCard entityCard, ExtraQuest speQuest, ExtraQuest cemeteryQuest, ExtraQuest banishedQuest = null,
                            ExtraQuest handQuest = null, ExtraQuest deckQuest = null)
        {
            this.entityCard = entityCard;                     //效果持有卡(includeSelf=1时其自身必须作为融合素材)
            this.extraQuest = speQuest;                       //特招UI回调(SpeSumEQ,其HandleTransfrom=fightUI.实体特招UI)与诱发时点
            this.cemeteryQuest = cemeteryQuest;               //素材送墓UI回调
            this.banishedQuest = banishedQuest;               //素材除外UI回调(目标区域=除外区4的素材走此UI,如烙印龙白界龙①的素材除外融合)
            this.handQuest = handQuest;                       //素材回手UI回调(目标区域=手牌2)
            this.deckQuest = deckQuest;                       //素材回卡组/额外卡组UI回调(目标区域=卡组0/额外卡组1)
        }
        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniFusionSummon = cardComponent as IniFusionSummon;
            if (iniFusionSummon == null) throw new Exception("Invalid card component type for FusionSummon");
            this.fusionSumLoc = iniFusionSummon.fusionSumLoc;
            this.materialLoc = iniFusionSummon.materialLoc;
            this.materialHandleWay = iniFusionSummon.materialHandleWay;
            this.speNameKey = iniFusionSummon.speNameKey;
            this.Name_Key = iniFusionSummon.Name_Key;
            this.includeSelf = iniFusionSummon.includeSelf;
            this.maxMaterialCount = iniFusionSummon.maxMaterialCount;
            this.fusionSumCode = LocationToBools(fusionSumLoc);
            this.materialCode = LocationToBools(materialLoc);
            this.QuestBools = QuestBools;
        }
        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(AllRead());          //全部区域,便于按索引移动卡牌
            fusionSumLocReg = QuestBools?.Invoke(fusionSumCode);
            materialLocReg = QuestBools?.Invoke(materialCode);
        }
        public async UniTask OnTrigger()
        {
            IniCardCompent();
            // 聚合融合怪兽区与素材区的全部卡
            List<EntityCard> fusionZoneCards = new List<EntityCard>();
            foreach (var region in fusionSumLocReg)
                if (region?.cards != null)
                    fusionZoneCards.AddRange(region.cards);
            List<EntityCard> materialCards = new List<EntityCard>();
            foreach (var region in materialLocReg)
                if (region?.cards != null)
                    materialCards.AddRange(region.cards);
            if (includeSelf == 1 && entityCard != null && !materialCards.Exists(c => c.currentid == entityCard.currentid))
                materialCards.Add(entityCard);                            //包含自身:效果持有卡自身并入素材池(其所在区域可不在materialLoc中,如"自身+对方场上"类效果)
            //永续拦截(OC):被操作候选逐个剔除——素材池/融合目标中"自身持OC编号对应且生效的永续"(本组件OperationContinues,如不能作为融合素材/不能被融合召唤)剔除;剔光则融合不执行
            fusionZoneCards = FilterOperationCandidates(fusionZoneCards);
            materialCards = FilterOperationCandidates(materialCards);
            if (fusionZoneCards.Count == 0 || materialCards.Count == 0)
            {
                return;
            }

            // 1.筛选可融合的融合怪兽(currentType==2,即具有融合素材要求的融合怪兽);素材不足的融合怪兽不进入选择弹窗
            List<EntityMonsterCard> candidates = new List<EntityMonsterCard>();
            foreach (var card in fusionZoneCards)
            {
                EntityMonsterCard monster = card as EntityMonsterCard;
                bool isFusion = monster != null && monster.currentType == 2;               //2=融合
                bool meetGrade = isFusion && MeetFusionGrade(monster);                      //speNameKey的等级条目(>=N/<=N):不满足等级的融合怪兽不进入候选
                bool banned = isFusion && IsBannedFusionMonster(monster);                   //除外卡名(Name_Key=-1):该怪兽不能经本效果融合召唤,不进入候选
                MonsterCard staticCard = monster != null ? monster.card as MonsterCard : null;
                bool hasMatter = staticCard?.SpeMatter != null && staticCard.SpeMatter.Count > 0;
                string matterDesc = hasMatter ? string.Join(";", staticCard.SpeMatter.Select(kv => $"{kv.Key}:{string.Join("/", kv.Value)}")) : "无";
                bool canSatisfy = isFusion && meetGrade && !banned && hasMatter && CanSatisfyMaterial(staticCard.SpeMatter, materialCards);   //当前素材无法凑齐该怪兽的全部条件,不显示
                if (!canSatisfy) continue;
                candidates.Add(monster);
            }
            if (candidates.Count == 0)
            {
                return;
            }

            // 2.场上怪兽区已满则无法特招
            EntityPlayer.Field field = entityFindComponents[5] as EntityPlayer.Field;
            if (field == null || field.GetMonsterZoneCount() >= 5)
            {
                return;
            }

            // 3.弹窗选择融合怪兽
            List<EntityCard> chooseRes;
            chooseRes = await extraQuest.GetEntityCard.Invoke(candidates.Cast<EntityCard>().ToList(), 1);
            if (chooseRes == null || chooseRes.Count == 0)
            {
                return;
            }
            // 弹窗选中的实体与额外卡组挂载的真实实体可能不是同一实例,映射回真实实体保证区域操作与诱发匹配
            EntityMonsterCard chooseMonster = (extraQuest.MapRealCard != null ? extraQuest.MapRealCard(chooseRes[0]) : chooseRes[0]) as EntityMonsterCard;
            if (chooseMonster == null)
            {
                return;
            }
            MonsterCard staticMonster = chooseMonster.card as MonsterCard;
            if (staticMonster?.SpeMatter == null || staticMonster.SpeMatter.Count == 0)
            {
                return;
            }

            // 4.逐步弹窗选择融合素材:先选第一个,刷新UI后再选剩余合法素材,直到选满素材条件数;素材未移动,任意环节失败直接结束(无需回退)
            List<EntityCard> pool = new List<EntityCard>();
            foreach (var card in materialCards)
                if (card.currentid != chooseMonster.currentid) pool.Add(card);           //排除融合怪兽本身(素材区含额外卡组时)
            KeyValuePair<string, string[]>[] conds = staticMonster.SpeMatter.ToArray();
            List<EntityCard> selectedMaterials = new List<EntityCard>();
            if (includeSelf == 1)
            {
                //包含自身:效果持有卡必须作为融合素材,直接从素材池并入(不弹窗);
                //  不在素材池(含恰为被融合怪兽本身)、或不匹配任一素材条件时本次融合不可行,直接结束
                EntityCard selfCard = entityCard == null ? null : pool.FirstOrDefault(c => c.currentid == entityCard.currentid);
                if (selfCard == null || selfCard.currentid == chooseMonster.currentid)
                    return;
                bool selfOk = false;
                for (int i = 0; i < conds.Length && !selfOk; i++)
                    if (IsMaterialMatch(conds[i], selfCard)) selfOk = true;
                if (!selfOk) return;
                pool.Remove(selfCard);                             //从弹窗池移除,避免被重复选择
                selectedMaterials.Add(selfCard);                   //自身作为强制素材并入(占用1个素材条件)
            }
            while (selectedMaterials.Count < conds.Length)
            {
                // 计算每个素材条件还需几张(已选素材贪心匹配剩余条件)
                int[] remain = new int[conds.Length];
                for (int i = 0; i < conds.Length; i++) remain[i] = 1;
                foreach (var sel in selectedMaterials)
                    for (int i = 0; i < conds.Length; i++)
                        if (remain[i] > 0 && IsMaterialMatch(conds[i], sel)) { remain[i] = 0; break; }
                // 刷新弹窗:候选=能匹配任一未满足条件的卡(素材条件仅为1个时同样走弹窗选择)
                List<EntityCard> avail = new List<EntityCard>();
                foreach (var card in pool)
                {
                    if (selectedMaterials.Exists(s => s.currentid == card.currentid)) continue;
                    bool ok = false;
                    for (int i = 0; i < conds.Length; i++)
                        if (remain[i] > 0 && IsMaterialMatch(conds[i], card)) { ok = true; break; }
                    if (ok) avail.Add(card);
                }
                if (avail.Count == 0)                                                     //无合法素材可选,直接结束
                {
                    return;
                }
                if (selectedMaterials.Count > 0)                                        //选完上一个素材后,等待1s再弹出选择下一个素材的弹窗
                    await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: FightAsyncScope.Token);   //对局结束:终止该融合选材链
                List<EntityCard> pick = await extraQuest.GetEntityCard.Invoke(avail, 1);
                if (pick == null || pick.Count == 0)
                {
                    return;
                }
                // 弹窗选中的实体与宿主区域挂载的真实实体可能不是同一实例,映射回真实实体保证区域操作与诱发匹配(对方区域卡同样可被选为素材)
                EntityCard realMat = extraQuest.MapRealCard != null ? extraQuest.MapRealCard(pick[0]) : pick[0];
                if (realMat == null)
                {
                    return;
                }
                selectedMaterials.Add(realMat);
            }
            // 整体回溯校验(贪心分配可能非最优),匹配结果作为送墓顺序
            List<EntityCard> materials = new List<EntityCard>();
            if (!BackTrackChoose(conds, 0, selectedMaterials, new List<EntityCard>(), materials))
            {
                return;
            }

            // 5.素材处理:从原区域移除,按materialHandleWay送往目标区域(缺省送墓地)
            //   归属对方的素材(playerIndex==1)物理宿主在对方区域(组件索引整体+6),移除后自动送往对方对应区域(如对方墓地);己方素材走原逻辑
            List<int> materialTargets = new List<int>();                    //与materials一一对应:记录每张素材的目标区域种类(0-5),供第7步UI按目标分发(4=除外区走除外UI)
            foreach (var mat in materials)
            {
                // 从真实宿主区域移除(不依赖location字段:额外卡组卡location曾失真,按location取区域Remove会落空→原区域数据残留)
                RemoveFromRealHost(mat);
                EntityPlayer matOwner = mat.GetOwner();                    //素材归属者(原本所有者)
                bool isEnemyMat = matOwner != null && matOwner.GetPlayerIndex() == 1;  //素材归属对方:目标区域取对方对应区域组件
                int srcIdx = LocToInt(mat.location);                       //素材位置种类(0-5),用于取materialHandleWay处理方式(缺省送墓)
                int tarBaseIdx = (materialHandleWay != null && srcIdx < materialHandleWay.Count) ? materialHandleWay[srcIdx] : 3;  //目标区域种类(0-5)
                //目标=返回卡组(0)时:额外怪兽(融合/同调/超量/连接,currentType∈2-5)按规则返回额外卡组(1),其余(主卡组/仪式怪兽/魔法陷阱)返回主卡组
                if (tarBaseIdx == 0 && IsExtraDeckMonster(mat)) tarBaseIdx = 1;
                int tarCompIdx = isEnemyMat ? tarBaseIdx + 6 : tarBaseIdx; //目标组件索引:对方素材自动送往对方对应区域
                mat.ChangeLocation(RegionCodeIndexToLocation(tarCompIdx));
                entityFindComponents[tarCompIdx].AddCard(mat);
                materialTargets.Add(tarBaseIdx);
                if (tarBaseIdx == 3 && extraQuest?.timePointBases != null) //素材送墓,加入诱发时点
                    extraQuest.timePointBases.Add(new StateMachine.InCemetery(mat, GameManage.CardLocation.Cemetery));
                else if (tarBaseIdx == 4 && extraQuest?.timePointBases != null) //素材除外,加入诱发时点
                    extraQuest.timePointBases.Add(new StateMachine.InBanished(mat, GameManage.CardLocation.Banished));
                //素材来自额外卡组且去向≠额外卡组(送墓/除外/回手/回卡组等):该素材即"离开额外卡组",额外登记LeaveEx时点供诱发
                if (srcIdx == 1 && tarBaseIdx != 1 && extraQuest?.timePointBases != null)
                    extraQuest.timePointBases.Add(new StateMachine.LeaveEx(mat, RegionCodeIndexToLocation(tarCompIdx)));
            }

            // 6.融合怪兽从真实宿主区域移除(不依赖location字段:开局额外卡组卡location曾默认Deck失真,按location取区域Remove会落空→融合怪残留额外卡组数据)
            //   特招动作由特招UI确认时触发SpeSom完成(避免与第7步UI特招重复AddMonster,且额外卡组原预制体由EntitySpeSom的ExtraDeck分支回收)
            RemoveFromRealHost(chooseMonster);

            // 7.UI同步: 素材按目标区域分发UI(送墓地→GotoCeCemeteryUI;除外区→GotoBanishedUI;返回手卡→GotoHandUI;
            //   返回卡组0/额外卡组1→GotoDeckUI回收预制体;额外怪兽回卡组在第5步已自动改为额外卡组), 融合怪兽特招(特招UI)
            List<EntityCard> moved = new List<EntityCard>();
            moved.AddRange(materials);
            moved.Add(chooseMonster);
            List<Transform> allTfs = ExtraQuest.EntityCardToTransfrom?.Invoke(moved);
            if (allTfs == null) allTfs = new List<Transform>();
            List<Transform> matTfs = new List<Transform>();
            for (int i = 0; i < materials.Count && i < allTfs.Count; i++)
                matTfs.Add(allTfs[i]);      //保留null占位与materials一一对应:无预制体素材(从卡组/额外卡组选取)由墓地/除外UI新建展示,不能预丢弃造成下标错位/素材丢失
            List<EntityCard> handMats = new List<EntityCard>();              //目标=手牌(2)的素材,走手牌区UI(PlaHandReg/EneHandReg)
            List<Transform> handTfs = new List<Transform>();
            List<EntityCard> deckMats = new List<EntityCard>();              //目标=卡组(0)/额外卡组(1)的素材,走GotoDeckUI回收预制体(卡组为隐藏区不新建展示)
            List<Transform> deckTfs = new List<Transform>();
            List<EntityCard> banishedMats = new List<EntityCard>();         //目标=除外区(4)的素材,走除外区UI(PlaBan/EneBan)
            List<Transform> banishedTfs = new List<Transform>();
            List<EntityCard> cemMats = new List<EntityCard>();              //目标=墓地(3,缺省)走墓地UI
            List<Transform> cemTfs = new List<Transform>();
            for (int i = 0; i < materials.Count; i++)
            {
                Transform matTf = i < matTfs.Count ? matTfs[i] : null;
                int target = (i < materialTargets.Count) ? materialTargets[i] : 3;
                if (target == 2)
                {
                    handMats.Add(materials[i]);
                    handTfs.Add(matTf);
                }
                else if (target == 0 || target == 1)
                {
                    deckMats.Add(materials[i]);
                    deckTfs.Add(matTf);
                }
                else if (target == 4)
                {
                    banishedMats.Add(materials[i]);
                    banishedTfs.Add(matTf);
                }
                else
                {
                    cemMats.Add(materials[i]);
                    cemTfs.Add(matTf);
                }
            }
            if (handMats.Count > 0 && handQuest?.HandleTransfrom != null)
                await handQuest.HandleTransfrom(handMats, handTfs);
            if (deckMats.Count > 0 && deckQuest?.HandleTransfrom != null)
                await deckQuest.HandleTransfrom(deckMats, deckTfs);
            if (banishedMats.Count > 0 && banishedQuest?.HandleTransfrom != null)
                await banishedQuest.HandleTransfrom(banishedMats, banishedTfs);
            if (cemMats.Count > 0 && cemeteryQuest?.HandleTransfrom != null)
                await cemeteryQuest.HandleTransfrom(cemMats, cemTfs);
            List<EntityCard> sumList = new List<EntityCard> { chooseMonster };
            List<Transform> sumTfs = new List<Transform>();
            if (allTfs.Count > materials.Count)
                sumTfs.Add(allTfs[materials.Count]);
            if (extraQuest?.HandleTransfrom != null)
            {
                await extraQuest.HandleTransfrom(sumList, sumTfs);
            }

            // 8.融合怪兽特招时点,供诱发效果响应
            if (extraQuest?.timePointBases != null)
            {
                extraQuest.timePointBases.Add(new StateMachine.FusionSummon(chooseMonster));
                //额外卡组融合怪兽经融合召唤出场即"离开额外卡组",额外登记LeaveEx时点供诱发
                extraQuest.timePointBases.Add(new StateMachine.LeaveEx(chooseMonster, GameManage.CardLocation.Field));
            }
        }

        public List<EntityCard> GetObjects()
        {
            return null;
        }
        private bool IsExtraDeckMonster(EntityCard mat)                     //是否额外卡组怪兽(融合/同调/超量/连接,currentType∈2-5;主卡组0与仪式1不属额外)
        {
            return mat is EntityMonsterCard em && em.currentType >= 2 && em.currentType <= 5;
        }
        private void RemoveFromRealHost(EntityCard card)                    //从真实宿主区域移除素材/融合怪兽
        {
            //不依赖location字段定位区域:location在开局(额外卡组卡曾默认Deck)或迁移过程中可能失真,按location取区域组件Remove会落在错误区域(空操作)→
            //素材/融合怪残留原区域数据(表现为"融合召唤后额外卡组仍有该怪兽,还能被再次选中/参与区域计数")。
            //改为遍历12个区域组件(己方0-5+对方6-11)按cards.Contains定位真实宿主后再移除。
            if (card == null || entityFindComponents == null) return;
            foreach (var reg in entityFindComponents)
            {
                if (reg?.cards == null || !reg.cards.Contains(card)) continue;
                if (reg is EntityPlayer.Field field)
                {
                    field.RemoveMonster(card);                              //场上怪兽:须同时清怪兽区/魔陷区(RemoveCard仅移除cards会残留占位→同实例双持)
                    field.RemoveMagicTrap(card);
                }
                else
                    reg.RemoveCard(card);
                return;
            }
        }
        private bool CanSatisfyMaterial(Dictionary<string, string[]> conditions, List<EntityCard> materials)   //当前素材池能否凑齐融合怪兽的全部素材条件(含特殊名字/字段要求)
        {
            if (maxMaterialCount > 0 && conditions.Count > maxMaterialCount)
                return false;                                                 //素材张数上限:目标怪兽所需素材条件数超过上限则不可融合(0=不限制)
            if (includeSelf == 1 && (entityCard == null || !materials.Exists(c => c.currentid == entityCard.currentid)))
                return false;                                                 //包含自身:效果持有卡不在素材区则不可能凑齐素材
            KeyValuePair<string, string[]>[] conds = conditions.ToArray();
            return BackTrackMatch(conds, 0, materials, new List<EntityCard>());
        }
        private bool BackTrackMatch(KeyValuePair<string, string[]>[] conds, int index, List<EntityCard> materials, List<EntityCard> used)
        {
            if (index == conds.Length)
            {
                if (includeSelf == 1 && !used.Exists(c => c.currentid == entityCard.currentid))  //包含自身:该组合必须用到效果持有卡
                    return false;
                return AnySpecialUsed(used);                                          //已使用的素材满足任一特殊名字/字段条件即可
            }
            for (int i = 0; i < materials.Count; i++)
            {
                EntityCard m = materials[i];
                if (used.Contains(m))
                    continue;
                if (!IsMaterialMatch(conds[index], m))
                    continue;
                used.Add(m);
                if (BackTrackMatch(conds, index + 1, materials, used))
                    return true;
                used.Remove(m);
            }
            return false;
        }
        private bool BackTrackChoose(KeyValuePair<string, string[]>[] conds, int index, List<EntityCard> materials, List<EntityCard> used, List<EntityCard> result)
        {
            if (index == conds.Length)
            {
                if (AnySpecialUsed(used))                                              //特殊名字/字段条件(任一满足)校验
                {
                    result.AddRange(used);
                    return true;
                }
                return false;
            }
            for (int i = 0; i < materials.Count; i++)
            {
                EntityCard m = materials[i];
                if (used.Contains(m))
                    continue;
                if (!IsMaterialMatch(conds[index], m))
                    continue;
                used.Add(m);
                if (BackTrackChoose(conds, index + 1, materials, used, result))
                    return true;
                used.Remove(m);
            }
            return false;
        }
        // 解析 speNameKey 等级条目:">=N" / "<=N"; 是则返回 true(ge=true表示大于等于,false表示小于等于;bound=等级阈值)
        private static bool TryParseGradeKey(string key, out bool ge, out int bound)
        {
            ge = false;
            bound = 0;
            if (string.IsNullOrEmpty(key)) return false;
            if (key.StartsWith(">=")) { if (int.TryParse(key.Substring(2), out bound)) { ge = true; return true; } return false; }
            if (key.StartsWith("<=")) { if (int.TryParse(key.Substring(2), out bound)) { ge = false; return true; } return false; }
            return false;
        }
        // 融合怪兽是否满足 speNameKey 中全部等级约束(>=N / <=N); 无等级条目恒真(等级约束作用于融合怪兽,素材仍走AnySpecialUsed的名字/字段匹配)
        private bool MeetFusionGrade(EntityMonsterCard monster)
        {
            if (monster == null || speNameKey == null) return true;
            foreach (var key in speNameKey)
            {
                if (!TryParseGradeKey(key, out bool ge, out int bound)) continue;
                if (ge ? monster.currentGrade < bound : monster.currentGrade > bound) return false;
            }
            return true;
        }
        private bool AnySpecialUsed(List<EntityCard> used)                             //已选素材是否满足任一特殊名字/字段条件(等级条目>=N/<=N作用于融合怪兽,不在此判断)
        {
            if (speNameKey == null || speNameKey.Count == 0)
                return true;
            bool hasNameCondition = false;                                            //是否存在真正要求素材匹配的名字/字段条件
            for (int i = 0; i < speNameKey.Count; i++)
            {
                string key = speNameKey[i];
                if (string.IsNullOrEmpty(key) || TryParseGradeKey(key, out _, out _))
                    continue;                                                         //等级条目作用于融合怪兽等级,不约束素材
                int mode = (Name_Key != null && i < Name_Key.Count) ? Name_Key[i] : 0;
                if (mode == -1) continue;                                                //除外卡名(Name_Key=-1):不要求素材包含,仅用于排除对应融合怪兽
                hasNameCondition = true;                                              //存在真实的素材名字/字段要求
                foreach (var m in used)
                {
                    if (mode == 0)
                    {
                        if (m.currentName == key)                                      //按卡名匹配
                            return true;
                    }
                    else if (m.currentKey != null && m.currentKey.Contains(key))       //按字段匹配
                        return true;
                }
            }
            if (!hasNameCondition) return true;                                       //素材无任何名字/字段要求(仅限制目标等级/排除卡名时),素材不受约束
            return false;
        }
        private bool IsBannedFusionMonster(EntityMonsterCard monster)         //除外卡名(Name_Key对应-1):该卡名的融合怪兽不能被本效果融合召唤
        {
            if (monster == null || speNameKey == null) return false;
            for (int i = 0; i < speNameKey.Count; i++)
            {
                string key = speNameKey[i];
                if (string.IsNullOrEmpty(key) || TryParseGradeKey(key, out _, out _)) continue;  //等级条目跳过
                int mode = (Name_Key != null && i < Name_Key.Count) ? Name_Key[i] : 0;
                if (mode != -1) continue;                                    //仅"除外卡名"(-1)参与排除
                if (monster.currentName == key) return true;                 //按卡名精确排除:该融合怪兽不能经本效果融合召唤
            }
            return false;
        }
        private bool IsMaterialMatch(KeyValuePair<string, string[]> condition, EntityCard m)
        {
            EntityMonsterCard monster = m as EntityMonsterCard;
            switch (condition.Key)
            {
                case "name":                                   //卡名精确匹配
                    return condition.Value.Contains(m.currentName);
                case "type":                                   //卡类型（主卡组0/仪式1/融合2/同调3/超量4/连接5）
                    return monster != null && condition.Value.Contains(monster.currentType.ToString());
                case "attribute":                              //属性
                    return monster != null && condition.Value.Contains(monster.currentAttribute);
                case "native":                                 //种族
                    return monster != null && condition.Value.Contains(monster.currentNative);
                case "baseType":                               //基本类型（通常/效果）
                    return monster != null && condition.Value.Contains(monster.currentBaseType.ToString());
                case "grade":                                  //星级
                    return monster != null && condition.Value.Contains(monster.currentGrade.ToString());
                default:
                    return false;
            }
        }
    }

    public class DestoryCard                                    //破坏卡片
        : CardComponent, Eff, IEffGetObject
    {
        private List<EntityCard> objects;                            //Effection.EffAction注入的取对象结果(快照副本)
        private List<GameManage.CardLocation> findComponent;        //破坏候选来源区域(为空=全部区域)
        private List<EntityFindComponent> entityFindComponents;
        //破坏条件(二维选卡条件,大组下标/编码与GoToCemeteries、JudExist共用,求值见Condition2D):
        private List<List<string>> cardCondition;                   //cardCondition(留空=不限制任何卡)
        private string excludeName;                                 //排除卡名(该卡名不算破坏候选;留空=不排除)
        private List<string> hitList;                               //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        private List<EntityCard> destroyed;                         //本次实际被破坏的卡(破坏动作+时点落位)
        public int isGetObject { get; private set; }                 //是否取对象(>=1=发动时先经弹窗选取对象,结算时直接破坏对象,不自行收集/弹窗)

        public DestoryCard(ExtraQuest extraQuest)
        {
            this.extraQuest = extraQuest;
        }
        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniDestoryCard = cardComponent as IniDestoryCard;
            if (iniDestoryCard == null) throw new Exception("Invalid card component type for DestoryCard");
            this.num = iniDestoryCard.num;
            this.findComponent = iniDestoryCard.findComponent;
            this.cardCondition = iniDestoryCard.cardCondition ?? new List<List<string>>();
            this.excludeName = iniDestoryCard.excludeName;
            this.hitList = iniDestoryCard.hitList;
            this.isGetObject = iniDestoryCard.isGetObject;
            entityFindComponentsCode = (findComponent == null || findComponent.Count == 0) ? AllRead() : LocationToBools(findComponent);   //未配置区域=全部区域
            this.QuestBools = QuestBools;
        }

        public void IniCardCompent()
        {
            entityCards = new List<EntityCard>();
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }

        public async UniTask OnTrigger()
        {
            IniCardCompent();
            destroyed = new List<EntityCard>();
            List<EntityCard> targets = new List<EntityCard>();
            if (isGetObject == 1)
            {
                //取对象模式:直接破坏注入的对象(不自行收集候选/不弹窗),结算时先校验对象是否已改变
                if (objects == null || objects.Count == 0) return;      //未取到对象:不处理
                List<EntityFindComponent> allRegions = QuestBools?.Invoke(AllRead());   //对象可能来自全部区域,用全区域回溯对照
                foreach (var obj in objects)
                {
                    //对象变化判定:注入副本与真实实体共用currentid,以id回溯对照——全部区域找不到同id真实实体,视为对象已改变
                    EntityCard real = FindRealCardById(allRegions, obj.currentid);
                    if (real == null) return;                           //对象已改变:直接返回,不操作
                    if (!targets.Contains(real)) targets.Add(real);
                }
            }
            else
            {
                //不取对象模式:二维读取候选并弹UI(即使候选数量恰好等于需求数,选择要破坏的卡
                if (entityFindComponents == null || entityFindComponents.Count == 0) return;
                List<EntityCard> candidates = new List<EntityCard>();
                foreach (var item in entityFindComponents)
                {
                    if (item?.cards == null) continue;
                    foreach (var card in item.cards)
                    {
                        if (card == null) continue;
                        if (IsCardOperationBlocked(card)) continue;   //永续拦截(OC):候选自身持OC编号对应且生效的永续(本组件OperationContinues,如"不受效果破坏"等)→ 该卡不能作为本次被操作候选,逐个剔除
                        if (!string.IsNullOrEmpty(excludeName) && card.currentName == excludeName) continue;   //排除名不算候选
                        if (!Condition2D.IsMatch(card, cardCondition, hitList, item.cards)) continue;           //二维选卡条件(cardCondition为空=不限制)
                        if (!candidates.Contains(card)) candidates.Add(card);                                  //跨区域同实例防御
                    }
                }
                if (candidates.Count < num) return;                     //候选不足需求数:无法完成破坏
                List<EntityCard> chooseRes = (extraQuest != null && extraQuest.GetEntityCard != null)
                    ? await extraQuest.GetEntityCard.Invoke(candidates, num)
                    : candidates.GetRange(0, num);
                if (chooseRes == null || chooseRes.Count < num) return; //玩家取消或未选满:不破坏
                //弹窗选中的实体与宿主区域挂载的真实实体可能不是同一实例(数据一致),破坏前映射回真实实体,保证区域移除与诱发匹配
                foreach (var card in chooseRes)
                {
                    EntityCard real = (extraQuest != null && extraQuest.MapRealCard != null) ? (extraQuest.MapRealCard(card) ?? card) : card;
                    if (real != null && !targets.Contains(real)) targets.Add(real);
                }
            }
            //永续拦截(OC):结算前对最终目标列表统一逐一剔除——含取对象对象(已映射回真实实体)/弹窗选定等全部被操作目标,
            //目标自身持OC编号对应且生效的永续(本组件OperationContinues,如"不受效果破坏"等)→剔除;全被剔光则效果不执行
            targets = FilterOperationCandidates(targets);
            if (targets.Count == 0) return;

            //破坏动作:从实际宿主区域移除(场上卡须同时从怪兽区/魔陷区移除),送往墓地;全部区域用于宿主定位与墓地添加
            //统一走公共送墓入口(数据层区域移动+InCemetery/LeaveEx诱发时点登记+预制体UI同步),被破坏送入墓地的卡同样会登记进墓诱发时点
            List<EntityFindComponent> moveRegions = QuestBools?.Invoke(AllRead());
            if (moveRegions == null || moveRegions.Count < 4) return;    //需含己方墓地(index 3)用于破坏后送墓
            destroyed = await SendCardToCemetery(targets, moveRegions, extraQuest);
            //破坏完成:将每张被破坏的卡作为DestroyCard时点加入时点队列,供"被破坏时"诱发效果响应(时点落位为被破坏的卡)
            if (extraQuest?.timePointBases != null)
            {
                foreach (var card in destroyed)
                    extraQuest.timePointBases.Add(new StateMachine.DestroyCard(card));
            }
        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }
    }

    public class EffReturnHand                                  //效果：返回持有者手卡(将卡从原区域移回其原本所有者的手牌,仿照EffGoToCemetery)
        : CardComponent, Eff, IEffGetObject
    {
        private List<EntityFindComponent> entityFindComponents;
        private List<EntityCard> entities;                       //返回手卡的卡列表
        private List<EntityCard> objects;                        //Effection.EffAction注入的取对象结果(快照副本)
        private int isEntityCard;                                //是否包含自身（=1 表示将效果持有卡自身返回手卡）
        public int isGetObject { get; private set; }             //是否取对象(>=1=发动时先经弹窗选取对象,再以选中的对象作为回手目标)

        public EffReturnHand(EntityCard entityCard, ExtraQuest extraQuest = null)
        {
            this.entityCard = entityCard;
            this.extraQuest = extraQuest;                       //注入UI同步回调(eQ.GoToHandEQ,其HandleTransfrom=fightUI.GotoHandUI),否则OnTrigger中无法移动卡牌预制体到手牌区
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniEffReturnHand = cardComponent as IniEffReturnHand;
            if (iniEffReturnHand == null)
                throw new Exception("Invalid card component type for EffReturnHand");
            this.isGetObject = iniEffReturnHand.isGetObject;
            this.isEntityCard = iniEffReturnHand.isEntityCard;
            entityFindComponentsCode = AllRead();
            this.QuestBools = QuestBools;
        }

        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public void SetObjects(List<EntityCard> objects)            //Effection.EffAction结算前注入:发动阶段(CostPay)取到的对象(快照副本)
        {
            this.objects = objects == null ? null : new List<EntityCard>(objects);
        }

        public async UniTask OnTrigger()
        {
            IniCardCompent();
            entities = new List<EntityCard>();
            if (isGetObject == 1)
            {
                //取对象模式:直接操作注入的对象
                if (objects == null || objects.Count == 0) return;      //未取到对象:不处理
                foreach (var obj in objects)
                {
                    //对象变化判定:注入副本与真实实体共用currentid,以id回溯对照——当前全部区域找不到同id真实实体,视为对象已改变
                    EntityCard real = FindRealCardById(entityFindComponents, obj.currentid);
                    if (real == null) return;                           //对象已改变:直接返回,不操作
                    if (!entities.Contains(real)) entities.Add(real);
                }
            }
            else if (isEntityCard == 1)
            {
                entities.Add(entityCard);                               //包含自身模式:效果持有卡自身返回手牌
            }
            if (entities.Count == 0) return;                            //无目标(未取到对象/非自身模式):不执行
            //永续拦截(OC):结算前对最终目标列表(自身/取对象对象已映射回真实实体)统一逐一剔除——目标自身持OC编号对应且生效的永续(本组件OperationContinues)→剔除;全被剔光则效果不执行
            entities = FilterOperationCandidates(entities);
            if (entities.Count == 0) return;
            foreach (var item in entities)
            {
                if (item == null) continue;
                GameManage.CardLocation cardLocation = item.location;
                //宿主定位:目标可能位于双方任意区域(对方场上/己方场上等),按实际持有列表查找宿主再移除,
                //避免按location索引误从同序己方区域移除导致目标残留在原区域(仿EffGoToBanished)
                EntityFindComponent srcRegion = null;
                if (entityFindComponents != null)
                    foreach (var r in entityFindComponents)
                        if (r?.cards != null && r.cards.Contains(item)) { srcRegion = r; break; }
                if (srcRegion == null) continue;                        //真实实体已不在任何区域(如对象变化后被其它效果移动):跳过
                srcRegion.RemoveCard(item);
                item.ChangeLocation(GameManage.CardLocation.Hand);
                entityFindComponents[2].AddCard(item);                  //送入己方手牌区组件(AddCard自动按原本所有者纠偏到其手牌=返回持有者手牌)
            }
            if (extraQuest != null && extraQuest.HandleTransfrom != null)
                await extraQuest.HandleTransfrom(entities, ExtraQuest.EntityCardToTransfrom?.Invoke(entities));
        }

        public List<EntityCard> GetObjects()
        {
            return objects == null ? null : new List<EntityCard>(objects);
        }
    }

    public class CloseBehindEff                                 //跳过后续效果结算
    {

    }

    public class GetCard                                      //效果:检索(从卡组按二维选卡条件选卡加入手卡;检索方=效果卡当前控制者,config参数布局与JudExist保持一致)
        : CardComponent, Eff
    {
        private int num;                                        //检索张数(>=1;候选多于检索张数时弹窗让玩家挑选)
        private List<List<string>> cardCondition;               //二维选卡条件(大组下标/编码与JudExist共用,见EffLogic.Condition2D;留空=不限制)
        private List<GameManage.CardLocation> findComponent;    //检索来源区域(Deck=检索方自己的卡组;e_Deck=对方卡组;留空=检索方自己的卡组)
        private List<string> hitList;                           //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        private List<EntityFindComponent> entityFindComponents;
        private ExtraQuest popupQuest;                          //选卡弹窗回调(eQ.GoToHandEQ:GetEntityCard=玩家选卡弹窗,MapRealCard=弹窗实体→真实实体映射);候选多于检索张数时使用

        public GetCard(EntityCard entityCard, ExtraQuest extraQuest = null, ExtraQuest popupQuest = null)
        {
            this.entityCard = entityCard;                       //效果持有卡(决定检索方:其当前控制者)
            this.extraQuest = extraQuest;                       //检索到手后UI与时点(eQ.DrawCardEQ:timePointBases=时点队列,DrawCardHandle=刷新手牌区),否则无法展示新入手卡与登记GetCard时点
            this.popupQuest = popupQuest;                       //选卡弹窗(eQ.GoToHandEQ:GetEntityCard/MapRealCard)
        }

        public void IniCardCompent(IniCardComponent cardComponent, Func<List<bool>, List<EntityFindComponent>> QuestBools)
        {
            var iniGetCard = cardComponent as IniGetCard;
            if (iniGetCard == null)
                throw new Exception("Invalid card component type for GetCard");
            this.num = Math.Max(1, iniGetCard.num);             //检索张数(至少1)
            this.cardCondition = iniGetCard.cardCondition ?? new List<List<string>>();
            this.findComponent = iniGetCard.findComponent;
            this.hitList = iniGetCard.hitList;
            entityFindComponentsCode = AllRead();               //读取全部区域(含对方0-11),便于按检索方索引(前6己方/后6对方)定位卡组与手牌
            this.QuestBools = QuestBools;
        }

        public void IniCardCompent()
        {
            entityFindComponents = QuestBools?.Invoke(entityFindComponentsCode);
        }

        public async UniTask OnTrigger()
        {
            IniCardCompent();
            //检索方=效果卡当前控制者(0我方/1对方);区域列表前6位为该方,后6位为对方
            int ownerPlayerIndex = entityCard?.GetCurrentOwner()?.GetPlayerIndex() ?? 0;
            //检索来源:配置Deck(缺省)=检索方自己的卡组;e_Deck=对方的卡组
            bool searchEnemyDeck = findComponent != null && findComponent.Contains(GameManage.CardLocation.EnemyDeck);
            int retrievePlayerIndex = searchEnemyDeck ? (ownerPlayerIndex == 0 ? 1 : 0) : ownerPlayerIndex;   //被检索方(卡组/手牌归属方)
            int offset = retrievePlayerIndex == 0 ? 0 : 6;
            if (entityFindComponents == null || offset + 2 >= entityFindComponents.Count) return;
            EntityFindComponent deck = entityFindComponents[offset + 0];    //检索卡组
            EntityFindComponent hand = entityFindComponents[offset + 2];    //检索手牌(加入手卡目标=卡组归属方自己的手牌)
            if (deck == null || hand == null || deck.cards == null) return;
            //1.收集候选:从检索卡组中挑出满足二维选卡条件的卡
            List<EntityCard> candidates = new List<EntityCard>();
            foreach (var card in deck.cards)
            {
                if (card == null) continue;
                if (IsCardOperationBlocked(card)) continue;   //永续拦截(OC):候选自身持OC编号对应且生效的永续(本组件OperationContinues)→ 该卡不能作为本次被操作候选,逐个剔除
                if (!Condition2D.IsMatch(card, cardCondition, hitList, deck.cards)) continue;       //deck.cards作poolCards供怪兽维度3/4/5查对应怪兽实体
                if (!candidates.Contains(card)) candidates.Add(card);                               //同实例防御
            }
            if (candidates.Count < num) return;                 //候选不足检索张数:无法完成检索(不执行)
            //2.选卡:候选多于检索张数时弹窗让玩家挑选;恰好等于检索张数时全部加入手卡
            List<EntityCard> chooseRes;
            if (candidates.Count > num)
            {
                if (popupQuest == null || popupQuest.GetEntityCard == null) return;    //无选卡弹窗回调且候选过多:无法挑选(不执行)
                chooseRes = await popupQuest.GetEntityCard.Invoke(candidates, num);
            }
            else chooseRes = candidates;
            if (chooseRes == null || chooseRes.Count == 0) return;  //玩家取消:不检索
            //3.真实实体归一+去重,并确保仍位于检索卡组(弹窗实体与卡组挂载的真实实体可能数据一致但实例不同)
            List<EntityCard> got = new List<EntityCard>();
            foreach (var card in chooseRes)
            {
                if (card == null) continue;
                EntityCard real = (popupQuest != null && popupQuest.MapRealCard != null) ? (popupQuest.MapRealCard(card) ?? card) : card;
                if (real != null && deck.cards.Contains(real) && !got.Contains(real)) got.Add(real);
            }
            if (got.Count == 0) return;
            //4.数据层移动:卡组移除→加入手牌,并逐卡登记GetCard时点(供"自身被从卡组加入手卡时"诱发效果响应)
            foreach (var item in got)
            {
                if (!deck.cards.Contains(item)) continue;       //已被前置效果移走(连锁途中):跳过
                GameManage.CardLocation fromLocation = item.location;   //移动前所在区域(检索来源=卡组)
                deck.RemoveCard(item);
                item.ChangeLocation(GameManage.CardLocation.Hand);
                hand.AddCard(item);
                if (extraQuest?.timePointBases != null)
                    extraQuest.timePointBases.Add(new StateMachine.GetCard(item, fromLocation));
            }
            //5.UI同步:检索卡从卡组揭开并按完整手牌列表重建手牌区(DrawCardHandle)
            if (extraQuest?.DrawCardHandle != null)
                await extraQuest.DrawCardHandle.Invoke(retrievePlayerIndex, hand.cards);
        }

        public List<EntityCard> GetObjects()
        {
            return null;
        }
    }
    #endregion

    #region 工厂模式初始化效果属性
    public abstract class IniCostComponent
    {

    }
    public class IniExist
        : IniCostComponent
    {
        public List<GameManage.CardLocation> cardLocations;   //查找地点(为空=全部区域)
        public List<List<string>> cardCondition;              //二维选卡条件(可为null):cardCondition[i][j]=大组i第j个候选取值,编码与维度约定见EffLogic.Condition2D
        public string excludeName;                            //排除卡名(该卡名不算命中;留空=不排除)
        public List<string> hitList;                          //命中链(可为null):各"或"分支(CSV中元素间用|分隔),分支内*连接叶子引用"大组/子项"须同时命中;见EffLogic.Condition2D
        public IniExist(List<GameManage.CardLocation> cardLocations, List<List<string>> cardCondition, string excludeName = "", List<string> hitList = null)
        {
            this.cardLocations = cardLocations;
            this.cardCondition = cardCondition;
            this.excludeName = excludeName;
            this.hitList = hitList;
        }
    }
    public class IniDrawCard
        : IniCostComponent
    {
        public int cardNum;
        public IniDrawCard(int cardNum)
        {
            this.cardNum = cardNum;
        }
    }

    public class IniTime
        : IniCostComponent
    {
        public int ownerPhase;
        public List<GameManage.GamePhase> gamePhase;
        public IniTime(int ownerPhase, List<GameManage.GamePhase> gamePhase)
        {
            this.ownerPhase = ownerPhase;
            this.gamePhase = gamePhase;
        }
    }

    public class IniLocation
        : IniCostComponent
    {
        public List<GameManage.CardLocation> cardLocation;
        public IniLocation(List<GameManage.CardLocation> cardLocation)
        {
            this.cardLocation = cardLocation;
        }
    }
    public class IniJudregTime
        : IniCostComponent
    {
        public int existTurns;                              //0=本回合进入登记模式(查墓地"本回合进入"登记表,登记随回合界限清空);>=1=停留回合门槛(regTime>=existTurns)
        public List<GameManage.CardLocation> cardLocations; //存在地点(为空=全部区域;对方区域用e_前缀)
        public List<List<string>> cardCondition;            //二维选卡条件(可为null):cardCondition[i][j]=大组i第j个候选取值,维度约定见EffLogic.Condition2D
        public IniJudregTime(int existTurns, List<GameManage.CardLocation> cardLocations, List<List<string>> cardCondition)
        {
            this.existTurns = existTurns;
            this.cardLocations = cardLocations;
            this.cardCondition = cardCondition;
        }
    }
    public class IniSelfJudregTime
        : IniCostComponent
    {
        public int needTurns;                               //自身回合门槛:0=本回合进入模式(regTime==0);>=1=停留回合模式(regTime>=needTurns;负数按0处理)
        public List<GameManage.CardLocation> cardLocations; //自身所在位置(为空=全部区域不限制;对方区域用e_前缀)
        public IniSelfJudregTime(int needTurns, List<GameManage.CardLocation> cardLocations)
        {
            this.needTurns = needTurns;
            this.cardLocations = cardLocations;
        }
    }
    public class IniJudLastEffCost
        : IniCostComponent
    {
        public List<string> effType;                        //允许的连锁中上一个效果类型(多个用|分隔,与链顶effOrderType任一一致)
        public IniJudLastEffCost(List<string> effType)
        {
            this.effType = effType;
        }
    }
    public class IniFusionSummonCost
        : IniCostComponent
    {
        public List<string> speNameKey;                                  //特殊名字,字段列表,素材满足其中任一名字/字段即可(卡牌实体名称,而非静态数据)/字段
        public List<int> Name_Key;                                       //与 speNameKey 一一对应: 0=卡名,1=字段,-1=除外卡名(不能融合召唤该怪兽);等级条目对应位填0或空格占位
        public List<GameManage.CardLocation> fusionSumLoc;
        public List<GameManage.CardLocation> materialLoc;
        public int includeSelf;                                          //是否包含自身(>=1:融合素材必须包含效果持有卡)
        public int maxMaterialCount;                                     //融合素材至多选多少个(>0:目标怪兽素材条件数(SpeMatter条目数)必须<=该值才可被本效果融合;0=不限制)
        public IniFusionSummonCost(List<string> speNameKey, List<int> Name_Key, List<GameManage.CardLocation> fusionSumLoc, List<GameManage.CardLocation> materialLoc, int includeSelf, int maxMaterialCount = 0)
        {
            this.speNameKey = speNameKey;
            this.Name_Key = Name_Key;
            this.fusionSumLoc = fusionSumLoc;
            this.materialLoc = materialLoc;
            this.includeSelf = includeSelf;
            this.maxMaterialCount = maxMaterialCount;
        }
    }
    public abstract class IniPayCostComponent
    {

    }

    public class IniGoToCemetery
        : IniPayCostComponent
    {
        public int isEntityCard;
        public IniGoToCemetery(int isEntityCard)
        {
            this.isEntityCard = isEntityCard;
        }
    }

    public class IniGoToCemeteries
        : IniPayCostComponent
    {
        public int num;                                        //需选择并送墓的张数(>=1)
        public List<GameManage.CardLocation> findComponent;    //候选来源区域(为空=全部区域含对方可被送墓)
        public List<List<string>> cardCondition;               //二维选卡条件(可为null):cardCondition[i][j]=大组i的第j个候选取值,语义见GoToCemeteries.cardCondition注释
        public int isEntityCard;                               //>=1将效果持有卡自身也送去墓地(自身不经选择直接加入送墓列表)
        public List<string> hitList;                           //命中链(可为null=默认:各条件大组须全部命中,大组内子项命中任一即可);元素间为"或"(CSV中元素间用|分隔),元素内*连接"大组/子项"引用须同时命中
        public IniGoToCemeteries(int num, List<GameManage.CardLocation> findComponent, List<List<string>> cardCondition, int isEntityCard, List<string> hitList = null)
        {
            this.num = num;
            this.findComponent = findComponent;
            this.cardCondition = cardCondition;
            this.isEntityCard = isEntityCard;
            this.hitList = hitList;
        }
    }

    public class IniGetObject
        : IniPayCostComponent
    {
        public int num;                                        //取对象数量(需在弹窗中选择的张数)
        public List<GameManage.CardLocation> findComponent;    //对象来源区域(为空=全部区域可被取为对象)
        public List<List<string>> cardCondition;               //二维选卡条件(大组下标/编码与GoToCemeteries、JudExist共用,见EffLogic.Condition2D;留空=不限制任何卡)
        public int excludeSelf;                                //>=1排除效果持有卡自身,0允许选择自身
        public List<string> hitList;                           //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public IniGetObject(int num, List<GameManage.CardLocation> findComponent, List<List<string>> cardCondition, int excludeSelf, List<string> hitList)
        {
            this.num = num;
            this.findComponent = findComponent;
            this.cardCondition = cardCondition;
            this.excludeSelf = excludeSelf;
            this.hitList = hitList;
        }
    }

    public class IniCostCard
        : IniPayCostComponent
    {
        public int num;                                        //需丢弃的手牌张数(>=1,丢弃来源固定为当前操作玩家的手牌)
        public List<List<string>> cardCondition;               //二维选卡条件(可为null):cardCondition[i][j]=大组i的第j个候选取值,语义见CostCard.cardCondition注释
        public List<string> hitList;                           //命中链(可为null=默认:各条件大组须全部命中,大组内子项命中任一即可);元素间为"或"(CSV中元素间用|分隔),元素内*连接"大组/子项"引用须同时命中
        public IniCostCard(int num, List<List<string>> cardCondition, List<string> hitList)
        {
            this.num = num;
            this.cardCondition = cardCondition;
            this.hitList = hitList;
        }
    }

    public abstract class IniCardComponent
    {


    }

    public class IniSpeSomEff
        : IniCardComponent
    {
        public int isGetObject;                                     //是否取对象(>=1=取对象,0=不取,CSV中作为第一个属性)
        public int num;                                             //选择特招数量
        public List<GameManage.CardLocation> findComponent;         //特招来源区域(为空=全部区域)

        //特招条件(二维选卡条件,大组下标/编码与GoToCemeteries、JudExist共用,见EffLogic.Condition2D):
        public List<List<string>> cardCondition;                    //cardCondition(留空=不限制任何卡)
        public string excludeName;                                  //排除卡名(该卡名不算特招候选;留空=不排除)
        public List<string> hitList;                                //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public IniSpeSomEff(int isGetObject, int num, List<GameManage.CardLocation> findComponent,
            List<List<string>> cardCondition, string excludeName, List<string> hitList)
        {
            this.isGetObject = isGetObject;
            this.num = num;
            this.findComponent = findComponent;
            this.cardCondition = cardCondition;
            this.excludeName = excludeName;
            this.hitList = hitList;
        }
    }
    public class IniSelfSpeSomEff
        : IniCardComponent
    {
        //自身特招(无参数):结算时直接特招效果持有卡自身,无需任何配置
    }
    public class IniDrawCardEff
        : IniCardComponent
    {
        public int number;                      //抽卡数量
        public IniDrawCardEff(int number) 
        {
            this.number = number;
        }
    }
    public class IniGetCard
        : IniCardComponent
    {
        public int num;                                         //检索张数(>=1;候选多于检索张数时弹窗让玩家挑选)
        public List<List<string>> cardCondition;                //二维选卡条件(大组下标/编码与SpeSomEff、JudExist共用,见EffLogic.Condition2D;留空=不限制任何卡)
        public List<GameManage.CardLocation> findComponent;     //检索来源区域(Deck=检索方自己的卡组;e_Deck=对方卡组;留空=检索方自己的卡组)
        public List<string> hitList;                            //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public IniGetCard(int num, List<List<string>> cardCondition, List<GameManage.CardLocation> findComponent = null, List<string> hitList = null)
        {
            this.num = num;
            this.cardCondition = cardCondition;
            this.findComponent = findComponent;
            this.hitList = hitList;
        }
    }

    public class IniEffGoToCemetery
        : IniCardComponent
    {
        public int isGetObject;                 //是否取对象(>=1=取对象,0=不取)
        public int isEntityCard;                //是否包含自身（>=1 表示将效果持有卡送去墓地）
        public IniEffGoToCemetery(int isGetObject, int isEntityCard)
        {
            this.isGetObject = isGetObject;
            this.isEntityCard = isEntityCard;
        }
    }
    public class IniEffGoToCemeteries
        : IniCardComponent
    {
        public int isGetObject;                                     //是否取对象(>=1=取对象,0=不取)
        public int num;                                             //不取对象模式下需选择并送墓的张数(>=1;取对象模式忽略,以注入对象数为准)
        public List<GameManage.CardLocation> findComponent;         //候选来源区域(为空=全部区域含对方)
        public List<List<string>> cardCondition;                    //二维选卡条件
        public int isEntityCard;                                    //>=1效果持有卡自身也送去墓地(不经选择直接加入送墓列表;候选收集始终排除自身)
        public List<string> hitList;                                //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public IniEffGoToCemeteries(int isGetObject, int num, List<GameManage.CardLocation> findComponent,
            List<List<string>> cardCondition, int isEntityCard, List<string> hitList)
        {
            this.isGetObject = isGetObject;
            this.num = num;
            this.findComponent = findComponent;
            this.cardCondition = cardCondition;
            this.isEntityCard = isEntityCard;
            this.hitList = hitList;
        }
    }
    public class IniEffGoToBanished
        : IniCardComponent
    {
        public int isGetObject;                 //是否取对象(>=1=取对象,0=不取)
        public int isEntityCard;                //是否包含自身（>=1 表示将效果持有卡自身除外）
        public IniEffGoToBanished(int isGetObject, int isEntityCard)
        {
            this.isGetObject = isGetObject;
            this.isEntityCard = isEntityCard;
        }
    }
    public class IniEffReturnHand
        : IniCardComponent
    {
        public int isGetObject;                 //是否取对象(>=1=取对象,0=不取)
        public int isEntityCard;                //是否包含自身（>=1 表示将效果持有卡自身返回手卡）
        public IniEffReturnHand(int isGetObject, int isEntityCard)
        {
            this.isGetObject = isGetObject;
            this.isEntityCard = isEntityCard;
        }
    }
    public class IniDestoryCard
        : IniCardComponent
    {
        public int isGetObject;                                     //是否取对象(>=1=取对象,0=不取)
        public int num;                                             //不取对象模式下选择破坏的数量(取对象模式忽略,以注入对象数为准)
        public List<GameManage.CardLocation> findComponent;         //候选来源区域(为空=全部区域)
        public List<List<string>> cardCondition;                    //二维选卡条件
        public string excludeName;                                  //排除卡名(该卡名不算破坏候选;留空=不排除)
        public List<string> hitList;                                //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public IniDestoryCard(int isGetObject, int num, List<GameManage.CardLocation> findComponent,
            List<List<string>> cardCondition, string excludeName, List<string> hitList)
        {
            this.isGetObject = isGetObject;
            this.num = num;
            this.findComponent = findComponent;
            this.cardCondition = cardCondition;
            this.excludeName = excludeName;
            this.hitList = hitList;
        }
    }
    public class IniBanishedCard
        : IniCardComponent
    {
        public int isGetObject;                                     //是否取对象(>=1=取对象,0=不取)
        public int num;                                             //不取对象模式下选择除外的数量(取对象模式忽略,以注入对象数为准)
        public List<GameManage.CardLocation> findComponent;         //候选来源区域(为空=全部区域)
        public List<List<string>> cardCondition;                    //二维选卡条件
        public string excludeName;                                  //排除卡名(该卡名不算除外候选;留空=不排除)
        public List<string> hitList;                                //命中链(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔)
        public int excludeSelf;                                     //>=1候选/对象排除效果持有卡自身(自身不被除外),0允许选择自身
        public IniBanishedCard(int isGetObject, int num, List<GameManage.CardLocation> findComponent,
            List<List<string>> cardCondition, string excludeName, List<string> hitList, int excludeSelf)
        {
            this.isGetObject = isGetObject;
            this.num = num;
            this.findComponent = findComponent;
            this.cardCondition = cardCondition;
            this.excludeName = excludeName;
            this.hitList = hitList;
            this.excludeSelf = excludeSelf;
        }
    }
    public class IniFusionSummon
        : IniCardComponent
    {
        public List<GameManage.CardLocation> fusionSumLoc;              //特招地点(融合怪兽所在区域)
        public List<GameManage.CardLocation> materialLoc;               //融合素材所在区域
        public List<int> materialHandleWay;                             //素材处理方式(索引=素材区域编号0-5,值=目标区域编号0-5,缺省送墓地3)
        public List<string> speNameKey;                                 //素材必须包含的名字/字段列表
        public List<int> Name_Key;                                      //与 speNameKey 一一对应: 0=卡名,1=字段,-1=除外卡名(不能融合召唤该怪兽);等级条目对应位填0或空格占位
        public int includeSelf;                                          //是否包含自身(>=1:融合素材必须包含效果持有卡)
        public int maxMaterialCount;                                     //融合素材至多选多少个(>0:目标怪兽素材条件数(SpeMatter条目数)必须<=该值才可被本效果融合;0=不限制)
        public IniFusionSummon(List<GameManage.CardLocation> fusionSumLoc, List<GameManage.CardLocation> materialLoc,
            List<int> materialHandleWay, List<string> speNameKey, List<int> Name_Key, int includeSelf, int maxMaterialCount = 0)
        {
            this.fusionSumLoc = fusionSumLoc;
            this.materialLoc = materialLoc;
            this.materialHandleWay = materialHandleWay;
            this.speNameKey = speNameKey;
            this.Name_Key = Name_Key;
            this.includeSelf = includeSelf;
            this.maxMaterialCount = maxMaterialCount;
        }
    }
    #endregion
}

