using UnityEngine;

namespace DefaultNamespace.MoveTable
{
    public class TargetTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Const.TAG_BALL) || other.GetComponent<Ball>() != null)
            {
                Destroy(other.gameObject);
            }
        }
    }
}