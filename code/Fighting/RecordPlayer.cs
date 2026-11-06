using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static EffLogic;

/// <summary>回放节拍常量:每条回放命令之间的停顿秒数(默认2秒)</summary>
public static class RecordPlaySetting
{
    public const float DefaultInterval = 2f;        //默认间隔:2秒
    public const float MinInterval = 0f;            //最小间隔:0=不停顿(连续回放)
    public const float MaxInterval = 60f;           //最大间隔:防止误填过大导致回放"卡住"
}

/// <summary>
/// 命令回放器:record场景专用。
/// record场景由fighting场景复制而来,具备全部UI功能但不具备对局逻辑(无cost判断/无AI/无状态机),
/// 整局完全由 Record 中的命令按序驱动,故回放结果与真实对局一致。
/// </summary>
public class RecordPlayer
{
    private readonly Record record;
    private readonly IRecordPlayBack handler;

    public int cursor { get; private set; }

    public RecordPlayer(Record record, IRecordPlayBack handler)
    {
        this.record = record;
        this.handler = handler;
        cursor = 0;
    }

    public int Count { get { return record == null ? 0 : record.Count; } }
    public bool IsFinish { get { return record == null || cursor >= record.Count; } }

    public void Reset() { cursor = 0; }

    public void Seek(int index)
    {
        if (record == null) return;
        cursor = Mathf.Clamp(index, 0, Mathf.Max(0, record.Count - 1));
    }

    /// <summary>回放一条命令,返回是否还有后续命令</summary>
    public async UniTask<bool> Step()
    {
        if (IsFinish) return false;
        int next;
        try
        {
            next = await record.PlayAt(cursor, handler);
        }
        catch (OperationCanceledException)
        {
            throw;                                                  //取消(退出回放场景)照常向上抛
        }
        catch (Exception ex)
        {
            //单条命令的UI表现出错不能拖垮整场回放:记日志后继续下一条。
            //且必须推进游标,否则会卡在同一条命令上死循环(表现为"回放一闪而过/没有间隔")。
            UnityEngine.Debug.LogError($"[回放] 第 {cursor + 1} 条命令回放失败:{ex.Message}\n{ex.StackTrace}");
            next = cursor + 1;
        }
        cursor = next < 0 ? record.Count : next;
        return !IsFinish;
    }

    /// <summary>连续回放至结束(interval=每条命令之间的停顿秒数,默认 RecordPlaySetting.DefaultInterval=2秒;token 用于退出回放场景时中断)</summary>
    public async UniTask PlayAll(float interval = RecordPlaySetting.DefaultInterval, CancellationToken token = default)
    {
        float wait = RecordPlaySetting.DefaultInterval;
        int waitMs = Mathf.RoundToInt(wait * 1000f);
        UnityEngine.Debug.Log($"[回放] PlayAll 开始:共 {Count} 条命令,间隔 {wait:0.##} 秒(waitMs={waitMs}),已完成={IsFinish}");
        try
        {
            while (!IsFinish)
            {
                if (token.CanBeCanceled) token.ThrowIfCancellationRequested();
                int played = cursor + 1;
                await Step();
                UnityEngine.Debug.Log($"[回放] 第 {played}/{Count} 条已回放,准备停顿 {wait:0.##} 秒");
                if (IsFinish) break;
                if (waitMs > 0)
                {
                    //ignoreTimeScale=true:不受 Time.timeScale 影响,保证间隔恒定(毫秒重载+命名参数,避免重载歧义)
                    float t0 = UnityEngine.Time.realtimeSinceStartup;
                    await UniTask.Delay(waitMs, ignoreTimeScale: true, cancellationToken: token);
                    //等待结束的日志:同时打出实际经过秒数,便于核对停顿是否真的生效
                    UnityEngine.Debug.Log($"[回放] 已等待 {wait:0.##} 秒(实际 {UnityEngine.Time.realtimeSinceStartup - t0:0.###} 秒)");
                }
            }
        }
        catch (OperationCanceledException)
        {
            //退出回放场景:中断回放,属正常流程
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[回放] 回放异常终止:{ex}");     //兜底:不要让异常静默吞掉回放
        }
    }
}

/// <summary>
/// 默认UI回放实现:直接驱动 FightUI,不做任何规则判断(判断类数据本就不在命令里)。
/// 需要更丰富表现时,实现自己的 IRecordPlayBack 传给 RecordPlayer 即可。
/// </summary>
public class RecordPlayBackUI : IRecordPlayBack
{
    private readonly FightUI fightUI;
    private readonly EntityPlayer[] players = new EntityPlayer[2];
    private readonly List<EntityCard>[] hands = new List<EntityCard>[2];
    private readonly Dictionary<string, EntityCard> cards = new Dictionary<string, EntityCard>();   //currentid → 实体
    //回放侧血量镜像(-1=尚未取初值):首次按界面当前显示的血量取初值,之后逐次扣减并直接刷新UI。
    //必须自己留一份:OnHPChangeToHandle 收到的是"变化量"而不是新血量,连续两次战斗伤害
    //会被当成"把血量设成这次的伤害值"(血量只减一次,第二次不变)。
    private readonly int[] hp = { -1, -1 };
    private int played;                                     //已回放命令条数(用于界面进度显示)
    private int total;                                      //命令总条数
    private float interval = RecordPlaySetting.DefaultInterval;   //当前间隔(用于界面进度显示)

