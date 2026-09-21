using System;
using System.Collections.Generic;
using static EffLogic;

public class EventMintor 
{


    public class MonsterSpecialSummon                    //怪兽特招事件
        : EventArgs
    {
        public List<EntityCard> entityCards { get; private set; }
        public MonsterSpecialSummon(EntityCard entityCard)
        {
            this.entityCards = new List<EntityCard>() { entityCard };
        }
        public MonsterSpecialSummon(List<EntityCard> entityCards)
        {
            this.entityCards = entityCards;
        }
    }

    public class FusionSummon                              //怪兽融合召唤成功事件
        : EventArgs
    {
        public List<EntityCard> entityCards { get; private set; }
        public FusionSummon(EntityCard entityCard)
        {
            this.entityCards = new List<EntityCard>() { entityCard };
        }
        public FusionSummon(List<EntityCard> entityCards)
        {
            this.entityCards = entityCards;
        }
    }

    public class MonsterSummon                           //怪兽通招事件
        : EventArgs
    {
        public List<EntityCard> entityCards { get; private set; }
        public List<int> playerID { get; private set; }
        public MonsterSummon(List<EntityCard> entityCards, List<int> playerID)
        {
            this.entityCards = entityCards;
            this.playerID = playerID;
        }
        public MonsterSummon(EntityCard entityCard)              //单实体:通召成功时点广播用
        {
            this.entityCards = new List<EntityCard>() { entityCard };
            this.playerID = new List<int>() { entityCard?.GetOwner() != null ? entityCard.GetOwner().GetPlayerIndex() : 0 };
        }
    }

    public class MonsterAtt                              //怪兽攻击事件
        : EventArgs
    {
        public EntityMonsterCard attack { get; private set; }
        public EntityMonsterCard target { get; private set; }
        public MonsterAtt(EntityMonsterCard attack, EntityMonsterCard target)
        {
            this.attack = attack;
            this.target = target;
        }
    }

    public class HurtEffHp                              //受效果伤害导致hp下降事件
        : EventArgs
    {
        public int hp { get; private set; }
        public int playerID { get; private set; }
        public EntityCard entityCard { get; private set; }     //由那张卡引起
        public HurtEffHp(int hp, int playerID, EntityCard entityCard)
        {
            this.hp = hp;
            this.playerID = playerID;
            this.entityCard = entityCard;
        }
    }

    public class HurtAttHp                              //受攻击伤害导致hp下降事件
        : EventArgs
    {
        public int hp { get; private set; }
        public int playerID { get; private set; }
        public EntityCard entityCard { get; private set; }
        public HurtAttHp(int hp, int playerID, EntityCard entityCard)
        {
            this.hp = hp;
            this.playerID = playerID;
            this.entityCard = entityCard;
        }
    }

    public class NoHurtHpChange                         //非伤害hp变化事件
        : EventArgs
    {
        public int changeHp { get; private set; }
        public int playerID { get; private set; }
        public EntityCard entityCard { get; private set; }
        public NoHurtHpChange(int changeHp, int playerID, EntityCard entityCard)
        {
            this.changeHp = changeHp;
            this.playerID = playerID;
            this.entityCard = entityCard;
        }
    }

    public class HpChange                               //血量改变总事件:任一玩家血量被修改(战斗伤害/效果伤害/直接设定等)后触发
        : EventArgs
    {
        public int changeHp { get; private set; }        //本次血量变化量(负=扣血,正=回血;直接设定血量时=old→new差值)
        public int hp { get; private set; }              //变化后的当前血量
        public int playerID { get; private set; }        //血量被改变的玩家(0=我方,1=对方)
        public HpChange(int changeHp, int hp, int playerID)
        {
            this.changeHp = changeHp;
            this.hp = hp;
            this.playerID = playerID;
        }
    }

