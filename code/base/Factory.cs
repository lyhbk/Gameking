using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EntityPlayer;

public static class Factory 
{
    public static EffLogic.EntityCard EntityCard(CardBase.Card card, EntityPlayer owner)
    {
        switch (card.card_type)
        {
            case 0:
                EffLogic.EntityMonsterCard entityMonsterCard = new EffLogic.EntityMonsterCard((CardBase.MonsterCard)card, owner);
                entityMonsterCard.GenerateId();
                return entityMonsterCard;
            case 1:
                EffLogic.EntityMagicCard entityMagicCard = new EffLogic.EntityMagicCard((CardBase.MagicCard)card, owner);
                entityMagicCard.GenerateId();
                return entityMagicCard;
            case 2:
                EffLogic.EntityTrapCard entityTrapCard = new EffLogic.EntityTrapCard((CardBase.TrapCard)card, owner);
                entityTrapCard.GenerateId();
                return entityTrapCard;
            default:
                return null;
        }
    }


    public static EntityPlayer EntityPlayer(string playerId, string playerName)
    {
        return new EntityPlayer(playerId, playerName);
    }


    public static List<GameManage.CardLocation> StringToCardLocation(string[] cardLocString)                  // string转GameManage.CardLocation
    {
        if(cardLocString == null || cardLocString.Length == 0)
        {
           return null;
        }
        try
        {
            List<GameManage.CardLocation> cardLocations = new List<GameManage.CardLocation>();
            for (int i = 0; i < cardLocString.Length; i++)
            {
                string locStr = cardLocString[i];
                bool isEnemy = !string.IsNullOrEmpty(locStr) && (locStr.StartsWith("e_") || locStr.StartsWith("E_"));
                if (isEnemy) locStr = locStr.Substring(2);
                GameManage.CardLocation cardLocation = (GameManage.CardLocation)Enum.Parse(typeof(GameManage.CardLocation), locStr, ignoreCase: true);
                if (isEnemy) cardLocation = GameManage.ToEnemyLocation(cardLocation);
                cardLocations.Add(cardLocation);
            }
            return cardLocations;
        }
        catch(Exception ex) when(ex is ArgumentException || ex is OverflowException)
        {
            throw new ArgumentException($"无法将字符串 '{cardLocString[0]}' 转换为 CardLocation 枚举。" +
                $"请检查拼写是否正确。支持的类型有: [{string.Join(", ", Enum.GetNames(typeof(GameManage.CardLocation)))}],对方区域使用 e_ 前缀,例如 e_Hand。");
        }
    }

    public static List<GameManage.GamePhase> StringToGamePhase(string[] cardPhaString)
    {
        if (cardPhaString == null || cardPhaString.Length == 0)
        {
            return null;
        }
        try
        {
            List<GameManage.GamePhase> gamePhase = new List<GameManage.GamePhase>();
            for (int i = 0; i < cardPhaString.Length; i++)
            {
                GameManage.GamePhase cardLocation = (GameManage.GamePhase)Enum.Parse(typeof(GameManage.GamePhase), cardPhaString[i], ignoreCase: true);
                gamePhase.Add(cardLocation);
            }
            return gamePhase;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is OverflowException)
        {
            throw new ArgumentException($"无法将字符串 '{cardPhaString}' 转换为 EffType 枚举。" +
                $"请检查拼写是否正确。支持的类型有: [{string.Join(", ", Enum.GetNames(typeof(GameManage.CardLocation)))}]。");
        }
    }