    public RecordPlayBackUI(FightUI fightUI)
    {
        this.fightUI = fightUI;
        players[0] = new EntityPlayer("record_0", "回放·我方");
        players[1] = new EntityPlayer("record_1", "回放·对方");
        players[0].SetPlayerIndex(0);
        players[1].SetPlayerIndex(1);
        hands[0] = new List<EntityCard>();
        hands[1] = new List<EntityCard>();
    }

    public void Reset()
    {
        cards.Clear();
        hp[0] = -1;
        hp[1] = -1;
        hands[0].Clear();
        hands[1].Clear();
        played = 0;
    }

    //注入命令总数与当前间隔:界面上显示"回放 i/N 间隔 Xs"
    public void SetPlayInfo(int totalCount, float playInterval)
    {
        total = totalCount;
        interval = playInterval;
    }

    public async UniTask OnRecord(Record.BaseRecord record)
    {
        if (record == null) return;
        played++;
        fightUI?.ShowRecordProgress(played, total, interval);       //每回放一条就刷新进度(含当前间隔)
        switch (record.order)
        {
            case Record.Order.IniHand: await OnIniHand(record); break;
            case Record.Order.DrawCard: await OnDrawCard(record); break;
            case Record.Order.NextPhase: await OnNextPhase(record); break;
            case Record.Order.EffectPay: await OnEffectPay(record); break;
            case Record.Order.EffectAct: await OnEffectAct(record); break;
            case Record.Order.Attack: await OnAttack(record); break;
            case Record.Order.End: await OnEnd(record); break;
            default: break;
        }
        FlipCoverToFaceUp(record);          //盖放卡发动:就地翻面(必须在移动之前,卡还在场上时翻)
        //每条命令执行完统一回放"区域改变"(上场/送墓/除外/回手/回卡组)。
        //记录里"移动前id→移动后id→去向"这类登记可能落在本命令上,也可能落在别的命令上:
        //① 数据层先改区域、命令后建(通召):由 Record.Add 刷进新命令;② 战斗破坏等结算后才新建下一条命令的:会落在后一条命令上。
        //因此不能只在"支付/结算/攻击"里处理,否则漏掉的那些命令在回放里就是"命令走过了、卡还在原地"——这正是UI不刷新的主要来源。
        await ApplyMoves(record);
        //再按回放维护的手牌数据重绘双方手牌:
        //手牌UI始终以 hands 为唯一数据源,任何命令造成的手牌变动(抽卡/上场/送墓/除外/回手)
        //都会在这一步统一反映出来,不必在各命令里各自判断是否需要重绘(那种判断极易漏)。
        RedrawAllHands();
    }

    #region 各命令的UI表现

    private async UniTask OnIniHand(Record.BaseRecord record)
    {
        await ToHand(record);
        fightUI?.RebackText("回放:初始手牌", 2);
    }

    private async UniTask OnDrawCard(Record.BaseRecord record)
    {
        int p = record.PlayerIndex;
        int num = record.GetDataInt(Record.Key.Num, 0);
        await ToHand(record);
        fightUI?.RebackText($"回放:{(p == 0 ? "我方" : "对方")}抽 {num} 张卡", 2);
    }

    private async UniTask ToHand(Record.BaseRecord record)          //把命令里的卡放进对应玩家手牌区
    {
        int p = record.PlayerIndex;
        List<string> ids = record.GetDataList(Record.Key.CardIds);
        List<string> curIds = record.GetDataList(Record.Key.CurrentIds);
        UnityEngine.Debug.Log($"[回放] {record.order} p={p}:卡名=[{JoinStrs(ids)}] id=[{JoinStrs(curIds)}]");
        for (int i = 0; i < ids.Count; i++)
        {
            EntityCard card = Resolve(ids[i], i < curIds.Count ? curIds[i] : null, p);
            if (card == null) continue;
            if (!hands[p].Contains(card))
                hands[p].Add(card);
        }
        //手牌数据(hands)已同步完毕,绘制统一由命令结束时 RedrawAllHands 处理(含预制体映射重新登记)
        await UniTask.Yield();
    }

    //严格按实例id在已登记实体里找卡:找不到返回null(不做任何"按卡名/遍历"兜底)。
    //回放与实际对局统一:每条移动命令都携带"移动前id→移动后id",定位只认id。
    private EntityCard ResolveById(string currentId)
    {
        if (string.IsNullOrEmpty(currentId)) return null;
        EntityCard card;
        return cards.TryGetValue(currentId, out card) ? card : null;
    }

    //按id定位;id未登记时,用命令里的"卡名id"新建实体并登记到该id。
    //这不是模糊兜底:卡此前处在隐藏区(卡组/额外卡组/未露面),回放中确实还没建过,
    //此刻正是它首次出现——与实际对局一致,故按卡名id建实体并记为该id。
    private EntityCard ResolveOrCreate(string staticId, string currentId, int playerIndex)
    {
        EntityCard card = ResolveById(currentId);
        if (card != null) return card;
        if (string.IsNullOrEmpty(staticId)) return null;
        CardBase.Card cardBase = CardBase.IdFindCard(staticId);
        if (cardBase == null) return null;
        EntityCard entity = Factory.EntityCard(cardBase, players[playerIndex == 1 ? 1 : 0]);
        if (entity == null) return null;
        if (!string.IsNullOrEmpty(currentId))
        {
            cards[currentId] = entity;
            entity.SetCurrentIdForReplay(currentId);
        }
        UnityEngine.Debug.Log($"[回放] 卡首次出现(源自隐藏区):{entity.currentName} id={currentId}");
        return entity;
    }

