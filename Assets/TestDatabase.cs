using DataGraph.Runtime;

using UnityEngine;

public class TestDatabase : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var item = Database.Get<DataGraph.Data.Item>().GetById(3);
        Debug.Log(item.name);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
