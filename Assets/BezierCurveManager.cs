using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bezier / 均匀B样条 参数曲线建模与交互。
/// 用法：新建空物体命名为 BezierCurve，把本脚本挂上去，点播放即可。
/// 鼠标左键拖拽红色控制点 -> 曲线实时更新。
/// 按键 C  = 增加一个控制点
/// 按键 X  = 删除最后一个控制点
/// 按键 B  = 在 Bezier 曲线 / 三次均匀 B 样条 之间切换
/// </summary>
public class BezierCurveManager : MonoBehaviour
{
    [Header("控制点")]
    public int initialPointCount = 4;
    public float controlPointSize = 0.15f;

    [Header("曲线")]
    public int curveSegments = 200;
    public float curveWidth = 0.04f;
    public Color curveColor = Color.cyan;
    public Color polylineColor = Color.gray;

    [Header("快捷键")]
    public KeyCode addKey = KeyCode.C;
    public KeyCode removeKey = KeyCode.X;
    public KeyCode toggleKey = KeyCode.B;

    private List<Transform> points = new List<Transform>();
    private LineRenderer curveLine;
    private LineRenderer polyLine;
    private bool useBSpline = false;

    void Start()
    {
        // 主曲线 LineRenderer
        curveLine = gameObject.GetComponent<LineRenderer>();
        if (curveLine == null) curveLine = gameObject.AddComponent<LineRenderer>();
        curveLine.useWorldSpace = true;
        curveLine.loop = false;
        curveLine.startWidth = curveWidth;
        curveLine.endWidth = curveWidth;
        curveLine.startColor = curveColor;
        curveLine.endColor = curveColor;
        curveLine.material = new Material(Shader.Find("Sprites/Default"));
        curveLine.positionCount = curveSegments + 1;

        // 控制多边形（灰色细连线）
        GameObject polyObj = new GameObject("ControlPolygon");
        polyLine = polyObj.AddComponent<LineRenderer>();
        polyLine.useWorldSpace = true;
        polyLine.loop = false;
        polyLine.startWidth = 0.01f;
        polyLine.endWidth = 0.01f;
        polyLine.startColor = polylineColor;
        polyLine.endColor = polylineColor;
        polyLine.material = new Material(Shader.Find("Sprites/Default"));

        // 初始一排控制点
        for (int i = 0; i < initialPointCount; i++)
        {
            float t = (float)i / (initialPointCount - 1);
            Vector3 pos = new Vector3(Mathf.Lerp(-2.5f, 2.5f, t), 0f, 0f);
            CreatePoint(pos);
        }

        // 让主相机看过来
        Camera.main.transform.position = new Vector3(0, 1.5f, -6f);
        Camera.main.transform.LookAt(new Vector3(0, 0, 0));
    }

    void CreatePoint(Vector3 pos)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = pos;
        sphere.transform.localScale = Vector3.one * controlPointSize;
        sphere.GetComponent<Renderer>().material.color = Color.red;
        sphere.name = "CP_" + points.Count;
        sphere.AddComponent<DragPoint>();
        points.Add(sphere.transform);
    }

    void Update()
    {
        if (Input.GetKeyDown(addKey))
        {
            Vector3 last = points[points.Count - 1].position;
            CreatePoint(last + new Vector3(0.6f, Random.Range(-0.5f, 0.5f), 0f));
        }
        if (Input.GetKeyDown(removeKey) && points.Count > 2)
        {
            Destroy(points[points.Count - 1].gameObject);
            points.RemoveAt(points.Count - 1);
        }
        if (Input.GetKeyDown(toggleKey))
        {
            useBSpline = !useBSpline;
            Debug.Log(useBSpline ? "[切换] 三次均匀 B 样条" : "[切换] Bezier 曲线");
        }

        DrawCurve();
        DrawPolygon();
    }

    // De Casteljau 算法求 Bezier 上一点
    Vector3 BezierPoint(float t)
    {
        Vector3[] p = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++) p[i] = points[i].position;
        for (int k = 1; k < p.Length; k++)
            for (int i = 0; i < p.Length - k; i++)
                p[i] = Vector3.Lerp(p[i], p[i + 1], t);
        return p[0];
    }

    // 三次均匀 B 样条（端点重复两次 clamped）
    Vector3 BSplinePoint(float t)
    {
        int n = points.Count;
        if (n < 4) return BezierPoint(t);

        List<Vector3> p = new List<Vector3>();
        p.Add(points[0].position); p.Add(points[0].position);
        foreach (var pt in points) p.Add(pt.position);
        p.Add(points[n - 1].position); p.Add(points[n - 1].position);

        int segCount = n - 1;
        float ft = t * segCount;
        int seg = Mathf.FloorToInt(ft);
        if (seg >= segCount) seg = segCount - 1;
        float u = ft - seg;

        Vector3 p0 = p[seg];
        Vector3 p1 = p[seg + 1];
        Vector3 p2 = p[seg + 2];
        Vector3 p3 = p[seg + 3];

        float u2 = u * u;
        float u3 = u2 * u;
        return 0.1666667f * (
            (-u3 + 3 * u2 - 3 * u + 1) * p0 +
            ( 3 * u3 - 6 * u2 + 4)     * p1 +
            (-3 * u3 + 3 * u2 + 3 * u + 1) * p2 +
            u3 * p3
        );
    }

    void DrawCurve()
    {
        curveLine.positionCount = curveSegments + 1;
        for (int i = 0; i <= curveSegments; i++)
        {
            float t = (float)i / curveSegments;
            curveLine.SetPosition(i, useBSpline ? BSplinePoint(t) : BezierPoint(t));
        }
    }

    void DrawPolygon()
    {
        polyLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            polyLine.SetPosition(i, points[i].position);
    }
}

/// <summary>
/// 挂在每个控制点 Sphere 上，支持鼠标左键拖拽（在平行于相机的平面上移动）。
/// </summary>
public class DragPoint : MonoBehaviour
{
    private Plane dragPlane;
    private Vector3 offset;

    void OnMouseDown()
    {
        dragPlane = new Plane(Camera.main.transform.forward, transform.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float dist;
        if (dragPlane.Raycast(ray, out dist))
            offset = transform.position - ray.GetPoint(dist);
    }

    void OnMouseDrag()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float dist;
        if (dragPlane.Raycast(ray, out dist))
            transform.position = ray.GetPoint(dist) + offset;
    }
}
//（注：内容由AI生成）