    //取命令里的"移动前id"列表:CurrentIdsBefore → CurrentIds → 单卡命令的 CurrentId(单数)
    private static List<string> BeforeIds(Record.BaseRecord record)
    {
        List<string> before = record.GetDataList(Record.Key.CurrentIdsBefore);
        if (before.Count > 0) return before;
        before = record.GetDataList(Record.Key.CurrentIds);
        if (before.Count > 0) return before;
        //通召/发动等单卡命令只写了 CurrentId(单数,记录的正是"移动前"的id)
        string one = record.GetData(Record.Key.CurrentId, "");
        if (!string.IsNullOrEmpty(one)) return new List<string> { one };
        return before;
    }

    //统一方法(供一切携带"移动前/后id"的命令复用):移除移动前id对应的那份卡UI。
    //必须"归还对象池"而不是 Destroy:Destroy 掉的物体仍留在池里,之后重绘手牌/新建卡取到它
    //就会显示异常——表现为某张手卡莫名消失、下一次重绘又冒出来(卡牌移动的乱序)。
    private void RemoveCardUI(string beforeId)
    {
        if (fightUI == null || string.IsNullOrEmpty(beforeId)) return;
        EntityCard oldCard = ResolveById(beforeId);
        if (oldCard == null)
        {
            //该id此前从未登记:卡来自隐藏区(卡组/额外卡组)或在回放中尚未露面,本就没有UI可删——不是错误
            UnityEngine.Debug.Log($"[回放] 无需移除:id={beforeId} 未登记(卡来自隐藏区,此前无UI);" +
                $" 手牌区实况={fightUI.DumpHandIds()}");
            return;
        }
        Transform old = fightUI.FindCardPrefabById(beforeId);
        if (old == null)
        {
            //已登记却找不到对应预制体:回放状态与记录对不上,打印实况便于核对id漂移
            UnityEngine.Debug.LogWarning($"[回放] 移除旧UI:已登记 id={beforeId}({oldCard.currentName}) 却找不到预制体;" +
                $" 手牌区实况={fightUI.DumpHandIds()}");
            return;
        }
        //卡名与id取自"实际被移除的那份UI"上的实体,便于核对是否删对了对象
        PreEntityCard pec = old.GetComponent<PreEntityCard>();
        EntityCard card = pec != null ? pec.GetEntityCard() : oldCard;
        UnityEngine.Debug.Log($"[回放] 移除旧UI(归还池):卡名={card?.currentName ?? "?"} id={beforeId}" +
            $" 物体={old.name} 所在={old.parent?.name}");
        fightUI.ReleaseCardPrefab(old);                        //解绑 + 归还对象池(不Destroy)
    }

    #region 区域改变命令的统一回放(上场/送墓/除外/回手/回卡组)

    //一条"逐卡移动"明细:移动前id → 移动后id(记录里为空=沿用前id)+ 去向(空=进入隐藏区)
    private class MoveItem
    {
        public string staticId;      //卡名id(建实体用)
        public string beforeId;      //移动前实例id(定位现有UI用)
        public string afterId;       //移动后实例id(''=沿用前id)
        public string move;          //去向(Record.MoveTo;空=卡组/额外卡组等隐藏区)
    }

    //组装本命令要回放的"逐卡移动"清单。
    //优先用记录里的逐卡明细(前id>后id>去向):一条命令里多张卡的去向可能不同,只有逐卡明细能完整还原;
    //老记录没有该键时退化为"命令级单一去向",保证旧录像仍能回放。
    private List<MoveItem> BuildMoves(Record.BaseRecord record)
    {
        List<MoveItem> moves = new List<MoveItem>();
        if (record == null) return moves;

        List<string> cardIds = record.GetDataList(Record.Key.CardIds);
        List<string> triples = record.GetDataList(Record.Key.CurrentIdsMoves);
        if (triples.Count > 0)
        {
            for (int i = 0; i < triples.Count; i++)
            {
                string[] seg = triples[i].Split('>');
                if (seg.Length < 2 || string.IsNullOrEmpty(seg[0])) continue;
                MoveItem item = new MoveItem();
                item.beforeId = seg[0];
                item.afterId = seg[1];
                item.move = seg.Length > 2 ? seg[2] : "";
                item.staticId = StaticIdOf(record, cardIds, i);
                moves.Add(item);
            }
            return moves;
        }

        //兼容老记录:只有"移动前/后id"列表 + 一个命令级去向
        List<string> before = BeforeIds(record);
        List<string> after = record.GetDataList(Record.Key.CurrentIdsAfter);
        string move = record.GetData(Record.Key.Move, "");
        //既没有去向、也没有"移动后id":这不是一条移动命令。
        //典型:选卡/取对象命令只登记了"选中了哪张卡"(CurrentIds/SelectCards/TargetCards),
        //若据此构造移动,会把被选中那张卡的UI当成"进入隐藏区"给删掉(墓地/场上的卡凭空消失)。
        if (string.IsNullOrEmpty(move) && after.Count == 0)
            return moves;
        //守卫不限于上场:送墓/除外同样适用。
        //典型:冰剑龙在对方回合除外对方场上卡,若记录里没登记移动明细,按 CurrentId 回落
        //就会把效果持有卡(冰剑龙)自己除外。宁可不表现,也不能除外/送墓错的卡。
        if (!record.HasData(Record.Key.CurrentIdsBefore) && !record.HasData(Record.Key.CurrentIds)
            && cardIds != null && cardIds.Count > 0 && !cardIds.Contains(record.CardId))
        {
            UnityEngine.Debug.LogWarning($"[问题1日志] 移动:命令 {record.code} 未登记移动前id," +
                $"且目标卡[{JoinStrs(cardIds)}]不是效果持有卡({record.CardId}),无法定位目标,跳过 去向={move}");
            return moves;
        }
        //上场命令里"被上场的卡"不是效果持有卡时(典型:用魔法卡/怪兽的效果特招另一张卡),
        //绝不能用 CurrentId(效果持有卡的id)当移动前id——那会把发动效果的卡自己当成上场目标,
        //表现为"魔法卡被当成怪兽特招到怪兽区"、"场上多出一张盖放卡"。宁可不落位,也不能放错卡。
        if (move == Record.MoveTo.Field
            && !record.HasData(Record.Key.CurrentIdsBefore) && !record.HasData(Record.Key.CurrentIds)
            && cardIds != null && cardIds.Count > 0 && !cardIds.Contains(record.CardId))
        {
            UnityEngine.Debug.LogWarning($"[问题1日志] 上场:命令 {record.code} 未登记移动前id," +
                $"且被上场的卡[{JoinStrs(cardIds)}]不是效果持有卡({record.CardId}),无法定位上场目标,跳过落位");
            return moves;
        }
        for (int i = 0; i < before.Count; i++)
        {
            MoveItem item = new MoveItem();
            item.beforeId = before[i];
            item.afterId = i < after.Count ? after[i] : "";
            item.move = move;
            item.staticId = StaticIdOf(record, cardIds, i);
            moves.Add(item);
        }
        return moves;
    }

