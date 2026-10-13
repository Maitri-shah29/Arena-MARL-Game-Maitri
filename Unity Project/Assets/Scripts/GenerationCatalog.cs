using System;
using Unity.Barracuda;
using UnityEngine;

[CreateAssetMenu(fileName = "GenerationCatalog", menuName = "Arena/Generation Catalog")]
public class GenerationCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string label;
        public NNModel model;
    }

    public Entry[] generations;
}