    public static EffLogic.IniCostComponent StringToCost(string costType, string costParameter)                    //生成cost初始化类
    {
        string[] costParRes = costParameter.Split('&');
        switch (costType)
        {
            case "JudExist":
                //  costParRes[0]=二维选卡条件
                //  costParRes[1]=查找地点(Deck|ExtraDeck|Hand|Field|Cemetery|Banished,|分隔;对方区域加e_前缀如e_Hand|e_Field;留空=全部区域)
                //  costParRes[2]=排除卡名(该卡名不算命中;留空=不排除)
                //  costParRes[3]=命中链hitList
                List<GameManage.CardLocation> judExistLocations = StringToCardLocation(string.IsNullOrEmpty(costParRes[1]) ? null : costParRes[1].Split('|'));
                List<List<string>> judExistCardCondition = new List<List<string>>();
                if (!string.IsNullOrEmpty(costParRes[0]))
                {
                    foreach (var groupStr in costParRes[0].Split('|'))     //大组间|分隔
                    {
                        if (groupStr == null) continue;
                        List<string> group = new List<string>();
                        if (!string.IsNullOrWhiteSpace(groupStr))
                            foreach (var item in groupStr.Split('*'))      //大组内子项*分隔
                                if (!string.IsNullOrWhiteSpace(item)) group.Add(item.Trim());
                        judExistCardCondition.Add(group);                  //空大组也保留占位(维度按下标对齐,空=不限制)
                    }
                }
                string judExistExcludeName = costParRes.Length > 2 ? costParRes[2] : "";
                List<string> judExistHitList = null;                       //costParRes[3]:各"或"分支用|分隔
                if (costParRes.Length > 3 && !string.IsNullOrEmpty(costParRes[3]))
                {
                    judExistHitList = new List<string>();
                    foreach (var branch in costParRes[3].Split('|'))
                        if (!string.IsNullOrWhiteSpace(branch)) judExistHitList.Add(branch.Trim());
                }
                EffLogic.IniExist iniExist = new EffLogic.IniExist(judExistLocations, judExistCardCondition, judExistExcludeName, judExistHitList);
                return iniExist;
            case "JudDrawCard":
                //  示例: JudDrawCard:1
                EffLogic.IniDrawCard iniDrawCard = new EffLogic.IniDrawCard(int.Parse(costParameter));
                return iniDrawCard;
            case "JudTime":
                //  示例: JudTime:2&Main1Phase|Main2Phase
                string[] phases = costParRes[1].Split('|');
                List<GameManage.GamePhase> gamePhases = StringToGamePhase(phases);
                EffLogic.IniTime iniTime = new EffLogic.IniTime(int.Parse(costParRes[0]), gamePhases);
                return iniTime;
            case "JudLocation":
                //参数格式: JudLocation:cardLocation（costParameter=允许的卡牌地点,Deck|ExtraDeck|Hand|Field|Cemetery|Banished,|分隔）
                //  示例: JudLocation:Field
                string[] location_Par = costParameter.Split("|");
                List<GameManage.CardLocation> locationPars = StringToCardLocation(location_Par);
                EffLogic.IniLocation iniLocation = new EffLogic.IniLocation(locationPars);
                return iniLocation;
            case "IsCanMagic":
                //参数格式: IsCanMagic:（无参数,内部判断魔法卡可否发动:从手卡直接发动仅限控制者自己回合,盖放在场须盖放满一回合）
                return null;
            case "IsCanTrap":
                //参数格式: IsCanTrap:（无参数,内部判断陷阱卡可否发动）
                return null;
            case "FusionSummonCost":
                //参数格式: FusionSummonCost:speNameKey&Name_Key&fusionSumLoc&materialLoc&includeSelf&maxMaterialCount
                //  costParRes[0]=特殊名字/字段列表(多个用|分隔,素材满足其中任一即可,留空不限制);等级条目填 >=N 或 <=N 表示融合怪兽等级大于等于/小于等于N
                //  costParRes[1]=匹配模式(0=按卡名,1=按字段,-1=除外卡名,与[0]一一对应,|分隔;等级条目的对应位填0或空格占位;-1表示该卡名的融合怪兽不能被本效果融合召唤,不参与素材要求)
                //  costParRes[2]=融合怪兽所在区域(ExtraDeck等,多个用|分隔)   costParRes[3]=融合素材所在区域(Hand|Field|Cemetery等,多个用|分隔;对方区域加e_前缀)
                //  costParRes[4]=是否包含自身(0=否,1=是:融合素材必须包含效果持有卡;留空按0);=1时效果持有卡自身自动并入素材池,无需在素材区域中重复配置其所在区域(例:阿不思"自身+对方场上"只需e_Field&1)
                //  costParRes[5]=融合素材至多选多少个(>0:目标怪兽素材条件数(SpeMatter条目数)必须<=该值才可被本效果融合,超限目标不参与判定;留空/0=不限制)
                //  示例: FusionSummonCost:阿不思的落胤|烙印&0|1&ExtraDeck&Hand|Field   等级示例: FusionSummonCost:>=8&0&ExtraDeck&Hand|Field
                //  示例(可使用对方场上/对方手牌为素材): FusionSummonCost:阿不思的落胤|烙印&0|1&ExtraDeck&e_Field|e_Hand|Field|Hand
                List<string> speNameKeyList = string.IsNullOrEmpty(costParRes[0]) ? new List<string>() : costParRes[0].Split('|').ToList();
                string[] strNameKeyList = costParRes[1].Split('|');
                List<int> nameKeyList = new List<int>();
                for (int i = 0; i < strNameKeyList.Length; i++)
                {
                    string modeStr = strNameKeyList[i].Trim();
                    nameKeyList.Add(string.IsNullOrEmpty(modeStr) ? 0 : int.Parse(modeStr));  //等级条目对应位可为空格占位(按0处理),-1=除外卡名
                }
                List<GameManage.CardLocation> fusionSumLocs = StringToCardLocation(costParRes[2].Split('|'));
                List<GameManage.CardLocation> materialLocs = StringToCardLocation(costParRes[3].Split('|'));
                int includeSelfCost = (costParRes.Length > 4 && !string.IsNullOrEmpty(costParRes[4])) ? int.Parse(costParRes[4]) : 0;
                int fusionMatMaxCost = (costParRes.Length > 5 && !string.IsNullOrEmpty(costParRes[5])) ? int.Parse(costParRes[5]) : 0;  //融合素材至多选多少个(留空/0=不限制)
                EffLogic.IniFusionSummonCost iniFusionSummonCost = new EffLogic.IniFusionSummonCost(speNameKeyList, nameKeyList, fusionSumLocs, materialLocs, includeSelfCost, fusionMatMaxCost);
                return iniFusionSummonCost;
            case "JudLastEffCost":
                //参数格式: JudLastEffCost:effType（costParameter=允许的连锁中上一个效果类型,多个用|分隔;与链顶效果的effOrderType任一一致即通过,留空不限制）
                //  示例: JudLastEffCost:active|magic
                List<string> effTypeList = string.IsNullOrEmpty(costParameter) ? new List<string>() : costParameter.Split('|').ToList();
                EffLogic.IniJudLastEffCost iniJudLastEffCost = new EffLogic.IniJudLastEffCost(effTypeList);
                return iniJudLastEffCost;
            case "JudregTime":
                //参数格式: JudregTime:existTurns&locations&cardCondition
                //  costParRes[0]=模式与回合门槛:
                //         0=本回合进入登记模式:查匹配区域(任意区域)的"本回合进入"登记表(卡真正进入该区域时登记快照,对方回合开始前随回合界限清空;卡随后离开该区域记录仍保留),
                //           用于表达"这个回合有[满足选卡条件]的卡进入过该区域"一类效果(墓地典型:如"这个回合有融合怪兽被送去自己墓地,结束阶段才能发动")
                //         >=1=停留回合模式:regTime进入当前区域=0/每过一个回合结束+1,regTime>=existTurns才算命中
                //  costParRes[1]=存在地点(Deck|ExtraDeck|Hand|Field|Cemetery|Banished,|分隔;对方区域加e_前缀如e_Field;留空=全部区域)
                //  costParRes[2]=二维选卡条件cardCondition(大组间|分隔,组内子项*分隔;大组下标/编码与JudExist一致见Condition2D;留空=不限制任何卡)
                //  示例1(本回合有融合怪兽被送去自己墓地;配合JudTime:2&EndPhase): JudregTime:0&Cemetery&融合怪兽条件
                //  示例2(自己场上存在至少1回合的任意怪兽): JudregTime:1&Field&0
                //  示例3(自己墓地存在至少2回合的字段含"烙印"的怪兽): JudregTime:2&Cemetery&&烙印
                int judregExistTurns = int.Parse(costParRes[0]);
                List<GameManage.CardLocation> judregLocations = StringToCardLocation(string.IsNullOrEmpty(costParRes.Length > 1 ? costParRes[1] : "") ? null : costParRes[1].Split('|'));
                EffLogic.IniJudregTime iniJudregTime = new EffLogic.IniJudregTime(judregExistTurns, judregLocations,
                    SplitCardCondition(costParRes.Length > 2 ? costParRes[2] : ""));
                return iniJudregTime;
            case "SelfJudregTime":
                //参数格式: SelfJudregTime:needTurns&locations (判断"效果持有卡自身"当前所在位置+在该位置的停留回合数,只针对自身卡不扫全区域)
                //  costParRes[0]=回合门槛:0=本回合才进入该位置(自身进入该区域regTime置0,每过自身回合结束+1,即尚未经过回合界限);
                //         >=1=自身在该位置已停留达到needTurns回合(regTime>=needTurns);负数防错按0
                //  costParRes[1]=自身所在位置(Deck|ExtraDeck|Hand|Field|Cemetery|Banished,|分隔;对方区域加e_前缀如e_Field|e_Cemetery;留空=全部区域)
                //  示例1(自身已在场上停留至少1回合才能发动): SelfJudregTime:1&Field
                //  示例2(自身在对方场上停留至少2回合): SelfJudregTime:2&e_Field
                //  示例3(自身本回合才被送入自己墓地,结束阶段才能发动): SelfJudregTime:0&Cemetery
                int selfJudregTurns = int.Parse(costParRes[0]);
                List<GameManage.CardLocation> selfJudregLocations = StringToCardLocation(string.IsNullOrEmpty(costParRes.Length > 1 ? costParRes[1] : "") ? null : costParRes[1].Split('|'));
                EffLogic.IniSelfJudregTime iniSelfJudregTime = new EffLogic.IniSelfJudregTime(selfJudregTurns, selfJudregLocations);
                return iniSelfJudregTime;
            case "JudCostCard":
                //参数格式: JudCostCard:num（判断当前操作玩家手牌数>=num,作为"丢弃X张手卡才能发动"的前置判断,与costpay的CostCard配对使用）
                //  示例: JudCostCard:1
                return null;
            default:
                throw new ArgumentException($"输入错误{costType}");
        }
    }

