using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EffLogic;

public class EnemyAI
{
    private readonly FightLogic fightLogic;
    private readonly UILinkLogic requestHandler;

    public bool IsEnable { get; set; } = true;                      // 是否启用AI（可在FightLogic中关闭用于测试）

    public EnemyAI(FightLogic fightLogic, UILinkLogic requestHandler)
    {
        this.fightLogic = fightLogic;
        this.requestHandler = requestHandler;
    }

    //AI回合：每进入一个新阶段由 FightLogic.BoutChange 调用，执行本阶段操作并自动推进下一阶段
    public async UniTask OnBoutChanged()
    {
        if (!IsEnable) return;

        GameManage.GamePhase phase = fightLogic.GetCurGamePhase();
        switch (phase)
        {
            case GameManage.GamePhase.Main1Phase:                   // 主阶段1/2：优先通召
            case GameManage.GamePhase.Main2Phase:
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f), cancellationToken: FightAsyncScope.Token);   //对局结束:延时被取消,后续动作不再执行
                if (FightAsyncScope.IsCanceled) return;
                TryNormalSummon();
                break;
            case GameManage.GamePhase.FightPhase:                   // 战斗阶段：做一次攻击
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f), cancellationToken: FightAsyncScope.Token);
                if (FightAsyncScope.IsCanceled) return;
                await TryAttack();
                break;
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(1.2f), cancellationToken: FightAsyncScope.Token);     // 操作完成停顿，便于观察
        await fightLogic.BoutChange();                              // 自动切换到下一阶段（递归推进，直至回到玩家回合）
    }

    //通召
    private void TryNormalSummon()
    {
        EntityPlayer enemy = fightLogic.enemy;
        if (!enemy.CanSummon()) return;                             // 本回合已通召过

        // 选手牌中攻击力最大的可通召怪兽
        EntityMonsterCard best = null;
        foreach (var c in enemy.hand.cards)
        {
            EntityMonsterCard mon = c as EntityMonsterCard;
            if (mon == null) continue;
            mon.IniOrdSummon(enemy.field);                         
            if (!mon.ordSummon.IsCanPay()) continue;
            if (best == null || mon.currentAtt > best.currentAtt)
                best = mon;
        }
        if (best == null) return;
        int sacCount = best.currentGrade >= 7 ? 2 : (best.currentGrade >= 5 ? 1 : 0);
        List<EntityMonsterCard> sacrifices = enemy.field.GetMonZon()
            .Select(c => c as EntityMonsterCard)
            .Where(m => m != null)
            .OrderBy(m => m.currentAtt)
            .Take(sacCount)
            .ToList();
        foreach (EntityMonsterCard sac in sacrifices)
        {
            requestHandler.RegionRemoveCardRequest(5, sac, true);   // 数据：场上移除
            requestHandler.RegionAddCardRequest(3, sac, true);      // 数据：加入墓地
            sac.ChangeLocation(GameManage.CardLocation.Cemetery);
            Transform t = fightLogic.FindRealCardTransform(sac);
            if (t != null) requestHandler.GotoCeCemeteryUIRequest(t);   // UI：移动至墓地
        }

        requestHandler.RegionRemoveCardRequest(2, best, true);      // 数据：手牌移除
        fightLogic.enemy.field.AddMonster(best);                    // 数据：加入场上怪兽区
        best.ChangeLocation(GameManage.CardLocation.Field);
        enemy.AddCurSomNum();                                       // 记录通召次数

        Transform bt = fightLogic.FindRealCardTransform(best);
        if (bt != null) requestHandler.PlaceCardToEnemyFieldRequest(bt);   // UI：放置到敌方怪兽区
        requestHandler.RebackTextRequest($"AI 通常召唤了 {best.currentName}！", 3);
    }

    //战斗阶段：用场上第一只可攻击的怪兽做一次攻击（无论是否有正收益）
    private async UniTask TryAttack()
    {
        foreach (var c in fightLogic.enemy.field.GetMonZon())
        {
            EntityMonsterCard mon = c as EntityMonsterCard;
            if (mon != null && fightLogic.IsMonsterAttackable(mon))    
            {
                await requestHandler.AttackRequest(mon);            // 攻击宣言 -> 目标选择 -> 伤害判定
                return;                                             // 只攻击一次
            }
        }
    }
}
