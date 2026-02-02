using System.Collections;
using UnityEngine;

/// <summary>
/// Bowl 缩小控制器，负责在加工完成后让 bowl 执行缩小动画回到初始状态，但不销毁 bowl 对象
/// </summary>
public class BowlShrinkController : MonoBehaviour
{
    private Vector3 targetPosition;
    private float targetScale;
    private float animationDuration;
    private AnimationCurve positionCurve;
    private AnimationCurve scaleCurve;

    private Vector3 startPosition;
    private float startScale;
    private Coroutine shrinkCoroutine;

    /// <summary>
    /// 初始化缩小控制器
    /// </summary>
    /// <param name="targetPos">目标位置（初始位置）</param>
    /// <param name="targetScl">目标缩放（初始缩放）</param>
    /// <param name="duration">动画时长</param>
    /// <param name="posCurve">位置动画曲线</param>
    /// <param name="sclCurve">缩放动画曲线</param>
    public void Initialize(Vector3 targetPos, float targetScl, float duration, AnimationCurve posCurve, AnimationCurve sclCurve)
    {
        targetPosition = targetPos;
        targetScale = targetScl;
        animationDuration = duration;
        positionCurve = posCurve;
        scaleCurve = sclCurve;

        // 记录当前状态作为起始状态
        startPosition = transform.position;
        startScale = transform.localScale.x; // 等比例缩放，取 x 值

        // 开始缩小动画
        shrinkCoroutine = StartCoroutine(ShrinkAnimation());
    }

    /// <summary>
    /// 缩小动画协程
    /// </summary>
    private IEnumerator ShrinkAnimation()
    {
        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / animationDuration);

            // 使用曲线计算插值
            float positionT = positionCurve.Evaluate(normalizedTime);
            float scaleT = scaleCurve.Evaluate(normalizedTime);

            // 插值位置和缩放
            transform.position = Vector3.Lerp(startPosition, targetPosition, positionT);
            float currentScale = Mathf.Lerp(startScale, targetScale, scaleT);
            transform.localScale = Vector3.one * currentScale;

            yield return null;
        }

        // 确保最终状态精确
        transform.position = targetPosition;
        transform.localScale = Vector3.one * targetScale;

        // 动画完成后移除控制器组件，但保留 bowl 对象继续存在
        Destroy(this);
    }

    /// <summary>
    /// 强制完成动画（用于清理）
    /// </summary>
    public void ForceComplete()
    {
        if (shrinkCoroutine != null)
        {
            StopCoroutine(shrinkCoroutine);
        }

        transform.position = targetPosition;
        transform.localScale = Vector3.one * targetScale;

        // 强制完成后移除控制器组件，但保留 bowl 对象
        Destroy(this);
    }
}
