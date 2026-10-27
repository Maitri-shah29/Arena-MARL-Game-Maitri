using UnityEngine;
using UnityEngine.Rendering;

public static class ShaderHelper
{
    private static bool? isUrpCached;

    public static bool IsURP
    {
        get
        {
            if (!isUrpCached.HasValue)
            {
                RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline == null)
                {
                    pipeline = QualitySettings.renderPipeline;
                }
                isUrpCached = pipeline != null && pipeline.GetType().Name.Contains("Universal");
            }
            return isUrpCached.Value;
        }
    }

    public static Material CreateOpaqueMaterial(Color color, string name = "CustomOpaque")
    {
        Shader shader = null;
        if (IsURP)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader) { name = name };
        SetMaterialColor(mat, color);
        return mat;
    }

    public static Material CreateEmissiveMaterial(Color baseColor, Color emissionColor, float intensity = 1f, string name = "CustomEmissive")
    {
        Material mat = CreateOpaqueMaterial(baseColor, name);
        Color finalEmission = emissionColor * intensity;

        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", finalEmission);
        }

        return mat;
    }

    public static Material CreateTransparentMaterial(Color color, string name = "CustomTransparent")
    {
        Shader shader = null;
        if (IsURP)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader) { name = name };
        SetMaterialColor(mat, color);

        // Standard transparency setup if Standard shader is used
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        return mat;
    }

    public static Material CreateUnlitMaterial(Color color, string name = "CustomUnlit")
    {
        Shader shader = null;
        if (IsURP)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader) { name = name };
        SetMaterialColor(mat, color);
        return mat;
    }

    public static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }
}
