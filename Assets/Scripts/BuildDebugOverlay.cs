using UnityEngine;
using System.Text;

public class BuildDebugOverlay : MonoBehaviour
{
    public static BuildDebugOverlay Instance { get; private set; }

    [Header("Display")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [SerializeField] private int fontSize = 22;
    [SerializeField] private float width = 900f;
    [SerializeField] private float height = 400f;
    [SerializeField] private Vector2 position = new Vector2(20f, 20f);

    [Header("Style")]
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);

    private static readonly StringBuilder SharedBuilder = new StringBuilder(1024);
    private string debugText = "BuildDebugOverlay active";
    private GUIStyle labelStyle;
    private Texture2D backgroundTexture;

    public static void SetText(string text)
    {
        if (Instance == null) return;
        Instance.debugText = text;
    }

    public static void AppendLine(string line)
    {
        if (Instance == null) return;
        SharedBuilder.Clear();
        SharedBuilder.AppendLine(Instance.debugText);
        SharedBuilder.AppendLine(line);
        Instance.debugText = SharedBuilder.ToString();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateStyle();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showOverlay = !showOverlay;
        }
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

        if (labelStyle == null || backgroundTexture == null)
        {
            CreateStyle();
        }

        Rect rect = new Rect(position.x, position.y, width, height);

        Color prevColor = GUI.color;
        GUI.color = backgroundColor;
        GUI.DrawTexture(rect, backgroundTexture);
        GUI.color = prevColor;

        GUI.Label(rect, debugText, labelStyle);
    }

    private void CreateStyle()
    {
        labelStyle = new GUIStyle();
        labelStyle.fontSize = fontSize;
        labelStyle.richText = true;
        labelStyle.wordWrap = true;
        labelStyle.alignment = TextAnchor.UpperLeft;
        labelStyle.padding = new RectOffset(12, 12, 12, 12);
        labelStyle.normal.textColor = textColor;
    
        backgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        backgroundTexture.SetPixel(0, 0, Color.white);
        backgroundTexture.Apply();
    }
}