    public static EffLogic.JudCost EntityCost(EffLogic.EntityCard entityCard, string costType, string costParameter, 
        Func<EntityPlayer> QuestEntityPlayer, Func<List<bool>, List<EntityFindComponent>> QuestBools,
        Func<GameManage.GamePhase> QuestGamePhase, Func<int> QuestOwnerPhase,
        Func<EntityPlayer> QuestOpponentPlayer, Func<StateMachine.EffOrder> QuestEffOrder, EventMintor eventMintor)            //初始化转实体
    {
        EffLogic.IniCostComponent iniCostComponent = StringToCost(costType, costParameter);
        switch (costType)
        {
            case "JudExist":
                var iniExist = iniCostComponent as EffLogic.IniExist;
                EffLogic.JudExist judExist = new EffLogic.JudExist(iniExist, QuestBools);
                return judExist;
            case "JudDrawCard":
                var iniDrawCard = iniCostComponent as EffLogic.IniDrawCard;
                EffLogic.JudDrawCard judDrawCard = new EffLogic.JudDrawCard(iniDrawCard, QuestBools);
                return judDrawCard;
            case "JudTime":
                var iniTime = iniCostComponent as EffLogic.IniTime;
                EffLogic.JudTime judTime = new EffLogic.JudTime(iniTime, QuestGamePhase, QuestOwnerPhase);
                return judTime;
            case "JudLocation":
                var iniLocation = iniCostComponent as EffLogic.IniLocation;
                EffLogic.JudLocation judLocation = new EffLogic.JudLocation(entityCard,iniLocation);
                return judLocation;
            case "IsCanMagic":
                EffLogic.IsCanMagic isCanMagic = new EffLogic.IsCanMagic(entityCard, QuestBools, QuestOwnerPhase);
                return isCanMagic;
            case "IsCanTrap":
                EffLogic.IsCanTrap isCanTrap = new EffLogic.IsCanTrap(entityCard, QuestBools);
                return isCanTrap;
            case "FusionSummonCost":
                var iniFusionSummonCost = iniCostComponent as EffLogic.IniFusionSummonCost;
                EffLogic.FusionSummonCost fusionSummonCost = new EffLogic.FusionSummonCost(iniFusionSummonCost, QuestBools, entityCard);
                return fusionSummonCost;
            case "JudLastEffCost":
                var iniJudLastEffCost = iniCostComponent as EffLogic.IniJudLastEffCost;
                EffLogic.JudLastEffCost judLastEffCost = new EffLogic.JudLastEffCost(iniJudLastEffCost, QuestEffOrder);
                return judLastEffCost;
            case "JudregTime":
                var iniJudregTime = iniCostComponent as EffLogic.IniJudregTime;
                if (iniJudregTime == null) throw new ArgumentException("JudregTime:无效的IniJudregTime配置");
                EffLogic.JudregTime judregTime = new EffLogic.JudregTime(iniJudregTime, QuestBools);
                return judregTime;
            case "SelfJudregTime":
                var iniSelfJudregTime = iniCostComponent as EffLogic.IniSelfJudregTime;
                if (iniSelfJudregTime == null) throw new ArgumentException("SelfJudregTime:无效的IniSelfJudregTime配置");
                EffLogic.SelfJudregTime selfJudregTime = new EffLogic.SelfJudregTime(iniSelfJudregTime, entityCard);
                return selfJudregTime;
            case "JudCostCard":
                //丢弃手牌数前置判断:当前操作玩家手牌数>=num才可发动(与costpay的CostCard配对)
                EffLogic.JudCostCard judCostCard = new EffLogic.JudCostCard(int.Parse(costParameter), QuestEntityPlayer);
                return judCostCard;
            default:
                throw new ArgumentException($"输入错误{costType}");
        }
    }

    public static EffLogic.IniPayCostComponent StringToPay(string payType, string payParameter)                                     //生成costPay初始化类
    {
        string[] pays = payParameter.Split('&');
        switch(payType)
        {
            case "MagicActBase":
                //参数格式: MagicActBase:（无参数,效果发动时将魔陷放置到场上）
                return null;
            case "TrapActBase":
                //参数格式: TrapActBase:（无参数,效果发动时将陷阱放置到场上）
                return null;
            case "GoToCemetery":
                //参数格式: GoToCemetery:isEntityCard（pays[0]=是否包含自身,>=1表示将效果持有卡作为代价送去墓地）
                //  示例: GoToCemetery:1
                EffLogic.IniGoToCemetery iniGoToCemetery = new EffLogic.IniGoToCemetery(int.Parse(pays[0]));
                return iniGoToCemetery;
            case "CostCard":
                //参数格式: CostCard:num&cardCondition&hitList (丢弃手卡:从当前操作玩家手牌选卡送墓地,丢弃来源固定为手牌,不再配区域)
                //  pays[0]=需选择并丢弃的手牌张数(>=1)
                //  pays[1]=二维选卡条件cardCondition(大组间用|分隔,组内子项用*分隔;整段留空=不限制任何卡):
                //          大组0=卡种类(0怪兽/1魔法/2陷阱)  大组1=卡名(currentName精确匹配)  大组2=字段(currentKey包含)
                //          大组3=怪兽基础类型(currentBaseType)  大组4=怪兽类型(currentType)  大组5=怪兽特殊类型(currentSpecialType)
                //          大组6=是否盖放(0表侧/1里侧)  大组7=卡名记述(currentHavKey包含:该卡效果文本记载过对应卡名,实体含该词条即可)
                //          大组8=除外卡名(与其它"命中"大组相反,为"排除"语义:卡名命中该名单任一项即整体不算命中,不受命中链影响;组内多项用*分隔)
                //          大组9=怪兽属性(currentAttribute:值与属性字符串精确相等(光/暗/地/水/炎/风/神等),仅怪兽有效;组内多项用*分隔=命中任一项)
                //  pays[2]=命中链hitList(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔,分支内*连接叶子引用"大组下标/子项下标"须同时命中)
                //  示例1(丢弃任意1张手牌): CostCard:1
                //  示例2(丢弃1张怪兽手牌): CostCard:1&0
                //  示例3(丢弃1张"烙印"字段的手牌): CostCard:1&&烙印
                EffLogic.IniCostCard iniCostCard = new EffLogic.IniCostCard(
                    int.Parse(pays[0]),
                    SplitCardCondition(pays.Length > 1 ? pays[1] : ""),
                    SplitHitList(pays.Length > 2 ? pays[2] : ""));
                return iniCostCard;
            case "GetObject":
                //参数格式: GetObject:num&findComponent&cardCondition&excludeSelf&hitList (候选条件已二维化,与GoToCemeteries/JudExist共用同一套cardCondition+hitList编码与求值,见Condition2D)
                //  pays[0]=需选择张数  pays[1]=对象来源区域(多个用|分隔;对方区域加e_前缀;留空=全部区域)
                //  pays[2]=二维选卡条件cardCondition:大组间用|分隔,大组内子项用*分隔;大组下标约定其匹配维度(空大组占位=该维度不限制;整段留空=不限制任何卡):
                //          大组0=卡种类(0怪兽/1魔法/2陷阱)  大组1=卡名(currentName精确匹配)  大组2=字段(currentKey包含)
                //          大组3=怪兽基础类型(currentBaseType)  大组4=怪兽类型(currentType)  大组5=怪兽特殊类型(currentSpecialType)
                //          大组6=是否盖放(0表侧/1里侧)  大组7=卡名记述(currentHavKey包含:该卡效果文本记载过对应卡名,实体含该词条即可)
                //          大组8=除外卡名(与其它"命中"大组相反,为"排除"语义:卡名命中该名单任一项即整体不算命中,不受命中链影响;组内多项用*分隔)
                //          大组9=怪兽属性(currentAttribute:值与属性字符串精确相等(光/暗/地/水/炎/风/神等),仅怪兽有效;组内多项用*分隔=命中任一项)
                //  pays[3]=是否排除自身(留空或>=1=排除效果持有卡自身;0=允许选择自身)
                //  pays[4]=命中链hitList(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔,分支内*连接叶子引用"大组下标/子项下标"须同时命中)
                //  示例1(取场上1只怪兽为对象): GetObject:1&Field&0&1
                //  示例2(取对方手卡1张 名为"阿不思的落胤"的怪兽 或 字段含"烙印"的魔法 为对象,排除自身): GetObject:1&e_Hand&0*1|阿不思的落胤|烙印&1&0/0*1/0|0/1*2/0
                EffLogic.IniGetObject iniGetObject = new EffLogic.IniGetObject(
                    int.Parse(pays[0]),
                    string.IsNullOrEmpty(pays[1]) ? null : StringToCardLocation(pays[1].Split('|')),
                    SplitCardCondition(pays.Length > 2 ? pays[2] : ""),
                    pays.Length > 3 && !string.IsNullOrEmpty(pays[3]) ? int.Parse(pays[3]) : 1,
                    SplitHitList(pays.Length > 4 ? pays[4] : ""));
                return iniGetObject;
            case "GoToCemeteries":
                //参数格式: GoToCemeteries:num&cardLoc&cardCondition&isEntityCard&hitList
                //  pays[0]=需选择并送墓的张数
                //  pays[1]=候选来源区域(多个用|分隔;对方区域加e_前缀;留空=全部区域)
                //  pays[2]=二维选卡条件cardCondition:大组间用|分隔,大组内子项用*分隔;大组下标约定其匹配维度(空=不限制):
                //  pays[3]=是否将效果持有卡自身也送去墓地(留空或0=否,>=1=是;自身不经选择直接加入送墓列表)
                //  pays[4]=命中链hitList(留空=默认:各条件大组须全部命中,大组内任一子项命中即可):各"或"分支用|分隔(hitList[0]|hitList[1]|...)
                //          每个分支内用*连接多个"叶子引用"须同时命中(且关系);叶子引用格式"大组下标/子项下标",如"0/0"=cardCondition[0][0]
                //  示例1(从手牌选1张怪兽送墓): GoToCemeteries:1&Hand&0&0
                //  示例2(将1张"阿不思的落胤"怪兽卡 或 1张"白之圣女"魔法卡送去墓地):
                //    GoToCemeteries:1&Field&0*1|阿不思的落胤*白之圣女&0/0*1/0|0/1*1/1
                string[] gtcLocs = string.IsNullOrEmpty(pays[1]) ? null : pays[1].Split('|');
                List<GameManage.CardLocation> gtcFindComponent = string.IsNullOrEmpty(pays[1]) ? null : StringToCardLocation(gtcLocs);
                List<List<string>> gtcCardCondition = new List<List<string>>();
                if (pays.Length > 2 && !string.IsNullOrEmpty(pays[2]))
                {
                    foreach (var groupStr in pays[2].Split('|'))          //大组间|分隔
                    {
                        if (groupStr == null) continue;
                        List<string> group = new List<string>();
                        if (!string.IsNullOrWhiteSpace(groupStr))
                            foreach (var item in groupStr.Split('*'))     //大组内子项*分隔
                                if (!string.IsNullOrWhiteSpace(item)) group.Add(item.Trim());
                        gtcCardCondition.Add(group);                      //空大组也保留占位(维度按下标对齐,空=不限制)
                    }
                }
                int gtcIsEntityCard = pays.Length > 3 && !string.IsNullOrEmpty(pays[3]) ? int.Parse(pays[3]) : 0;
                List<string> gtcHitList = null;                           //pays[4]:各"或"分支用|分隔
                if (pays.Length > 4 && !string.IsNullOrEmpty(pays[4]))
                {
                    gtcHitList = new List<string>();
                    foreach (var branch in pays[4].Split('|'))
                        if (!string.IsNullOrWhiteSpace(branch)) gtcHitList.Add(branch.Trim());
                }
                EffLogic.IniGoToCemeteries iniGoToCemeteries = new EffLogic.IniGoToCemeteries(int.Parse(pays[0]), gtcFindComponent, gtcCardCondition, gtcIsEntityCard, gtcHitList);
                return iniGoToCemeteries;
            default:
                throw new ArgumentException($"输入错误{payType}");
        }
    }

