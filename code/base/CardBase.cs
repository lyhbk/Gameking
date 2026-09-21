
using System;
using System.Collections.Generic;



public static class CardBase
{
    public class StaticEff
    {
        public List<string> specificCostClass { get; private set; }
        public List<string> costParameter { get; private set; }                  
         
        public List<string> specificEffectClass { get; private set; }         
        public List<string> effectParameter { get; private set; }


        public List<string> specificCostPay {  get; private set; }
        public List<string> costPayParameter { get; private set; }

        public List<string> baseEffParameter {  get; private set; }
        public StaticEff(List<string> specificCostClass, List<string> costParameter, List<string> specificEffectClass, 
                            List<string> effectParameter, List<string> specificCostPay, List<string> costPayParameter, List<string> baseEffParameter)
        {
            this.costParameter = costParameter;
            this.effectParameter = effectParameter;
            this.specificCostClass = specificCostClass;
            this.specificEffectClass = specificEffectClass;
            this.specificCostPay = specificCostPay;
            this.costPayParameter = costPayParameter;
            this.baseEffParameter = baseEffParameter;
        }

        public StaticEff Clone()                        //效果定义深拷贝
        {
            return new StaticEff(
                specificCostClass == null ? null : new List<string>(specificCostClass),
                costParameter == null ? null : new List<string>(costParameter),
                specificEffectClass == null ? null : new List<string>(specificEffectClass),
                effectParameter == null ? null : new List<string>(effectParameter),
                specificCostPay == null ? null : new List<string>(specificCostPay),
                costPayParameter == null ? null : new List<string>(costPayParameter),
                baseEffParameter == null ? null : new List<string>(baseEffParameter));
        }
    }

    public class Card
    {
        public string id { get; protected set; }               //卡片id
        public string name { get; set; }                        //卡片名字
        public int limition { get; protected set; }           //卡片限制，0为禁止，1为限制，2为准制，3为无限制
        public int card_type { get; protected set; }           //卡片类型，0为怪兽卡，1为魔法卡，2为陷阱卡
        public string effection { get; protected set; }        //效果描述

        public string[] key { get; protected set; }           //字段
        public string[] havingKey {  get; protected set; }    //卡名记述
        public int EffNum { get; set; }                       //效果数量
        public List<StaticEff> staticEffs { get; set; }       //静态效果列表
        public Card(string id, string name, int limition, int card_type, string effection, string[] key, string[] havingKey)
        {
            this.id = id;
            this.name = name;
            this.limition = limition;
            this.card_type = card_type;
            this.effection = effection;
            this.key = key;
            this.havingKey = havingKey;
            staticEffs = new List<StaticEff>();
        }
        public void GetEffNum(string str)
        {
            EffNum = 0;
            if (str == "") return;
            EffNum = int.Parse(str);
        }
        public void GetEffection(StaticEff staticEff)
        {
            staticEffs.Add(staticEff);
        }

        //深拷贝
        public virtual Card Clone()
        {
            Card copy = new Card(id, name, limition, card_type, effection, key == null ? null : (string[])key.Clone(),
                havingKey == null ? null : (string[])havingKey.Clone());
            CopySharedTo(copy);
            return copy;
        }

        //复制与子类无关的共享数据
        protected void CopySharedTo(Card copy)
        {
            copy.EffNum = EffNum;
            copy.staticEffs = new List<StaticEff>();
            if (staticEffs != null)
                foreach (var staticEff in staticEffs)
                    copy.staticEffs.Add(staticEff.Clone());
        }
    }

