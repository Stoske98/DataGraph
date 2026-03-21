using DataGraph.Data;
using DataGraph.Runtime;

using UnityEditor;

using UnityEngine;

public class TestDatabase : MonoBehaviour
{
    void Start()
    {
        // SO
        var hero = Database.Get<Hero>().GetById(2);
        Debug.Log($"[SO] Hero: {hero.name}, HP: {hero.stats.hp}");

        // Blob
        ref var blobHero = ref BlobDatabase.Get<HeroBlob>().GetById(2);
        Debug.Log($"[Blob] Hero: {blobHero.name.ToString()}, HP: {blobHero.stats.hp}");
    }
}
