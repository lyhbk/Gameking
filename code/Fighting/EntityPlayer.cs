using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EffLogic;

[System.Serializable]
public class EntityPlayer                               //实体玩家
{
    public const int PlayerHp = 8000;
    public const int PlayerIniHandNum = 5;

    public Player.StaticPlayer StaticPlayer { get; private set; }          //指向静态数据
    public EntityPlayer(string playerId, string playerName)
    {
        StaticPlayer = new Player.StaticPlayer(playerId, playerName);
        Ini();
        IniEffNum();
    }
    public EntityPlayer(string playerId, string playerName, 
        Deck deck, ExtraDeck extraDeck, Hand hand, Cemetery cemetery, Banished banished, Field field)
    {
        StaticPlayer = new Player.StaticPlayer(playerId, playerName);
        playerHP = PlayerHp;
        playerHandNum = PlayerIniHandNum;
        this.deck = deck;
        this.extraDeck = extraDeck;
        this.hand = hand;
        this.cemetery = cemetery;
        this.banished = banished;
        this.field = field;
        deck?.BindOwner(this);            //区域绑定归属玩家(原本所有者)
        extraDeck?.BindOwner(this);
        hand?.BindOwner(this);
        cemetery?.BindOwner(this);
        banished?.BindOwner(this);
        field?.BindOwner(this);
        Ini();
        IniEffNum();
    }
    //玩家基础实体属性
    private int playerHP;                                                //玩家生命值
    private int playerHandNum;                                          //玩家手牌数
    public int GetPlayerHP()
    {
        return playerHP;
    }
    public void ChangeHP(int changeHp)
    {
        playerHP += changeHp;
    }
    public void ChangeHPTo(int targetHp)
    {
        playerHP = targetHp;
    }

    public int GetPlayerHandNum()
    {
        return playerHandNum;
    }
    public void ChangeHandNum(int changeHandNum)
    {
        playerHandNum += changeHandNum;
    }

    public int playerIndex { get; private set; } = 0;                    //对局玩家索引:0=entityPlayer1(我方),1=enemy(对方)
    public void SetPlayerIndex(int playerIndex)                          //对局初始化时指定玩家索引
    {
        this.playerIndex = playerIndex;
    }
    public int GetPlayerIndex()                                          //获取玩家索引
    {
        return playerIndex;
    }

    [field:SerializeField]public int canSomNum { get; private set; }                              //通常召唤次数
    [field: SerializeField]public int curSomNum { get; private set; }                              //已当前召唤次数
    public void Ini()
    {
        canSomNum = 1;
        curSomNum = 0;
    }
    public void AddCanSomNum()
    {
        canSomNum++;
    }
    public void AddCurSomNum()
    {
        curSomNum++;
    }
    public bool CanSummon()
    {
        return curSomNum < canSomNum;
    }
    public void ResetSomNum()
    {
        curSomNum = 0;
        canSomNum = 1;
    }

    public bool IsNeedDiscard()
    {
        return hand.cards.Count > 6;
    }

    [System.Serializable]
    public class EntityFindComponent                       //卡牌实体检查
    {
        [field:SerializeField]public List<EntityCard> cards { get; private set; }          //牌组链表
        public EntityPlayer owner { get; private set; }                                    //区域归属玩家(原本所有者):控制权转移后用于归属纠偏


        public EntityFindComponent()
        {
            cards = new List<EntityCard>();
        }
        //本回合进入本区域的登记表(上提至基类,所有区域通用):卡真正收进本区域时登记其快照副本,即使随后离开区域,记录保留到回合界限清空。
        //支撑JudregTime"0回合模式"判定"本回合有[满足条件]的卡进入过该区域
        private readonly List<EntityCard> boutSentSnapshots = new List<EntityCard>();

