using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(GameController))]
public class HandTrackingReceiver : MonoBehaviour
{
    [Serializable]
    private class GesturePacket
    {
        public float x;
        public float y;
        public string gesture;
    }

    [SerializeField] private int port = 5005;
    private UdpClient udp;
    private string previousGesture = "";
    private Vector2 previousPoint;
    private float lastPacketTime = -100f;

    public Vector2 HandScreenPosition { get; private set; }
    public bool HasActiveHand => Time.realtimeSinceStartup - lastPacketTime < 1f;
    public bool Pinching => HasActiveHand && previousGesture == "pinch";
    public bool PinchPressed { get; private set; }
    public bool PinchReleased { get; private set; }
    public bool PointPressed { get; private set; }
    public Vector2 OpenPalmDelta { get; private set; }

    private void OnEnable()
    {
        if (Application.isBatchMode) return;
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            udp.Client.Blocking = false;
        }
        catch (SocketException error)
        {
            Debug.LogWarning("Hand tracking unavailable on UDP " + port + ": " + error.Message);
            udp = null;
        }
    }

    private void Update()
    {
        PinchPressed = false;
        PinchReleased = false;
        PointPressed = false;
        OpenPalmDelta = Vector2.zero;
        if (udp == null) return;
        if (!HasActiveHand) previousGesture = "";
        try
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Loopback, 0);
            while (udp.Available > 0)
            {
                byte[] bytes = udp.Receive(ref sender);
                GesturePacket packet = JsonUtility.FromJson<GesturePacket>(Encoding.UTF8.GetString(bytes));
                if (packet == null || packet.gesture == null) continue;
                Vector2 point = new Vector2(Mathf.Clamp01(packet.x), Mathf.Clamp01(packet.y));
                string gesture = packet.gesture.ToLowerInvariant();
                PinchPressed |= gesture == "pinch" && previousGesture != "pinch";
                PinchReleased |= gesture != "pinch" && previousGesture == "pinch";
                PointPressed |= gesture == "point" && previousGesture != "point";
                if (gesture == "open_palm" && previousGesture == "open_palm")
                    OpenPalmDelta += point - previousPoint;
                previousGesture = gesture;
                previousPoint = point;
                HandScreenPosition = new Vector2(point.x * Screen.width, (1f - point.y) * Screen.height);
                lastPacketTime = Time.realtimeSinceStartup;
            }
        }
        catch (Exception error)
        {
            Debug.LogWarning("Hand tracking packet ignored: " + error.Message);
        }
    }

    private void OnDisable()
    {
        if (udp != null)
        {
            udp.Close();
            udp = null;
        }
    }
}
