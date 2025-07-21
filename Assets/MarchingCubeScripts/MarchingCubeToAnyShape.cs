using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubeToAnyShape : MonoBehaviour
{
     [SerializeField] private int width = 30;
    [SerializeField] private int height = 30;

    //float resolution = 0.1f;
    [SerializeField] float noiseScale = 0.1f;

    [SerializeField] [Range(0,1f)]private float heightThreshold = 0.5f;

    [SerializeField] bool visualizeNoise;
    [SerializeField] bool use3DNoise;

    // 新增：笔刷参数
    [Header("笔刷设置")]
    [SerializeField] private float brushRadius = 2f; // 笔刷半径（世界单位）
    [SerializeField] private float brushStrength = 0.4f; // 笔刷强度（正值凸起，负值凹陷）
    [SerializeField] private KeyCode brushKey = KeyCode.Mouse0; // 激活键（鼠标左键）
    [SerializeField] private LayerMask terrainLayer; // 地形检测层

    // 新增：记录笔刷点击位置（世界坐标）
    [SerializeField] private Vector3? brushWorldPos;
    private Vector3 brushGridPos;

    private float[,,] heights;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    private MeshFilter meshFilter;
    private Dictionary<long, int> vertexCache;

    private MeshCollider meshCollider;
    private Mesh mesh;

    // [Header("Test:")] 
    // public GameObject sphere;
    // public Vector3 instancePos;
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        vertexCache = new Dictionary<long, int>();
        StartCoroutine(TestAll());
    }

    void Update()
    {
        // 检测鼠标点击并记录笔刷位置（仅在点击地形时生效）
        if (Input.GetKeyDown(brushKey))
        {
            print("hit!");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayer))
            {
                brushWorldPos = hit.point; // 记录点击的世界位置
            }
        }
    }

    private IEnumerator TestAll()
    {
        while (true)
        {
            SetHeights();
            MarchCubes();
            SetMesh();
            yield return new WaitForSeconds(0.1f); // 缩短更新间隔，笔刷反馈更及时
        }
    }

    private void SetMesh()
    {
        mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        vertexCache.Clear();
        mesh.normals = CalculateSmoothNormals(mesh.vertices, mesh.triangles);
        meshCollider.sharedMesh = mesh;
        meshFilter.mesh = mesh;
    }

    private Vector3[] CalculateSmoothNormals(Vector3[] vertice, int[] triangle)
    {
        Vector3[] normals = new Vector3[vertice.Length];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.zero;
        
        for (int i = 0; i < triangle.Length; i += 3)
        {
            int i1 = triangle[i];
            int i2 = triangle[i + 1];
            int i3 = triangle[i + 2];
            Vector3 v1 = vertice[i1];
            Vector3 v2 = vertice[i2];
            Vector3 v3 = vertice[i3];
            Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
            normals[i1] += normal;
            normals[i2] += normal;
            normals[i3] += normal;
        }
        
        for (int i = 0; i < normals.Length; i++) normals[i].Normalize();
        return normals;
    }


    private void SetHeights()
    {
        heights = new float[width + 1, height + 1, width + 1];

        // 计算笔刷影响范围（网格坐标，避免每帧重复计算）
        int brushGridRadius = 0;
        brushGridPos = Vector3.zero;
        bool hasBrush = false;

        if (brushWorldPos.HasValue)
        {
            // 世界坐标转网格坐标（x/z对应width，y对应height）
            brushGridPos = new Vector3(
                brushWorldPos.Value.x,
                brushWorldPos.Value.y,
                brushWorldPos.Value.z
            );
            
            brushGridRadius = Mathf.CeilToInt(brushRadius); // 网格半径
            hasBrush = true;
        }

        // 生成原始噪声标量场
        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    // 原始噪声计算（保持不变）
                    float originalValue;
                    
                    
                    if (use3DNoise)
                    {
                        originalValue = PerlinNoise3D(
                            (float)x / width * noiseScale, 
                            (float)y / height * noiseScale, 
                            (float)z / width * noiseScale);
                    }
                    else
                    {
                        float currentHeight = height * Mathf.PerlinNoise(x * noiseScale, z * noiseScale);
                        if (y <= currentHeight - 0.5f) originalValue = 0f;
                        else if (y > currentHeight + 0.5f) originalValue = 1f;
                        else if (y > currentHeight) originalValue = y - currentHeight;
                        else originalValue = currentHeight - y;
                    }

                    // 笔刷修改：仅在有点击且在影响范围内时生效
                    if (hasBrush)
                    {
                         // 计算当前网格点到笔刷中心的网格距离
                         float dx = x - brushGridPos.x;
                         float dy = y - brushGridPos.y;
                         float dz = z - brushGridPos.z;
                         float gridDistance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                        
                         // 在笔刷范围内才修改
                         if (gridDistance <= brushGridRadius)
                         {
                             // 平滑衰减（核心：避免硬边，用二次曲线+平滑过渡）
                             float normalizedDist = gridDistance / brushGridRadius;
                             float influence = 1 - normalizedDist * normalizedDist; // 二次衰减
                             influence = Mathf.SmoothStep(0, 1, influence); // 边缘更平滑
                        
                             // 应用笔刷（基于原始噪声叠加）
                             originalValue += brushStrength * influence;
                             
                             // 限制值在0-1之间（避免超出标量场合理范围）
                             originalValue = Mathf.Clamp01(originalValue);
                         }
                    }

                    // 最终标量值（原始噪声+笔刷修改）
                    heights[x, y, z] = originalValue;
                }
            }
        }
    }

    private float PerlinNoise3D(float x, float y, float z)
    {
        float xyz = Mathf.PerlinNoise(x + y, z + y);
        float yxz = Mathf.PerlinNoise(y + x, z + x);
        return (xyz + yxz) / 2;
    }

    // 以下方法保持不变
    private void MarchCubes()
    {
        vertices.Clear();
        triangles.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    float[] cubeCorners = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
                        cubeCorners[i] = heights[corner.x, corner.y, corner.z];
                    }
                    MarchCube(new Vector3Int(x, y, z), cubeCorners);
                }
            }
        }
    }

    [SerializeField] private bool isUseSmoothness = false;
    private void MarchCube(Vector3Int position, float[] cubeCorners)
    {
        int configIndex = GetConfigIndex(cubeCorners);
        if (configIndex == 0 || configIndex == 255) return;

        int edgeIndex = 0;
        for (int t = 0; t < 5; t++)
        {
            for (int v = 0; v < 3; v++)
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];
                if (triTableValue == -1) return;

                Vector3Int edgeStart = position + MarchingTable.Edges[triTableValue, 0];
                Vector3Int edgeEnd = position + MarchingTable.Edges[triTableValue, 1];
                
                Vector3 vertex = (edgeStart + edgeEnd) / 2;
                if (isUseSmoothness)
                {
                    float edgeStartValue = heights[edgeStart.x, edgeStart.y, edgeStart.z];
                    float edgeEndValue = heights[edgeEnd.x, edgeEnd.y, edgeEnd.z];
                    vertex = InterpolateEdgePosition(heightThreshold, edgeStart, edgeStartValue, edgeEnd, edgeEndValue);
                }

                int vertexIndex = GetVertexIndex(vertex);
                triangles.Add(vertexIndex);
                edgeIndex++;
            }
        }
    }

    private int GetVertexIndex(Vector3 vertex)
    {
        long key = GetVertexKey(vertex);
        if (vertexCache.TryGetValue(key, out int index)) return index;
        index = vertices.Count;
        vertices.Add(vertex);
        vertexCache[key] = index;
        return index;
    }

    private long GetVertexKey(Vector3 vertex)
    {
        int x = Mathf.RoundToInt(vertex.x * 1000);
        int y = Mathf.RoundToInt(vertex.y * 1000);
        int z = Mathf.RoundToInt(vertex.z * 1000);
        return (long)x << 40 | (long)y << 20 | z;
    }

    private Vector3 InterpolateEdgePosition(float threshold, Vector3 vertex1, float value1, Vector3 vertex2, float value2)
    {
        if (Mathf.Approximately(threshold - value1, 0)) return vertex1;
        if (Mathf.Approximately(threshold - value2, 0)) return vertex2;
        if (Mathf.Approximately(value1 - value2, 0)) return vertex1;

        float mu = (threshold - value1) / (value2 - value1);
        return new Vector3(
            vertex1.x + mu * (vertex2.x - vertex1.x),
            vertex1.y + mu * (vertex2.y - vertex1.y),
            vertex1.z + mu * (vertex2.z - vertex1.z)
        );
    }

    private int GetConfigIndex(float[] cubeCorners)
    {
        int configIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cubeCorners[i] > heightThreshold)
                configIndex |= 1 << i;
        }
        return configIndex;
    }

    // private void OnDrawGizmosSelected()
    // {
    //     if (!visualizeNoise || !Application.isPlaying) return;
    //
    //     // 可视化标量场（原有逻辑）
    //     for (int x = 0; x < width + 1; x++)
    //     {
    //         for (int y = 0; y < height + 1; y++)
    //         {
    //             for (int z = 0; z < width + 1; z++)
    //             {
    //                 Gizmos.color = new Color(heights[x, y, z], heights[x, y, z], heights[x, y, z], 1);
    //                 Gizmos.DrawSphere(new Vector3(x * resolution, y * resolution, z * resolution), 0.2f * resolution);
    //             }
    //         }
    //     }
    //
    //     // 可视化笔刷位置
    //     if (brushWorldPos.HasValue)
    //     {
    //         Gizmos.color = Color.green;
    //         Gizmos.DrawWireSphere(brushWorldPos.Value, brushRadius);
    //     }
    // }
}
