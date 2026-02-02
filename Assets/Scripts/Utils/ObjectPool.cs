using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : Singleton<ObjectPool>
{
  #region 私有字段
  private Dictionary<string, Queue<GameObject>> m_ObjectPool = new Dictionary<string, Queue<GameObject>>();
  #endregion
  #region 公共方法
  public GameObject GetObject(GameObject _prefab, bool autoActive = true)
  {
    GameObject obj;
    if (!m_ObjectPool.ContainsKey(_prefab.name) || m_ObjectPool[_prefab.name].Count == 0)
    {
      obj = Instantiate(_prefab);
      PushObject(obj);

      Transform childPoolTrans = Instance.transform.Find(_prefab.name + "Pool");
      GameObject childPool = childPoolTrans != null ? childPoolTrans.gameObject : null;
      if (!childPool)
      {
        childPool = new GameObject(_prefab.name + "Pool");
        childPool.transform.SetParent(Instance.transform);
      }
      obj.transform.SetParent(childPool.transform);
    }
    obj = m_ObjectPool[_prefab.name].Dequeue();
    if (!obj)
      Debug.LogWarning("WTF:ObjectPool get a null object!");
    if (autoActive)
      obj.SetActive(true);
    return obj;
  }

  public GameObject GetObject(GameObject _prefab, Vector3 _position, Quaternion _rotation, bool autoActive = true)
  {
    GameObject obj = GetObject(_prefab, autoActive);
    obj.transform.position = _position;
    obj.transform.rotation = _rotation;
    return obj;
  }


  public void PushObject(GameObject _prefab)
  {
    string name = _prefab.name.Replace("(Clone)", string.Empty);
    if (!m_ObjectPool.ContainsKey(name))
      m_ObjectPool.Add(name, new Queue<GameObject>());
    if (_prefab.activeSelf)
      m_ObjectPool[name].Enqueue(_prefab);
    _prefab.SetActive(false);
  }
  #endregion
}
