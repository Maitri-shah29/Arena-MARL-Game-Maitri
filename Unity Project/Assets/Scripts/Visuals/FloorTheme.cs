using UnityEngine;

public class FloorTheme : MonoBehaviour
{
    private Material floorMaterialInstance;

    public void ApplyTheme()
    {
        // Find floor GameObject under platform
        GameObject floorObj = FindFloorObject();
        if (floorObj == null)
        {
            Debug.LogWarning("[FloorTheme] Floor GameObject not found.");
            return;
        }

        MeshRenderer floorRenderer = floorObj.GetComponent<MeshRenderer>();
        if (floorRenderer == null)
        {
            Debug.LogWarning("[FloorTheme] Floor MeshRenderer not found.");
            return;
        }

        Texture2D crackedConcreteTex = ProceduralTextures.GenerateCrackedConcrete(512, 512);

        // Create a new instance material (never modify shared Floor.mat asset)
        floorMaterialInstance = ShaderHelper.CreateOpaqueMaterial(new Color(0.85f, 0.88f, 0.90f), "Floor_CrackedConcrete");
        if (floorMaterialInstance.HasProperty("_MainTex"))
        {
            floorMaterialInstance.SetTexture("_MainTex", crackedConcreteTex);
            floorMaterialInstance.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }
        if (floorMaterialInstance.HasProperty("_BaseMap"))
        {
            floorMaterialInstance.SetTexture("_BaseMap", crackedConcreteTex);
            floorMaterialInstance.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
        }
        if (floorMaterialInstance.HasProperty("_Smoothness"))
        {
            floorMaterialInstance.SetFloat("_Smoothness", 0.15f);
        }

        floorRenderer.material = floorMaterialInstance;
    }

    private GameObject FindFloorObject()
    {
        // Check direct children or scene
        Transform t = transform.Find("Floor");
        if (t != null) return t.gameObject;

        GameObject[] allFloors = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (var go in allFloors)
        {
            if (go.name == "Floor" && go.GetComponent<MeshRenderer>() != null)
            {
                return go;
            }
        }

        // Fallback search
        GameObject sceneFloor = GameObject.Find("Floor");
        if (sceneFloor != null) return sceneFloor;

        return null;
    }

    private void OnDestroy()
    {
        if (floorMaterialInstance != null)
        {
            Destroy(floorMaterialInstance);
        }
    }
}
