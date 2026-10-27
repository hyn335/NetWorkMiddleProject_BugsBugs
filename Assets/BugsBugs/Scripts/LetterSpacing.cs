using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// uGUI Text 에 자간(글자 사이 간격)을 넣어주는 효과.
// 기본 Text 에는 자간 설정이 없어서, 글자 모양(사각형)을 한 줄씩 모아 옆으로 조금씩 밀어줌.
[RequireComponent(typeof(Text))]
public class LetterSpacing : BaseMeshEffect
{
    public float em = 0.06f; // 글자 크기 대비 간격 (0.06 = 글자 크기의 6%)

    readonly List<UIVertex> verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || em == 0f) return;
        var text = GetComponent<Text>();
        float spacing = text.fontSize * em;

        verts.Clear();
        vh.GetUIVertexStream(verts); // 글자 하나 = 삼각형 2개 = 정점 6개
        int quads = verts.Count / 6;
        if (quads < 2) return;

        // 줄마다 글자들을 모음 (같은 줄 = 글자 아래쪽 y 가 거의 같음)
        var lines = new List<List<int>>();
        var lineY = new List<float>();
        for (int q = 0; q < quads; q++)
        {
            float y = QuadMinY(q);
            int li = -1;
            for (int l = 0; l < lineY.Count; l++)
                if (Mathf.Abs(lineY[l] - y) < text.fontSize * 0.5f) { li = l; break; }
            if (li < 0) { lineY.Add(y); lines.Add(new List<int>()); li = lines.Count - 1; }
            lines[li].Add(q);
        }

        // 정렬에 맞춰 줄 전체가 가운데/오른쪽 기준을 유지하도록 보정
        TextAnchor a = text.alignment;
        bool center = a == TextAnchor.UpperCenter || a == TextAnchor.MiddleCenter || a == TextAnchor.LowerCenter;
        bool right = a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight;

        foreach (var line in lines)
        {
            line.Sort((p, q) => QuadMinX(p).CompareTo(QuadMinX(q)));
            float total = spacing * (line.Count - 1);
            float start = center ? -total * 0.5f : right ? -total : 0f;
            for (int i = 0; i < line.Count; i++)
            {
                float dx = start + spacing * i;
                int baseIdx = line[i] * 6;
                for (int v = 0; v < 6; v++)
                {
                    var vert = verts[baseIdx + v];
                    vert.position.x += dx;
                    verts[baseIdx + v] = vert;
                }
            }
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    float QuadMinY(int q)
    {
        float m = float.MaxValue;
        for (int v = 0; v < 6; v++) m = Mathf.Min(m, verts[q * 6 + v].position.y);
        return m;
    }

    float QuadMinX(int q)
    {
        float m = float.MaxValue;
        for (int v = 0; v < 6; v++) m = Mathf.Min(m, verts[q * 6 + v].position.x);
        return m;
    }
}