    //折叠"同一张卡的连续移动":明细里若出现 a>b 紧接着 b>c,说明这张卡在本条命令里被移动了两次,
    //中间那次只是过渡态,只为它建一次UI再删掉会留下残影(表现为"墓地已有,场上还多一份")。
    //折叠成 a>c(去向取最后一次),中间态不产生任何UI。
    private static List<MoveItem> CollapseChains(List<MoveItem> moves)
    {
        if (moves == null || moves.Count < 2) return moves;
        bool[] consumed = new bool[moves.Count];
        List<MoveItem> result = new List<MoveItem>();
        for (int i = 0; i < moves.Count; i++)
        {
            if (consumed[i]) continue;
            MoveItem cur = moves[i];
            if (cur == null) continue;
            while (!string.IsNullOrEmpty(cur.afterId))
            {
                int next = -1;
                for (int k = i + 1; k < moves.Count; k++)
                    if (!consumed[k] && moves[k] != null && moves[k].beforeId == cur.afterId) { next = k; break; }
                if (next < 0) break;
                UnityEngine.Debug.Log($"[问题1日志] 折叠连续移动:{cur.beforeId}>{cur.afterId} 与 {moves[next].beforeId}>{moves[next].afterId}" +
                    $" → {cur.beforeId}>{moves[next].afterId}({moves[next].move})");
                cur = new MoveItem { staticId = moves[next].staticId, beforeId = cur.beforeId, afterId = moves[next].afterId, move = moves[next].move };
                consumed[next] = true;
            }
            result.Add(cur);
        }
        return result;
    }

    //取第i条移动的"卡名id":命令登记的多卡卡名id(与逐卡明细同序) → 单卡命令的 CardId
    private static string StaticIdOf(Record.BaseRecord record, List<string> cardIds, int index)
    {
        if (cardIds != null && cardIds.Count > 0)
        {
            if (index < cardIds.Count) return cardIds[index];
            if (cardIds.Count == 1) return cardIds[0];
        }
        return record != null ? record.CardId : "";
    }

    //区域改变命令的统一回放:逐卡还原"删旧UI → 换id → 在新区域建UI"。
    //旧实现"命令已执行但UI不更新"的根因有三点,这里逐一修正:
    //  1) 换id必须改写"同一个实体"的实例id(数据层 ChangeLocation 就是这么做的)。旧实现另建新实体,
    //     场景里那份预制体绑定的仍是旧id,之后按id定位全部落空,UI就再也不动了;
    //  2) 必须先删掉全部"移动前id"的旧UI,再逐卡落位:先落位的卡会顶掉同区域后面的卡,
    //     反过来(先落位再删)就会找不到旧UI,卡残留在原区域;
    //  3) 一律以记录里的逐卡去向为准,不再按命令级单一 Move 分支(会丢掉同命令里其它去向的卡)。
    private async UniTask ApplyMoves(Record.BaseRecord record)
    {
        if (fightUI == null || record == null) return;
        //不启用 CollapseChains:它按"前一条的afterId == 后一条的beforeId"判定同一张卡的连续移动,
        //但实例id是随机分配的、会撞车,会把"融合怪兽上场(109>231>Field)"与
        //"另一张恰好也叫231的卡被除外(231>163>Banished)"误判为同一张卡,折叠成 109>163>Banished
        //——融合怪兽就此从不上场。id相等不足以证明是同一张卡,宁可多走一步也不要吞掉一次上场。
        List<MoveItem> moves = BuildMoves(record);
        UnityEngine.Debug.Log($"[问题1日志] 移动回放:order={record.order} code={record.code} 逐卡明细={moves.Count} 条 p={record.PlayerIndex}");
        if (moves.Count == 0) return;

        for (int i = 0; i < moves.Count; i++)
            RemoveCardUI(moves[i].beforeId);                    //1 先删旧UI:此刻还是移动前的布局,按实例id必然命中

        for (int i = 0; i < moves.Count; i++)
            await ApplyOneMove(record, moves[i]);               //2 逐卡换id并落到新区域

        RedrawAllHands();                                       //3 手牌数据可能变了(回手/离手),立刻重绘
    }

