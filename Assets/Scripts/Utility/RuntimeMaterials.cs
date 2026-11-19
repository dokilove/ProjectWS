using UnityEngine;

/// <summary>
/// 코드에서 생성하는 Line/Trail 렌더러용 기본 머티리얼을 한 번만 만들어 공유합니다.
/// (발사체마다 new Material을 만들면 머티리얼 인스턴스가 계속 늘어나므로)
/// </summary>
public static class RuntimeMaterials
{
    private static Material s_vertexColorUnlit;

    /// <summary>
    /// 정점 색상(Gradient)을 그대로 쓰는 알파 블렌딩 언릿 머티리얼.
    /// Sprites/Default는 항상 빌드에 포함되므로 URP에서도 안전하게 사용할 수 있습니다.
    /// </summary>
    public static Material VertexColorUnlit
    {
        get
        {
            if (s_vertexColorUnlit == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    s_vertexColorUnlit = new Material(shader) { name = "RuntimeVertexColorUnlit" };
                }
            }
            return s_vertexColorUnlit;
        }
    }
}
