using UnityEngine;
using System.Collections.Generic;

public class Checkpoint : MonoBehaviour
{
    public int index;   // 0, 1, 2... in driving order

    static readonly List<Checkpoint> All = new List<Checkpoint>();
    public static int Count => All.Count;
    public static Checkpoint Get(int i) => All.Find(c => c.index == i);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void OnEnable()  { All.Add(this); }
    void OnDisable() { All.Remove(this); }
}