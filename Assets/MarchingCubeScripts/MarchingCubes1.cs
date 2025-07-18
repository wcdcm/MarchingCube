using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes1 : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    // 网格尺寸：宽度和高度（Z 轴使用与 X 相同的宽度）
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 10;

    // 每个体素单元格的单位尺寸（用于坐标缩放）
    float resolution = 1;

    // 控制噪声的尺度（值越大，变化越缓慢）
    [SerializeField] float noiseScale = 1;

    // 判断是否为“地形表面”的阈值
    [SerializeField] private float heightTresshold = 0.5f;

    // 是否可视化密度值点
    [SerializeField] bool visualizeNoise;

    // 是否使用 3D 噪声（否则为 2D 高度图）
    [SerializeField] bool use3DNoise;

    // 网格数据缓存：顶点和三角形索引
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    // 三维密度场（体素格点的标量值）
    private float[,,] heights;

    // MeshFilter 引用，用于设置最终生成的 Mesh
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    void Start()
    {
        mainCamera = Camera.main;
        meshCollider = GetComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();
        SetCustomHeight();
        StartCoroutine(TestAll()); // 启动网格动态更新协程
    }

    void Update()
    {
        UpdateHeights();
    }

    // 协程：每秒重新计算一次网格
    private IEnumerator TestAll()
    {
        while (true)
        {
            //SetHeights();   // 生成密度场
            
            MarchCubes();   // 执行 Marching Cubes 算法
            SetMesh();      // 更新 Mesh 数据
            yield return new WaitForSeconds(0.1f); // 固定间隔刷新一次
        }
    }

    
    // 将生成的顶点和三角形赋值给 Mesh
    private void SetMesh()
    {
        Mesh mesh = new Mesh();
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals(); // 自动计算法线以便光照正确

        meshFilter.mesh = mesh;
        // 先清空旧碰撞体，防止缓存
        meshCollider.sharedMesh = null;
        // 再设置新的碰撞体
        meshCollider.sharedMesh = mesh;
    }
    
    
    [SerializeField] int radius = 1; // 控制立方体高度
    private void SetCustomHeight()
    {
        heights = new float[width + 1, height + 1, width + 1];
        Vector3 center = new Vector3(width / 2, height / 2, width / 2);
        float radiusSqr = radius * radius;
        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    Vector3 point = new Vector3(x, y, z);
                    float distSqr = (point - center).sqrMagnitude;

                    //heights[x, y, z] = distSqr < radiusSqr ? 0f : 1f;
                    // if (x == 0 && y == 0 && z == 0) heights[x, y, z] = 0f;
                    // else heights[x, y, z] = 1f;
                    if (y == 1) heights[x, y, z] = 0f;
                    else heights[x, y, z] = 1f;
                }
            }
        }
    }
    
    // 根据 cube 8 个角点的密度值判断配置索引（0~255）
    private int GetConfigIndex (float[] cubeCorners)
    {
        int configIndex = 0;

        for (int i = 0; i < 8; i++)
        {
            if (cubeCorners[i] > heightTresshold)
            {
                configIndex |= 1 << i;
            }
        }

        return configIndex;
    }

    // 遍历整个体积，对每个小立方体执行 MarchCube
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

                    // 获取当前立方体 8 个角点的密度值
                    for (int i = 0; i < 8; i++)
                    {
                        //得到当前 cube 的 第 i 个角点在整个标量场中的真实索引位置
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
                        //遍历 cube 时，cube 的 8 个角点的位置（也就是空间中的坐标）正是由构建好的标量场 heights[x, y, z] 中的这些格点组成的
                        cubeCorners[i] = heights[corner.x, corner.y, corner.z];
                    }

                    // 对当前 cube 执行三角化
                    MarchCube(new Vector3(x, y, z), cubeCorners);
                }
            }
        }
    }

    // 根据配置索引查表生成三角形
    private void MarchCube (Vector3 position, float[] cubeCorners)
    {
        //得到一个0-255的索引
        int configIndex = GetConfigIndex(cubeCorners);

        // 跳过空 cube 或满 cube
        if (configIndex == 0 || configIndex == 255)
        {
            return;
        }

        int edgeIndex = 0;

        // 一个配置最多生成 5 个三角形（15 个顶点）
        for (int t = 0; t < 5; t++)
        {
            for (int v = 0; v < 3; v++) // 一个三角形三个点
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex]; //拿到索引对应的三角形的边
                // 只要还能形成三角形就一定不是-1
                if (triTableValue == -1)
                {
                    return;
                }

                // 取出该边的起点和终点，使用中点作为三角形顶点
                Vector3 edgeStart = position + MarchingTable.Edges[triTableValue, 0];
                Vector3 edgeEnd = position + MarchingTable.Edges[triTableValue, 1];

                Vector3 vertex = (edgeStart + edgeEnd) / 2;
                
                vertices.Add(vertex);
                triangles.Add(vertices.Count - 1);

                edgeIndex++;
            }
        }
    }

    private void UpdateHeights()
    {
        if (Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 direction = transform.InverseTransformDirection(ray.direction.normalized);

                // 世界坐标转物体本地坐标
                Vector3 localPoint = transform.InverseTransformPoint(hit.point);
                Vector3 scaledLocalPoint = new Vector3(
                    localPoint.x / transform.localScale.x,
                    localPoint.y / transform.localScale.y,
                    localPoint.z / transform.localScale.z
                );
                
                //物体本地坐标转标量场索引
                Vector3Int index = new Vector3Int(
                    Mathf.FloorToInt(localPoint.x),
                    Mathf.FloorToInt(localPoint.y),
                    Mathf.FloorToInt(localPoint.z)
                    );
                
                heights[index.x, index.y + 1, index.z] = 0f;
                Debug.Log(index + " " + direction);
            }
        }
    }
    
    // 在场景中可视化每个点的密度值（仅编辑器中显示）
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
                    float val = heights[x, y, z];
                    Gizmos.color = new Color(val, val, val, 1); // 用灰度显示密度值
                    Gizmos.DrawSphere(new Vector3(x * resolution, y * resolution, z * resolution), 0.2f * resolution);
                }
            }
        }
    }
}
