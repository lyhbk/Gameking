
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CardBase;


public class Player : MonoBehaviour
{
    public PreCardGroupList preCardGroupList { get; private set; }          //预设卡组列表

    public StaticPlayer staticPlayer;                                              //玩家实体
    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        preCardGroupList = new PreCardGroupList();
        PreCardGroup preCardGroup = new PreCardGroup("默认卡组");
        preCardGroupList.AddPreCardGroup(preCardGroup);
        staticPlayer = new StaticPlayer("player1", "玩家1");
    }



    public class PreCardGroup                                   //卡组
    {
        public string groupName { get; private set; }          //组名
        public List<Card> cards { get; private set; }          //预设卡组

        public PreCardGroup(string groupName)
        {
            this.groupName = groupName;
            this.cards = new List<Card>();
        }

        public PreCardGroup Clone()
        {
            var preCardGroup = new PreCardGroup(this.groupName);
            foreach (var card in this.cards)
            {
                preCardGroup.cards.Add(card.Clone());
            }
            return preCardGroup;
        }

        public bool AddCard(Card card)                        //添加卡片到预设卡组
        {
            if (GetCardsCountByName(card.name) < card.limition)
            {
                cards.Add(card);
                return true;
            }
            return false;
        }

        public bool AddCard(string id)
        {
            CardBase.Card card = IdFindCard(id);
            return AddCard(card);
        }

        public void RemoveCard(Card card)                     //从预设卡组移除卡片
        {
            cards.Remove(card);
        }

        public int GetCardCount()                                 //获取预设卡组中卡片数量
        {
            return cards.Count;
        }
        public void RemoveCard(string id)
        {
            Card card = cards.FirstOrDefault(c => c.id == id);
            RemoveCard(card);
        }

        public int GetCardCount(string id)                     //获取预设卡组中指定id的卡片数量
        {
            return cards.Count(card => card.id == id);
        }
        public int GetCardsCountByName(string name)             //获取预设卡组中指定名字的卡片数量
        {
            return cards.Count(card => card.name == name);
        }

        public void Clear()                                   //清空预设卡组
        {
            cards.Clear();
        }

        public void ChangeName(string groupName)            //修改预设卡组名字
        {
            this.groupName = groupName;
        }

        public List<Card> GetExtraCards()                     //获取预设卡组中额外卡片列表
        {
            return cards.Where(card => {
                if (card.card_type != 0)
                    return false;
                Card monsterCard = IdFindCard(card.id);
                MonsterCard monster = monsterCard as MonsterCard;
                return monster.type != 0 && monster.type != 1;
            }).ToList();
        }

        public List<Card> GetMainCards(List<Card> extraCards)
        {
            return cards.Where(card => !extraCards.Any(
                extra => extra.id == card.id)
                ).ToList();
        }
    }

    public class PreCardGroupList                               //卡组列表
    {
        public int i { get; private set; }                      //当前预设卡组索引

        public List<PreCardGroup> preCardGroups { get; private set; }          //预设卡组列表

        public PreCardGroupList()
        {
            i = 0;
            this.preCardGroups = new List<PreCardGroup>();
        }
        public void AddPreCardGroup(PreCardGroup preCardGroup)                        //添加预设卡组到列表
        {
            preCardGroups.Add(preCardGroup);
        }
        public void RemovePreCardGroup(PreCardGroup preCardGroup)                     //从列表移除预设卡组
        {
            preCardGroups.Remove(preCardGroup);
        }
        public PreCardGroup GetPreCardGroup(string groupName)                     //根据组名获取预设卡组
        {
            return preCardGroups.FirstOrDefault(group => group.groupName == groupName);
        }
        public void PlusIndex()                                   //切换到下一个预设卡组
        {
            if (preCardGroups.Count == 0)
                return;
            i = (i + 1) % preCardGroups.Count;
        }
        public void MinusIndex()                                  //切换到上一个预设卡组
        {
            if (preCardGroups.Count == 0)
                return;
            i = (i - 1 + preCardGroups.Count) % preCardGroups.Count;
        }

        public void Changei(int i)
        {
            this.i = i;
        }
    }

    public class StaticPlayer
    {
        private string playerId;        //玩家id
        private string playerName;       //玩家名字    

        public StaticPlayer(string playerId, string playerName)
        {
            this.playerId = playerId;
            this.playerName = playerName;
        }

        public string GetPlayerId()
        {
            return playerId;
        }

        public string GetPlayerName()
        {
            return playerName;
        }
    }

}