    public class Entercemtery                            //有卡进入墓地事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }              //进入者
        public GameManage.CardLocation cardLocation { get; private set; } //从哪里进入
        public Entercemtery(EntityCard entityCard, GameManage.CardLocation cardLocation)
        {
            this.entityCard = entityCard;
            this.cardLocation = cardLocation;
        }
    }

    public class EnterBanished                           //有卡被除外(进入除外区)事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }              //被除外者
        public GameManage.CardLocation cardLocation { get; private set; } //从哪里被除外
        public EnterBanished(EntityCard entityCard, GameManage.CardLocation cardLocation)
        {
            this.entityCard = entityCard;
            this.cardLocation = cardLocation;
        }
    }

    public class CardDestroy                             //有卡被破坏事件
        : EventArgs
    {
        public EntityCard beDes { get; private set; }              //被破坏的卡
        public EntityCard des { get; private set; }                //破坏它的卡
        public CardDestroy(EntityCard beDes, EntityCard des)
        {
            this.beDes = beDes;
            this.des = des;
        }
    }

    public class LeaveExtra                              //有卡离开额外区事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }              //离开者
        public GameManage.CardLocation cardLocation { get; private set; } //离开到哪里
        public LeaveExtra(EntityCard entityCard, GameManage.CardLocation cardLocation)
        {
            this.entityCard = entityCard;
            this.cardLocation = cardLocation;
        }
    }

    public class DrawCard                                //抽卡事件
        : EventArgs
    {
        public int drawNum { get; private set; }                 //抽卡数量
        public EntityCard[] entityCards { get; private set; }    //抽到的卡
        public DrawCard(int drawNum, EntityCard[] entityCards)
        {
            this.drawNum = drawNum;
            this.entityCards = entityCards;
        }
    }

    public class MagicEff                               //魔法卡发动效果事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }         //发动的魔法卡
        public MagicEff(EntityCard entityCard)
        {
            this.entityCard = entityCard;
        }
    }

    public class TrapEff                                //陷阱卡发动效果事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }         //发动的陷阱卡
        public TrapEff(EntityCard entityCard)
        {
            this.entityCard = entityCard;
        }
    }

    public class MonsterEff                             //怪兽发动效果事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }         //发动效果的怪兽
        public MonsterEff(EntityCard entityCard)
        {
            this.entityCard = entityCard;
        }
    }

    public class GetCard                                //有卡从卡组加入手卡(被检索)事件
        : EventArgs
    {
        public EntityCard entityCard { get; private set; }              //加入手卡的卡
        public GameManage.CardLocation cardLocation { get; private set; } //从哪里加入(检索来源=卡组)
        public GetCard(EntityCard entityCard, GameManage.CardLocation cardLocation)
        {
            this.entityCard = entityCard;
            this.cardLocation = cardLocation;
        }
    }

    public Action<MonsterSpecialSummon> OnMonsterSpecialSummon;
    public void AddOnMonsterSpecialSummon(Action<MonsterSpecialSummon> listener)
    {
        OnMonsterSpecialSummon += listener;
    }
    public void DesOnMonsterSpecialSummon(Action<MonsterSpecialSummon> listener)
    {
        OnMonsterSpecialSummon -= listener;
    }
    public void AwakeOnMonsterSpecialSummon(MonsterSpecialSummon monsterSpecialSummon)
    {
        OnMonsterSpecialSummon?.Invoke(monsterSpecialSummon);
    }

    private Action<FusionSummon> OnFusionSummon;
    public void AddOnFusionSummon(Action<FusionSummon> listener)
    {
        OnFusionSummon += listener;
    }
    public void DesOnFusionSummon(Action<FusionSummon> listener)
    {
        OnFusionSummon -= listener;
    }
    public void AwakeOnFusionSummon(FusionSummon fusionSummon)
    {
        OnFusionSummon?.Invoke(fusionSummon);
    }

    private Action<MonsterSummon> OnMonsterSummon;
    public void AddOnMonsterSummon(Action<MonsterSummon> listener)
    {
        OnMonsterSummon += listener;
    }
    public void DesOnMonsterSummon(Action<MonsterSummon> listener)
    {
        OnMonsterSummon -= listener;
    }
    public void AwakeOnMonsterSummon(MonsterSummon monsterSummon)
    {
        OnMonsterSummon?.Invoke(monsterSummon);
    }

    private Action<MonsterAtt> OnMonsterAtt;
    public void AddOnMonsterAtt(Action<MonsterAtt> listener)
    {
        OnMonsterAtt += listener;
    }
    public void DesOnMonsterAtt(Action<MonsterAtt> listener)
    {
        OnMonsterAtt -= listener;
    }
    public void AwakeOnMonsterAtt(MonsterAtt monsterAtt)
    {
        OnMonsterAtt?.Invoke(monsterAtt);
    }

    private Action<HurtEffHp> OnHurtEffHp;
    public void AddOnHurtEffHp(Action<HurtEffHp> listener)
    {
        OnHurtEffHp += listener;
    }
    public void DesOnHurtEffHp(Action<HurtEffHp> listener)
    {
        OnHurtEffHp -= listener;
    }
    public void AwakeOnHurtEffHp(HurtEffHp hurtEffHp)
    {
        OnHurtEffHp?.Invoke(hurtEffHp);
    }

    private Action<HurtAttHp> OnHurtAttHp;
    public void AddOnHurtAttHp(Action<HurtAttHp> listener)
    {
        OnHurtAttHp += listener;
    }
    public void DesOnHurtAttHp(Action<HurtAttHp> listener)
    {
        OnHurtAttHp -= listener;
    }
    public void AwakeOnHurtAttHp(HurtAttHp hurtAttHp)
    {
        OnHurtAttHp?.Invoke(hurtAttHp);
    }

    private Action<NoHurtHpChange> OnNoHurtHpChange;
    public void AddOnNoHurtHpChange(Action<NoHurtHpChange> listener)
    {
        OnNoHurtHpChange += listener;
    }
    public void DesOnNoHurtHpChange(Action<NoHurtHpChange> listener)
    {
        OnNoHurtHpChange -= listener;
    }
    public void AwakeOnNoHurtHpChange(NoHurtHpChange noHurtHpChange)
    {
        OnNoHurtHpChange?.Invoke(noHurtHpChange);
    }

    private Action<HpChange> OnHpChange;
    public void AddOnHpChange(Action<HpChange> listener)
    {
        OnHpChange += listener;
    }
    public void DesOnHpChange(Action<HpChange> listener)
    {
        OnHpChange -= listener;
    }
    public void AwakeOnHpChange(HpChange hpChange)
    {
        OnHpChange?.Invoke(hpChange);
    }

    private Action<Entercemtery> OnEntercemtery;
    public void AddOnEntercemtery(Action<Entercemtery> listener)
    {
        OnEntercemtery += listener;
    }
    public void DesOnEntercemtery(Action<Entercemtery> listener)
    {
        OnEntercemtery -= listener;
    }
    public void AwakeOnEntercemtery(Entercemtery entercemtery)
    {
        OnEntercemtery?.Invoke(entercemtery);
    }

    private Action<EnterBanished> OnEnterBanished;
    public void AddOnEnterBanished(Action<EnterBanished> listener)
    {
        OnEnterBanished += listener;
    }
    public void DesOnEnterBanished(Action<EnterBanished> listener)
    {
        OnEnterBanished -= listener;
    }
    public void AwakeOnEnterBanished(EnterBanished enterBanished)
    {
        OnEnterBanished?.Invoke(enterBanished);
    }

    private Action<CardDestroy> OnCardDestroy;
    public void AddOnCardDestroy(Action<CardDestroy> listener)
    {
        OnCardDestroy += listener;
    }
    public void DesOnCardDestroy(Action<CardDestroy> listener)
    {
        OnCardDestroy -= listener;
    }
    public void AwakeOnCardDestroy(CardDestroy cardDestroy)
    {
        OnCardDestroy?.Invoke(cardDestroy);
    }

    private Action<LeaveExtra> OnLeaveExtra;
    public void AddOnLeaveExtra(Action<LeaveExtra> listener)
    {
        OnLeaveExtra += listener;
    }
    public void DesOnLeaveExtra(Action<LeaveExtra> listener)
    {
        OnLeaveExtra -= listener;
    }
    public void AwakeOnLeaveExtra(LeaveExtra leaveExtra)
    {
        OnLeaveExtra?.Invoke(leaveExtra);
    }

    private Action<DrawCard> OnDrawCard;
    public void AddOnDrawCard(Action<DrawCard> listener)
    {
        OnDrawCard += listener;
    }
    public void DesOnDrawCard(Action<DrawCard> listener)
    {
        OnDrawCard -= listener;
    }
    public void AwakeOnDrawCard(DrawCard drawCard)
    {
        OnDrawCard?.Invoke(drawCard);
    }

    private Action<MagicEff> OnMagicEff;
    public void AddOnMagicEff(Action<MagicEff> listener)
    {
        OnMagicEff += listener;
    }
    public void DesOnMagicEff(Action<MagicEff> listener)
    {
        OnMagicEff -= listener;
    }
    public void AwakeOnMagicEff(MagicEff magicEff)
    {
        OnMagicEff?.Invoke(magicEff);
    }

    private Action<TrapEff> OnTrapEff;
    public void AddOnTrapEff(Action<TrapEff> listener)
    {
        OnTrapEff += listener;
    }
    public void DesOnTrapEff(Action<TrapEff> listener)
    {
        OnTrapEff -= listener;
    }
    public void AwakeOnTrapEff(TrapEff trapEff)
    {
        OnTrapEff?.Invoke(trapEff);
    }

    private Action<MonsterEff> OnMonsterEff;
    public void AddOnMonsterEff(Action<MonsterEff> listener)
    {
        OnMonsterEff += listener;
    }
    public void DesOnMonsterEff(Action<MonsterEff> listener)
    {
        OnMonsterEff -= listener;
    }
    public void AwakeOnMonsterEff(MonsterEff monsterEff)
    {
        OnMonsterEff?.Invoke(monsterEff);
    }

    private Action<GetCard> OnGetCard;
    public void AddOnGetCard(Action<GetCard> listener)
    {
        OnGetCard += listener;
    }
    public void DesOnGetCard(Action<GetCard> listener)
    {
        OnGetCard -= listener;
    }
    public void AwakeOnGetCard(GetCard getCard)
    {
        OnGetCard?.Invoke(getCard);
    }

}
