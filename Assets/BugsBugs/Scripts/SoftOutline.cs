using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 매끈한 글자 외곽선.
// 유니티 기본 Outline 은 대각선 4방향으로만 글자를 복사해서, 굵게 하면 모서리가 계단처럼 깨져 보임.
// 이건 원을 따라 여러 방향(기본 12방향)으로 복사해서 둥글고 매끈한 외곽선을 만듦.
public class SoftOutline : BaseMeshEffect
{
    public Color effectColor = Color.black;
    public float distance = 2f;   // 외곽선 굵기 (UI 픽셀)
    public int directions = 12;   // 복사 방향 수 (많을수록 매끈)

    readonly List<UIVertex> verts = new List<UIVertex>();
    readonly List<UIVertex> result = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || distance <= 0f) return;
        verts.Clear();
        vh.GetUIVertexStream(verts);
        result.Clear();

        // 바깥 고리 + 안쪽 고리(절반 거리) 로 빈틈 없이 채움, 원래 글자는 맨 위에
        AddRing(distance, directions);
        AddRing(distance * 0.5f, Mathf.Max(6, directions / 2));
        result.AddRange(verts);

        vh.Clear();
        vh.AddUIVertexTriangleStream(result);
    }

    void AddRing(float dist, int count)
    {
        for (int d = 0; d < count; d++)
        {
            float a = d * Mathf.PI * 2f / count;
            Vector3 off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * dist;
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                v.position += off;
                Color32 c = effectColor;
                c.a = (byte)(c.a * v.color.a / 255);
                v.color = c;
                result.Add(v);
            }
        }
    }
}
