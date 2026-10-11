using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public GameObject carPrefab;
    public int count = 5;
    public Transform spawnPoint;
    public Vector2 offsetPerCar = new Vector2(0f, 0f); // optional spread for testing

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = spawnPoint.position + (Vector3)(offsetPerCar * i);
            Instantiate(carPrefab, pos, spawnPoint.rotation);
        }
    }
}