    public static EffLogic.CostPay EntityPay(EffLogic.EntityCard entityCard, string payType,string payParameter, 
        Func<EntityPlayer> QuestEntityPlayer, Func<List<bool>, List<EntityFindComponent>> QuestBools,EQ eQ)                              //生成costPay实体
    {
        EffLogic.IniPayCostComponent iniPayCostComponent = StringToPay(payType, payParameter);
        switch(payType)
        {
            case "MagicActBase":
                EffLogic.MagicActBase magicActBase = new EffLogic.MagicActBase(entityCard, QuestBools, eQ.MagicActBaseEQ);
                return magicActBase;
            case "TrapActBase":
                EffLogic.TrapActBase trapActBase = new EffLogic.TrapActBase(entityCard, QuestBools, eQ.MagicActBaseEQ);   //复用魔陷发动UI流程（FightUI.MagicActBase已兼容陷阱卡）
                return trapActBase;
            case "GoToCemetery":
                var iniGoToCemetery = iniPayCostComponent as EffLogic.IniGoToCemetery;
                EffLogic.GoToCemetery goToCemetery = new EffLogic.GoToCemetery(entityCard,iniGoToCemetery, QuestBools,eQ.GoToCemeteryEQ);
                return goToCemetery;
            case "CostCard":
                //丢弃手卡:复用墓地UI回调(丢弃即送墓,手牌预制体移入墓地)
                var iniCostCard = iniPayCostComponent as EffLogic.IniCostCard;
                EffLogic.CostCard costCard = new EffLogic.CostCard(entityCard, iniCostCard, QuestEntityPlayer, QuestBools, eQ?.GoToCemeteryEQ);
                return costCard;
            case "GetObject":
                var iniGetObject = iniPayCostComponent as EffLogic.IniGetObject;
                EffLogic.GetObject getObject = new EffLogic.GetObject(entityCard, iniGetObject, QuestBools, eQ?.GetObjectEQ);
                return getObject;
            case "GoToCemeteries":
                var iniGoToCemeteries = iniPayCostComponent as EffLogic.IniGoToCemeteries;
                EffLogic.GoToCemeteries goToCemeteries = new EffLogic.GoToCemeteries(entityCard, iniGoToCemeteries, QuestBools, eQ?.GoToCemeteryEQ);
                return goToCemeteries;
            default: 
                throw new ArgumentException($"输入错误{payType}");
        }
    }           

    //二维选卡条件共用解析:cardCondition串各大组用|分隔、组内子项用*分隔;空大组也保留占位(维度按下标对齐,空=不限制)。GoToCemeteries/JudExist/SpeSomEff/GetObject 共用同一套解析与求值
    private static List<List<string>> SplitCardCondition(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        List<List<string>> cardCondition = new List<List<string>>();
        foreach (var groupStr in raw.Split('|'))
        {
            if (groupStr == null) continue;
            List<string> group = new List<string>();
            if (!string.IsNullOrWhiteSpace(groupStr))
                foreach (var item in groupStr.Split('*'))
                    if (!string.IsNullOrWhiteSpace(item)) group.Add(item.Trim());
            cardCondition.Add(group);
        }
        return cardCondition;
    }

    //二维命中链共用解析:各"或"分支用|分隔,分支内用*连接多个叶子引用"大组下标/子项下标"(须同时命中);空白=null(默认语义)
    private static List<string> SplitHitList(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        List<string> hitList = new List<string>();
        foreach (var branch in raw.Split('|'))
            if (!string.IsNullOrWhiteSpace(branch)) hitList.Add(branch.Trim());
        return hitList.Count == 0 ? null : hitList;
    }

