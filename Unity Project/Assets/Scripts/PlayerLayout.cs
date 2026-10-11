using System;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayerLayoutFile
{
    public Piece[] pieces = new Piece[0];

    [Serializable]
    public class Piece
    {
        public string kind;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public float sx, sy, sz;
        public int lockTeam;
    }
}

public static class PlayerLayout
{
    public static string DefaultPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "../config/player_layout.json"));

    public static void Save(Transform platform)
    {
        var pieces = new System.Collections.Generic.List<PlayerLayoutFile.Piece>();
        foreach (ArchitectObject marker in UnityEngine.Object.FindObjectsOfType<ArchitectObject>())
        {
            if (marker == null)
            {
                continue;
            }

            Vector3 local = platform.InverseTransformPoint(marker.transform.position);
            Quaternion localRotation = Quaternion.Inverse(platform.rotation) * marker.transform.rotation;
            BoxHolding box = marker.GetComponent<BoxHolding>();
            pieces.Add(new PlayerLayoutFile.Piece
            {
                kind = box != null ? "box" : "wall",
                px = local.x,
                py = local.y,
                pz = local.z,
                rx = localRotation.x,
                ry = localRotation.y,
                rz = localRotation.z,
                rw = localRotation.w,
                sx = marker.transform.localScale.x,
                sy = marker.transform.localScale.y,
                sz = marker.transform.localScale.z,
                lockTeam = box != null ? box.ArchitectLockTeam : 0
            });
        }

        var file = new PlayerLayoutFile { pieces = pieces.ToArray() };
        string path = SystemArgs.LayoutPath ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(file, true));
        Debug.Log("Saved player arena to " + path);
    }

    public static bool TryLoad(out PlayerLayoutFile layout)
    {
        layout = null;
        string path = SystemArgs.LayoutPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        layout = JsonUtility.FromJson<PlayerLayoutFile>(File.ReadAllText(path));
        return layout != null && layout.pieces != null;
    }
}
