using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// EffLogic 全部代价/判定组件(Cost/JudCost)的单元测试。
/// 全部为纯数据层判定:不依赖场景/UI/异步,EditMode 直接运行。
/// 效果执行类(GoToCemetery/SpeSomEff/FusionSummon 等)为异步+UI交互,不在本文件范围。
/// 静态状态(id池/GC登记/记录上下文)跨用例存在,id池充足(0~299)无需复位。
/// </summary>
public class EffCostJudgementTests
{
    [SetUp]
    public void SetUp()
    {
        Record.ClearAllForNewDuel();            //清除记录上下文/落位采集器等静态残留
    }

    #region 构造辅助

    //玩家:区域按需传入(ctor2 各区域判空绑定),Field 必给(JudLocation 容错/IsCanMagic 等依赖)
    private static EntityPlayer MakePlayer(int index = 0)
    {
        var p = new EntityPlayer("p" + index, index == 0 ? "我方" : "对方", null, null,
            new EntityPlayer.Hand(), new EntityPlayer.Cemetery(),
            new EntityPlayer.Banished(), new EntityPlayer.Field());
        p.SetPlayerIndex(index);
        return p;
    }

    private static EntityPlayer.Deck MakeDeck(EntityPlayer owner)
        => new EntityPlayer.Deck(new List<CardBase.Card>(), owner);

    private static EntityPlayer.Cemetery MakeRegion(EntityPlayer owner)
    {
        var r = new EntityPlayer.Cemetery();
        r.BindOwner(owner);
        return r;
    }

    //怪兽实体:type=0主卡组/2融合;grade=星级
    private static EffLogic.EntityMonsterCard MakeMonster(string name, int grade, EntityPlayer owner, int type = 0)
    {
        var card = new CardBase.MonsterCard("m_" + name, name, 3, 0, "", null, null,
            1000, 1000, grade, "暗", "龙", 0, type, 0);
        return new EffLogic.EntityMonsterCard(card, owner);
    }

    private static EffLogic.EntityMagicCard MakeMagic(EntityPlayer owner, string name = "烙印融合")
        => new EffLogic.EntityMagicCard(new CardBase.MagicCard("mg_" + name, name, 3, 1, "", null, null, 0), owner);

    private static EffLogic.EntityTrapCard MakeTrap(EntityPlayer owner)
        => new EffLogic.EntityTrapCard(new CardBase.TrapCard("tp_1", "陷阱卡", 3, 2, "", null, null, 0), owner);

    //入手牌:先加容器再加location(location唯一写入口是ChangeLocation;容器是权威)
    private static void ToHand(EffLogic.EntityCard card, EntityPlayer owner)
    {
        owner.hand.AddCard(card);
        card.ChangeLocation(GameManage.CardLocation.Hand);
    }

    //上场魔陷区(盖放判定/发动判定用)
    private static void ToFieldMT(EffLogic.EntityCard card, EntityPlayer owner)
    {
        owner.field.AddMagicTrap(card);
        card.ChangeLocation(GameManage.CardLocation.Field);
    }

    //上场怪兽区(通召判定用)
    private static void ToFieldMon(EffLogic.EntityCard card, EntityPlayer owner)
    {
        owner.field.AddMonster(card);
        card.ChangeLocation(GameManage.CardLocation.Field);
    }

    //GC(全局永续拦截)测试替身:可控制 IsHaveEff 生效与否
    private class GateContinue : FightLogic.Continue
    {
        private readonly bool effective;
        public GateContinue(bool effective) : base("GC_TEST", null) { this.effective = effective; }
        public override bool IsHaveEff() => effective;
    }

    //QuestBools 桩:忽略区域编码,直接返回指定区域
    private static Func<List<bool>, List<EntityPlayer.EntityFindComponent>> Stub(params EntityPlayer.EntityFindComponent[] regions)
        => _ => new List<EntityPlayer.EntityFindComponent>(regions);

    #endregion

    #region OrdSummon:通常召唤判断

