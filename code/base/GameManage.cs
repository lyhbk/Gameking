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





    //场景切换
    public static void ChangeScene(string name)
    {
        SceneManager.LoadScene(name);
    }

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
