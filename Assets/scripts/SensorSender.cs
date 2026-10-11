using UnityEngine;
using System;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class SensorSender : MonoBehaviour
{
    [Serializable] class ControlMsg { public string id; public float steer; public float throttle; }
    [Serializable] class ControlBatch { public ControlMsg[] controls; }

    private TcpClient client;
    private volatile NetworkStream stream;
    private readonly StringBuilder sb = new StringBuilder(1024);

    private readonly object lockObj = new object();
    private string latestControlLine;

    void Start()
    {
        new Thread(ConnectToPython) { IsBackground = true }.Start();
    }

    void ConnectToPython()
    {
        try
        {
            client = new TcpClient("127.0.0.1", 5000);
            client.NoDelay = true;
            stream = client.GetStream();
            Debug.Log("Connected to Python!");
            ReadLoop();
        }
        catch (Exception e)
        {
            Debug.LogError("Could not connect to Python: " + e.Message);
        }
    }

    // background thread: keeps only the newest complete line
    void ReadLoop()
    {
        var buf = new byte[4096];
        string pending = "";
        try
        {
            while (true)
            {
                int n = stream.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                pending += Encoding.UTF8.GetString(buf, 0, n);

                int nl;
                while ((nl = pending.IndexOf('\n')) >= 0)
                {
                    string line = pending.Substring(0, nl);
                    pending = pending.Substring(nl + 1);
                    if (line.Length > 0)
                        lock (lockObj) { latestControlLine = line; }
                }
            }
        }
        catch { /* socket closed on shutdown */ }
    }

    void ApplyControls()
    {
        string line;
        lock (lockObj) { line = latestControlLine; latestControlLine = null; }
        if (line == null) return;

        ControlBatch batch;
        try { batch = JsonUtility.FromJson<ControlBatch>(line); }
        catch { return; }
        if (batch == null || batch.controls == null) return;

        foreach (var c in batch.controls)
        {
            foreach (var car in CarSensors.All)
            {
                if (car.carId == c.id)
                {
                    car.aiSteer = Mathf.Clamp(c.steer, -1f, 1f);
                    car.aiThrottle = Mathf.Clamp(c.throttle, -1f, 1f);
                    car.SetAI(car.aiThrottle, car.aiSteer);
                    break;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (stream == null || CarSensors.All.Count == 0) return;

        ApplyControls();

        sb.Clear();
        sb.Append("{\"gen\":").Append(GenerationManager.Gen).Append(",\"cars\":{");

        for (int c = 0; c < CarSensors.All.Count; c++)
        {
            CarSensors car = CarSensors.All[c];
            if (c > 0) sb.Append(',');

            sb.Append('"').Append(car.carId).Append("\":{\"sensors\":[");
            float[] data = car.GetSensorData();
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(data[i].ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append("],\"crashed\":").Append(car.crashed ? "true" : "false");
            sb.Append(",\"fitness\":").Append(car.Fitness.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('}');
        }

        sb.Append("}}\n");

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            Debug.LogWarning("Connection to Python lost.");
            stream = null;
        }
    }

    void OnDestroy()
    {
        stream?.Close();
        client?.Close();
    }
}