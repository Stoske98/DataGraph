using DataGraph.Data;
using DataGraph.Runtime;

using UnityEditor;

using UnityEngine;

public class TestDatabase : MonoBehaviour
{
    void Start()
    {
        // SO
        /* hero = Database.Get<Hero>().GetById(1);
        Debug.Log(hero.name + " " + hero.flag);
        
        // Blob
        ref var blobHero = ref BlobDatabase.Get<HeroBlob>().GetById(2);
        Debug.Log($"[Blob] Hero: {blobHero.name.ToString()}, HP: {blobHero.attribute.ToString()}");
    */}
}
