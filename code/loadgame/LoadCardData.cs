
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using Unity.VisualScripting;
using UnityEngine;
using static CardBase;
public class LoadCardData : MonoBehaviour
{

    public TextAsset cardData;
    
    void Start()
    {
        loadcard();
    }


    void loadcard()
    {
        using (var reader = new StringReader(cardData.text))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();


            while (csv.Read())
            {
                string s0 = csv.GetField(0);
                string s1 = csv.GetField(1);
                string s2 = csv.GetField(2);
                string s3 = csv.GetField(3);
                string s4 = csv.GetField(4);
                string s5 = csv.GetField(5);
                string s6 = csv.GetField(6);                       //卡名记述(havingKey),位于key的下一列

                string[] keyArray = string.IsNullOrEmpty(s5) ? new string[0] : s5.Split('&');
                string[] havingKeyArray = string.IsNullOrEmpty(s6) ? new string[0] : s6.Split('|');
                string s17 = csv.GetField(17) ?? "";
                int effNN;
                if (!int.TryParse(s17, out effNN) || effNN < 0)
                {
                    effNN = 0;
                    s17 = "0";
                }
                if (s3 == "0") 
                {
                    string s7 = csv.GetField(7);      //攻击力
                    string s8 = csv.GetField(8);      //防御力
                    string s9 = csv.GetField(9);      //星级
                    string s10 = csv.GetField(10);    //属性
                    string s11 = csv.GetField(11);    //种族
                    string s12 = csv.GetField(12);    //基本类型
                    string s13 = csv.GetField(13);    //类型
                    string s14 = csv.GetField(14);    //特殊类型
                    string s16 = csv.GetField(16);    //特召素材
                    MonsterCard monsterCard = new MonsterCard(
                        s0, s1, int.Parse(s2), int.Parse(s3), s4, keyArray, havingKeyArray,
                        int.Parse(s7), int.Parse(s8), int.Parse(s9), s10, s11,
                        int.Parse(s12), int.Parse(s13), int.Parse(s14)
                    );
                    monsterCard.GetSpeMatter(s16);
                    monsterCard.GetEffNum(s17);
                    GetStaticEff(effNN, csv, monsterCard);
                    monsterCards.Add(s0, monsterCard);
                }
                else if (s3 == "1") 
                {
                    string s15 = csv.GetField(15);    //魔陷类型

                    MagicCard magicCard = new MagicCard(
                        s0, s1, int.Parse(s2), int.Parse(s3), s4, keyArray, havingKeyArray,
                        int.Parse(s15)
                    );
                    magicCard.GetEffNum(s17);
                    GetStaticEff(effNN, csv, magicCard);
                    magicCards.Add(s0, magicCard);
                }
                else 
                {
                    string s15 = csv.GetField(15);    //魔陷类型

                    TrapCard trapcard = new TrapCard(
                        s0, s1, int.Parse(s2), int.Parse(s3), s4, keyArray, havingKeyArray,
                        int.Parse(s15)
                    );
                    trapcard.GetEffNum(s17);
                    GetStaticEff(effNN, csv, trapcard);
                    trapCards.Add(s0, trapcard);
                }
            }
        }
    }

    private void GetStaticEff(int effNN,CsvReader csv,Card card) 
    {
        int num = 17 + 4 * effNN;
        for (int i = 18; i <= num; i=i+4)
        {
            string str1 = SafeField(csv, i, card.name);
            string str2 = SafeField(csv, i + 1, card.name);
            string str3 = SafeField(csv, i + 2, card.name);
            string str4 = SafeField(csv, i + 3, card.name);

            if (str1 == "" && str2 == "" && str3 == "" && str4 == "")
            {
                continue;
            }

            List<string> specificCostClass = new List<string>();
            List<string> costParameter = new List<string>();
            List<string> specificEffectClass = new List<string>();
            List<string> effectParameter = new List<string>();
            List<string> specificCostPay = new List<string>();
            List<string> costPayParameter = new List<string>();
            List<string> baseEffParameter = new List<string>();

            ParseKeyValuePairs(specificCostClass, costParameter, str1.Split(':'), card.name, "cost");
            ParseKeyValuePairs(specificEffectClass, effectParameter, str2.Split(':'), card.name, "efftype");
            ParseKeyValuePairs(specificCostPay, costPayParameter, str3.Split(':'), card.name, "costpay");

            string[] baseEffPar = str4.Split('&');
            for (int j = 0; j < baseEffPar.Length; j++)
            {
                baseEffParameter.Add(baseEffPar[j] ?? "");
            }

            card.GetEffection(new StaticEff(specificCostClass, costParameter, specificEffectClass, effectParameter, specificCostPay, costPayParameter, baseEffParameter));
        }
    }

    // 读取字段并去首尾空格;null或空统一返回空字符串
    private string SafeField(CsvReader csv, int index, string cardName)
    {
        string field;
        try
        {
            field = csv.GetField(index) ?? "";
        }
        catch (System.Exception)
        {
            return "";
        }
        return field.Trim();
    }

    // 解析"类型:参数"成对配置;空字段(该列无配置,合法)直接跳过;非空奇数段视为配置不完整,多余段丢弃并告警
    private void ParseKeyValuePairs(List<string> keys, List<string> values, string[] parts, string cardName, string columnName)
    {
        if (parts == null || parts.Length == 0)
            return;
        if (parts.Length == 1 && parts[0].Length == 0)      //空列:该类型无条目,合法,不告警
            return;
        for (int j = 0; j + 1 < parts.Length; j += 2)
        {
            keys.Add(parts[j] ?? "");
            values.Add(parts[j + 1] ?? "");
        }
    } 
}

