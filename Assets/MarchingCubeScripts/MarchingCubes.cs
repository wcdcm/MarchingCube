using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 30;

    float resolution = 0.1f;
    [SerializeField] float noiseScale = 0.1f;

    [SerializeField] [Range(0,1f)]private float heightThreshold = 0.5f;

    [SerializeField] bool visualizeNoise;
    [SerializeField] bool use3DNoise;

    private float[,,] heights;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    private MeshFilter meshFilter;
    
    private Dictionary <long,int> vertexCache;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        vertexCache = new Dictionary<long,int>();
        StartCoroutine(TestAll());
    }

    void Update()
    {

    }

    private IEnumerator TestAll()
    {
        while (true)
        {
            SetHeights();//SetHeight->Perlin3D
            MarchCubes();//MarchCubes->MarchCube->GetConfigIndex
            SetMesh();
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// 把计算好的顶点和三角形数据配置到 Unity 的 Mesh 对象里，进而生成可渲染的网格模型
    /// </summary>
    // 修改SetMesh方法：手动计算平滑法线
    private void SetMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        
        // 清除缓存，为下一次生成做准备
        vertexCache.Clear();
        
        // 使用梯度计算平滑法线（替代RecalculateNormals）
        mesh.normals = CalculateSmoothNormals(mesh.vertices, mesh.triangles);
        
        meshFilter.mesh = mesh;
    }

    // 新增：计算平滑法线的方法
    private Vector3[] CalculateSmoothNormals(Vector3[] vertice, int[] triangle)
    {
        Vector3[] normals = new Vector3[vertice.Length];
        
        // 初始化所有法线为零向量
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.zero;
        }
        
        // 累加每个三角形的面法线到其顶点
        for (int i = 0; i < triangle.Length; i += 3)
        {
            int i1 = triangle[i];
            int i2 = triangle[i + 1];
            int i3 = triangle[i + 2];
            
            Vector3 v1 = vertice[i1];
            Vector3 v2 = vertice[i2];
            Vector3 v3 = vertice[i3];
            
            // 计算三角形的面法线
            Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
            
            // 累加面法线到每个顶点
            normals[i1] += normal;
            normals[i2] += normal;
            normals[i3] += normal;
        }
        
        // 归一化所有顶点法线
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i].Normalize();
        }
        
        return normals;
    }


    private void SetHeights()
    {
        heights = new float[width + 1, height + 1, width + 1];

        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    if (use3DNoise)
                    {
                        float currentHeight = PerlinNoise3D((float)x / width * noiseScale, (float)y / height * noiseScale, (float)z / width * noiseScale);

                        heights[x, y, z] = currentHeight;
                    }
                    else
                    {
                        float currentHeight = height * Mathf.PerlinNoise(x * noiseScale, z * noiseScale);
                        float distToSufrace;

                        if (y <= currentHeight - 0.5f)
                            distToSufrace = 0f;
                        else if (y > currentHeight + 0.5f)
                            distToSufrace = 1f;
                        else if (y > currentHeight)
                            distToSufrace = y - currentHeight;
                        else
                            distToSufrace = currentHeight - y;

                        heights[x, y, z] = distToSufrace;
                    }
                }
            }
        }
    }

    private float PerlinNoise3D(float x, float y, float z)
    {
        // 改进：增加Y方向的权重，让Y变化对噪声的影响更连续
        float xyz = Mathf.PerlinNoise(x + y, z + y); // 将Y融入X/Z维度，增强Y相关性
        float yxz = Mathf.PerlinNoise(y + x, z + x);
        return (xyz + yxz) / 2; // 减少2D组合数量，增强核心方向连续性
    }

    

    //在预先定义好的长宽为width、高为height组成的标量场中构建MarchCubes
    private void MarchCubes()
    {
        //清除顶点和三角形列表中所有的数据
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
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];//定位每个小立方体的8个顶点在标量场中的绝对位置（这是一个三维int向量：Vector3Int）
                        cubeCorners[i] = heights[corner.x, corner.y, corner.z];//根据小立方体的8个顶点的绝对位置的xyz坐标得到对应的柏林噪声的数值（因为corner的xyz三个分量和heights的三个分量都是分别由width，height，width来决定的，所以可以准确定位到）
                    }
                    MarchCube(new Vector3Int(x, y, z), cubeCorners);//在第三层的单次循环中，cubeCorners储存了单个march cube中的8个顶点的噪声值。向MarchCube中传入单个立方体的位置和8个顶点的噪声数据数组
                }
            }
        }
    }

    [SerializeField] private bool isUseSmoothness = false;
    //单个行进立方体构造器
    private void MarchCube (Vector3Int position, float[] cubeCorners)
    {
        int configIndex = GetConfigIndex(cubeCorners);//将单个立方体的顶点噪声数据传入GetConfigIndex函数中，与heightThreshold比较 来生成配置索引，这是一个8位的数值，对应立方体的八个顶点。表示单个立方体的8个顶点是否在需要构建的形状的内部（如在形状外还是形状内）

        if (configIndex == 0 || configIndex == 255)//如果 configIndex是0000 0000或1111 1111，即立方体的所有点都在需要构建的图形内部或者外部，因为立方体没有边和图形相交，所以不需要参与图形重构（不需要生成新的顶点和三角形）
        {
            return;
        }

        int edgeIndex = 0;
        for (int t = 0; t < 5; t++)//一个立方体中三角形的最大数量为5
        {
            for (int v = 0; v < 3; v++)//遍历每个三角形的顶点
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];//得到MarchingTable.Triangles[,]这个二维数组的边索引

                if (triTableValue == -1)
                {
                    return;
                }

                Vector3Int edgeStart = position + MarchingTable.Edges[triTableValue, 0];//获取边表的第一个顶点
                Vector3Int edgeEnd = position + MarchingTable.Edges[triTableValue, 1];//获取边表的第二个顶点
                
                Vector3 vertex = (edgeStart + edgeEnd) / 2;//取中点

                
                if (isUseSmoothness)
                {
                    float edgeStartValue = heights[edgeStart.x, edgeStart.y, edgeStart.z];
                    float edgeEndValue = heights[edgeEnd.x, edgeEnd.y, edgeEnd.z];
                    vertex = InterpolateEdgePosition(heightThreshold, edgeStart,edgeStartValue, edgeEnd,edgeEndValue);
                }

                // 使用顶点缓存，避免重复顶点
                int vertexIndex = GetVertexIndex(vertex);
                
                //vertices.Add(vertexIndex);
                triangles.Add(vertexIndex);

                edgeIndex++;
            }
        }
    }

    private int GetVertexIndex(Vector3 vertex)
    {
        // 创建一个唯一键（使用定点数避免浮点数精度问题）
        long key = GetVertexKey(vertex);
        
        // 检查缓存中是否已存在该顶点
        if (vertexCache.TryGetValue(key, out int index))
        {
            return index;
        }
        
        // 如果不存在，添加到列表和缓存
        index = vertices.Count;
        vertices.Add(vertex);
        vertexCache[key] = index;
        return index;
    }
    // 新增：为顶点生成唯一键
    private long GetVertexKey(Vector3 vertex)
    {
        // 将浮点数转换为定点数（乘以1000并取整），减少精度问题
        int x = Mathf.RoundToInt(vertex.x * 1000);
        int y = Mathf.RoundToInt(vertex.y * 1000);
        int z = Mathf.RoundToInt(vertex.z * 1000);
        
        // 使用位运算组合三个整数为一个唯一的long值
        return (long)x << 40 | (long)y << 20 | z;
    }
    private Vector3 InterpolateEdgePosition(float threshold, Vector3 vertex1, float value1, Vector3 vertex2, float value2)
    {
        Vector3 pointOnEdge = Vector3.zero;
        if (Mathf.Approximately(threshold - value1, 0) == true)
            return vertex1;
        if (Mathf.Approximately(threshold - value2, 0) == true)
            return vertex2;
        if (Mathf.Approximately(value1 - value2, 0) == true) return vertex1;

        float mu = (threshold - value1) / (value2 - value1);
        pointOnEdge.x = vertex1.x + mu * (vertex2.x - vertex1.x);
        pointOnEdge.y = vertex1.y + mu * (vertex2.y - vertex1.y);
        pointOnEdge.z = vertex1.z + mu * (vertex2.z - vertex1.z);
        return pointOnEdge;
    }
    private int GetConfigIndex (float[] cubeCorners)
    {
        int configIndex = 0;
        for (int i = 0; i < 8; i++)//循环遍历立方体的8个顶点
        {
            /*
             * cubeCorners[i]是立方体顶点的密度值（由柏林噪声生成），表示该点的 “物质浓度”。例如：
             * 密度值接近 1：表示该点在地形 “内部”（如山脉、固体）。
             * 密度值接近 0：表示该点在地形 “外部”（如空气、空洞）。
             *
             * 等值面：heightThreshold定义了一个密度等值面，即所有密度值等于该阈值的点构成的表面。例如：
               当heightThreshold = 0.5时，算法会提取所有密度值为 0.5 的点，形成地形的 “表面”。
             */
            if (cubeCorners[i] > heightThreshold)//判断顶点是否在等值面的内部，如果heightThreshold为1，（因为柏林噪声的最大值就为1）所以没有点会大于heightThreshold，即所有点都不在形状内部，也就是说不会形成形状
            {
                //用二进制位标记顶点状态，生成配置索引。
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