    public static EffLogic.IniCardComponent StingToEffPar(string effType, string effectParameter)                                   // 生成效果初始化类
    {

        string[] effParRes = effectParameter.Split('&');
        switch (effType)
        {
            case "SpeSomEff":
                //参数格式: SpeSomEff:isGetObject&num&findComponent&cardCondition&excludeName&hitList (候选条件已二维化,与GoToCemeteries/JudExist共用同一套cardCondition+hitList编码与求值,见Condition2D)
                //  effParRes[0]=是否取对象(留空或0=不取,>=1=取对象)   effParRes[1]=选择特招数量
                //  effParRes[2]=特招来源区域(Deck|ExtraDeck|Hand|Field|Cemetery|Banished,|分隔;对方区域加e_前缀;留空=全部区域)
                //  effParRes[3]=二维选卡条件cardCondition:大组间用|分隔,大组内子项用*分隔;大组下标约定其匹配维度(空大组占位=该维度不限制;整段留空=不限制任何卡):
                //          大组0=卡种类(entityType:0怪兽/1魔法/2陷阱)  大组1=卡名(currentName精确匹配)  大组2=字段(currentKey包含)
                //          大组3=怪兽基础类型(currentBaseType)  大组4=怪兽类型(currentType)  大组5=怪兽特殊类型(currentSpecialType)
                //          大组6=是否盖放(0表侧/1里侧)  大组7=卡名记述(currentHavKey包含:该卡效果文本记载过对应卡名,实体含该词条即可)
                //          大组8=除外卡名(与其它"命中"大组相反,为"排除"语义:卡名命中该名单任一项即整体不算命中,不受命中链影响;组内多项用*分隔)
                //          大组9=怪兽属性(currentAttribute:值与属性字符串精确相等(光/暗/地/水/炎/风/神等),仅怪兽有效;组内多项用*分隔=命中任一项)
                //  effParRes[4]=排除卡名(该卡名不算特招候选;留空=不排除)   effParRes[5]=命中链hitList(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔,分支内*连接叶子引用"大组下标/子项下标"须同时命中)
                //  示例1(从手卡/卡组特招1只名为"青眼白龙"的怪兽): SpeSomEff:0&1&Hand|Deck&0|青眼白龙&&
                //  示例2(从手卡/卡组特招 名为"阿不思的落胤"的怪兽 或 字段含"相剑"的怪兽,排除"阿不思的落胤"): SpeSomEff:0&1&Hand|Deck&0|阿不思的落胤|相剑&阿不思的落胤&0/0*1/0|0/0*2/0
                EffLogic.IniSpeSomEff iniSpeSumEff = new EffLogic.IniSpeSomEff(
                    int.Parse(effParRes[0]),
                    int.Parse(effParRes[1]),
                    string.IsNullOrEmpty(effParRes[2]) ? null : StringToCardLocation(effParRes[2].Split('|')),
                    SplitCardCondition(effParRes.Length > 3 ? effParRes[3] : ""),
                    effParRes.Length > 4 ? effParRes[4] : "",
                    SplitHitList(effParRes.Length > 5 ? effParRes[5] : ""));
                return iniSpeSumEff;
            case "SelfSpeSomEff":
                //参数格式: SelfSpeSomEff:(无参数)
                //  自身特招:效果结算时把效果持有卡自身从当前所在区域特殊召唤上场,不收集候选/不弹窗/不取对象
                //  示例: SelfSpeSomEff
                return new EffLogic.IniSelfSpeSomEff();
            case "DestoryCard":
                //参数格式: DestoryCard:isGetObject&num&findComponent&cardCondition&excludeName&hitList (候选条件已二维化,与GoToCemeteries/JudExist共用同一套cardCondition+hitList编码与求值,见Condition2D)
                //  effParRes[0]=是否取对象(留空或0=不取,>=1=取对象:对象由发动阶段GetObject选取,结算时直接破坏对象)
                //  effParRes[1]=不取对象模式下选择破坏的数量(取对象模式忽略)  effParRes[2]=破坏候选来源区域(留空=全部区域)
                //  effParRes[3]=二维选卡条件cardCondition(大组编码与SpeSomEff一致,留空=不限制任何卡)  effParRes[4]=排除卡名(留空不排除)  effParRes[5]=命中链hitList(留空=默认)
                //  示例1(破坏对方场上1只怪兽): DestoryCard:0&1&e_Field&0&
                //  示例2(取对象破坏1张卡:由GetObject选好对象,结算时直接破坏): DestoryCard:1&1&e_Field&0&
                EffLogic.IniDestoryCard iniDestoryCard = new EffLogic.IniDestoryCard(
                    int.Parse(effParRes[0]),
                    effParRes.Length > 1 && !string.IsNullOrEmpty(effParRes[1]) ? int.Parse(effParRes[1]) : 1,
                    string.IsNullOrEmpty(effParRes[2]) ? null : StringToCardLocation(effParRes[2].Split('|')),
                    SplitCardCondition(effParRes.Length > 3 ? effParRes[3] : ""),
                    effParRes.Length > 4 ? effParRes[4] : "",
                    SplitHitList(effParRes.Length > 5 ? effParRes[5] : ""));
                return iniDestoryCard;
            case "BanishedCard":
                //参数格式: BanishedCard:isGetObject&num&findComponent&cardCondition&excludeName&hitList&excludeSelf (候选条件已二维化,与DestoryCard/JudExist共用同一套cardCondition+hitList编码与求值,见Condition2D)
                //  effParRes[0]=是否取对象(留空或0=不取,>=1=取对象:对象由发动阶段GetObject选取,结算时直接除外对象)
                //  effParRes[1]=不取对象模式下选择除外的数量(取对象模式忽略)  effParRes[2]=除外候选来源区域(留空=全部区域)
                //  effParRes[3]=二维选卡条件cardCondition(大组编码与SpeSomEff一致,留空=不限制任何卡)  effParRes[4]=排除卡名(留空不排除)
                //  effParRes[5]=命中链hitList(留空=默认)  effParRes[6]=是否排除效果持有卡自身(留空或>=1=排除自身,0=允许选择自身)
                //  示例1(除外对方场上1只怪兽): BanishedCard:0&1&e_Field&0&&&
                //  示例2(除外自己场上1只其他怪兽,排除自身): BanishedCard:0&1&Field&0&&&1
                //  示例3(取对象除外1张卡:由GetObject选好对象,结算时直接除外): BanishedCard:1&1&e_Field&0&&&
                EffLogic.IniBanishedCard iniBanishedCard = new EffLogic.IniBanishedCard(
                    int.Parse(effParRes[0]),
                    effParRes.Length > 1 && !string.IsNullOrEmpty(effParRes[1]) ? int.Parse(effParRes[1]) : 1,
                    string.IsNullOrEmpty(effParRes[2]) ? null : StringToCardLocation(effParRes[2].Split('|')),
                    SplitCardCondition(effParRes.Length > 3 ? effParRes[3] : ""),
                    effParRes.Length > 4 ? effParRes[4] : "",
                    SplitHitList(effParRes.Length > 5 ? effParRes[5] : ""),
                    effParRes.Length > 6 && !string.IsNullOrEmpty(effParRes[6]) ? int.Parse(effParRes[6]) : 1);
                return iniBanishedCard;
            case "DrawCard":
                //参数格式: DrawCard:number（effParRes[0]=抽卡数量）
                //  示例: DrawCard:2
                EffLogic.IniDrawCardEff iniDrawCardEff = new EffLogic.IniDrawCardEff(int.Parse(effParRes[0]));
                return iniDrawCardEff;
            case "EffGoToCemetery":
                //参数格式: EffGoToCemetery:isGetObject&isEntityCard
                //  effParRes[0]=是否取对象   effParRes[1]=是否包含自身,>=1表示将效果持有卡送去墓地
                //  示例: EffGoToCemetery:0&1
                EffLogic.IniEffGoToCemetery iniEffGoToCemetery = new EffLogic.IniEffGoToCemetery(int.Parse(effParRes[0]), int.Parse(effParRes[1]));
                return iniEffGoToCemetery;
            case "EffGoToCemeteries":
                //参数格式: EffGoToCemeteries:isGetObject&num&findComponent&cardCondition&isEntityCard&hitList (效果形态的cost送墓GoToCemeteries,通过持有EffGoToCemetery完成送墓)
                //  effParRes[0]=是否取对象(留空或0=不取,>=1=取对象:对象由发动阶段costpay的GetObject选取,结算时直接送墓对象)
                //  effParRes[1]=不取对象模式下需选择并送墓的张数(取对象模式忽略,以注入对象数为准;留空按1)
                //  effParRes[2]=候选来源区域(Deck|ExtraDeck|Hand|Field|Cemetery|Banished,多个用|分隔;对方区域加e_前缀如e_Field;留空=全部区域含对方)
                //  effParRes[3]=二维选卡条件cardCondition:大组间用|分隔,组内子项用*分隔;大组下标/编码与GoToCemeteries、JudExist共用(求值见EffLogic.Condition2D;留空=不限制任何卡):
                //          大组0=卡种类(0怪兽/1魔法/2陷阱)  大组1=卡名(currentName精确匹配)  大组2=字段(currentKey包含)
                //          大组3=怪兽基础类型(currentBaseType)  大组4=怪兽类型(currentType)  大组5=怪兽特殊类型(currentSpecialType)
                //          大组6=是否盖放(0表侧/1里侧)  大组7=卡名记述(currentHavKey包含:该卡效果文本记载过对应卡名,实体含该词条即可)
                //          大组8=除外卡名(与其它"命中"大组相反,为"排除"语义:卡名命中该名单任一项即整体不算命中,不受命中链影响;组内多项用*分隔)
                //          大组9=怪兽属性(currentAttribute:值与属性字符串精确相等(光/暗/地/水/炎/风/神等),仅怪兽有效;组内多项用*分隔=命中任一项)
                //  effParRes[4]=是否将效果持有卡自身也送去墓地(留空或0=否,>=1=是:自身不经选择直接加入送墓列表;候选收集始终排除自身)
                //  effParRes[5]=命中链hitList(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔,分支内*连接叶子引用"大组下标/子项下标"须同时命中)
                //  示例1(将对方场上1只怪兽送去墓地): EffGoToCemeteries:0&1&e_Field&0&0&
                //  示例2(从手卡选1张"烙印"字段的卡送去墓地): EffGoToCemeteries:0&1&Hand&&烙印&0&
                //  示例3(取对象:将选中的卡送去墓地,与costpay的GetObject:1&...配对): EffGoToCemeteries:1&1&&0&0&
                EffLogic.IniEffGoToCemeteries iniEffGoToCemeteries = new EffLogic.IniEffGoToCemeteries(
                    int.Parse(effParRes[0]),
                    effParRes.Length > 1 && !string.IsNullOrEmpty(effParRes[1]) ? int.Parse(effParRes[1]) : 1,
                    string.IsNullOrEmpty(effParRes[2]) ? null : StringToCardLocation(effParRes[2].Split('|')),
                    SplitCardCondition(effParRes.Length > 3 ? effParRes[3] : ""),
                    effParRes.Length > 4 && !string.IsNullOrEmpty(effParRes[4]) ? int.Parse(effParRes[4]) : 0,
                    SplitHitList(effParRes.Length > 5 ? effParRes[5] : ""));
                return iniEffGoToCemeteries;
            case "EffGoToBanished":
                //参数格式: EffGoToBanished:isGetObject&isEntityCard
                //  effParRes[0]=是否取对象(>=1=取对象:对象由发动阶段costpay的GetObject选取,结算时直接除外)
                //  effParRes[1]=是否包含自身,>=1表示将效果持有卡自身除外(不经选择直接加入除外列表)
                //  示例(将自身除外): EffGoToBanished:0&1
                //  示例2(除外场上1只对方怪兽:与costpay的GetObject:1&e_Field&0&1配对): EffGoToBanished:1&0
                EffLogic.IniEffGoToBanished iniEffGoToBanished = new EffLogic.IniEffGoToBanished(int.Parse(effParRes[0]), int.Parse(effParRes[1]));
                return iniEffGoToBanished;
            case "EffReturnHand":
                //参数格式: EffReturnHand:isGetObject&isEntityCard
                //  effParRes[0]=是否取对象(>=1=取对象:对象由发动阶段costpay的GetObject选取,结算时直接使其返回持有者手牌)
                //  effParRes[1]=是否包含自身,>=1表示将效果持有卡自身返回手牌(不经选择直接加入回手列表)
                //  示例(自身返回手牌): EffReturnHand:0&1
                //  示例2(取对象回手1张卡:与costpay的GetObject:1&...配对): EffReturnHand:1&0
                EffLogic.IniEffReturnHand iniEffReturnHand = new EffLogic.IniEffReturnHand(int.Parse(effParRes[0]), int.Parse(effParRes[1]));
                return iniEffReturnHand;
            case "GetCard":
                //参数格式: GetCard:num&cardCondition&findComponent&hitList (检索:从检索方当前控制者的卡组选卡加入手牌;二维选卡条件编码与JudExist/EffGoToCemeteries共用,求值见EffLogic.Condition2D)
                //  effParRes[0]=检索张数(>=1;候选多于检索张数时结算时弹窗让玩家挑选;留空按1)
                //  effParRes[1]=二维选卡条件cardCondition:大组间用|分隔,组内子项用*分隔;大组下标约定其匹配维度(空大组占位=该维度不限制;整段留空=不限制任何卡):
                //          大组0=卡种类(entityType:0怪兽/1魔法/2陷阱)  大组1=卡名(currentName精确匹配)  大组2=字段(currentKey包含)
                //          大组3=怪兽基础类型(currentBaseType)  大组4=怪兽类型(currentType)  大组5=怪兽特殊类型(currentSpecialType)
                //          大组6=是否盖放(0表侧/1里侧)  大组7=卡名记述(currentHavKey包含:该卡效果文本记载过对应卡名,实体含该词条即可)
                //          大组8=除外卡名(与其它"命中"大组相反,为"排除"语义:卡名命中该名单任一项即整体不算命中,不受命中链影响;组内多项用*分隔)
                //          大组9=怪兽属性(currentAttribute:值与属性字符串精确相等(光/暗/地/水/炎/风/神等),仅怪兽有效;组内多项用*分隔=命中任一项)
                //  effParRes[2]=检索来源区域findComponent(Deck=检索方自己的卡组;e_Deck=对方卡组;留空=检索方自己的卡组)
                //  effParRes[3]=命中链hitList(留空=默认:各非空大组都命中,大组内任一子项命中即可;非空=各"或"分支用|分隔,分支内*连接叶子引用"大组下标/子项下标"须同时命中)
                //  示例(死狱乡的导化 阿伯鲁①:从卡组把1张"烙印"字段的魔法·陷阱卡加入手卡): GetCard:1&1*2||烙印||||||&Deck&0/0*2/0|0/1*2/0
                EffLogic.IniGetCard iniGetCard = new EffLogic.IniGetCard(
                    effParRes.Length > 0 && !string.IsNullOrEmpty(effParRes[0]) ? int.Parse(effParRes[0]) : 1,
                    SplitCardCondition(effParRes.Length > 1 ? effParRes[1] : ""),
                    string.IsNullOrEmpty(effParRes.Length > 2 ? effParRes[2] : "") ? null : StringToCardLocation(effParRes[2].Split('|')),
                    SplitHitList(effParRes.Length > 3 ? effParRes[3] : ""));
                return iniGetCard;
            case "FusionSummon":
                //参数格式: FusionSummon:fusionSumLoc&materialLoc&materialHandleWay&speNameKey&Name_Key&includeSelf&maxMaterialCount
                //  effParRes[0]=融合怪兽所在区域(ExtraDeck等,多个用|分隔)   effParRes[1]=融合素材所在区域(Hand|Field|Cemetery等,多个用|分隔;对方区域加e_前缀,如e_Field|e_Hand)
                //  effParRes[2]=素材处理方式(索引=素材区域编号0卡组/1额外/2手牌/3墓地/4除外/5场上,值=目标区域编号,多个用|分隔;留空全部送墓地;对方素材自动送入对方对应区域,无需特判)
                //  effParRes[3]=素材必须包含的名字/字段列表(多个用|分隔,素材满足其中任一即可,留空不限制);等级条目填 >=N 或 <=N 表示融合怪兽等级大于等于/小于等于N
                //  effParRes[4]=匹配模式(0=按卡名,1=按字段,-1=除外卡名,与[3]一一对应,|分隔;等级条目的对应位填0或空格占位;-1表示该卡名的融合怪兽不能被本效果融合召唤,不参与素材要求)
                //  effParRes[5]=是否包含自身(0=否,1=是:融合素材必须包含效果持有卡;留空按0);=1时效果持有卡自身自动并入素材池,无需在素材区域中重复配置其所在区域(例:阿不思"自身+对方场上"只需e_Field&1)
                //  effParRes[6]=融合素材至多选多少个(>0:目标怪兽素材条件数(SpeMatter条目数)必须<=该值才可被本效果融合,超限目标不进入选择弹窗;留空/0=不限制)
                //  示例: FusionSummon:ExtraDeck&Hand|Field&3&阿不思的落胤&0   等级示例: FusionSummon:ExtraDeck&Hand|Field&3&>=8&0
                List<GameManage.CardLocation> fusionSumLocs = StringToCardLocation(effParRes[0].Split('|'));
                List<GameManage.CardLocation> materialLocs = StringToCardLocation(effParRes[1].Split('|'));
                List<int> materialHandleWay = new List<int>();
                if (!string.IsNullOrEmpty(effParRes[2]))
                    foreach (var s in effParRes[2].Split('|'))
                        materialHandleWay.Add(int.Parse(s));
                List<string> fusionSpeNameKeys = string.IsNullOrEmpty(effParRes[3]) ? new List<string>() : effParRes[3].Split('|').ToList();
                List<int> fusionNameKeys = new List<int>();
                if (!string.IsNullOrEmpty(effParRes[4]))
                {
                    foreach (var s in effParRes[4].Split('|'))
                    {
                        string modeStr = s.Trim();
                        fusionNameKeys.Add(string.IsNullOrEmpty(modeStr) ? 0 : int.Parse(modeStr));  //等级条目对应位可为空格占位(按0处理),-1=除外卡名
                    }
                }
                int includeSelfEff = (effParRes.Length > 5 && !string.IsNullOrEmpty(effParRes[5])) ? int.Parse(effParRes[5]) : 0;
                int fusionMatMaxEff = (effParRes.Length > 6 && !string.IsNullOrEmpty(effParRes[6])) ? int.Parse(effParRes[6]) : 0;  //融合素材至多选多少个(留空/0=不限制)
                EffLogic.IniFusionSummon iniFusionSummon = new EffLogic.IniFusionSummon(fusionSumLocs, materialLocs, materialHandleWay, fusionSpeNameKeys, fusionNameKeys, includeSelfEff, fusionMatMaxEff);
                return iniFusionSummon;
            default:
                throw new ArgumentException($"输入错误{effType}");
        }
    }

