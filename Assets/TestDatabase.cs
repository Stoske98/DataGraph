using DataGraph.Data;
using DataGraph.Runtime;

using UnityEditor;

using UnityEngine;

public class TestDatabase : MonoBehaviour
{
    void Start()
    {
        /*// SO
        var hero = Database.Get<Hero>().GetById(2);
        Debug.Log($"[SO] Hero: {hero.name}, HP: {hero.stats.hp}");

        // Blob
        ref var blobHero = ref BlobDatabase.Get<HeroBlob>().GetById(2);
        Debug.Log($"[Blob] Hero: {blobHero.name.ToString()}, HP: {blobHero.stats.hp}");*/

        var sb = new System.Text.StringBuilder();
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!a.FullName.Contains("Quantum"))
                continue;
            foreach (var t in a.GetExportedTypes())
            {
                if (t.IsClass && typeof(UnityEngine.ScriptableObject).IsAssignableFrom(t))
                    sb.AppendLine($"{t.FullName} in {a.GetName().Name}");
            }
        }
        Debug.Log(sb.ToString());
    }
}
