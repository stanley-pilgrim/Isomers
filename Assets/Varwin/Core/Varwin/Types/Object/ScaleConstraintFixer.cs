using System.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace Varwin
{
    /* ScaleConstraint в Unity 2018 в следующий после создания кадр добавляет непонятно откуда взятую дробную часть
     * к значениям скейла, не равным степени двойки. Этот класс последующим кадром пытается компенсировать ситуацию.
     * В Unity 2019+ такого поведения не наблюдается, и при обновлении этот костыль должен быть удален.*/
    public class ScaleConstraintFixer : MonoBehaviour
    {
        private static ScaleConstraintFixer _fixer;

        private void Awake()
        {
            _fixer = this;
        }

        public static void FixScaleConstraint(Transform baseTransform, Transform targetTransform)
        {
            _fixer.StartCoroutine(FixScaleConstraintRoutine(baseTransform, targetTransform));
        }

        private static IEnumerator FixScaleConstraintRoutine(Transform baseTransform, Transform targetTransform)
        {
            var constraint = baseTransform.GetComponent<ScaleConstraint>();
            if (!constraint)
            {
                yield break;
            }

            yield return null;

            constraint.constraintActive = false;

            Vector3 baseScale = baseTransform.localScale;
            Vector3 targetScale = targetTransform.lossyScale;

            constraint.scaleOffset = new Vector3(
                targetScale.x / baseScale.x,
                targetScale.y / baseScale.y,
                targetScale.z / baseScale.z);

            constraint.constraintActive = true;
        }
    }
}