    public static EffLogic.Eff EntityEff(string effType, string effectParameter, EffLogic.EntityCard entityCard,
        Func<EntityPlayer> QuestEntityPlayer, Func<List<bool>, List<EntityFindComponent>> QuestBools,
        Func<EntityPlayer> QuestOpponentPlayer, EQ eQ)                         // 初始化转实体
    {
        EffLogic.IniCardComponent iniCardComponent = StingToEffPar(effType, effectParameter);
        switch (effType)
        {
            case "SpeSomEff":
                var iniSpeSumEff = iniCardComponent as EffLogic.IniSpeSomEff;
                if (iniSpeSumEff != null)
                {
                    EffLogic.SpeSomEff speSumEff = new EffLogic.SpeSomEff(eQ.SpeSumEQ);
                    speSumEff.IniCardCompent(iniSpeSumEff, QuestBools);
                    return speSumEff;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniSpeSumEff 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "SelfSpeSomEff":
                var iniSelfSpeSomEff = iniCardComponent as EffLogic.IniSelfSpeSomEff;
                if (iniSelfSpeSomEff != null)
                {
                    //注入效果持有卡(entityCard=特招目标)与UI回调eQ.SpeSumEQ(其HandleTransfrom=实体特招UI):直接特招自身,不弹选卡窗
                    EffLogic.SelfSpeSomEff selfSpeSomEff = new EffLogic.SelfSpeSomEff(entityCard, eQ.SpeSumEQ);
                    selfSpeSomEff.IniCardCompent(iniSelfSpeSomEff, QuestBools);
                    return selfSpeSomEff;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniSelfSpeSomEff 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "DestoryCard":
                var iniDestoryCard = iniCardComponent as EffLogic.IniDestoryCard;
                if (iniDestoryCard != null)
                {
                    //注入UI回调: eQ.GoToCemeteryEQ(其GetEntityCard=选卡弹窗,HandleTransfrom=送墓UI)——
                    //  不取对象模式用于"一张也弹"的选择弹窗,破坏动作后将被破坏卡预制体移动到墓地;同时提供timePointBases供登记"卡被破坏"时点
                    EffLogic.DestoryCard destoryCard = new EffLogic.DestoryCard(eQ.GoToCemeteryEQ);
                    destoryCard.IniCardCompent(iniDestoryCard, QuestBools);
                    return destoryCard;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniDestoryCard 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "BanishedCard":
                var iniBanishedCard = iniCardComponent as EffLogic.IniBanishedCard;
                if (iniBanishedCard != null)
                {
                    //注入UI回调: eQ.GoToBanishedEQ(其GetEntityCard=选卡弹窗,HandleTransfrom=除外UI GotoBanishedUI)——不取对象模式用于"一张也弹"的选择弹窗,
                    //  除外动作后将被除外卡的预制体移动到除外区;同时提供timePointBases供登记InBanished时点(供"被除外时"诱发效果响应)
                    EffLogic.BanishedCard banishedCard = new EffLogic.BanishedCard(entityCard, eQ.GoToBanishedEQ);
                    banishedCard.IniCardCompent(iniBanishedCard, QuestBools);
                    return banishedCard;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniBanishedCard 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "DrawCard":
                var iniDrawCard = iniCardComponent as EffLogic.IniDrawCardEff;
                if (iniDrawCard != null)
                {
                    //注入entityCard(决定抽卡方)、抽卡配置IniDrawCardEff与抽卡UI回调eQ.DrawCardEQ(其DrawCardHandle=刷新手牌区),效果抽卡后手牌预制体同步更新
                    EffLogic.DrawCardEff drawCardEff = new EffLogic.DrawCardEff(entityCard, iniDrawCard, eQ?.DrawCardEQ);
                    drawCardEff.IniCardCompent(iniDrawCard, QuestBools);
                    return drawCardEff;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniDrawCardEff 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "EffGoToCemetery":
                var iniEffGoToCemetery = iniCardComponent as EffLogic.IniEffGoToCemetery;
                if (iniEffGoToCemetery != null)
                {
                    //注入UI同步回调eQ.GoToCemeteryEQ(其HandleTransfrom=fightUI.GotoCeCemeteryUI),确保效果送墓后卡牌预制体同步移动到墓地
                    EffLogic.EffGoToCemetery effGoToCemetery = new EffLogic.EffGoToCemetery(entityCard, eQ.GoToCemeteryEQ);
                    effGoToCemetery.IniCardCompent(iniEffGoToCemetery, QuestBools);
                    return effGoToCemetery;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniEffGoToCemetery 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "EffGoToCemeteries":
                var iniEffGoToCemeteries = iniCardComponent as EffLogic.IniEffGoToCemeteries;
                if (iniEffGoToCemeteries != null)
                {
                    //注入UI回调: eQ.GoToCemeteryEQ(其GetEntityCard=选卡弹窗,HandleTransfrom=fightUI.GotoCeCemeteryUI)——
                    //  不取对象模式用于"一张也弹"的选择弹窗;内部持有EffGoToCemetery(配置0&0)经GetEntities注入后执行数据层移动+InCemetery时点+预制体UI同步
                    EffLogic.EffGoToCemeteries effGoToCemeteries = new EffLogic.EffGoToCemeteries(entityCard, eQ.GoToCemeteryEQ);
                    effGoToCemeteries.IniCardCompent(iniEffGoToCemeteries, QuestBools);
                    return effGoToCemeteries;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniEffGoToCemeteries 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "EffGoToBanished":
                var iniEffGoToBanished = iniCardComponent as EffLogic.IniEffGoToBanished;
                if (iniEffGoToBanished != null)
                {
                    //注入UI同步回调eQ.GoToBanishedEQ(其HandleTransfrom=fightUI.GotoBanishedUI),确保效果除外后卡牌预制体同步移动到除外区
                    EffLogic.EffGoToBanished effGoToBanished = new EffLogic.EffGoToBanished(entityCard, eQ.GoToBanishedEQ);
                    effGoToBanished.IniCardCompent(iniEffGoToBanished, QuestBools);
                    return effGoToBanished;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniEffGoToBanished 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "EffReturnHand":
                var iniEffReturnHand = iniCardComponent as EffLogic.IniEffReturnHand;
                if (iniEffReturnHand != null)
                {
                    //注入UI同步回调eQ.GoToHandEQ(其HandleTransfrom=fightUI.GotoHandUI),确保效果回手后卡牌预制体同步移动到持有者手牌区
                    EffLogic.EffReturnHand effReturnHand = new EffLogic.EffReturnHand(entityCard, eQ.GoToHandEQ);
                    effReturnHand.IniCardCompent(iniEffReturnHand, QuestBools);
                    return effReturnHand;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniEffReturnHand 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "GetCard":
                var iniGetCard = iniCardComponent as EffLogic.IniGetCard;
                if (iniGetCard != null)
                {
                    //注入entityCard(决定检索方=效果卡当前控制者)、检索配置IniGetCard与UI回调: eQ.DrawCardEQ(其DrawCardHandle=检索加入手牌后刷新手牌区,timePointBases=GetCard时点队列)
                    //  + eQ.GoToHandEQ(其GetEntityCard=候选多于检索张数时玩家选卡弹窗,MapRealCard=弹窗实体→真实实体映射)
                    EffLogic.GetCard getCard = new EffLogic.GetCard(entityCard, eQ.DrawCardEQ, eQ.GoToHandEQ);
                    getCard.IniCardCompent(iniGetCard, QuestBools);
                    return getCard;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniGetCard 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            case "FusionSummon":
                var iniFusionSummon = iniCardComponent as EffLogic.IniFusionSummon;
                if (iniFusionSummon != null)
                {
                    //UI回调: eQ.SpeSumEQ(实体特招UI) + eQ.GoToCemeteryEQ(送墓UI) + eQ.GoToBanishedEQ(除外UI) + eQ.GoToHandEQ(返回手卡UI) + eQ.GoToDeckEQ(返回卡组/额外卡组回收UI)
                    //  素材处理方式materialHandleWay:目标=卡组0/额外卡组1时经deckQuest收回预制体(隐藏区);目标=手牌2经handQuest移回原持有者手牌;目标=除外区4经banishedQuest放入除外容器
                    EffLogic.FusionSummon fusionSummon = new EffLogic.FusionSummon(entityCard, eQ.SpeSumEQ, eQ.GoToCemeteryEQ, eQ.GoToBanishedEQ, eQ.GoToHandEQ, eQ.GoToDeckEQ);
                    fusionSummon.IniCardCompent(iniFusionSummon, QuestBools);
                    return fusionSummon;
                }
                else
                {
                    throw new ArgumentException($"无法将 iniCardComponent 转换为 IniFusionSummon 类型。请确保传入的 iniCardComponent 是正确的类型:{effType}");
                }
            default:
                throw new ArgumentException($"输入错误{effType}");

        }
    }

    public static StateMachine.IniQuestPlayer EntityIniQuestPlayer(Action<string> GetQuestReback, Func<UniTask<bool>> QuestWaitEffUI)       //生成状态机
    {
        StateMachine.IniQuestPlayer iniQuestPlayer = new StateMachine.IniQuestPlayer(GetQuestReback, QuestWaitEffUI);
        return iniQuestPlayer;
    }

    
    public static List<EffLogic.TriggerBase> EntityTrigger(List<string> timePoint,EffLogic.Effection effection)                        //实例化诱发效果触发器
    {
        List<EffLogic.TriggerBase> res = new List<EffLogic.TriggerBase>();
        foreach (string str  in timePoint)
        {
            switch (str)
            {
                case "SelfSpeSom":
                    //timePoint字符串: SelfSpeSom（无参数,效果组base列[str4]第6段baseEffParameter[6]中以|分隔填入,表示该效果在自身特殊召唤成功时诱发）
                    //  示例(baseEffParameter[6]): SelfSpeSom
                    EffLogic.TriggerBase triggerBase = new EffLogic.SelfSpeSom(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBase);
                    break;
                case "SelfInCemetery":
                    //timePoint字符串: SelfInCemetery（无参数,效果组base列[str4]第6段baseEffParameter[6]中以|分隔填入,表示该效果在自身被送去墓地时诱发）
                    //  示例(baseEffParameter[6]): SelfInCemetery
                    EffLogic.TriggerBase triggerBaseInCemetery = new EffLogic.SelfInCemetery(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBaseInCemetery);
                    break;
                case "SelfInBanished":
                    //timePoint字符串: SelfInBanished（无参数,效果组base列[str4]第6段baseEffParameter[6]中以|分隔填入,表示该效果在自身被除外时诱发）
                    //  示例(baseEffParameter[6]): SelfInBanished
                    EffLogic.TriggerBase triggerBaseInBanished = new EffLogic.SelfInBanished(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBaseInBanished);
                    break;
                case "SelfFusionSummon":
                    //timePoint字符串: SelfFusionSummon（无参数,效果组base列[str4]第6段baseEffParameter[6]中以|分隔填入,表示该效果在自身融合召唤成功时诱发）
                    //  示例(baseEffParameter[6]): SelfFusionSummon
                    EffLogic.TriggerBase triggerBaseFusionSummon = new EffLogic.SelfFusionSummon(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBaseFusionSummon);
                    break;
                case "SelfGetCard":
                    //timePoint字符串: SelfGetCard（无参数,效果组base列[str4]时点段中以|分隔填入,表示该效果在自身被从卡组加入手卡(被GetCard检索)时诱发）
                    //  示例: SelfGetCard;可与SelfSpeSom等用|连写
                    EffLogic.TriggerBase triggerBaseGetCard = new EffLogic.SelfGetCard(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBaseGetCard);
                    break;
                case "SelfSummon":
                    //timePoint字符串: SelfSummon（无参数,效果组base列[str4]第6段baseEffParameter[6]中以|分隔填入,表示该效果在自身通常召唤成功时诱发）
                    //  示例(baseEffParameter[6]): SelfSummon;可与SelfSpeSom用|连写(召唤·特殊召唤的场合): SelfSummon|SelfSpeSom
                    EffLogic.TriggerBase triggerBaseSummon = new EffLogic.SelfSummon(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBaseSummon);
                    break;
                case "LeaveEx":
                    //timePoint字符串: LeaveEx（无参数,效果组base列[str4]时点段中以|分隔填入,表示该效果在"自己·对方的卡从额外卡组离开"时诱发,触发事件见EventMintor.LeaveExtra）
                    //  示例: LeaveEx
                    EffLogic.TriggerBase triggerBaseLeaveEx = new EffLogic.LeaveEx(effection.eventMintor, effection.entityCard);
                    res.Add(triggerBaseLeaveEx);
                    break;
                default:
                    throw new Exception($"输入错误:{str}");
            }
        }
        return res;
    }

    public static FightLogic.Continue EntityContiue(EffLogic.EntityCard entityCard,string cntiueName,string par)
    {
        switch (cntiueName)
        {
            case "0":
                FightLogic.OnlyOne_InField onlyOne_InField = new FightLogic.OnlyOne_InField(entityCard);
                return onlyOne_InField;
            default:
                throw new Exception($"输入有误{cntiueName}");
        }
    }
}
