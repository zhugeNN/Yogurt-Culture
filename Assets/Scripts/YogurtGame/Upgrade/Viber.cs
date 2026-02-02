using UnityEngine;

/// <summary>
/// 搅拌棒类，实现可升级物品接口
/// </summary>
public class Viber : IUpgradeItem
{
    /// <summary>
    /// 实现升级方法，目前内容留空
    /// </summary>
    public void Upgrade(string data)
    {
        Debug.Log("viber upgrade");
        ProgressController.Instance.OnIngredientCreated += (i) => {
            if(i is NormalYogurt){
                (i as NormalYogurt).SetDrag(6);
            }
        };
    }
}
