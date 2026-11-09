using NUnit.Framework;

public class EffLogicPureTests
{
    #region TryParseLimition:自肃形式 -> 编码int

    //直接填数字编码:成功且回填编码
    [Test]
    public void TryParseLimition_数字编码返回真并回填()
    {
        int code;
        bool ok = EffLogic.TryParseLimition("1021", out code);
        Assert.IsTrue(ok);
        Assert.AreEqual(1021, code);
    }

    //填自肃类名称:枚举解析(忽略大小写)
    [Test]
    public void TryParseLimition_枚举名忽略大小写()
    {
        int code;
        Assert.IsTrue(EffLogic.TryParseLimition("Ex_No_Summon", out code));
        Assert.AreEqual(1021, code);

        Assert.IsTrue(EffLogic.TryParseLimition("ex_no_summon", out code));
        Assert.AreEqual(1021, code);
    }

    //0=None视为无效:无论数字还是枚举名都失败
    [Test]
    public void TryParseLimition_零值视为无效()
    {
        int code;
        Assert.IsFalse(EffLogic.TryParseLimition("0", out code));
        Assert.IsFalse(EffLogic.TryParseLimition("None", out code));
    }

    //非法输入:空白/乱串一律失败,且out参数不残留脏值
    [Test]
    public void TryParseLimition_非法输入返回假()
    {
        int code;
        Assert.IsFalse(EffLogic.TryParseLimition("", out code));
        Assert.IsFalse(EffLogic.TryParseLimition("   ", out code));
        Assert.IsFalse(EffLogic.TryParseLimition("abc", out code));
        Assert.AreEqual(0, code);
    }

    #endregion

    #region CanChainByOrderState:连锁限制判定

    //orderStackState=-1 表示无连锁限制,任何卡类型均可连锁
    [Test]
    public void CanChainByOrderState_无限制时任意卡型可连锁()
    {
        Assert.IsTrue(EffLogic.CanChainByOrderState(-1, 0));
        Assert.IsTrue(EffLogic.CanChainByOrderState(-1, 1));
        Assert.IsTrue(EffLogic.CanChainByOrderState(-1, 2));
        Assert.IsTrue(EffLogic.CanChainByOrderState(-1, 99));
    }

    //0=禁怪兽:cardType==0 不可连锁,其余可
    [Test]
    public void CanChainByOrderState_禁怪兽()
    {
        Assert.IsFalse(EffLogic.CanChainByOrderState(0, 0));
        Assert.IsTrue(EffLogic.CanChainByOrderState(0, 1));
        Assert.IsTrue(EffLogic.CanChainByOrderState(0, 2));
    }

    //1=禁魔法 / 2=禁陷阱:对应卡型不可连锁,其余可
    [Test]
    public void CanChainByOrderState_禁魔法与禁陷阱()
    {
        Assert.IsFalse(EffLogic.CanChainByOrderState(1, 1));
        Assert.IsTrue(EffLogic.CanChainByOrderState(1, 0));

        Assert.IsFalse(EffLogic.CanChainByOrderState(2, 2));
        Assert.IsTrue(EffLogic.CanChainByOrderState(2, 1));
    }

    #endregion

    #region NewRegionCode:区域码初始化

    //区域码长度=12(双方各6区),初始全 false
    [Test]
    public void NewRegionCode_长度12且初始全否()
    {
        var code = EffLogic.NewRegionCode();
        Assert.AreEqual(EffLogic.RegionCodeLength, code.Count);
        Assert.AreEqual(EffLogic.RegionCodeLength, code.FindAll(b => !b).Count);
    }

    #endregion
}
