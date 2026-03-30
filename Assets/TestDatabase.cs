using DataGraph.Data;
using DataGraph.Runtime;

using UnityEditor;

using UnityEngine;

public class TestDatabase : MonoBehaviour
{
    void Start()
    {
        // SO
        var attributeStructure = Database.Get<Attribute>().GetById(10);
        Debug.Log(attributeStructure.name + " " + attributeStructure.structure.str);

        /*// Blob
        ref var blobHero = ref BlobDatabase.Get<HeroBlob>().GetById(2);
        Debug.Log($"[Blob] Hero: {blobHero.name.ToString()}, HP: {blobHero.stats.hp}");*/
    }
}