    [Test]
    public void OrdSummon_手卡4星空场可通召()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);
        Assert.IsTrue(new EffLogic.OrdSummon(m, p.field).IsCanPay());
    }

    [Test]
    public void OrdSummon_非手卡不可通召()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);          //location=Deck,未入手牌
        Assert.IsFalse(new EffLogic.OrdSummon(m, p.field).IsCanPay());
    }

    [Test]
    public void OrdSummon_5星需要场上解放()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 5, p);
        ToHand(m, p);
        Assert.IsFalse(new EffLogic.OrdSummon(m, p.field).IsCanPay());   //空场不能上5星

        ToFieldMon(MakeMonster("祭品", 4, p), p);
        Assert.IsTrue(new EffLogic.OrdSummon(m, p.field).IsCanPay());    //有1怪可上5星
    }

    [Test]
    public void OrdSummon_7星需要两只解放()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 7, p);
        ToHand(m, p);
        ToFieldMon(MakeMonster("祭品A", 4, p), p);
        Assert.IsFalse(new EffLogic.OrdSummon(m, p.field).IsCanPay());   //只有1怪不可上7星

        ToFieldMon(MakeMonster("祭品B", 4, p), p);
        Assert.IsTrue(new EffLogic.OrdSummon(m, p.field).IsCanPay());    //2怪可上7星
    }

    [Test]
    public void OrdSummon_场满时4星也不可通召()
    {
        var p = MakePlayer();
        for (int i = 0; i < 5; i++) ToFieldMon(MakeMonster("占位" + i, 4, p), p);
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);
        Assert.IsFalse(new EffLogic.OrdSummon(m, p.field).IsCanPay());
    }

    #endregion

    #region Cover:盖放判断

    [Test]
    public void Cover_手卡且魔陷区有空位可盖放()
    {
        var p = MakePlayer();
        var m = MakeTrap(p);
        ToHand(m, p);
        Assert.IsTrue(new EffLogic.Cover(m, p.field).IsCanPay());
    }

    [Test]
    public void Cover_非手卡不可盖放()
    {
        var p = MakePlayer();
        var m = MakeTrap(p);
        Assert.IsFalse(new EffLogic.Cover(m, p.field).IsCanPay());
    }

    [Test]
    public void Cover_魔陷区满5张不可盖放()
    {
        var p = MakePlayer();
        for (int i = 0; i < 5; i++) p.field.GetMTZon().Add(MakeMagic(p, "占位" + i));
        var m = MakeTrap(p);
        ToHand(m, p);
        Assert.IsFalse(new EffLogic.Cover(m, p.field).IsCanPay());
    }

    #endregion

    #region IsCanMagic:魔法卡发动判断

    private static EffLogic.IsCanMagic MakeMagicCost(EffLogic.EntityMagicCard magic, EntityPlayer p, Func<int> ownerPhase)
        => new EffLogic.IsCanMagic(magic, Stub(p.field), ownerPhase);

    [Test]
    public void IsCanMagic_自己回合可从手卡发动()
    {
        var p = MakePlayer(0);
        var m = MakeMagic(p);
        ToHand(m, p);
        Assert.IsTrue(MakeMagicCost(m, p, () => 0).IsCanPay());
    }

    [Test]
    public void IsCanMagic_对方回合不能从手卡发动()
    {
        var p = MakePlayer(0);
        var m = MakeMagic(p);
        ToHand(m, p);
        Assert.IsFalse(MakeMagicCost(m, p, () => 1).IsCanPay());
    }

    [Test]
    public void IsCanMagic_未注入回合归属则放行()
    {
        var p = MakePlayer(0);
        var m = MakeMagic(p);
        ToHand(m, p);
        Assert.IsTrue(MakeMagicCost(m, p, null).IsCanPay());
    }

    [Test]
    public void IsCanMagic_盖放当回合不可发动()
    {
        var p = MakePlayer(0);
        var m = MakeMagic(p);
        ToFieldMT(m, p);
        m.isCover = 1;
        m.coverTime = 0;
        Assert.IsFalse(MakeMagicCost(m, p, () => 1).IsCanPay());
    }

    [Test]
    public void IsCanMagic_盖放满一回合后可发动()
    {
        var p = MakePlayer(0);
        var m = MakeMagic(p);
        ToFieldMT(m, p);
        m.isCover = 1;
        m.coverTime = 1;
        Assert.IsTrue(MakeMagicCost(m, p, () => 1).IsCanPay());
    }

    [Test]
    public void IsCanMagic_location滞后但卡实际在场上容器内不算手卡发动()
    {
        //容器为权威容错:location被改成Hand但卡还在魔陷区 → 按"已在场"处理,对方回合也可发动
        var p = MakePlayer(0);
        var m = MakeMagic(p);
        ToFieldMT(m, p);
        m.ChangeLocation(GameManage.CardLocation.Hand);   //模拟location滞后
        Assert.IsTrue(MakeMagicCost(m, p, () => 1).IsCanPay());
    }

    [Test]
    public void IsCanMagic_魔陷区满时从手卡发动被拦()
    {
        var p = MakePlayer(0);
        for (int i = 0; i < 5; i++) p.field.GetMTZon().Add(MakeMagic(p, "占位" + i));
        var m = MakeMagic(p);
        ToHand(m, p);
        Assert.IsFalse(MakeMagicCost(m, p, () => 0).IsCanPay());
    }

    #endregion

    #region IsCanTrap:陷阱卡发动判断

    private static EffLogic.IsCanTrap MakeTrapCost(EffLogic.EntityTrapCard trap, EntityPlayer p)
        => new EffLogic.IsCanTrap(trap, Stub(p.field));

    [Test]
    public void IsCanTrap_未盖放不可发动()
    {
        var p = MakePlayer();
        var t = MakeTrap(p);
        ToFieldMT(t, p);
        t.isCover = 0;
        Assert.IsFalse(MakeTrapCost(t, p).IsCanPay());
    }

    [Test]
    public void IsCanTrap_盖放当回合不可发动()
    {
        var p = MakePlayer();
        var t = MakeTrap(p);
        ToFieldMT(t, p);
        t.isCover = 1;
        t.coverTime = 0;
        Assert.IsFalse(MakeTrapCost(t, p).IsCanPay());
    }

    [Test]
    public void IsCanTrap_盖放满一回合后可发动()
    {
        var p = MakePlayer();
        var t = MakeTrap(p);
        ToFieldMT(t, p);
        t.isCover = 1;
        t.coverTime = 1;
        Assert.IsTrue(MakeTrapCost(t, p).IsCanPay());
    }

    #endregion

    #region JudCostHp / JudCostCard:数值类代价

    [Test]
    public void JudCostHp_生命值足够则通过()
    {
        var p = MakePlayer();                       //初始8000
        Assert.IsTrue(new EffLogic.JudCostHp(8000, () => p).IsCanPay());
        Assert.IsFalse(new EffLogic.JudCostHp(8001, () => p).IsCanPay());

        p.ChangeHP(-100);                           //7900
        Assert.IsTrue(new EffLogic.JudCostHp(7900, () => p).IsCanPay());
        Assert.IsFalse(new EffLogic.JudCostHp(7901, () => p).IsCanPay());
    }

    [Test]
    public void Cost_Gc永续拦截_生效时判断失败()
    {
        var p = MakePlayer();
        var cost = new EffLogic.JudCostHp(0, () => p);
        cost.SetContinueGates(new List<string> { "GC_TEST" }, null);

        cost.SetContinueRegistry(new List<FightLogic.Continue> { new GateContinue(true) });
        Assert.IsFalse(cost.IsCanPay());            //命中且生效 → 拦截

        cost.SetContinueRegistry(new List<FightLogic.Continue> { new GateContinue(false) });
        Assert.IsTrue(cost.IsCanPay());             //命中但不生效 → 放行
    }

    [Test]
    public void JudCostCard_手牌数量足够则通过()
    {
        var p = MakePlayer();                       //初始5张
        Assert.IsTrue(new EffLogic.JudCostCard(5, () => p).IsCanPay());
        Assert.IsFalse(new EffLogic.JudCostCard(6, () => p).IsCanPay());

        p.ChangeHandNum(2);                         //7张
        Assert.IsTrue(new EffLogic.JudCostCard(7, () => p).IsCanPay());
    }

    #endregion

    #region JudExist:判断存在cost

    [Test]
    public void JudExist_无候选区域时失败()
    {
        var p = MakePlayer();
        var cost = new EffLogic.JudExist(new EffLogic.IniExist(null, null), Stub(p.hand));
        Assert.IsFalse(cost.IsCanPay());            //手牌空
    }

    [Test]
    public void JudExist_存在满足条件的卡即通过()
    {
        var p = MakePlayer();
        ToHand(MakeMonster("烙印龙", 4, p), p);
        var cost = new EffLogic.JudExist(new EffLogic.IniExist(null, null), Stub(p.hand));
        Assert.IsTrue(cost.IsCanPay());             //无条件=任意卡命中
    }

    [Test]
    public void JudExist_排除卡名不算命中()
    {
        var p = MakePlayer();
        ToHand(MakeMonster("烙印龙", 4, p), p);
        var cost = new EffLogic.JudExist(new EffLogic.IniExist(null, null, "烙印龙"), Stub(p.hand));
        Assert.IsFalse(cost.IsCanPay());            //唯一一张卡被排除 → 失败
    }

    [Test]
    public void JudExist_区域查询为空时失败()
    {
        var cost = new EffLogic.JudExist(new EffLogic.IniExist(null, null), null);
        Assert.IsFalse(cost.IsCanPay());            //Value得到null → 防御返回false
    }

    #endregion

    #region JudDrawCard:抽卡cost

    [Test]
    public void JudDrawCard_卡组数量不足不可抽()
    {
        var p = MakePlayer();
        var deck = MakeDeck(p);
        var cost = new EffLogic.JudDrawCard(new EffLogic.IniDrawCard(1), Stub(deck));
        Assert.IsFalse(cost.IsCanPay());            //卡组空

        deck.AddCard(MakeMonster("卡组怪", 4, p));
        Assert.IsTrue(cost.IsCanPay());             //有1张够抽
    }

    #endregion

    #region JudTime:阶段/回合归属判断

    [Test]
    public void JudTime_自己回合且阶段匹配可通过()
    {
        var cost = new EffLogic.JudTime(
            new EffLogic.IniTime(0, new List<GameManage.GamePhase> { GameManage.GamePhase.Main1Phase }),
            () => GameManage.GamePhase.Main1Phase, () => 0);
        Assert.IsTrue(cost.IsCanPay());
    }

    [Test]
    public void JudTime_阶段不匹配则失败()
    {
        var cost = new EffLogic.JudTime(
            new EffLogic.IniTime(0, new List<GameManage.GamePhase> { GameManage.GamePhase.Main1Phase }),
            () => GameManage.GamePhase.FightPhase, () => 0);
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void JudTime_对方回合且要求自己回合则失败()
    {
        var cost = new EffLogic.JudTime(
            new EffLogic.IniTime(0, new List<GameManage.GamePhase> { GameManage.GamePhase.Main1Phase }),
            () => GameManage.GamePhase.Main1Phase, () => 1);
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void JudTime_双方回合不限制归属()
    {
        var cost = new EffLogic.JudTime(
            new EffLogic.IniTime(2, new List<GameManage.GamePhase> { GameManage.GamePhase.Main2Phase }),
            () => GameManage.GamePhase.Main2Phase, () => 1);
        Assert.IsTrue(cost.IsCanPay());
    }

    #endregion

    #region JudLocation:发动地点判断(含场上容错)

    [Test]
    public void JudLocation_地点匹配即通过()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);
        Assert.IsTrue(new EffLogic.JudLocation(m,
            new EffLogic.IniLocation(new List<GameManage.CardLocation> { GameManage.CardLocation.Hand })).IsCanPay());
    }

    [Test]
    public void JudLocation_要求场上但字段为Hand且不在场则失败()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);
        Assert.IsFalse(new EffLogic.JudLocation(m,
            new EffLogic.IniLocation(new List<GameManage.CardLocation> { GameManage.CardLocation.Field })).IsCanPay());
    }

    [Test]
    public void JudLocation_字段滞后但卡在实际场上容器内仍通过()
    {
        //容器为权威容错:location字段滞后为Hand,但卡确实在控制者场上容器 → 不误拦
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToFieldMon(m, p);
        m.ChangeLocation(GameManage.CardLocation.Hand);   //模拟字段滞后
        Assert.IsTrue(new EffLogic.JudLocation(m,
            new EffLogic.IniLocation(new List<GameManage.CardLocation> { GameManage.CardLocation.Field })).IsCanPay());
    }

    [Test]
    public void JudLocation_控制权转移后原持有者场上容器内也通过()
    {
        var p = MakePlayer(0);
        var other = MakePlayer(1);
        var m = MakeMonster("怪", 4, p);
        ToFieldMon(m, p);
        m.ChangeLocation(GameManage.CardLocation.Hand);   //字段滞后为Hand
        m.SetCurrentOwner(other);                          //控制权转移
        Assert.IsTrue(new EffLogic.JudLocation(m,
            new EffLogic.IniLocation(new List<GameManage.CardLocation> { GameManage.CardLocation.Field })).IsCanPay());
    }

    [Test]
    public void JudLocation_要求手卡但卡在场上则失败()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToFieldMon(m, p);
        Assert.IsFalse(new EffLogic.JudLocation(m,
            new EffLogic.IniLocation(new List<GameManage.CardLocation> { GameManage.CardLocation.Hand })).IsCanPay());
    }

    #endregion

    #region JudLastEffCost:连锁中上一个效果类型

    private static StateMachine.EffOrder MakeOrder(params string[] effOrderType)
        => new StateMachine.EffOrder(0, "0", 1, effOrderType, () => UniTask.CompletedTask, 1);

    [Test]
    public void JudLastEffCost_连锁栈为空则失败()
    {
        var cost = new EffLogic.JudLastEffCost(new EffLogic.IniJudLastEffCost(new List<string> { "active" }), () => null);
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void JudLastEffCost_链顶类型匹配则通过()
    {
        var cost = new EffLogic.JudLastEffCost(
            new EffLogic.IniJudLastEffCost(new List<string> { "active", "magic" }),
            () => MakeOrder("active"));
        Assert.IsTrue(cost.IsCanPay());
    }

    [Test]
    public void JudLastEffCost_链顶类型不匹配则失败()
    {
        var cost = new EffLogic.JudLastEffCost(
            new EffLogic.IniJudLastEffCost(new List<string> { "trap" }),
            () => MakeOrder("active"));
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void JudLastEffCost_未配置允许类型则不限制()
    {
        var cost = new EffLogic.JudLastEffCost(new EffLogic.IniJudLastEffCost(new List<string>()), () => MakeOrder("anything"));
        Assert.IsTrue(cost.IsCanPay());
    }

    #endregion

    #region SelfJudregTime:自身位置+停留回合

    [Test]
    public void SelfJudregTime_初始化数据为空直接抛异常()
    {
        Assert.Throws<ArgumentException>(() => new EffLogic.SelfJudregTime(null, null));   //fail-fast防配置缺失静默失效
    }

    [Test]
    public void SelfJudregTime_本回合进入模式()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);                                    //AddCard重置regTime=0
        var cost = new EffLogic.SelfJudregTime(new EffLogic.IniSelfJudregTime(0, null), m);
        Assert.IsTrue(cost.IsCanPay());

        m.AddRegTime();                                  //过一个回合结束 → 1
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void SelfJudregTime_停留回合模式()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);
        m.AddRegTime(2);
        Assert.IsTrue(new EffLogic.SelfJudregTime(new EffLogic.IniSelfJudregTime(2, null), m).IsCanPay());
        Assert.IsFalse(new EffLogic.SelfJudregTime(new EffLogic.IniSelfJudregTime(3, null), m).IsCanPay());
    }

    [Test]
    public void SelfJudregTime_位置限制不命中则失败()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);                                    //location=Hand
        var cost = new EffLogic.SelfJudregTime(
            new EffLogic.IniSelfJudregTime(0, new List<GameManage.CardLocation> { GameManage.CardLocation.Cemetery }), m);
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void SelfJudregTime_负数回合按零处理()
    {
        var p = MakePlayer();
        var m = MakeMonster("怪", 4, p);
        ToHand(m, p);
        Assert.IsTrue(new EffLogic.SelfJudregTime(new EffLogic.IniSelfJudregTime(-3, null), m).IsCanPay());
    }

    #endregion

    #region JudregTime:区域进入登记/停留回合判断

    [Test]
    public void JudregTime_初始化数据为空直接抛异常()
    {
        Assert.Throws<ArgumentException>(() => new EffLogic.JudregTime(null, null));
    }

    [Test]
    public void JudregTime_停留回合模式按regTime判定()
    {
        var p = MakePlayer();
        var region = p.cemetery;
        var cost = new EffLogic.JudregTime(new EffLogic.IniJudregTime(2, null, null), Stub(region));

        Assert.IsFalse(cost.IsCanPay());                //区域空

        var m = MakeMonster("怪", 4, p);
        region.AddCard(m);                              //进入重置regTime=0
        Assert.IsFalse(cost.IsCanPay());                //未停留满2回合

        m.AddRegTime(2);
        Assert.IsTrue(cost.IsCanPay());
    }

    [Test]
    public void JudregTime_本回合进入登记模式()
    {
        var p = MakePlayer();
        var region = p.cemetery;
        var cost = new EffLogic.JudregTime(new EffLogic.IniJudregTime(0, null, null), Stub(region));
        var m = MakeMonster("怪", 4, p);

        Assert.IsFalse(cost.IsCanPay());                //本回合无进入登记

        region.RegisterBoutSent(m);                     //登记"本回合进入"(快照副本)
        Assert.IsTrue(cost.IsCanPay());                 //登记表命中即通过(卡本身无需还在区域里)

        region.ClearBoutSent();                         //回合界限清空
        Assert.IsFalse(cost.IsCanPay());
    }

    #endregion

    #region FusionSummonCost:融合判断

    private static EffLogic.EntityMonsterCard MakeFusionMonster(string name, int grade, EntityPlayer owner, string speMatter)
    {
        var card = new CardBase.MonsterCard("f_" + name, name, 3, 0, "", null, null,
            2000, 2000, grade, "暗", "龙", 0, 2, 0);     //type=2融合怪兽
        if (!string.IsNullOrEmpty(speMatter)) card.GetSpeMatter(speMatter);
        return new EffLogic.EntityMonsterCard(card, owner);
    }

    //FusionSummonCost.Value()按融合地点/素材地点各调一次QuestBools,桩按调用序返回对应区域
    private static EffLogic.FusionSummonCost MakeFusionCost(EntityPlayer p,
        EntityPlayer.EntityFindComponent fusionZone, EntityPlayer.EntityFindComponent materialZone,
        List<string> speNameKey, List<int> nameKey, int includeSelf = 0, int maxMaterialCount = 0,
        EffLogic.EntityCard holder = null)
    {
        var fusionRegions = new List<EntityPlayer.EntityFindComponent> { fusionZone };
        var materialRegions = new List<EntityPlayer.EntityFindComponent> { materialZone };
        int call = 0;
        var ini = new EffLogic.IniFusionSummonCost(speNameKey, nameKey,
            new List<GameManage.CardLocation> { GameManage.CardLocation.Field },
            new List<GameManage.CardLocation> { GameManage.CardLocation.Cemetery }, includeSelf, maxMaterialCount);
        return new EffLogic.FusionSummonCost(ini,
            _ => { call++; return call % 2 == 1 ? fusionRegions : materialRegions; }, holder);
    }

    [Test]
    public void FusionSummon_素材满足名字条件可融合()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);
        fusionZone.AddCard(MakeFusionMonster("冰剑龙", 8, p, "name:素材A"));
        materialZone.AddCard(MakeMonster("素材A", 4, p));

        var cost = MakeFusionCost(p, fusionZone, materialZone, new List<string> { "素材A" }, new List<int> { 0 });
        Assert.IsTrue(cost.IsCanPay());
    }

    [Test]
    public void FusionSummon_素材不满足则失败()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);
        fusionZone.AddCard(MakeFusionMonster("冰剑龙", 8, p, "name:素材A"));
        materialZone.AddCard(MakeMonster("别的卡", 4, p));              //名字不匹配

        var cost = MakeFusionCost(p, fusionZone, materialZone, new List<string> { "素材A" }, new List<int> { 0 });
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void FusionSummon_素材区为空则失败()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);
        fusionZone.AddCard(MakeFusionMonster("冰剑龙", 8, p, "name:素材A"));

        var cost = MakeFusionCost(p, fusionZone, materialZone, new List<string> { "素材A" }, new List<int> { 0 });
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void FusionSummon_包含自身时效果持有卡并入素材池()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);                               //素材区为空
        fusionZone.AddCard(MakeFusionMonster("冰剑龙", 8, p, "name:持有卡"));
        var holder = MakeMonster("持有卡", 4, p);
        holder.ChangeLocation(GameManage.CardLocation.Hand);            //给持有卡分配id(与素材池判重)

        var cost = MakeFusionCost(p, fusionZone, materialZone, new List<string> { "持有卡" }, new List<int> { 0 },
            includeSelf: 1, holder: holder);
        Assert.IsTrue(cost.IsCanPay());
    }

    [Test]
    public void FusionSummon_融合怪兽等级约束()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);
        fusionZone.AddCard(MakeFusionMonster("高星龙", 8, p, "name:素材A"));
        materialZone.AddCard(MakeMonster("素材A", 4, p));

        Assert.IsTrue(MakeFusionCost(p, fusionZone, materialZone, new List<string> { ">=8" }, new List<int> { 0 }).IsCanPay());

        fusionZone.RemoveCard(fusionZone.cards[0]);
        fusionZone.AddCard(MakeFusionMonster("低星龙", 7, p, "name:素材A"));
        Assert.IsFalse(MakeFusionCost(p, fusionZone, materialZone, new List<string> { ">=8" }, new List<int> { 0 }).IsCanPay());
    }

    [Test]
    public void FusionSummon_除外卡名的融合怪兽不可被融合()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);
        fusionZone.AddCard(MakeFusionMonster("冰剑龙", 8, p, "name:素材A"));
        materialZone.AddCard(MakeMonster("素材A", 4, p));

        //speNameKey="冰剑龙"+Name_Key=-1 → 该卡名的融合怪兽被排除,即使素材满足也不可融合
        var cost = MakeFusionCost(p, fusionZone, materialZone, new List<string> { "冰剑龙" }, new List<int> { -1 });
        Assert.IsFalse(cost.IsCanPay());
    }

    [Test]
    public void FusionSummon_素材张数上限()
    {
        var p = MakePlayer();
        var fusionZone = MakeRegion(p);
        var materialZone = MakeRegion(p);
        //目标怪兽需2条素材条件(name:素材A + type:0);素材区提供2张可分别满足
        fusionZone.AddCard(MakeFusionMonster("冰剑龙", 8, p, "name:素材A&type:0"));
        materialZone.AddCard(MakeMonster("素材A", 4, p));
        materialZone.AddCard(MakeMonster("杂兵", 4, p));

        Assert.IsTrue(MakeFusionCost(p, fusionZone, materialZone, new List<string> { "素材A" }, new List<int> { 0 },
            maxMaterialCount: 2).IsCanPay());
        Assert.IsFalse(MakeFusionCost(p, fusionZone, materialZone, new List<string> { "素材A" }, new List<int> { 0 },
            maxMaterialCount: 1).IsCanPay());                            //条件数2>上限1 → 判断失败
    }

    #endregion
}
