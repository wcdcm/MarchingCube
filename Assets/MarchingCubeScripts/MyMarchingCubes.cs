using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyMarchingCubes : MonoBehaviour
{
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 10;

    float resolution = 1f;
    //[SerializeField] float noiseScale = 0.1f;

    [SerializeField] [Range(0,1f)]private float heightThreshold = 0.5f; // 等值面阈值

    [SerializeField] bool visualizeNoise;
    [SerializeField] bool use3DNoise;

    private float[,,] heights; // 密度场数据
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector3> normals = new List<Vector3>(); // 顶点法线
    private List<Vector2> uvs = new List<Vector2>(); // 新增：存储UV坐标

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer; 
    
    // 球体参数
    [SerializeField] float sphereRadius = 5f;
    private Vector3 sphereCenter = new Vector3();
    
    void Start()
    {
        // 初始化球心（确保在网格范围内）
        sphereCenter = new Vector3(sphereRadius, sphereRadius, sphereRadius);
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // 使用支持法线的材质
        if (meshRenderer != null && meshRenderer.material == null)
        {
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }
        
        StartCoroutine(TestAll());
    }

    private IEnumerator TestAll()
    {
        while (true)
        {
            SetHeights(); // 生成球体密度场
            MarchCubes(); // 生成网格
            SetMesh(); // 应用网格
            yield return new WaitForSeconds(1f);
        }
    }

    private void SetMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray(); // 应用手动计算的法线（关键：不覆盖）
        mesh.uv = uvs.ToArray();
        mesh.RecalculateBounds(); // 重新计算边界
        meshFilter.mesh = mesh;
    }

    // 生成球体的密度场（核心：定义球体的等值面）
    private void SetHeights()
    {
        heights = new float[width + 1, height + 1, width + 1];
        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    // 计算当前点到球心的距离
                    Vector3 point = new Vector3(x, y, z);
                    float distance = Vector3.Distance(point, sphereCenter);
                    // 密度值：球内（距离 < 半径）密度高，球外密度低
                    // 当距离 = 半径时，密度 = 0.5（与阈值匹配，形成球面）
                    heights[x, y, z] = 1.0f - (distance / sphereRadius);
                }
            }
        }
    }
    
    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();
        normals.Clear();
        uvs.Clear();

        // 遍历每个小立方体
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    // 获取当前立方体8个顶点的密度值
                    float[] cubeCorners = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
                        cubeCorners[i] = heights[corner.x, corner.y, corner.z];
                    }
                    // 处理当前立方体
                    MarchCube(new Vector3(x, y, z), cubeCorners);
                }
            }
        }
    }

    // 处理单个立方体：根据密度值生成三角形（核心优化点）
    private void MarchCube(Vector3 position, float[] cubeCorners)
    {
        // 获取立方体的配置（哪些顶点在等值面内/外）
        int configIndex = GetConfigIndex(cubeCorners);
        if (configIndex == 0 || configIndex == 255) // 完全在内部或外部，不生成三角形
        {
            return;
        }

        // 缓存边的交点（避免重复计算，关键：平滑的核心）
        Vector3[] edgeVertices = new Vector3[12]; // 12条边，存储每条边的交点
        bool[] isEdgeCalculated = new bool[12]; // 标记边是否已计算

        int edgeIndex = 0;
        
        for (int t = 0; t < 5; t++)
        {
            for (int v = 0; v < 3; v++)
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];
                if (triTableValue == -1) 
                {
                    return;
                }

                // 如果边未计算，则计算交点（基于阈值和顶点密度插值）
                if (!isEdgeCalculated[triTableValue])
                {
                    // 获取边的两个顶点（立方体的顶点）
                    Vector3 edgeStart = position + MarchingTable.Edges[triTableValue, 0];
                    Vector3 edgeEnd = position + MarchingTable.Edges[triTableValue, 1];
                    
                    
                    Vector3 vertex = (edgeStart + edgeEnd) * 0.5f;
                    edgeVertices[triTableValue] = vertex;
                    isEdgeCalculated[triTableValue] = true; // 标记已计算
                }

                // 添加顶点和法线
                Vector3 currentVertex = edgeVertices[triTableValue];
                vertices.Add(currentVertex);
                // 法线：从球心指向顶点（球体的法线天然平滑）
                normals.Add((currentVertex - sphereCenter).normalized);
                
                // 核心：计算当前顶点的UV坐标（球面映射）
                Vector2 uv = CalculateSphereUV(currentVertex);
                uvs.Add(uv);
                
                // 添加三角形索引
                triangles.Add(vertices.Count - 1);

                edgeIndex++;
            }
        }
    }
    
    // 核心方法：将球体表面顶点转换为UV坐标
    private Vector2 CalculateSphereUV(Vector3 vertex)
    {
        // 1. 计算顶点相对于球心的方向向量（归一化，消除半径影响）
        Vector3 dir = (vertex - sphereCenter).normalized;

        // 2. 将方向向量转换为球面坐标（方位角和极角）
        // 方位角（绕Y轴旋转）：范围 [-π, π] → 映射到 [0, 1]（U坐标）
        float azimuth = Mathf.Atan2(dir.x, dir.z); // Atan2(x,z)：避免Z=0时的问题
        // 转换为 [0, 2π] 范围
        if (azimuth < 0)
            azimuth += 2 * Mathf.PI;
        float u = azimuth / (2 * Mathf.PI); // U：0~1（横向环绕）

        // 极角（绕X轴旋转）：范围 [0, π] → 映射到 [0, 1]（V坐标）
        float polar = Mathf.Acos(Mathf.Clamp(dir.y, -1f, 1f)); // 范围 0~π（从北极到南极）
        float v = polar / Mathf.PI; // V：0~1（纵向从上到下）

        // 3. 返回UV坐标（可根据需求翻转V，避免贴图上下颠倒）
        return new Vector2(u, v);
    }
    
    private int GetConfigIndex(float[] cubeCorners)
    {
        int configIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cubeCorners[i] > heightThreshold)
            {
                configIndex |= 1 << i;
            }
        }
        return configIndex;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!visualizeNoise || !Application.isPlaying)
        {
            return;
        }
        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    Gizmos.color = new Color(heights[x, y, z], heights[x, y, z], heights[x, y, z], 1);
                    Gizmos.DrawSphere(new Vector3(x * resolution, y * resolution, z * resolution), 0.2f * resolution);
                }
            }
        }
    }
}