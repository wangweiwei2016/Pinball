using UnityEngine;

namespace DefaultNamespace.MoveTable
{
    /// <summary>
    /// 挡板组件：跟随父物体移动
    /// </summary>
    public class BarrierComponent : MonoBehaviour
    {
        private Transform parentTransform;
    
        public void SetOwner(Transform parent)
        {
            parentTransform = parent;
        }
    
        private void Update()
        {
            if (parentTransform != null)
            {
                transform.position = parentTransform.position + transform.localPosition;
            }
        }
    }
}