using Cysharp.Threading.Tasks;
using System;
using System.Threading;

//对局级异步作用域:对局结束(胜负结算/返回主菜单/场景卸载)时统一取消该局全部在途异步。
//原理:把同一 CancellationToken 贯穿所有"会长期挂起/会自动推进"的异步停车点(UniTask.Delay、
//FightUI 的 tcs 等待、StateMachine 结算临界区门闩等);对局结束调用 End() 后,仍停在这些点的
//异步原地抛出 OperationCanceledException 并沿调用链终止,不再继续改场/改UI;之后新启动的异步
//一进入取消点也会立即自终止,杜绝"对局已结束但后台还在推进"。
public static class FightAsyncScope
{
    private static CancellationTokenSource cts;

    //对局开始(或重新开局)时调用,幂等;自动回收上一次遗留的 token 源
    public static void Begin()
    {
        CancellationTokenSource old = cts;
        cts = new CancellationTokenSource();
        try
        {
            if (old != null)
            {
                old.Cancel();
                old.Dispose();
            }
        }
        catch (ObjectDisposedException)
        {
            //理论上不应发生,吞掉保证 Begin 稳定
        }
    }

    //对局结束:终止本局全部已启动异步(可重复调用)
    public static void End()
    {
        //不置空/不Dispose:保留"已取消"令牌,使结算后新触发的异步也在首个取消点立即自终止;
        //下一局 Begin() 会替换并回收
        CancellationTokenSource cur = cts;
        if (cur != null && !cur.IsCancellationRequested)
        {
            cur.Cancel();
        }
    }

    //当前对局的取消令牌(未 Begin 时为 None)
    public static CancellationToken Token => cts?.Token ?? CancellationToken.None;

    //是否已结束(未 Begin 一律视为已结束,停止放行驱动逻辑)
    public static bool IsCanceled => cts == null || cts.IsCancellationRequested;
}