    private async UniTask ApplyOneMove(Record.BaseRecord record, MoveItem item)
    {
        if (item == null) return;
        int p = record.PlayerIndex;                             //命令归属玩家=卡的主人(区域改变只登记在卡主人的命令里)

        //2.1 定位实体:同id的实体只存在一份(换id后回放侧会同步改字典键),找不到才按卡名id新建(卡此前在隐藏区)
        EntityCard card = ResolveById(item.beforeId);
        //"移动后id"已被登记过(典型:同一张卡在本条命令里先走抽卡/回手路径,已按新id建好实体):
        //必须复用那一份,绝不能再新建——否则同一张卡会有两个实体、两套UI(手牌重影/场上又冒出一份)。
        if (card == null && !string.IsNullOrEmpty(item.afterId)) card = ResolveById(item.afterId);
        if (card == null) card = ResolveOrCreate(item.staticId, item.beforeId, p);
        if (card == null)
        {
            UnityEngine.Debug.LogWarning($"[回放] 移动:前id={item.beforeId} 卡名id={item.staticId} 无法解析实体,跳过");
            return;
        }

        //2.2 换成移动后的新id(与真实对局一致:区域改变必然换id,必须改写同一个实体的id)
        string newId = !string.IsNullOrEmpty(item.afterId) ? item.afterId : item.beforeId;
        if (!string.IsNullOrEmpty(newId) && card.currentid != newId)
        {
            EntityCard other;
            if (cards.TryGetValue(newId, out other) && other != card)
                cards.Remove(newId);                            //新旧id撞车:以本次移动为准
            if (!string.IsNullOrEmpty(card.currentid)) cards.Remove(card.currentid);
            card.SetCurrentIdForReplay(newId);
            UnityEngine.Debug.Log($"[回放] 换id:{card.currentName} {item.beforeId} → {newId}");
        }
        if (!string.IsNullOrEmpty(newId)) cards[newId] = card;

        //2.3 手牌区的目标归属以"卡自己的持有者"为准(与 FightUI.IsOwnerEnemy/GotoHandUI 同一口径):
        //命令归属玩家 p 只是"谁发起了这条命令",对方卡因效果回到其主人手牌时用 p 会写错一方,
        //重绘时手牌就会错位(卡跑到对手手牌里、或该回的手牌不出现)。
        int handOwner = HandOwnerIndex(card, p);
        //卡已经在本命令的目标手牌数据里(典型:抽卡命令 ToHand 已按新id建好并登记):数据与UI都无需再动,
        //直接返回——避免"先删再插"把手牌顺序打乱(手牌顺序也是回放表现的一部分)。
        if (item.move == Record.MoveTo.Hand && hands[handOwner].Contains(card))
        {
            UnityEngine.Debug.Log($"[回放] 回手:{card.currentName} 已在手牌数据中,无需变动");
            return;
        }
        //2.3.1 卡若在手牌数据里,同步移除(手牌数据是重绘手牌的唯一依据)
        RemoveFromHands(card, item.beforeId);

        //2.4 按去向落到目标区域
        if (item.move == Record.MoveTo.Field)
        {
            await PlaceOnField(record, card, item);
            return;
        }
        if (item.move == Record.MoveTo.Hand)
        {
            //回手:只放回手牌数据,UI由 ApplyMoves 末尾的重绘统一重建
            //(不走 GotoHandUI:那条分支会先建一份手牌UI,紧接着整体重绘又把它刷掉,白多一次建卡与回收)
            if (!hands[handOwner].Contains(card)) hands[handOwner].Add(card);
            return;
        }
        if (item.move == Record.MoveTo.Cemetery || item.move == Record.MoveTo.Banished || item.move == Record.MoveTo.Deck)
        {
            List<EntityCard> one = new List<EntityCard> { card };
            List<Transform> noPrefab = new List<Transform> { null };    //传null预制体 → 走"新建展示"分支
            if (item.move == Record.MoveTo.Cemetery)
                await fightUI.GotoCeCemeteryUI(one, noPrefab);
            else if (item.move == Record.MoveTo.Banished)
                await fightUI.GotoBanishedUI(one, noPrefab);
            else
                await fightUI.GotoDeckUI(one, noPrefab);
            UnityEngine.Debug.Log($"[回放] {MoveName(item.move)}:{card.currentName} id={newId}");
            return;
        }
        //去向为空(卡组/额外卡组等隐藏区):卡已不在任何可见区域,删掉旧UI即可,没有新UI可建
        UnityEngine.Debug.Log($"[回放] 进入隐藏区(无UI表现):{card.currentName} id={item.beforeId} → {newId}");
    }