    public class MonsterCard : Card
    {
        public int attack { get; protected set; }                //攻击力
        public int defense { get; protected set; }                 //防御力
        public int grade { get; protected set; }                  //星级,阶级，连接等级
        public string attribute { get; protected set; }         //属性
        public string native { get; protected set; }              //种族
        public int baseType { get; protected set; }               //基本类型（通常，效果)
        public int type { get; protected set; }               //类型（主卡组，仪式，融合，同调，超量，连接） 
        public int specialType { get; protected set; }        //特殊类型（无，调整，衍生，灵摆）
        public Dictionary<string, string[]> SpeMatter {  get; protected set; }          //正规特招素材
        public MonsterCard(string id, string name, int limition, int card_type, string effectnn, string[] key, string[] havingKey,
            int attack, int defense, int grade, string attribute, string native, int baseType, int type, int specialType) : base(id, name, limition, card_type, effectnn, key, havingKey)
        {
            this.attack = attack;
            this.defense = defense;
            this.grade = grade;
            this.attribute = attribute;
            this.native = native;
            this.baseType = baseType;
            this.type = type;
            this.specialType = specialType;
        }
        public override Card Clone()                       //怪兽深拷贝:含攻防等级属性种族与融合/素材字典
        {
            MonsterCard copy = new MonsterCard(id, name, limition, card_type, effection,
                key == null ? null : (string[])key.Clone(), havingKey == null ? null : (string[])havingKey.Clone(),
                attack, defense, grade, attribute, native, baseType, type, specialType);
            CopySharedTo(copy);
            copy.SpeMatter = new Dictionary<string, string[]>();
            if (SpeMatter != null)
                foreach (var kv in SpeMatter)
                    copy.SpeMatter.Add(kv.Key, kv.Value == null ? null : (string[])kv.Value.Clone());
            return copy;
        }
        public void GetSpeMatter(string str)
        {
            if(str == "") return;
            SpeMatter = new Dictionary<string, string[]>();
            string[] matterNum = str.Split("&");
            for(int i = 0; i < matterNum.Length; i++)
            {
                string[] matterName = matterNum[i].Split(":");
                string[] matterVal = matterName[1].Split("|");
                SpeMatter.Add(matterName[0], matterVal);
            }
        }
    }

    public class MagicCard : Card
    {
        public int magictype { get; protected set; }         //魔法类型（通常，速攻，装备，场地，永续，仪式）
        public MagicCard(string id, string name, int limition, int card_type,string effectnn, string[] key, string[] havingKey,
            int magictype) : base(id, name, limition, card_type, effectnn, key, havingKey)
        {
            this.magictype = magictype;
        }
        public override Card Clone()                       //魔法深拷贝
        {
            MagicCard copy = new MagicCard(id, name, limition, card_type, effection,
                key == null ? null : (string[])key.Clone(), havingKey == null ? null : (string[])havingKey.Clone(), magictype);
            CopySharedTo(copy);
            return copy;
        }
    }

    public class TrapCard : Card
    {
        public int traptype { get; protected set; }          //陷阱类型（通常，永续，反击）
        public TrapCard(string id, string name, int limition, int card_type, string effectnn, string[] key, string[] havingKey,
            int traptype) : base(id, name, limition, card_type, effectnn, key, havingKey)
        {
            this.traptype = traptype;
        }
        public override Card Clone()                       //陷阱深拷贝
        {
            TrapCard copy = new TrapCard(id, name, limition, card_type, effection,
                key == null ? null : (string[])key.Clone(), havingKey == null ? null : (string[])havingKey.Clone(), traptype);
            CopySharedTo(copy);
            return copy;
        }
    }
    public static Dictionary<string, MonsterCard> monsterCards = new Dictionary<string, MonsterCard>();
    public static Dictionary<string, MagicCard> magicCards = new Dictionary<string, MagicCard>();
    public static Dictionary<string, TrapCard> trapCards = new Dictionary<string, TrapCard>();

    public static Card IdFindCard(string id)
    {
        if(monsterCards.TryGetValue(id, out var valueMos))
            return valueMos;
        if (magicCards.TryGetValue(id, out var valueMag))
            return valueMag;
        if (trapCards.TryGetValue(id, out var valueTrap))
            return valueTrap;
        return null;
    }

    public static List<Card> NameSearchCard(string nameField)
    {
        List<Card> res = new List<Card>();
        foreach(var card in monsterCards)
        {
            if (card.Value.name.Contains(nameField))
                res.Add(card.Value);
        }
        foreach(var card in magicCards)
        {
            if(card.Value.name.Contains(nameField))
                res.Add(card.Value);
        }
        foreach (var card in trapCards)
        {
            if(card.Value.name.Contains(nameField))
                res.Add(card.Value);
        }
        return res;
    }

}



    

