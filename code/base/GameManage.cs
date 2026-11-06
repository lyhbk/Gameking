using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static EffLogic;
using static EntityPlayer;

public class GameManage : MonoBehaviour
{
    public static GameManage instance;
    public EventMintor eventMintor;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        eventMintor = new EventMintor();
        DontDestroyOnLoad(this.gameObject);
    }

    public enum GamePhase
    {
        DrawCardPhase,
        PreparePhase,
        Main1Phase,
        FightPhase,
        Main2Phase,
        EndPhase
    }


    //区域查询编码说明：bool编码共12位，前6位(0-5)为自己区域，后6位(6-11)为对方区域，
    //且两侧区域顺序均遵循组件索引：Deck, ExtraDeck, Hand, Cemetery, Banished, Field
    public enum CardLocation
    {
        Deck,                         //卡组
        ExtraDeck,                     //额外牌组
        Hand,                         //手牌
        Field,                        //场上
        Cemetery,                    //墓地
        Banished,                    //除外
        EnemyDeck, EnemyExtraDeck, EnemyHand, EnemyField, EnemyCemetery, EnemyBanished   //6-11 对方区域(仅用于区域查询编码,卡实体location不使用)
    }

    //CardLocation是否表示对方区域（>=6）
    public static bool IsEnemyLocation(CardLocation location)
    {
        return location >= CardLocation.EnemyDeck;
    }

    //对方区域位置转为本地(己方)同名位置
    public static CardLocation ToLocalLocation(CardLocation location)
    {
        return IsEnemyLocation(location) ? (CardLocation)((int)location - 6) : location;
    }

    //本地(己方)位置转为对方区域位置
    public static CardLocation ToEnemyLocation(CardLocation location)
    {
        return IsEnemyLocation(location) ? location : (CardLocation)((int)location + 6);
    }





    //===== 场景 =====
    public const string MainSceneName = "mainscene";     //主菜单场景(mainscene.unity:持"开始对局/进入回放"按钮)
    public const string FightSceneName = "fighting";     //对局场景(fighting.unity)
    public const string RecordSceneName = "record";      //回放场景(record.unity):由fighting复制,具备全部UI但不具备对局逻辑,整局由命令回放驱动

    public static string CurSceneName { get { return SceneManager.GetActiveScene().name; } }

    //当前是否处于回放场景:回放场景里 FightLogic/UILinkLogicComRoot 不初始化(无对局逻辑),FightUI 切换为回放模式
    public static bool IsRecordScene() { return CurSceneName == RecordSceneName; }

    //场景切换
    public static void ChangeScene(string name)
    {
        SceneManager.LoadScene(name);
    }

    public static void EnterRecordScene() { ChangeScene(RecordSceneName); }     //进入回放场景(进入即开始回放)
    public static void ExitRecordScene() { ChangeScene(MainSceneName); }        //退出回放场景:回主菜单

    //卡图加载
    public static void SetImage(Image targetImage, string path)
    {
        path = "card_img/" + path;
        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite != null)
        {
            targetImage.sprite = sprite;
        }
    }

    public static void SetImageUI(Image targetImage, string path)
    {
        path = "UI/" + path;
        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite != null)
        {
            targetImage.sprite = sprite;
        }
    }


}