    //上场落位:有落地区号(特招/效果落位,结算时补全)就照搬,没有则由卡类型决定怪兽区/魔陷区
    private async UniTask PlaceOnField(Record.BaseRecord record, EntityCard card, MoveItem item)
    {
        List<int> zones = record.GetDataIntList(Record.Key.SpeSomZone);
        //魔陷落位记的是 MagicTrapZone(魔法区/陷阱区第几格);没有它回放只能取第一个空位,
        //于是魔法卡总是被放到一号位而不是玩家实际点的那一格。
        if (zones.Count == 0) zones = record.GetDataIntList(Record.Key.MagicTrapZone);
        int idx = MoveIndexOf(record, item);
        int slotCode = (idx >= 0 && idx < zones.Count) ? zones[idx] : Record.Slot.Unknown;
        if (slotCode == Record.Slot.Unknown)
            slotCode = fightUI.GetEmptySlotCode(card, record.PlayerIndex);       //通召/魔陷发动:按卡类型找空位

        Transform slot = fightUI.GetSlotTransform(slotCode);
        if (slot == null)
        {
            UnityEngine.Debug.LogWarning($"[问题1日志] 上场:{card.currentName} 槽位码={slotCode} 取不到区域格,跳过落位");
            return;
        }
        fightUI.IniSpeSom(card);                       //按实体建特招预制体(并置为当前待放置)
        //魔陷必须走"魔法发动"落位路径并保持正面:
        //SumOrCovBaseHandle 对魔陷是一律换卡背(盖放),真实发动则是由 MagicActBase 开启等待源后
        //走 LayOutMagicTrapCardHandle(waitMagicActBase 非空 → 保持正面)。回放没有点击等待,
        //必须手工置入等待源并指定 magicOrTrap,否则魔法卡发动就被显示成盖放。
        //魔陷有两条完全不同的落位分支,必须按记录里的盖放标记分开:
        //① 表侧发动 → LayOutMagicTrapCardHandle(保持正面);
        //② 里侧盖放 → SumOrCovBaseHandle(换卡背)。混用会让盖放显示成正面。
        bool isMagicTrap = card is EntityMagicCard || card is EntityTrapCard;
        bool isCover = record.GetDataInt(Record.Key.IsCover, 0) == 1;
        if (isMagicTrap && !isCover)
        {
            fightUI.GetMagicTrap(fightUI.GetSpeSumOrCover());
            fightUI.BeginWaitMagicActForReplay();
            fightUI.LayOutMagicTrapCardHandle(slot);   //表侧发动落位
            fightUI.EndWaitMagicActForReplay();
        }
        else
            fightUI.SumOrCovBaseHandle(slot);          //怪兽 / 里侧盖放:直接落位到记录下来的那个格子
        fightUI.ClearRegionUIHandle();                 //收尾:关蓝区/释放待放置引用
        UnityEngine.Debug.Log($"[问题1日志] 上场:{card.currentName} id={card.currentid} 槽位码={slotCode} 区域={slot.parent?.name}" +
            $" 路径={(isMagicTrap ? (isCover ? "魔陷·盖放" : "魔陷·表侧") : "怪兽")}");
        await UniTask.Yield();
    }

    //该移动明细在命令里的序号(用于把 SpeSomZone 这类"同序的多卡数据"对上号;老记录按单卡处理)
    private static int MoveIndexOf(Record.BaseRecord record, MoveItem item)
    {
        List<string> triples = record.GetDataList(Record.Key.CurrentIdsMoves);
        if (triples.Count == 0) return 0;
        //区号只登记了"上场"的那几张,而一条命令里常常混着送墓/除外(如融合:素材送墓+融合怪兽上场)。
        //按全部明细的下标取会越界(SpeSomZone 只有1个值、上场的却是第3条),于是回退到"第一个空位"
        //——表现为融合怪兽总是落在1号位。故这里只数"当前这条之前出现过几条上场明细"。
        int idx = 0;
        for (int i = 0; i < triples.Count; i++)
        {
            string[] seg = triples[i].Split('>');
            if (seg.Length > 0 && seg[0] == item.beforeId)
                return item.move == Record.MoveTo.Field ? idx : -1;
            if (seg.Length > 2 && seg[2] == Record.MoveTo.Field) idx++;
        }
        return -1;
    }

    #endregion

    //手牌归属:回手只回"卡自己的持有者"的手牌区,与 FightUI.IsOwnerEnemy(实体持有者 playerIndex==1)同一口径
    //(FightUI.GotoHandUI 也是按它决定进 PlaHandReg 还是 EneHandReg,两边必须一致,否则UI与手牌数据分家)。
    //取不到持有者时退回命令归属玩家,保证任何情况下都有确定归属。
    private int HandOwnerIndex(EntityCard card, int fallback)
    {
        if (fightUI == null || card == null) return fallback == 1 ? 1 : 0;
        return fightUI.IsOwnerEnemyPublic(card) ? 1 : 0;
    }

