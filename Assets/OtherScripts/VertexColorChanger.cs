using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VertexColorChanger : MonoBehaviour
{
    private static VertexColorChanger _instance;
    public static VertexColorChanger Instance => _instance;

    [Tooltip("顶点颜色变化的影响半径")] public float radius = 0.5f;

    [Tooltip("颜色变化的持续时间（秒），设为0则永久变化")] public float colorDuration = 2.0f;

    //public Mesh mesh;
    public Vector3[] vertices;
    private Color[] originalColors;
    private Color[] currentColors;
    private float[] colorChangeTimes;

    private void Awake()
    {
        _instance = this;
    }

    void Start()
    {
        // 获取并存储网格数据
        //mesh = GetComponent<MeshFilter>().mesh;
        //vertices = mesh.vertices;
    }

    public Mesh InitializeColor(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        
        // 初始化颜色数组

        originalColors = new Color[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            originalColors[i] = Color.red;
            print("初始化颜色数组");
        }

        mesh.colors = originalColors;
        return mesh;
    }

    void Update()
    {
        
    }

    public void HandleColorChange(ref Mesh mesh)
    {
        // 处理鼠标点击
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
            {
                ChangeVertexColors(hit.point,mesh);
            }
        }

        // 处理颜色恢复（如果设置了持续时间）
        if (colorDuration > 2)
        {
            UpdateColorFade();
        }
    }

    public void ChangeVertexColors(Vector3 hitPoint,Mesh mesh)
    {
        if (mesh == null) return;
        // 将世界坐标转换为局部坐标
        Vector3 localHitPoint = transform.InverseTransformPoint(hitPoint);

        // 遍历所有顶点，检查与点击位置的距离
        for (int i = 0; i < vertices.Length; i++)
        {
            print("ChangeVertexColors");
            float distance = Vector3.Distance(vertices[i], localHitPoint);

            if (distance <= radius)
            {
            // 计算影响权重（距离越近，红色越明显）
            float weight = 1.0f - Mathf.Clamp01(distance / radius);

            // 设置顶点颜色
            currentColors[i] = Color.Lerp(originalColors[i], Color.red, weight);
            colorChangeTimes[i] = Time.time;
            }
        }
        // 应用颜色更改
        mesh.colors = currentColors;
    }

    void UpdateColorFade()
    {
        bool needsUpdate = false;

        for (int i = 0; i < currentColors.Length; i++)
        {
            if (colorChangeTimes[i] > 0)
            {
                float elapsedTime = Time.time - colorChangeTimes[i];

                if (elapsedTime < colorDuration)
                {
                    // 渐变为原始颜色
                    float t = elapsedTime / colorDuration;
                    currentColors[i] = Color.Lerp(Color.red, originalColors[i], t);
                    needsUpdate = true;
                }
                else if (currentColors[i] != originalColors[i])
                {
                    // 恢复为原始颜色
                    currentColors[i] = originalColors[i];
                    colorChangeTimes[i] = 0;
                    needsUpdate = true;
                }
            }
        }

        // 如果有变化，更新网格颜色
        if (needsUpdate)
        {
            //mesh.colors = currentColors;
        }
    }
}