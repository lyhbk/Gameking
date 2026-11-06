using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// record场景(回放场景)入口:由fighting场景复制而来,具备全部UI功能但不具备对局逻辑,整局按命令回放。
///
/// 挂载方式(二选一):
///   1) 在record场景任意物体上挂本组件,把场景里的 FightUI 拖到 fightUI 上;
///   2) 不挂也行:FightUI 在回放模式下会自动 AddComponent 本组件并调用 PlayNow() 即刻回放。
///
/// 进入:由 mainscene 的"进入回放"按钮(ButtonLogic)切场景;对局结束时 FightLogic.RecordEnd 已把
///      合成后的时间线写入 RecordTransfer,本组件取出后按序回放。
/// 退出:由 FightUI 的"退出回放"按钮(OutRecord)切回 mainscene,同时 Stop() 中断回放。
/// </summary>
public class RecordSceneRoot : MonoBehaviour
{
    [SerializeField] private FightUI fightUI;
    //每条回放命令之间的停顿(秒):默认取常量 RecordPlaySetting.DefaultInterval=2秒(定义在 RecordPlayer.cs 开头)
    [SerializeField] private float interval = RecordPlaySetting.DefaultInterval;
    [SerializeField] private bool autoPlay = true;                   //进入场景即自动回放

    public RecordPlayer player { get; private set; }
    public RecordPlayBackUI playBackUI { get; private set; }
    public bool IsPlaying { get; private set; }

    //实际生效的间隔:夹到常量允许范围内,防止 Inspector 误填(负数/过大)导致回放卡死或连续刷屏
    public float PlayInterval
    {
        get { return Mathf.Clamp(interval, RecordPlaySetting.MinInterval, RecordPlaySetting.MaxInterval); }
        set { interval = Mathf.Clamp(value, RecordPlaySetting.MinInterval, RecordPlaySetting.MaxInterval); }
    }

    //编辑器里改动时夹住;并提示"当前实际间隔",便于核对是否就是常量默认值(2秒)
    private void OnValidate()
    {
        interval = Mathf.Clamp(interval, RecordPlaySetting.MinInterval, RecordPlaySetting.MaxInterval);
    }

    //Inspector 里若还留着旧序列化值(如改动前的0.8),用它一键恢复成常量默认值
    [ContextMenu("重置为默认间隔")]
    private void ResetInterval()
    {
        interval = RecordPlaySetting.DefaultInterval;
        UnityEngine.Debug.Log($"[RecordSceneRoot] 回放间隔已重置为默认 {RecordPlaySetting.DefaultInterval} 秒");
    }

    private CancellationTokenSource cts;
    private bool started;                                            //防止与 Start() 重复启动

    private void Start()
    {
        if (started) return;                                         //已由 FightUI 触发过:不重复启动
        if (!autoPlay) return;
        BeginAsync().Forget();
    }

    private void OnDestroy()
    {
        Stop();                                                      //场景卸载兜底:中断回放,避免在途 UniTask 指向已销毁对象
    }

    /// <summary>由 FightUI 在回放模式下注入自身(避免手写场景文件)</summary>
    public void BindFightUI(FightUI ui)
    {
        if (ui != null) fightUI = ui;
    }

    /// <summary>即刻开始回放(进入回放场景时调用)</summary>
    public void PlayNow()
    {
        started = true;
        BeginAsync().Forget();
    }

    private async UniTask BeginAsync()
    {
        Debug.Log("[回放] RecordSceneRoot 开始初始化");
        await UniTask.Yield();                       //等一帧:让 FightUI.Start(初始UI)先跑完,避免回放与之抢同一帧
        if (fightUI == null)
            fightUI = FindObjectOfType<FightUI>();
        Debug.Log($"[回放] FightUI={(fightUI == null ? "null(场景里没有FightUI!)" : "已获取")}");
        playBackUI = new RecordPlayBackUI(fightUI);

        Record record = RecordTransfer.Take();
        Debug.Log($"[回放] 取到记录:{(record == null ? "null(RecordTransfer为空)" : record.Count + " 条命令")}");
        if (record == null)
        {
            Debug.LogWarning("[RecordSceneRoot] 没有可回放的命令记录(RecordTransfer 为空)");
            fightUI?.RebackText("暂无可回放的对局记录", 3);
            return;
        }

        Stop();
        cts = new CancellationTokenSource();
        IsPlaying = true;
        player = new RecordPlayer(record, playBackUI);
        playBackUI.SetPlayInfo(record.Count, PlayInterval);
        Debug.Log($"[RecordSceneRoot] 开始回放,共 {record.Count} 条命令,间隔 {PlayInterval} 秒");
        Debug.Log($"[RecordSceneRoot] 回放命令清单:\n{record.DumpAll()}");     //开始前先把全部命令及参数打印出来,便于核对记录内容
        fightUI?.RebackText($"开始回放:共 {record.Count} 条命令,间隔 {PlayInterval} 秒", 3);
        await player.PlayAll(PlayInterval, cts.Token);
        IsPlaying = false;
        fightUI?.RebackText("回放结束", 3);
    }

    /// <summary>停止回放(退出回放场景时调用)</summary>
    public void Stop()
    {
        IsPlaying = false;
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
        cts = null;
    }

    /// <summary>回放下一条命令(手动单步)</summary>
    public void PlayNext() { if (player != null) player.Step().Forget(); }

    /// <summary>从头连续回放(按 PlayInterval 间隔)</summary>
    public void PlayAll() { if (player != null) player.PlayAll(PlayInterval, cts == null ? default : cts.Token).Forget(); }

    /// <summary>复位并重新回放</summary>
    public void Restart()
    {
        if (player == null || playBackUI == null) return;
        player.Reset();
        playBackUI.Reset();
        PlayAll();
    }
}
