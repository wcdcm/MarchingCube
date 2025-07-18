using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingCubeToAnyShape : MonoBehaviour
{
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 20;

    [SerializeField] [Range(0,1f)]private float heightThreshold = 0.5f;

    [SerializeField] bool visualizeNoise;
    [SerializeField] bool use3DNoise;

    private float[,,] heights;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector3> normals = new List<Vector3>();
    private List<Vector2> uvs = new List<Vector2>();

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    // 球体基础参数
    [SerializeField] float sphereRadius = 10f;
    private Vector3 sphereCenter;

    // 延伸相关参数（新增/修改）
    [SerializeField] float extendPower = 0.4f; // 延伸强度
    [SerializeField] float extendRange = 3f; // 基础影响范围
    [SerializeField] float directionFactor = 1.5f; // 沿摄像机方向的延伸系数（越大越偏向方向延伸）
    private List<(Vector3 hitPoint, Vector3 camDirection)> extendData = new List<(Vector3, Vector3)>(); // 存储点击点和摄像机方向


    void Start()
    {
        sphereCenter = new Vector3(width/2f, height/2f, width/2f);
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();

        if (meshRenderer != null && meshRenderer.material == null)
        {
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }

        UpdateMesh();
    }


    void Update()
    {
        // 鼠标左键点击时触发延伸
        if (Input.GetMouseButtonDown(0))
        {
            // 核心修改1：从摄像机中心点（屏幕中心）发射射线
            Ray centerRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); // 视口中心(0.5,0.5)对应屏幕中心
            
            // 检测射线是否击中网格
            if (meshCollider.Raycast(centerRay, out RaycastHit hit, 1000f))
            {
                Vector3 surfaceNormal = hit.normal;
                // 记录点击点和摄像机方向（用于后续沿该方向延伸）
                extendData.Add((hit.point, surfaceNormal));
                UpdateMesh(); // 立即更新网格
            }
        }
    }


    private void UpdateMesh()
    {
        SetHeights(); 
        MarchCubes(); 
        SetMesh();    
    }


    private void SetMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateBounds();
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh; // 更新碰撞体
    }


    // 核心修改2：调整密度场计算，让凸起沿摄像机方向延伸
    private void SetHeights()
    {
        heights = new float[width + 1, height + 1, width + 1];

        for (int x = 0; x < width + 1; x++)
        {
            for (int y = 0; y < height + 1; y++)
            {
                for (int z = 0; z < width + 1; z++)
                {
                    Vector3 point = new Vector3(x, y, z);
                    
                    // 基础球体密度（保持不变）
                    float distanceToCenter = Vector3.Distance(point, sphereCenter);
                    float sphereDensity = 1.0f - (distanceToCenter / sphereRadius);

                    // 延伸效果计算（核心修改）
                    float extendInfluence = 0;
                    foreach (var data in extendData)
                    {
                        Vector3 hitPoint = data.hitPoint;
                        Vector3 camDir = data.camDirection; // 摄像机方向（延伸方向）

                        // 计算点到点击点的基础距离
                        float distanceToHit = Vector3.Distance(point, hitPoint);

                        // 核心逻辑：让延伸沿摄像机方向增强
                        // 计算点在摄像机方向上的投影（沿延伸方向的偏移）
                        Vector3 pointToHit = point - hitPoint;
                        float dotProduct = Vector3.Dot(pointToHit.normalized, camDir); // 点与延伸方向的夹角（-1~1）
                        
                        // 沿摄像机方向（dotProduct正方向）的点获得更强影响，反方向减弱
                        float directionWeight = Mathf.Lerp(0.2f, 1.5f, (dotProduct + 1) / 2); // 方向权重（0.2~1.5）

                        // 综合计算影响范围：基础范围 + 方向延伸
                        float effectiveRange = extendRange + (directionFactor * dotProduct); // 沿方向增加有效范围
                        if (distanceToHit < effectiveRange)
                        {
                            // 高斯衰减 + 方向权重：沿摄像机方向的凸起更明显、范围更远
                            float gaussian = Mathf.Exp(-(distanceToHit * distanceToHit) / (2 * 0.8f));
                            extendInfluence += extendPower * gaussian * directionWeight;
                        }
                    }

                    // 最终密度 = 球体密度 + 延伸影响
                    heights[x, y, z] = Mathf.Clamp01(sphereDensity + extendInfluence);
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
                    MarchCube(new Vector3(x, y, z), cubeCorners);
                }
            }
        }
    }


    private void MarchCube(Vector3 position, float[] cubeCorners)
    {
        int configIndex = GetConfigIndex(cubeCorners);
        if (configIndex == 0 || configIndex == 255)
            return;

        Vector3[] edgeVertices = new Vector3[12];
        bool[] isEdgeCalculated = new bool[12];
        int edgeIndex = 0;
        
        for (int t = 0; t < 5; t++)
        {
            for (int v = 0; v < 3; v++)
            {
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];
                if (triTableValue == -1) 
                    return;

                if (!isEdgeCalculated[triTableValue])
                {
                    Vector3 edgeStart = position + MarchingTable.Edges[triTableValue, 0];
                    Vector3 edgeEnd = position + MarchingTable.Edges[triTableValue, 1];
                    Vector3 vertex = Vector3.Lerp(edgeStart, edgeEnd, 0.5f);
                    edgeVertices[triTableValue] = vertex;
                    isEdgeCalculated[triTableValue] = true;
                }

                Vector3 currentVertex = edgeVertices[triTableValue];
                vertices.Add(currentVertex);
                normals.Add((currentVertex - sphereCenter).normalized);
                uvs.Add(CalculateSphereUV(currentVertex));
                triangles.Add(vertices.Count - 1);
                edgeIndex++;
            }
        }
    }


    private Vector2 CalculateSphereUV(Vector3 vertex)
    {
        Vector3 dir = (vertex - sphereCenter).normalized;

        float azimuth = Mathf.Atan2(dir.x, dir.z);
        if (azimuth < 0) azimuth += 2 * Mathf.PI;
        float u = azimuth / (2 * Mathf.PI);

        float polar = Mathf.Acos(Mathf.Clamp(dir.y, -1f, 1f));
        float v = polar / Mathf.PI;

        return new Vector2(u, v);
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


    private void OnDrawGizmosSelected()
    {
        if (!visualizeNoise || !Application.isPlaying)
            return;

        // 可视化密度场
        for (int x = 0; x < width + 1; x += 2)
        {
            for (int y = 0; y < height + 1; y += 2)
            {
                for (int z = 0; z < width + 1; z += 2)
                {
                    Gizmos.color = new Color(heights[x, y, z], heights[x, y, z], heights[x, y, z], 0.5f);
                    Gizmos.DrawSphere(new Vector3(x, y, z), 0.3f);
                }
            }
        }

        // 可视化延伸点和延伸方向（辅助调试）
        Gizmos.color = Color.red;
        foreach (var data in extendData)
        {
            // 绘制点击点
            Gizmos.DrawSphere(data.hitPoint, 0.5f);
            // 绘制延伸方向线（摄像机方向）
            Gizmos.DrawLine(data.hitPoint, data.hitPoint + data.camDirection * 3f);
        }

        // 可视化中心射线（摄像机中心点发出的射线）
        Gizmos.color = Color.cyan;
        Ray centerRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Gizmos.DrawLine(centerRay.origin, centerRay.origin + centerRay.direction * 20f);
    }
}