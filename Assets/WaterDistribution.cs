using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class WaterDistribution : MonoBehaviour
{
    private const int InitialWater = 80;
    private const int Step = 5;
    private const int Center = 1;
    private const float EnterTilt = 14f;
    private const float ExitTilt = 8f;

    private readonly string[] sectorNames = { "Viviendas", "Huerta", "Humedal" };
    private readonly int[] received = new int[3];
    private readonly Text[] sectorLabels = new Text[3];
    private readonly Image[] sectorPanels = new Image[3];

    private AttitudeSensor sensor;
    private Quaternion neutral;
    private Font font;
    private Text tankLabel;
    private Text selectionLabel;
    private Text statusLabel;
    private Button calibrateButton;
    private Button chooseButton;
    private Button lessButton;
    private Button moreButton;
    private Button sendButton;
    private Button cancelButton;
    private int water = InitialWater;
    private int highlighted = Center;
    private int destination = -1;
    private int amount;
    private bool calibrated;

    private bool HasReading => sensor != null && sensor.lastUpdateTime > 0;
    private bool KeyboardPreview => Application.isEditor && sensor == null;

    private void Start()
    {
        Screen.orientation = ScreenOrientation.Portrait;
        sensor = AttitudeSensor.current;
        if (sensor != null)
            InputSystem.EnableDevice(sensor);

        BuildBoard();
        statusLabel.text = KeyboardPreview
            ? "Vista de prueba: A, S y D seleccionan el destino."
            : sensor == null
                ? "Este dispositivo no ofrece orientación por giroscopio."
                : "Sostené el teléfono y tocá Calibrar.";
        Refresh();
    }

    private void Update()
    {
        calibrateButton.interactable = HasReading;
        if (destination >= 0)
            return;

        int next = highlighted;
        if (KeyboardPreview && Keyboard.current is Keyboard keyboard)
        {
            if (keyboard.aKey.wasPressedThisFrame) next = 0;
            if (keyboard.sKey.wasPressedThisFrame) next = Center;
            if (keyboard.dKey.wasPressedThisFrame) next = 2;
        }
        else if (calibrated && HasReading)
        {
            Quaternion relative = Quaternion.Inverse(neutral) * sensor.attitude.ReadValue();
            Vector3 right = relative * Vector3.right;
            float tilt = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
            if (tilt > EnterTilt) next = 0;
            else if (tilt < -EnterTilt) next = 2;
            else if (Mathf.Abs(tilt) < ExitTilt) next = Center;
        }

        if (next != highlighted)
        {
            highlighted = next;
            Refresh();
        }
    }

    private void Calibrate()
    {
        if (!HasReading)
            return;

        neutral = sensor.attitude.ReadValue();
        calibrated = true;
        highlighted = Center;
        statusLabel.text = "Incliná el teléfono y tocá Elegir.";
        Refresh();
    }

    private void Choose()
    {
        if (destination >= 0 || water < Step || (!calibrated && !KeyboardPreview))
            return;

        destination = highlighted;
        amount = Step;
        Refresh();
    }

    private void ChangeAmount(int change)
    {
        if (destination < 0)
            return;

        amount = Mathf.Clamp(amount + change, Step, water);
        Refresh();
    }

    private void ClearOrder()
    {
        destination = -1;
        amount = 0;
        Refresh();
    }

    private void Send()
    {
        if (destination < 0 || amount < Step || amount > water)
            return;

        water -= amount;
        received[destination] += amount;
        statusLabel.text = $"Enviaste {amount} u a {sectorNames[destination]}.";
        ClearOrder();
    }

    private void Refresh()
    {
        tankLabel.text = $"Tanque: {water} u";
        bool preparing = destination >= 0;
        for (int index = 0; index < sectorNames.Length; index++)
        {
            bool selected = index == (preparing ? destination : highlighted)
                && (calibrated || KeyboardPreview);
            sectorLabels[index].text = $"{sectorNames[index]}\n{received[index]} u";
            sectorPanels[index].color = selected
                ? new Color32(29, 106, 102, 255)
                : new Color32(33, 53, 72, 255);
        }

        selectionLabel.text = preparing ? $"{sectorNames[destination]}: {amount} u" :
            calibrated || KeyboardPreview ? sectorNames[highlighted] : "Calibrá para seleccionar";
        chooseButton.interactable = !preparing && water >= Step && (calibrated || KeyboardPreview);
        lessButton.interactable = preparing && amount > Step;
        moreButton.interactable = preparing && amount < water;
        sendButton.interactable = preparing;
        cancelButton.interactable = preparing;
    }

    private void BuildBoard()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var ui = new GameObject("Tablero de reparto", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(600, 960);
        scaler.matchWidthOrHeight = 0.5f;
        var panel = MakeRect("Fondo", ui.transform, 0, 0, 1, 1);
        panel.gameObject.AddComponent<Image>().color = new Color32(18, 33, 48, 255);

        MakeLabel("Agua Compartida", panel, 38, .08f, .84f, .92f, .93f);
        tankLabel = MakeLabel("", panel, 36, .08f, .53f, .92f, .63f);
        MakeSector(0, panel, .05f, .62f, .33f, .72f);
        MakeSector(1, panel, .36f, .73f, .64f, .83f);
        MakeSector(2, panel, .67f, .62f, .95f, .72f);
        selectionLabel = MakeLabel("", panel, 28, .08f, .43f, .92f, .50f);
        calibrateButton = MakeButton("Calibrar", panel, .08f, .34f, .47f, .42f, Calibrate);
        chooseButton = MakeButton("Elegir", panel, .53f, .34f, .92f, .42f, Choose);
        lessButton = MakeButton("−5", panel, .08f, .24f, .28f, .32f, () => ChangeAmount(-Step));
        moreButton = MakeButton("+5", panel, .30f, .24f, .50f, .32f, () => ChangeAmount(Step));
        sendButton = MakeButton("Enviar", panel, .53f, .24f, .92f, .32f, Send);
        cancelButton = MakeButton("Cancelar", panel, .29f, .14f, .71f, .22f, ClearOrder);
        statusLabel = MakeLabel("", panel, 24, .08f, .05f, .92f, .13f);

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void MakeSector(int index, Transform parent, float left, float bottom, float right, float top)
    {
        RectTransform rect = MakeRect(sectorNames[index], parent, left, bottom, right, top);
        sectorPanels[index] = rect.gameObject.AddComponent<Image>();
        sectorLabels[index] = MakeLabel("", rect, 25, 0, 0, 1, 1);
    }

    private Button MakeButton(string title, Transform parent, float left, float bottom, float right, float top,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = MakeRect(title, parent, left, bottom, right, top);
        Image background = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);
        MakeLabel(title, rect, 25, 0, 0, 1, 1).color = new Color32(18, 33, 48, 255);
        return button;
    }

    private Text MakeLabel(string content, Transform parent, int size, float left, float bottom, float right, float top)
    {
        var label = MakeRect("Texto", parent, left, bottom, right, top).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = content;
        return label;
    }

    private static RectTransform MakeRect(string name, Transform parent, float left, float bottom, float right, float top)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