    //把已移动到别处的卡从双方手牌数据里移除。
    //必须同时按"实体引用"和"移动前id"匹配:卡可能在之前的命令里换过id(命令里记的是移动前的id),
    //只按id匹配会漏,而漏掉的卡会在下一次重绘手牌时被画回来——表现为"卡已上场/送墓,手牌里还留着一份"。
    private void RemoveFromHands(EntityCard card, string beforeId)
    {
        if (hands == null) return;
        int removed = 0;
        for (int pl = 0; pl < hands.Length && pl < 2; pl++)
        {
            if (hands[pl] == null) continue;
            for (int k = hands[pl].Count - 1; k >= 0; k--)
            {
                EntityCard inHand = hands[pl][k];
                if (inHand == null || inHand == card ||
                    (!string.IsNullOrEmpty(beforeId) && inHand.currentid == beforeId))
                {
                    hands[pl].RemoveAt(k);
                    removed++;
                }
            }
        }
        //未命中多为正常情况(该卡此前不在手牌:如墓地→除外、场上→墓地),故只记日志不告警
        if (removed == 0)
            UnityEngine.Debug.Log($"[回放] 手牌数据里没有该卡(无需移除):id={beforeId}" +
                $" 我方=[{JoinCardIds(hands[0])}] 对方=[{JoinCardIds(hands[1])}]");
    }

    //盖放的魔陷发动:卡不移动,只是从里侧翻成表侧。回放没有这一步的话,
    //那条命令既不落位也不移动,场上那份盖放UI就会在后续命令里"直接消失"。
    private void FlipCoverToFaceUp(Record.BaseRecord record)
    {
        if (fightUI == null || record.GetDataInt(Record.Key.IsFaceUp, 0) != 1) return;
        string id = record.GetData(Record.Key.CurrentId, "");
        if (string.IsNullOrEmpty(id)) return;
        Transform t = fightUI.FindCardPrefabById(id);
        if (t == null) return;
        PreEntityCard pec = t.GetComponent<PreEntityCard>();
        EntityCard card = pec != null ? pec.GetEntityCard() : ResolveById(id);
        if (pec == null || card == null) return;
        pec.SetCardImage(card, card.card != null ? card.card.id : "");       //换回正面卡图(不再是卡背)
        if (card is EntityMagicCard magicCard) magicCard.isCover = 0;
        else if (card is EntityTrapCard trapCard) trapCard.isCover = 0;
        UnityEngine.Debug.Log($"[问题1日志] 盖放翻面:{card.currentName} id={id}");
    }

    //按回放维护的手牌数据重绘双方手牌。
    //手牌UI一律以 hands 为唯一数据源,不维护任何"卡→预制体"映射:
    //映射会在换id/复用预制体时逐步失真,之后按它定位就会删错对象或找不到(UI不动的根源)。
    //只重绘某一方会漏:送墓/特招都可能涉及另一边的卡,漏掉的那方旧手牌UI会残留在手牌区。
    private void RedrawAllHands()
    {
        if (fightUI == null || hands == null) return;
        UnityEngine.Debug.Log($"[回放] 重绘手牌:我方=[{JoinCardIds(hands[0])}] 对方=[{JoinCardIds(hands[1])}]");
        for (int pl = 0; pl < hands.Length && pl < 2; pl++)
        {
            if (hands[pl] == null) continue;
            fightUI.DrawCardHandle(pl, new List<EntityCard>(hands[pl]));
        }
    }

    //打印手牌数据里的"卡名@id",用于核对每条命令重绘时手牌数据是否已同步
    private static string JoinCardIds(List<EntityCard> list)
    {
        if (list == null) return "";
        List<string> parts = new List<string>();
        for (int i = 0; i < list.Count; i++)
            parts.Add(list[i] == null ? "?" : $"{list[i].currentName}@{list[i].currentid}");
        return string.Join(",", parts.ToArray());
    }

    private UniTask OnNextPhase(Record.BaseRecord record)
    {
        int owner = record.GetDataInt(Record.Key.Owner, 0);
        GameManage.GamePhase phase;
        if (!Enum.TryParse(record.GetData(Record.Key.Phase, ""), out phase))
            phase = GameManage.GamePhase.DrawCardPhase;
        fightUI?.ShowPhase(phase, owner);
        fightUI?.RebackText($"回放:进入 {FightUIPhaseName(phase)}({(owner == 0 ? "我方" : "对方")}回合)", 2);
        return UniTask.CompletedTask;
    }

    //支付造成的卡移动(如"白之圣女支付代价:将自身送去墓地")统一由 OnRecord 末尾的 ApplyMoves 回放,
    //本方法只做文字表现。这里绝不能再调一次:同一条命令会执行两遍非幂等的 ApplyMoves,
    //而第二遍进入时"移动前id"已被第一遍换掉,于是按卡名id新建出第二份实体并把字典键改指副本,
    //表现为同一张卡两份UI、手牌重复,之后所有按id的定位全部落空(回放中"UI不刷新"的主要原因之一)。
    private UniTask OnEffectPay(Record.BaseRecord record)
    {
        fightUI?.RebackText($"回放:支付 [{record.code}] {CardName(record.CardId)}", 2);
        return UniTask.CompletedTask;
    }

