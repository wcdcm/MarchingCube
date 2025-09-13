using UnityEditor;
using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public float speed = 10f;         // 移动速度
    public float rotationSpeed = 2f;  // 旋转速度
    public float minVerticalAngle = -60f; // 最小垂直角度
    public float maxVerticalAngle = 60f;  // 最大垂直角度
    
    private float currentVerticalAngle;
    private bool isRotating = false;  // 是否正在旋转

    void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
        currentVerticalAngle = transform.localEulerAngles.x;
    }

    void Update()
    {
        CameraMovement();
        CameraRotation();
    }

    private void CameraMovement()
    {
        if (Input.GetKey(KeyCode.W))
        {
            this.transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S))
        {
            this.transform.Translate(Vector3.back * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.A))
        {
            this.transform.Translate(Vector3.left * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D))
        {
            this.transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.Q))
        {
            this.transform.Translate(Vector3.up * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.E))
        {
            this.transform.Translate(Vector3.down * speed * Time.deltaTime);
        }
    }

    private void CameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

        // 左右旋转（绕Y轴）
        transform.Rotate(Vector3.up * mouseX);

        // 上下旋转（限制角度范围）
        currentVerticalAngle -= mouseY;
        currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);

        // 保持当前Y轴旋转角度，只修改X轴旋转角度
        Vector3 currentEulerAngles = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(currentVerticalAngle, currentEulerAngles.y, 0);
        
    }
}