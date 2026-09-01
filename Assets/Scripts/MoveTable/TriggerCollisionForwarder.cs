using UnityEngine;

namespace DefaultNamespace.MoveTable
{
    /// <summary>
    /// 触发器碰撞转发器
    /// </summary>
    public class TriggerCollisionForwarder : MonoBehaviour
    {
        private BarTrigger owner;
    
        public void SetOwner(BarTrigger trigger)
        {
            owner = trigger;
        }
    
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner != null && (other.CompareTag(Const.TAG_BALL) || other.GetComponent<Ball>() != null))
            {
                owner.OnTriggerEnter();
            }
        }
    
        private void OnTriggerStay2D(Collider2D other)
        {
            if (owner != null && (other.CompareTag(Const.TAG_BALL) || other.GetComponent<Ball>() != null))
            {
                owner.OnTriggerStay();
            }
        }
    }
}