using UnityEngine;

/// <summary>
/// 弹珠台挡板：左右各一个，按住按键向台面内挥动（向上抬起）。
/// 通过 HingeJoint2D 的马达实现，松开后回到初始角度。
/// 用法：将本脚本挂在挡板（一个带 BoxCollider2D + Rigidbody2D 的精灵）上。
///       并把 HingeJoint2D 的 Anchor（轴心）放在挡板靠近桌面中心一侧的端点。
/// </summary>
[RequireComponent(typeof(HingeJoint2D))]
public class Flipper : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("挡板配置")]
    public Side side = Side.Left;

    [Tooltip("按下按键时的目标角度（相对初始角度的度数，正值为向台面内挥）。")]
    public float activeAngle = 45f;

    [Tooltip("松开按键时回到的初始角度。")]
    public float restAngle = 0f;

    [Tooltip("挥动时马达的目标速度（度/秒）。")]
    public float motorSpeed = 1500f;

    [Tooltip("松开回位时马达的目标速度（度/秒）。")]
    public float returnSpeed = 800f;

    [Header("按键")]
    [Tooltip("如果留空，会根据 side 自动选择 LeftArrow / RightArrow。")]
    public KeyCode key = KeyCode.None;

    private HingeJoint2D hinge;
    private JointMotor2D motor;

    private void Awake()
    {
        hinge = GetComponent<HingeJoint2D>();
        hinge.useMotor = true;

        if (key == KeyCode.None)
        {
            key = side == Side.Left ? KeyCode.LeftArrow : KeyCode.RightArrow;
        }

        motor = hinge.motor;
        motor.maxMotorTorque = 10000f; // 保证能甩动球
        ApplyRestMotor();
    }

    private void Update()
    {
        if (Input.GetKey(key))
        {
            ApplyActiveMotor();
        }
        else
        {
            ApplyRestMotor();
        }
    }

    private void ApplyActiveMotor()
    {
        float dir = Mathf.Sign(activeAngle - restAngle);
        if (dir == 0f) dir = 1f;
        motor.motorSpeed = dir * motorSpeed;
        hinge.motor = motor;
    }

    private void ApplyRestMotor()
    {
        float dir = Mathf.Sign(restAngle - activeAngle);
        if (dir == 0f) dir = -1f;
        motor.motorSpeed = dir * returnSpeed;
        hinge.motor = motor;
    }

    private void OnDrawGizmosSelected()
    {
        // 标出铰链轴位置，方便在 Scene 视图里调试
        if (hinge == null) hinge = GetComponent<HingeJoint2D>();
        if (hinge != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 anchorWorld = transform.TransformPoint(hinge.anchor);
            Gizmos.DrawSphere(anchorWorld, 0.08f);
        }
    }
}