    private static string MoveName(string move)
    {
        switch (move)
        {
            case Record.MoveTo.Cemetery: return "送去墓地";
            case Record.MoveTo.Banished: return "除外";
            case Record.MoveTo.Hand: return "返回手卡";
            case Record.MoveTo.Deck: return "返回卡组";
            default: return "移动";
        }
    }
    private static string JoinNames(List<EntityCard> cards)
    {
        List<string> names = new List<string>();
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null) names.Add(cards[i].currentName);
        return string.Join("、", names.ToArray());
    }
    private static string JoinStrs(List<string> list) { return list == null ? "" : string.Join(",", list.ToArray()); }
    private static string JoinInts(List<int> list)
    {
        if (list == null) return "";
        List<string> s = new List<string>();
        for (int i = 0; i < list.Count; i++) s.Add(list[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(",", s.ToArray());
    }

    //效果结算:一切区域改变(上场/送墓/破坏送墓/除外/回手/回卡组)统一交给 ApplyMoves 逐卡回放。
    //旧实现先判"本命令是否上场":是就整条跳过卡移动——一条命令里多张卡去往不同区域时,
    //送墓/除外的那些卡会被直接丢掉(UI完全不动);而命令级 Move 只记了一个去向,靠它判断本来就不可靠。
    private async UniTask OnEffectAct(Record.BaseRecord record)
    {
        fightUI?.RebackText($"回放:结算 [{record.code}] {CardName(record.CardId)} 第{record.GetDataInt(Record.Key.EffIndex, 0) + 1}个效果", 2);
        //结算文字先出这一帧;区域改变(上场/送墓/除外/回手/回卡组)统一由 OnRecord 末尾的 ApplyMoves 回放。
        //这里同样不能再调 ApplyMoves:否则同一条命令执行两遍,第二遍会新建出第二份实体(详见 OnEffectPay 处的说明)。
        await UniTask.Yield();
    }

    //攻击宣言:把伤害同步到回放侧的血量镜像并推给场景HP显示。
    //旧实现只弹一条伤害文字、血量数字不变——"命令执行了但UI没变化"在血量上表现最直观。
    private UniTask OnAttack(Record.BaseRecord record)
    {
        int dmg = record.GetDataInt(Record.Key.Damage, 0);
        //受伤方:优先取记录里的受伤方(攻击/效果伤害都会登记),缺失时按"命令归属玩家的对手"处理
        int damaged = record.GetDataInt(Record.Key.DamageTo, -1);
        if (damaged != 0 && damaged != 1) damaged = record.PlayerIndex == 0 ? 1 : 0;
        fightUI?.RebackText($"回放:攻击宣言,造成 {dmg} 点伤害", 2);
        if (dmg > 0) ChangeHP(damaged, -dmg);
        return UniTask.CompletedTask;
    }

    //回放侧血量镜像:初值取场景 FightUI 当前显示的血量(与真实一局完全一致),之后按回放命令里的伤害扣减,
    //再推回场景HP显示。回放不会走对局逻辑的血量结算,不自己维护这份镜像的话血条就永远停在初始值。
    private void ChangeHP(int playerIndex, int delta)
    {
        if (fightUI == null || hp == null) return;
        int idx = playerIndex == 1 ? 1 : 0;
        if (hp[idx] < 0) hp[idx] = fightUI.GetShownHP(idx);      //首次扣血:以界面当前显示的血量为初值
        hp[idx] = Mathf.Max(0, hp[idx] + delta);
        fightUI.OnHPChangeToHandle(idx, hp[idx]);                //直接刷HP数字与血条(FightUI内唯一的HP显示出口)
        UnityEngine.Debug.Log($"[回放] 血量更新:玩家{idx} 变化={delta:+#;-#;0} → {hp[idx]}");
    }

    private UniTask OnEnd(Record.BaseRecord record)
    {
        bool isWin = record.GetDataBool(Record.Key.IsWin, false);
        if (isWin) fightUI?.VecUI();
        else fightUI?.DefUI();
        return UniTask.CompletedTask;
    }

    #endregion

    #region 实体重建(回放期只按命令里的卡名id重建实体,不做任何逻辑判断)

    private EntityCard Resolve(string staticId, string currentId, int playerIndex)
    {
        if (string.IsNullOrEmpty(staticId)) return null;
        if (!string.IsNullOrEmpty(currentId) && cards.ContainsKey(currentId))
            return cards[currentId];
        CardBase.Card card = CardBase.IdFindCard(staticId);
        if (card == null) return null;
        int idx = playerIndex == 1 ? 1 : 0;
        EntityCard entity = Factory.EntityCard(card, players[idx]);
        if (entity == null) return null;
        if (!string.IsNullOrEmpty(currentId))
        {
            //必须把实体的id设为命令记录的id:Factory分配的是随机id,
            //若不改写,后续任何"按id定位/销毁"都会落空(表现为同一张卡在场上和墓地各有一份、且两份实体id不同)。
            entity.SetCurrentIdForReplay(currentId);
            cards[currentId] = entity;
        }
        return entity;
    }

    private static string CardName(string staticId)
    {
        if (string.IsNullOrEmpty(staticId)) return "";
        CardBase.Card card = CardBase.IdFindCard(staticId);
        return card == null ? staticId : card.name;
    }

    private static string FightUIPhaseName(GameManage.GamePhase phase)
    {
        switch (phase)
        {
            case GameManage.GamePhase.DrawCardPhase: return "抽卡阶段";
            case GameManage.GamePhase.PreparePhase: return "准备阶段";
            case GameManage.GamePhase.Main1Phase: return "主要阶段1";
            case GameManage.GamePhase.FightPhase: return "战斗阶段";
            case GameManage.GamePhase.Main2Phase: return "主要阶段2";
            case GameManage.GamePhase.EndPhase: return "结束阶段";
            default: return phase.ToString();
        }
    }

    #endregion
}

/// <summary>
/// 空回放(自检用):只把命令按序打印出来,不驱动任何UI。
/// </summary>
public class RecordPlayBackLog : IRecordPlayBack
{
    public UniTask OnRecord(Record.BaseRecord record)
    {
        Debug.Log($"[回放] {record}");
        return UniTask.CompletedTask;
    }
}
