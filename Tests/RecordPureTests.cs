using NUnit.Framework;

/// <summary>
/// Record 命令数据单元测试(纯数据层,不依赖场景;静态状态每个用例前复位)。
/// 覆盖:命令读写/移动补记/最近命令查找/开局清空。
/// </summary>
public class RecordPureTests
{
    [SetUp]
    public void SetUp()
    {
        //静态状态(采集器/暂存/重定向/LastInstance)跨用例复位,消除污染
        Record.ClearAllForNewDuel();
    }

    private Record NewRecordWithAttack()
    {
        var record = new Record();
        record.Add(Record.Order.DrawCard);          // 一条无关命令,用于验证补记不会落错
        record.Add(Record.Order.Attack);            // 最近一条攻击命令(Add 自动登记 LastInstance)
        return record;
    }

    #region AppendMoveToLastOrder:移动补记(战斗破坏等不走ChangeLocation的移动)

    [Test]
    public void AppendMove_追加到最近一条攻击命令()
    {
        var record = NewRecordWithAttack();
        var records = record.GetRecords();

        Record.AppendMoveToLastOrder(Record.Order.Attack, "127", null, Record.MoveTo.Cemetery);

        //落在 Attack 上,且 afterId 为空时等于 beforeId
        var attack = records[1];
        Assert.AreEqual("127", attack.GetData(Record.Key.CurrentIdsBefore, ""));
        Assert.AreEqual("127", attack.GetData(Record.Key.CurrentIdsAfter, ""));
        Assert.AreEqual("127>127>Cemetery", attack.GetData(Record.Key.CurrentIdsMoves, ""));
        Assert.AreEqual(Record.MoveTo.Cemetery, attack.GetData(Record.Key.Move, ""));

        //不污染前面的无关命令
        Assert.IsFalse(records[0].HasData(Record.Key.CurrentIdsMoves));
    }

    [Test]
    public void AppendMove_连续追加用竖线分隔不覆盖()
    {
        var record = NewRecordWithAttack();
        var attack = record.GetRecords()[1];

        Record.AppendMoveToLastOrder(Record.Order.Attack, "156", "156", Record.MoveTo.Cemetery);
        Record.AppendMoveToLastOrder(Record.Order.Attack, "127", "127", Record.MoveTo.Cemetery);

        Assert.AreEqual("156|127", attack.GetData(Record.Key.CurrentIdsBefore, ""));
        Assert.AreEqual("156>156>Cemetery|127>127>Cemetery", attack.GetData(Record.Key.CurrentIdsMoves, ""));
    }

    //没有可落的命令时必须静默跳过,不能抛异常(回放记录侧的兜底原则:宁缺勿错)
    [Test]
    public void AppendMove_无匹配命令时静默跳过()
    {
        var record = new Record();
        record.Add(Record.Order.DrawCard);          // 没有任何 Attack 命令

        Assert.DoesNotThrow(() =>
            Record.AppendMoveToLastOrder(Record.Order.Attack, "127", "127", Record.MoveTo.Cemetery));

        Assert.IsFalse(record.GetRecords()[0].HasData(Record.Key.CurrentIdsMoves));
    }

    //beforeId 为空:无定位依据,直接忽略
    [Test]
    public void AppendMove_移动前id为空时忽略()
    {
        var record = NewRecordWithAttack();
        var attack = record.GetRecords()[1];

        Record.AppendMoveToLastOrder(Record.Order.Attack, "", "127", Record.MoveTo.Cemetery);

        Assert.IsFalse(attack.HasData(Record.Key.CurrentIdsBefore));
    }

    #endregion

    #region FindLast:最近命令查找(PatchAttackDamage/破坏登记的落点依据)

    [Test]
    public void FindLast_多条同类命令取最近一条()
    {
        var record = new Record();
        var first = record.Add(Record.Order.Attack).AddData(Record.Key.Damage, 100);
        var second = record.Add(Record.Order.Attack).AddData(Record.Key.Damage, 300);

        var found = record.FindLast(Record.Order.Attack);

        Assert.AreSame(second, found);
        Assert.AreEqual("300", found.GetData(Record.Key.Damage, ""));
    }

    [Test]
    public void FindLast_无同类命令返回空()
    {
        var record = new Record();
        record.Add(Record.Order.DrawCard);

        Assert.IsNull(record.FindLast(Record.Order.Attack));
    }

    #endregion

    #region RebackClear / 开局清空

    [Test]
    public void RebackClear_清空全部命令与pending()
    {
        var record = NewRecordWithAttack();

        record.RebackClear();

        Assert.AreEqual(0, record.Count);
        Assert.IsNull(record.pending);
    }

    [Test]
    public void ClearAllForNewDuel_复位LastInstance防止跨局污染()
    {
        var record = NewRecordWithAttack();
        Assert.IsNotNull(Record.LastInstance);      // Add 时自动登记

        Record.ClearAllForNewDuel();

        Assert.IsNull(Record.LastInstance);         // 跨局残留的入口必须清掉
    }

    #endregion

    #region BaseRecord 数据读写

    [Test]
    public void GetDataList_竖线分割且跳过空段()
    {
        var record = new Record();
        var cmd = record.Add(Record.Order.EffectAct)
            .AddData(Record.Key.CardIds, "87746184||55273560");

        var list = cmd.GetDataList(Record.Key.CardIds);

        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("87746184", list[0]);
        Assert.AreEqual("55273560", list[1]);
    }

    //同键二次 AddData:覆盖而非追加(与 PatchLastPending 的"已有同键不覆盖"语义互补)
    [Test]
    public void AddData_同键覆盖旧值()
    {
        var record = new Record();
        var cmd = record.Add(Record.Order.EffectAct)
            .AddData(Record.Key.MagicTrapZone, "1")
            .AddData(Record.Key.MagicTrapZone, "3");

        Assert.AreEqual("3", cmd.GetData(Record.Key.MagicTrapZone, ""));
    }

    [Test]
    public void GetDataInt_非数字回退默认值()
    {
        var record = new Record();
        var cmd = record.Add(Record.Order.EffectAct).AddData(Record.Key.Damage, "abc");

        Assert.AreEqual(-1, cmd.GetDataInt(Record.Key.Damage));
        Assert.AreEqual(5, cmd.GetDataInt(Record.Key.Damage, 5));
    }

    #endregion
}