        public List<EntityCard> GetBoutSentSnapshots()          //本回合进入过本区域的卡牌快照(供JudregTime本回合模式扫描)
        {
            return boutSentSnapshots;
        }
        public void ClearBoutSent()                             //回合界限清空:由回合结束流程在对方回合开始前调用,上一回合的记录全部作废
        {
            boutSentSnapshots.Clear();
        }
        public void RegisterBoutSent(EntityCard card)           //"本回合进入"登记:真正收下卡后登记其快照(AddCard/场上入场等收卡路径调用)
        {
            if (card != null)
                boutSentSnapshots.Add(card.CreateSnapshot());
        }
        public void BindOwner(EntityPlayer owner)                   //绑定区域归属玩家
        {
            this.owner = owner;
        }
        public virtual void AddCard(EntityCard card)                //添加
        {
            if (card == null) return;
            card.ResetRegTime();                                    //进入该区域:重置停留回合计数(regTime=0,随回合结束递增)
            EntityPlayer cardOwner = card.GetOwner();
            // 防御:控制权转移后的卡进入墓地/除外/卡组/额外卡组/手卡等非场上区域时,必须改道进入原本所有者的同类型区域
            if (owner != null && cardOwner != null && cardOwner != owner && !(this is Field))
            {
                GameManage.CardLocation targetKind;
                if (this is Deck) targetKind = GameManage.CardLocation.Deck;
                else if (this is ExtraDeck) targetKind = GameManage.CardLocation.ExtraDeck;
                else if (this is Hand) targetKind = GameManage.CardLocation.Hand;
                else if (this is Cemetery) targetKind = GameManage.CardLocation.Cemetery;
                else if (this is Banished) targetKind = GameManage.CardLocation.Banished;
                else
                {
                    cards.Add(card);          //未知区域类型:不防御
                    RegisterBoutSent(card);
                    return;
                }
                EntityFindComponent realRegion = cardOwner.GetRegion(targetKind);   //原本所有者的同类型区域
                if (realRegion != null && realRegion != this)
                {
                    realRegion.AddCard(card);
                    return;
                }
            }
            cards.Add(card);
            RegisterBoutSent(card);                             //真正收进本区域:登记"本回合进入"快照(纠偏转投别处的卡由真正收卡的区域登记,此处不会执行)
        }
        public virtual void RemoveCard(EntityCard card)             //移除
        {
            cards.Remove(card);
        }
        public int GetCardNum()
        {
            return cards.Count;
        }

        public bool IsContainCard(EntityCard entityCard)
        {
            return cards.Contains(entityCard);
        }
    }

    // 区域组件索引: Deck, ExtralDeck, Hand, Cemetery, Banished, Field
    //               0       1         2       3         4        5
    // bool编码12位:前6位(0-5)为自己区域,后6位(6-11)为对方区域,顺序一致
    #region 场地数据
    // 局内数据
    public Deck deck { get; private set; }
    public ExtraDeck extraDeck { get; private set; }
    public Hand hand { get; private set; }
    public Cemetery cemetery { get; private set; }
    public Banished banished { get; private set; }
    public Field field { get; private set; }

    public EntityFindComponent GetRegion(GameManage.CardLocation location)      //按区域种类取本玩家对应区域(控制权转移归属纠偏用)
    {
        switch (location)
        {
            case GameManage.CardLocation.Deck: return deck;
            case GameManage.CardLocation.ExtraDeck: return extraDeck;
            case GameManage.CardLocation.Hand: return hand;
            case GameManage.CardLocation.Cemetery: return cemetery;
            case GameManage.CardLocation.Banished: return banished;
            case GameManage.CardLocation.Field: return field;
            default: return null;
        }
    }
    public void BindDeck(Deck deck)                          //卡组晚于玩家构造创建
    {
        this.deck = deck;
    }
    public void BindExtraDeck(ExtraDeck extraDeck)           //额外卡组晚于玩家构造创建
    {
        this.extraDeck = extraDeck;
    }
    public void ClearAllBoutSent()                           //回合界限清空
    {
        deck?.ClearBoutSent();
        extraDeck?.ClearBoutSent();
        hand?.ClearBoutSent();
        cemetery?.ClearBoutSent();
        banished?.ClearBoutSent();
        field?.ClearBoutSent();
    }

    [System.Serializable]
    public class Hand : EntityFindComponent
    {
        public void GiveUpHand(EntityCard entityCard)
        {
            cards.Remove(entityCard);
        }

    }
    [System.Serializable]
    public class Deck : EntityFindComponent
    {
        public Deck(List<CardBase.Card> cards, EntityPlayer owner)
        {
            BindOwner(owner);            //卡组区域归属玩家
            foreach (var card in cards)
            {
                EntityCard entityCard = Factory.EntityCard(card, owner);
                entityCard.ChangeLocation(GameManage.CardLocation.Deck);
                AddCard(entityCard);
            }
        }


