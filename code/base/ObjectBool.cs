using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private int maxSize;
    private Stack<GameObject> pool;
    private GameObject prefab;
    private Transform parent;

    public ObjectPool(GameObject prefab, int initialSize,Transform parent=null)
    {
        maxSize = initialSize;
        pool = new Stack<GameObject>(initialSize);
        this.prefab = prefab;
        this.parent = parent;
        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = GameObject.Instantiate(prefab,parent);
            obj.SetActive(false);
            pool.Push(obj);
        }
    }

    public GameObject Get()
    {
        if (pool.Count > 0)
        {
            GameObject obj = pool.Pop();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            GameObject obj = GameObject.Instantiate(prefab, parent);
            return obj;
        }
    }

    public void Release(GameObject obj)
    {
        // 去重:空对象直接返回(否则对null调SendMessage会抛异常);已在池里的对象也必须直接返回,
        // 否则同一物体被重复入池,之后Get()会把同一个GameObject同时发给两张卡,表现为卡牌UI错乱/消失。
        if (obj == null || pool.Contains(obj)) return;
        obj.SendMessage("OnReturnToPool", SendMessageOptions.DontRequireReceiver);
        obj.SetActive(false);
        pool.Push(obj);
    }


    public void Clear()
    {
        while (pool.Count > 0)
        {
            GameObject obj = pool.Pop();
            GameObject.Destroy(obj);
        }
    }
}