        public void ShuffCard()                             //洗牌算法
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                EntityCard temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }
        }

        public void DrawCard(Hand hand, int num)                     //抽卡
        {

            for (int i = 0; i < num; i++)
            {
                if (cards.Count == 0)
                    break;
                EntityCard topCard = cards[0];
                topCard.ChangeLocation(GameManage.CardLocation.Hand);
                cards.RemoveAt(0);
                hand.AddCard(topCard);
     
            }
        }
    }
    [System.Serializable]
    public class ExtraDeck : EntityFindComponent                //额外牌组
    {
        public ExtraDeck(List<CardBase.Card> cards, EntityPlayer owner)
        {
            BindOwner(owner);            //额外卡组区域归属玩家
            foreach (var card in cards)
            {
                EntityCard entityCard = Factory.EntityCard(card, owner);
                entityCard.ChangeLocation(GameManage.CardLocation.ExtraDeck);   //修正:标记真实位置,否则额外卡组所有卡location=Deck失真→按location的区域移除(融合召唤出额外/素材出额外/送除外等)会漏删→额外卡组数据残留
                AddCard(entityCard);
            }
        }

    }
    [System.Serializable]
    public class Cemetery : EntityFindComponent                               //墓地
    {
        //"本回合进入"登记表(字段/注册/查询/清空)已上提至基类EntityFindComponent,各区域通用
    }
    [System.Serializable]
    public class Field : EntityFindComponent                                  //场上
    {
        private List<EntityCard> monsterZones;                      //怪兽区
        private List<EntityCard> magicTrapZones;                    //魔陷区
        private EntityCard venue;                                   //场地
        public Field()
        {
            monsterZones = new List<EntityCard>();
            magicTrapZones = new List<EntityCard>();
        }

        public int GetMonsterZoneCount()
        {
            return monsterZones.Count;
        }
        public int GetMagicTrapZoneCount()
        {
            return magicTrapZones.Count;
        }

        public void AddMonster(EntityCard entityCard)
        {
            entityCard.ResetRegTime();                              //上场进入场上区域:重置停留回合计数
            monsterZones.Add(entityCard);
            cards.Add(entityCard);
            RegisterBoutSent(entityCard);
        }
        public void RemoveMonster(EntityCard entityCard)
        {
            monsterZones.Remove(entityCard);
            cards.Remove(entityCard);
        }
        public void AddMagicTrap(EntityCard entityCard)
        {
            entityCard.ResetRegTime();                              //入场进入场上区域:重置停留回合计数
            magicTrapZones.Add(entityCard);
            cards.Add(entityCard);
            RegisterBoutSent(entityCard);
        }
        public void RemoveMagicTrap(EntityCard entityCard)
        {
            magicTrapZones.Remove(entityCard);
            cards.Remove(entityCard);
        }
        public void AddVenue(EntityCard entityCard)
        {
            entityCard.ResetRegTime();                              //场地魔法入场:重置停留回合计数
            venue = entityCard;
            cards.Add(entityCard);
            RegisterBoutSent(entityCard);
        }
        public void RemoveVenue()
        {
            cards.Remove(venue);
            venue = null;
        }

        public List<EntityCard> GetMonZon()
        {
            return monsterZones;
        }
        public List<EntityCard> GetMTZon()
        {
            return magicTrapZones;
        }
        public override void RemoveCard(EntityCard card)            //从场上移除:须同步清理怪兽区/魔陷区/场地专用列表(仅cards.Remove会残留占位→同实例双持)
        {
            if (card == null) return;
            if (monsterZones.Contains(card)) { RemoveMonster(card); return; }
            if (magicTrapZones.Contains(card)) { RemoveMagicTrap(card); return; }
            if (venue == card) { RemoveVenue(); return; }
            cards.Remove(card);
        }
    }
    [System.Serializable]
    public class Banished : EntityFindComponent                               //除外
    {

    }
    #endregion

    #region 玩家卡牌效果发动次数数据
    [System.Serializable]
    public class EffNumKey                                       //发动次数键:
    {
        public string cardId { get; private set; }                            //原始卡名id(entityCard.card.id):卡名限制维度
        public string currentId { get; private set; }                         //当前卡id(entityCard.currentid):实例限制维度
        public int effId { get; private set; }                                //效果序号(Effection.id)
        public EffNumKey(string cardId, string currentId, int effId)
        {
            this.cardId = cardId ?? "";
            this.currentId = currentId ?? "";
            this.effId = effId;
        }
        public override bool Equals(object obj)
        {
            return obj is EffNumKey k && k.cardId == cardId && k.currentId == currentId && k.effId == effId;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return ((cardId.GetHashCode() * 397) ^ currentId.GetHashCode()) * 397 ^ effId;
            }
        }
    }

    private Dictionary<EffNumKey, int> EffNum;            //卡牌效果发动次数
    public void IniEffNum()
    {
        EffNum = new Dictionary<EffNumKey, int>();
        LinkEffNUm = new Dictionary<OneCardManyEff, int>();               //粘连效果计数与普通计数一同初始化
    }
    public int FindEffNum(EntityCard entityCard, bool isCardNameLim, int effId)     //查找发动次数
    {
        if (entityCard == null || entityCard.card == null) return 0;
        List<int> linkGroup = FindLinkGroup(entityCard, effId);      //粘连效果:整组共享次数,查组计数
        if (linkGroup != null)
            return FindLinkEffNum(entityCard, isCardNameLim, linkGroup);
        if (EffNum == null || EffNum.Count == 0) return 0;
        int num = 0;
        foreach (var kv in EffNum)
        {
            EffNumKey k = kv.Key;
            if (k.effId != effId) continue;
            if (isCardNameLim ? k.cardId == entityCard.card.id : k.currentId == entityCard.currentid)
                num += kv.Value;
        }
        return num;
    }
    public void AddNum(EntityCard entityCard, bool isCardNameLim, int effId)   //连锁实现时结算
    {
        if (entityCard == null || entityCard.card == null) return;
        List<int> linkGroup = FindLinkGroup(entityCard, effId);      //粘连效果:整组共享同一条计数
        if (linkGroup != null)
        {
            if (LinkEffNUm == null) LinkEffNUm = new Dictionary<OneCardManyEff, int>();
            OneCardManyEff key = new OneCardManyEff(entityCard.card.id, entityCard.currentid, linkGroup);
            if (LinkEffNUm.TryGetValue(key, out int num))
                LinkEffNUm[key] = num + 1;
            else
                LinkEffNUm[key] = 1;
            return;
        }
        EffNumKey key2 = new EffNumKey(entityCard.card.id, entityCard.currentid, effId);
        if (EffNum.TryGetValue(key2, out int num2))
            EffNum[key2] = num2 + 1;
        else
            EffNum[key2] = 1;
    }
    public void UpdateAllData()               //回合结束自动跟新
    {
        foreach (var key in EffNum.Keys.ToList())
        {
            EffNum[key] = 0;
        }
        if (LinkEffNUm != null)
        {
            foreach (var key in LinkEffNUm.Keys.ToList())
            {
                LinkEffNUm[key] = 0;
            }
        }
    }

    //粘连组识别
    private List<int> FindLinkGroup(EntityCard entityCard, int effId)
    {
        if (entityCard?.effections == null || entityCard.effections.Count == 0) return null;
        for (int i = 0; i < entityCard.effections.Count; i++)
        {
            EffLogic.Effection eff = entityCard.effections[i];
            if (eff == null || eff.isLinkEff != 1) continue;
            List<int> group = eff.isLinkEffID == null ? new List<int>() : new List<int>(eff.isLinkEffID);
            if (!group.Contains(eff.id))
                group.Add(eff.id);
            if (group.Contains(effId))
            {
                group.Sort();
                return group;
            }
        }
        return null;
    }
    private int FindLinkEffNum(EntityCard entityCard, bool isCardNameLim, List<int> group)   //粘连组共享计数
    {
        if (LinkEffNUm == null || LinkEffNUm.Count == 0) return 0;
        int num = 0;
        foreach (var kv in LinkEffNUm)
        {
            OneCardManyEff k = kv.Key;
            if (!k.effId.SequenceEqual(group)) continue;
            if (isCardNameLim ? k.cardId == entityCard.card.id : k.currentId == entityCard.currentid)
                num += kv.Value;
        }
        return num;
    }

    public class OneCardManyEff                                 //粘连效果发动次数键
    {
        public string cardId { get; }                            //原始卡名id(entityCard.card.id):卡名限制维度
        public string currentId { get; }                         //当前卡id(entityCard.currentid):实例限制维度
        public List<int> effId { get; }                          //粘连组效果编号(含自身,升序规范化)
        public OneCardManyEff(string cardId, string currentId, List<int> effId)
        {
            this.cardId = cardId ?? "";
            this.currentId = currentId ?? "";
            this.effId = effId == null ? new List<int>() : new List<int>(effId);
            this.effId.Sort();                                   //组编号排序:保证{1,2}与{2,1}视为同一组
        }
        public override bool Equals(object obj)
        {
            return obj is OneCardManyEff k && k.cardId == cardId && k.currentId == currentId && k.effId.SequenceEqual(effId);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (cardId.GetHashCode() * 397) ^ currentId.GetHashCode();
                for (int i = 0; i < effId.Count; i++)
                    hash = hash * 397 ^ effId[i];
                return hash;
            }
        }
    }

    private Dictionary<OneCardManyEff, int> LinkEffNUm;                     

    #endregion

    #region 自肃限制
    public enum Limition                       //自肃种类(CSV自肃段填此类名称)
    {
        None = 0,
        Ex_No_Summon = 1021,                   //禁止额外特招
        Ex_OnlySom_FusionSummon = 3626,        //额外仅能召唤融合
    }
    public class LimitionBase           //自肃基本类
    {
        public int id {  get; private set; }
        public int timeLength {  get;private set; }  //自肃时长
        public LimitionBase(int id, int timeLength)
        {
            this.id = id;
            this.timeLength = timeLength;
        }
        public int UpdateTimeLength()
        {
            return timeLength--;
        }
    }
    public List<LimitionBase> Limitions {  get;private set; } = new List<LimitionBase>(); //自肃列表

    public void AddLimition(int limitionId, int timeLength)              //添加自肃(自肃编码见Limition注释),timeLength=持续回合数
    {
        if (timeLength <= 0 || Limitions == null) return;
        Limitions.Add(new LimitionBase(limitionId, timeLength));
    }
    public bool HasLimition(int limitionId)                               //是否处于指定自肃中
    {
        if (Limitions == null) return false;
        for (int i = 0; i < Limitions.Count; i++)
        {
            if (Limitions[i].id == limitionId)
                return true;
        }
        return false;
    }

    public void UpdateLimitions()                           //回合结束更新自肃,自动调用
    {
        if (Limitions == null) return;
        for(int i = 0; i < Limitions.Count; i++)
        {
            int num = Limitions[i].UpdateTimeLength();
            if (num == 0)
            {
                Limitions.RemoveAt(i);
            }
        }
    }

    #endregion


    public bool BoutEndFunion()              //将此函数调入回合结束委托(参数=刚结束回合的玩家)
    {
        ResetBoutTurnData();
        UpdateLimitions();
        UpdateCoverTime();                   //回合结束：场上已盖放陷阱卡盖放时间+1（满一回合后才能发动）
        UpdateRegTime();                     //回合结束：该玩家所有区域中停留的卡牌进入区域回合数+1
        return IsNeedDiscard();
    }
    public void ResetBoutTurnData()          //回合界限清零：通召次数/效果发动次数按"每个回合"累计,与回合主无关,
    {                                        //双方每次回合切换都要归零(否则在对方回合发动的"1回合1次"会被带入自己回合造成误禁)
        ResetSomNum();
        UpdateAllData();
    }
    private void UpdateRegTime()              //回合结束：卡牌进入当前区域后每过一个(该玩家)回合结束regTime+1
    {
        AddRegTimeCards(deck?.cards);
        AddRegTimeCards(extraDeck?.cards);
        AddRegTimeCards(hand?.cards);
        AddRegTimeCards(cemetery?.cards);
        AddRegTimeCards(banished?.cards);
        AddRegTimeCards(field?.cards);
    }
    private void AddRegTimeCards(List<EntityCard> cards)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
            cards[i]?.AddRegTime(1);
    }
    private void UpdateCoverTime()            //场上已盖放的魔法陷阱卡盖放时间+1
    {
        if (field == null) return;
        foreach (var card in field.GetMTZon())
        {
            if (card.isCover != 1) continue;                    //仅里侧盖放在场上的卡递增
            if (card is EntityMagicCard magicCard)
                magicCard.coverTime++;                          //盖放的速攻等魔法:满一回合后才可发动
            else if (card is EntityTrapCard trapCard)
                trapCard.coverTime++;
        }
    }

}
